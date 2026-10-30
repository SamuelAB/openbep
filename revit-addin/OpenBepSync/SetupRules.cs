using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace OpenBepSync
{
    /// <summary>What a setup plan line does.</summary>
    public enum SetupAction { Rename, Fill, Create, Delete, Check }

    /// <summary>
    /// One line of a setup plan: the reviewed plan the Set up project window shows, and an AI or MCP may write.
    /// Element is the Revit element id (empty for something to create). New is the new name, or for Fill the value.
    /// </summary>
    public class SetupLine
    {
        public string Kind = "", Element = "", Current = "", New = "", Rule = "", Note = "";
        public SetupAction Action;
        public bool Apply = true;
        public SetupLine() { }
        public SetupLine(string kind, SetupAction action, string element, string current, string newName, string rule, string note = "")
        { Kind = kind; Action = action; Element = element ?? ""; Current = current ?? ""; New = newName ?? ""; Rule = rule ?? ""; Note = note ?? ""; }
    }

    /// <summary>A view as the audit sees it, without Revit.</summary>
    public class ViewFacts
    {
        public string Id = "", Name = "";
        public ViewKind Kind;
        public bool Perspective, OnSheet, IsDependent;
        public string Phase = "";        // phase code, empty for views with no phase (drafting)
        public string Level = "";        // associated level, after level renames
        public string ViewSet = "";      // OB_ViewSet as it is
        public string ViewType = "";     // OB_ViewType as it is
        public string ParentName = "";   // the parent's new name, for dependents
        public string Zone = "";         // scope box zone, for dependents
    }

    /// <summary>
    /// The Set up project rules: how existing levels and views get OpenBEP names (docs/naming-rules.md),
    /// and the setup plan file. No Revit needed.
    /// </summary>
    public static class SetupRules
    {
        // ---- Levels

        static readonly Regex Ground = new Regex(@"^(ground|ground floor|rdc|rez[- ]de[- ]chauss[ée]e|main floor|first floor)$", RegexOptions.IgnoreCase);
        static readonly Regex Roof = new Regex(@"^(roof|toit|toiture|roof level|niveau toit|rf)$", RegexOptions.IgnoreCase);
        static readonly Regex Basement = new Regex(@"^(?:basement|bsmnt|sous[- ]sol|ss|b|p|parking|cellar)\s*-?\s*0*(\d+)$", RegexOptions.IgnoreCase);
        static readonly Regex Numbered = new Regex(@"^(?:level|niveau|floor|[ée]tage|storey|story|l|n|lvl)\s*-?\s*0*(\d+)\s*(m|mezz|mezzanine)?$", RegexOptions.IgnoreCase);
        static readonly Regex Ordinal = new Regex(@"^0*(\d+)\s*(?:st|nd|rd|th|e|er|re|ème)\s*-?\s*(?:floor|flr|fl|étage|etage)$", RegexOptions.IgnoreCase);

        /// <summary>
        /// The OpenBEP code for a storey level from its current name: Level 1 or Ground floor gives 1ST-FLR, Level 2 gives 2ND-FLR,
        /// Basement 1 gives BSMNT-1, Level 1 Mezzanine gives 1.5-MEZZ, and the old codes (L02, B1, L01M) move over too.
        /// The roof takes the number after the top floor (4-ROOF over a 3rd floor), so it needs <paramref name="roofNumber"/>.
        /// Null when the name doesn't say (the window then asks a person). Revit's own default "Level 1" is the ground floor.
        /// </summary>
        public static string ProposeLevel(string name, int roofNumber = 0)
        {
            var n = Regex.Replace((name ?? "").Trim(), @"\s+", " ");
            if (ViewNaming.IsLevelName(n)) return n;
            if (Ground.IsMatch(n)) return ViewNaming.FloorCode(1);
            if (Roof.IsMatch(n)) return roofNumber >= 2 ? roofNumber + "-ROOF" : null;
            var m = Basement.Match(n);
            if (m.Success) return int.Parse(m.Groups[1].Value) >= 1 ? "BSMNT-" + int.Parse(m.Groups[1].Value) : null;
            m = Numbered.Match(n);
            if (!m.Success) m = Ordinal.Match(n);
            if (m.Success)
            {
                int k = int.Parse(m.Groups[1].Value);
                if (k < 1 || k > 199) return null;
                return m.Groups.Count > 2 && m.Groups[2].Success ? k + ".5-MEZZ" : ViewNaming.FloorCode(k);
            }
            return null;
        }

        // ---- Views

        /// <summary>The view type an unfiled view gets from what Revit made it.</summary>
        public static string DefaultType(ViewKind kind, bool perspective)
        {
            switch (kind)
            {
                case ViewKind.FloorPlan: return "010-PLAN";
                case ViewKind.CeilingPlan: return "030-RCP";
                case ViewKind.Section: return "200-SECT";
                case ViewKind.Elevation: return "300-ELEV";
                case ViewKind.Axo: return perspective ? "500-3D" : "000-AXO";
                case ViewKind.Camera: return "500-3D";
                default: return "400-DET";
            }
        }

        static readonly HashSet<string> Filler = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "VIEW", "PLAN", "FLOOR", "CEILING", "RCP", "SECTION", "SECT", "ELEVATION", "ELEV", "3D", "DETAIL", "DET", "DRAFTING", "COPY", "OF", "THE", "AND", "LEVEL" };

        /// <summary>
        /// What tells a view without a level apart, from its current name: "Section 1" gives 01, "East" gives EAST,
        /// "Stair section A" gives STAIR-A. Capitals, words joined by hyphens, numbers on two digits.
        /// </summary>
        public static string Suffix(string name)
        {
            var words = Regex.Matches(RemoveAccents(name ?? ""), @"[A-Za-z0-9]+").Cast<Match>().Select(m => m.Value.ToUpperInvariant())
                .Where(w => !Filler.Contains(w)).Select(w => Regex.IsMatch(w, @"^\d$") ? "0" + w : w).ToList();
            return string.Join("-", words);
        }

        static string RemoveAccents(string s) =>
            new string(s.Normalize(NormalizationForm.FormD).Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());

        /// <summary>The set code for an OB_ViewSet folder (CONSTRUCTION gives CON); a code or a person's set stays as it is.</summary>
        public static string SetCode(string folder)
        {
            var f = (folder ?? "").Trim();
            var s = ViewNaming.Sets.FirstOrDefault(x => string.Equals(x.Folder, f, StringComparison.OrdinalIgnoreCase) || string.Equals(x.Code, f, StringComparison.OrdinalIgnoreCase));
            if (s != null) return s.Code;
            return Regex.IsMatch(f, @"^Z-USER-[A-Z0-9]+$") ? f : "";
        }

        /// <summary>
        /// Splits a name that follows the view rule into phase, set, type and the rest. Null when it doesn't follow it.
        /// The phase is one of phaseCodes or DRAFT; the set a known code or Z-USER-NAME; the type an OpenBEP type code.
        /// </summary>
        public static string[] Parse(string name, ICollection<string> phaseCodes)
        {
            var n = name ?? "";
            foreach (var phase in phaseCodes.Concat(new[] { "DRAFT" }).OrderByDescending(p => p.Length))
            {
                if (!n.StartsWith(phase + "-", StringComparison.Ordinal)) continue;
                var rest = n.Substring(phase.Length + 1);
                var m = Regex.Match(rest, @"^(Z-USER-[A-Z0-9]+|Z-WIP|[A-Z]+)-(.+)$");
                if (!m.Success || SetCode(m.Groups[1].Value) == "") continue;
                var afterSet = m.Groups[2].Value;
                foreach (var t in ViewNaming.Types.OrderByDescending(t => t.Code.Length))
                    if (afterSet == t.Code || afterSet.StartsWith(t.Code + "-", StringComparison.Ordinal))
                        return new[] { phase, m.Groups[1].Value, t.Number, afterSet.Length > t.Code.Length ? afterSet.Substring(t.Code.Length + 1) : "" };
            }
            return null;
        }

        /// <summary>
        /// The name and browser values a view should have. The set comes from OB_ViewSet when filled, otherwise
        /// CON for a view on a sheet and WORK for the rest; Revit's own {3D} goes to Z-WIP. The type comes from OB_ViewType
        /// when filled, otherwise from what Revit made. A name that already follows the rule keeps its set, type and ending.
        /// </summary>
        public static (string name, string set, string type, string why) ProposeView(ViewFacts v, ICollection<string> phaseCodes)
        {
            var parsed = Parse(v.Name, phaseCodes);
            string phase = v.Kind == ViewKind.Drafting || v.Phase == "" ? "DRAFT" : v.Phase;
            string set = SetCode(v.ViewSet), type = ViewNaming.Type(v.ViewType)?.Number, why;
            if (parsed != null)
            {
                set = set == "" ? parsed[1] : set;
                type = type ?? parsed[2];
            }
            if (set == "")
            {
                set = v.Name.StartsWith("{") ? "Z-WIP" : v.OnSheet ? "CON" : "WORK";
                why = v.Name.StartsWith("{") ? "Revit's own view: unfinished views set" : v.OnSheet ? "set guessed: placed on a sheet" : "set guessed: not on a sheet";
            }
            else why = "";
            if (type == null && v.Kind == ViewKind.FloorPlan && SiteWords.IsMatch(v.Name)) type = "005-PLAN-SITE";
            type = type ?? DefaultType(v.Kind, v.Perspective);

            string name;
            if (v.IsDependent && v.ParentName != "")
                name = v.ParentName + "-" + (v.Zone != "" ? v.Zone : parsed != null && parsed[3] != "" ? parsed[3].Split('-').Last() : "A");
            else
            {
                var t = ViewNaming.Type(type);
                string ending;
                if (t.PerLevel && v.Level != "") ending = v.Level;
                else if (parsed != null && parsed[3] != "") ending = parsed[3];
                else ending = Suffix(v.Name);
                name = ViewNaming.Name(new PlannedView(phase, set, type, ending));
            }
            return (name, set, type, why);
        }

        static readonly Regex SiteWords = new Regex(@"\b(site|implantation)\b", RegexOptions.IgnoreCase);

        /// <summary>Makes every proposed name unique among themselves and the names that stay: the second N-WORK-SECT-01 becomes N-WORK-SECT-01-2.</summary>
        public static void MakeUnique(IList<SetupLine> renames, ICollection<string> namesThatStay)
        {
            var taken = new HashSet<string>(namesThatStay, StringComparer.Ordinal);
            foreach (var l in renames)
            {
                var n = l.New;
                for (int k = 2; taken.Contains(n); k++) n = l.New + "-" + k;
                if (n != l.New) { l.Note = Join(l.Note, $"{l.New} is taken"); l.New = n; }
                taken.Add(n);
            }
        }

        static string Join(string a, string b) => string.IsNullOrEmpty(a) ? b : a + "; " + b;

        // ---- Phase filters (docs/naming-rules.md, Phase filters)

        public static readonly string[] PhaseFiltersToDelete = { "Show Demo + New", "Show Previous Phase" };
        public const string PhaseFilterToAdd = "Show Existing";

        /// <summary>How each kept filter shows New, Existing, Demolished and Temporary elements.</summary>
        public static readonly Dictionary<string, string[]> PhaseFilterShows = new Dictionary<string, string[]>
        {
            ["Show All"] = new[] { "By category", "Overridden", "Overridden", "Overridden" },
            ["Show Complete"] = new[] { "By category", "By category", "Not displayed", "Not displayed" },
            ["Show New"] = new[] { "By category", "Not displayed", "Not displayed", "Not displayed" },
            ["Show Previous + Demo"] = new[] { "Not displayed", "Overridden", "Overridden", "Not displayed" },
            ["Show Previous + New"] = new[] { "By category", "Overridden", "Not displayed", "Not displayed" },
            ["Show Existing"] = new[] { "Not displayed", "By category", "Not displayed", "Not displayed" },
        };

        public static readonly string[] PhaseStatuses = { "New", "Existing", "Demolished", "Temporary" };
        public static readonly string[] Presentations = { "By category", "Overridden", "Not displayed" };

        /// <summary>"Shows=New: By category, Existing: Overridden, …", the value of a Fill line for a phase filter.</summary>
        public static string ShowsText(string[] shows) => "Shows=" + string.Join(", ", PhaseStatuses.Select((st, i) => st + ": " + shows[i]));

        /// <summary>Reads a ShowsText value back; null when it isn't one.</summary>
        public static string[] ParseShows(string text)
        {
            if (text == null || !text.StartsWith("Shows=")) return null;
            var result = new string[4];
            foreach (var part in text.Substring(6).Split(','))
            {
                var kv = part.Split(':');
                if (kv.Length != 2) return null;
                int i = Array.FindIndex(PhaseStatuses, st => string.Equals(st, kv[0].Trim(), StringComparison.OrdinalIgnoreCase));
                var how = Presentations.FirstOrDefault(pr => string.Equals(pr, kv[1].Trim(), StringComparison.OrdinalIgnoreCase));
                if (i < 0 || how == null) return null;
                result[i] = how;
            }
            return result.All(r => r != null) ? result : null;
        }

        // ---- The setup plan file: Kind, Action, Element, Current name, New name, Rule, Note

        public static readonly string[] PlanColumns = { "Kind", "Action", "Element", "Current name", "New name", "Rule", "Note" };

        public static string WritePlanText(IEnumerable<SetupLine> lines)
        {
            string Q(string s) => (s ?? "").IndexOfAny(new[] { ',', '"', '\n' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s ?? "";
            var sb = new StringBuilder(string.Join(",", PlanColumns)).Append("\r\n");
            foreach (var l in lines)
                sb.Append(string.Join(",", new[] { l.Kind, l.Action.ToString(), l.Element, l.Current, l.New, l.Rule, l.Note }.Select(Q))).Append("\r\n");
            return sb.ToString();
        }

        public static void WritePlan(string path, IEnumerable<SetupLine> lines) => File.WriteAllText(path, WritePlanText(lines), new UTF8Encoding(true));

        /// <summary>Reads a setup plan. Lines need a kind, a known action and, except to create, an element id or a current name.</summary>
        public static List<SetupLine> ReadPlanText(string text, out List<string> warnings)
        {
            warnings = new List<string>();
            var rows = BindingList.ParseCsv(text ?? "").Where(r => r.Any(c => c.Trim() != "") && !r[0].TrimStart().StartsWith("#")).ToList();
            var result = new List<SetupLine>();
            if (rows.Count == 0) return result;
            var head = rows[0].Select(h => h.Trim().TrimStart('﻿')).ToList();
            int[] col = PlanColumns.Select(c => head.FindIndex(h => string.Equals(h, c, StringComparison.OrdinalIgnoreCase))).ToArray();
            if (col[0] < 0 || col[1] < 0 || col[4] < 0) { warnings.Add("The first line must name the columns: " + string.Join(", ", PlanColumns)); return result; }
            for (int r = 1; r < rows.Count; r++)
            {
                string Cell(int i) => col[i] >= 0 && col[i] < rows[r].Count ? rows[r][col[i]].Trim() : "";
                if (!Enum.TryParse(Cell(1), true, out SetupAction action)) { warnings.Add($"Line {r + 1}: unknown action {Cell(1)}"); continue; }
                var l = new SetupLine(Cell(0), action, Cell(2), Cell(3), Cell(4), Cell(5), Cell(6));
                if (l.Kind == "") { warnings.Add($"Line {r + 1}: needs a kind"); continue; }
                if (action != SetupAction.Create && action != SetupAction.Check && l.Element == "" && l.Current == "") { warnings.Add($"Line {r + 1}: needs an element id or its current name"); continue; }
                if (action == SetupAction.Create && l.New == "") { warnings.Add($"Line {r + 1}: needs a new name"); continue; }
                // A rename with no new name is one left for a person to type: it comes back unticked.
                if (action == SetupAction.Check || (action == SetupAction.Rename && l.New == "")) l.Apply = false;
                result.Add(l);
            }
            return result;
        }

        public static List<SetupLine> ReadPlan(string path, out List<string> warnings) => ReadPlanText(File.ReadAllText(path, Encoding.UTF8), out warnings);
    }
}
