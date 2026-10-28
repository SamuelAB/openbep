using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace OpenBepSync
{
    /// <summary>One line of a replace list: every use of Old becomes New.</summary>
    public class MaterialRule
    {
        public string Old = "";
        public string New = "";
        public MaterialRule() { }
        public MaterialRule(string oldName, string newName) { Old = oldName ?? ""; New = newName ?? ""; }
    }

    /// <summary>What the replace will do with one rule, decided before anything changes.</summary>
    public enum RuleAction
    {
        Replace,   // New exists: every use of Old moves to New
        Rename,    // New does not exist yet: Old is renamed to New, so its uses stay where they are
        Remove,    // New is <By Category>: the element falls back to its category's material
        Skip       // see Note
    }

    public class ResolvedRule
    {
        public MaterialRule Rule;
        public string Target = "";   // final name after following chains (A > B, B > C gives A > C)
        public RuleAction Action;
        public string Note = "";
    }

    /// <summary>
    /// Replace lists for materials (and material assets): read and write the CSV,
    /// and check a list against the names in the model before anything changes. No Revit needed.
    /// </summary>
    public static class MaterialRules
    {
        public const string ByCategory = "<By Category>";

        /// <summary>
        /// Reads a replace list. Takes OpenBEP's own columns ("Old name", "New name"), the Polynine
        /// columns ("Existing material", "New material"), or, with no known header, the first two columns.
        /// Blank lines and lines starting with # are skipped.
        /// </summary>
        public static List<MaterialRule> ReadText(string text, out List<string> warnings)
        {
            warnings = new List<string>();
            var rows = BindingList.ParseCsv(text ?? "").Where(r => r.Any(c => c.Trim() != "") && !r[0].TrimStart().StartsWith("#")).ToList();
            var rules = new List<MaterialRule>();
            if (rows.Count == 0) return rules;
            var head = rows[0].Select(h => h.Trim().TrimStart('﻿')).ToList();
            int Find(params string[] names) => head.FindIndex(h => names.Any(n => string.Equals(h, n, StringComparison.OrdinalIgnoreCase)));
            int cOld = Find("Old name", "Old material", "Existing material", "Existing", "Old");
            int cNew = Find("New name", "New material", "New");
            int start = 1;
            if (cOld < 0 || cNew < 0) { cOld = 0; cNew = 1; start = 0; }
            for (int r = start; r < rows.Count; r++)
            {
                var row = rows[r];
                string o = row.Count > cOld ? row[cOld].Trim() : "", n = row.Count > cNew ? row[cNew].Trim() : "";
                if (o == "" || n == "") { warnings.Add($"Line {r + 1}: needs both an old and a new name"); continue; }
                rules.Add(new MaterialRule(o, n));
            }
            return rules;
        }

        public static List<MaterialRule> Read(string path, out List<string> warnings) =>
            ReadText(File.ReadAllText(path, Encoding.UTF8), out warnings);

        public static string WriteText(IEnumerable<MaterialRule> rules)
        {
            string Q(string s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
            var sb = new StringBuilder();
            sb.Append("Old name,New name\r\n");
            foreach (var r in rules) sb.Append(Q(r.Old)).Append(',').Append(Q(r.New)).Append("\r\n");
            return sb.ToString();
        }

        public static void Write(string path, IEnumerable<MaterialRule> rules) =>
            File.WriteAllText(path, WriteText(rules), new UTF8Encoding(true));

        /// <summary>
        /// Checks the list against the names in the model, in order, and says what each rule will do:
        /// - an old name not in the model, a rule from a name to itself, or a second rule for the same old name is skipped;
        /// - chains are followed (A > B and B > C: A goes straight to C); a loop skips every rule in it;
        /// - a new name not in the model is a rename, but only for one old name: the others then merge into it.
        ///   The old name with the most uses is the one renamed, so the fewest elements change.
        /// </summary>
        public static List<ResolvedRule> Resolve(IList<MaterialRule> rules, ICollection<string> namesInModel, Func<string, int> usesOf = null)
        {
            var inModel = new HashSet<string>(namesInModel, StringComparer.Ordinal);
            usesOf = usesOf ?? (_ => 0);
            var result = new List<ResolvedRule>();
            var first = new Dictionary<string, ResolvedRule>(StringComparer.Ordinal);

            foreach (var rule in rules)
            {
                var rr = new ResolvedRule { Rule = rule, Target = rule.New };
                result.Add(rr);
                if (rule.Old == rule.New) Skip(rr, "old and new are the same");
                else if (first.TryGetValue(rule.Old, out var earlier))
                    Skip(rr, earlier.Rule.New == rule.New ? "same rule twice" : $"{rule.Old} already goes to {earlier.Rule.New}");
                else
                {
                    first[rule.Old] = rr;
                    if (rule.Old == ByCategory) Skip(rr, "<By Category> is not a material");
                    else if (!inModel.Contains(rule.Old)) Skip(rr, "not in this model");
                }
            }

            // Follow chains through the rules that are still live.
            var live = result.Where(r => r.Action != RuleAction.Skip).ToDictionary(r => r.Rule.Old, r => r, StringComparer.Ordinal);
            foreach (var rr in live.Values.ToList())
            {
                var seen = new List<string> { rr.Rule.Old };
                var t = rr.Rule.New;
                while (live.TryGetValue(t, out var next) && !seen.Contains(t)) { seen.Add(t); t = next.Rule.New; }
                if (seen.Contains(t)) Skip(rr, "loop: " + string.Join(" > ", seen) + " > " + t);
                else { if (t != rr.Rule.New) rr.Note = $"follows {rr.Rule.New} on to {t}"; rr.Target = t; }
            }

            // Names that will exist after the run: what's in the model, minus the old names that are renamed away.
            foreach (var group in result.Where(r => r.Action != RuleAction.Skip).GroupBy(r => r.Target, StringComparer.Ordinal))
            {
                var target = group.Key;
                if (target == ByCategory) { foreach (var r in group) r.Action = RuleAction.Remove; continue; }
                if (inModel.Contains(target) && !live.ContainsKey(target)) { foreach (var r in group) r.Action = RuleAction.Replace; continue; }
                // The target is new (or is itself being replaced, which a chain already followed): rename one, merge the rest into it.
                var keep = group.OrderByDescending(r => usesOf(r.Rule.Old)).First();
                foreach (var r in group)
                {
                    r.Action = r == keep ? RuleAction.Rename : RuleAction.Replace;
                    if (r != keep) r.Note = Join(r.Note, $"merges into {target} after {keep.Rule.Old} is renamed");
                }
            }

            foreach (var r in result.Where(r => r.Action != RuleAction.Skip && r.Action != RuleAction.Remove && !IsOpenBepMaterialName(r.Target)))
                r.Note = Join(r.Note, r.Action == RuleAction.Rename ? "new name does not follow OB_<Class>_<Material>_<Detail>" : "the material it goes to does not follow OB_<Class>_<Material>_<Detail>");
            return result;
        }

        static void Skip(ResolvedRule r, string why) { r.Action = RuleAction.Skip; r.Note = why; }
        static string Join(string a, string b) => string.IsNullOrEmpty(a) ? b : a + "; " + b;

        // OB_<Class>_<Material>_<Detail>, with Detail optional; letters, digits, dots and hyphens inside a part.
        static readonly Regex ObName = new Regex(@"^OB_[A-Z][A-Za-z]*(_[A-Za-z0-9][A-Za-z0-9.\-]*){1,3}$");

        /// <summary>True for names that follow the OpenBEP material rule (docs/graphics-and-materials-standard.md).</summary>
        public static bool IsOpenBepMaterialName(string name) => name != null && ObName.IsMatch(name);
    }
}
