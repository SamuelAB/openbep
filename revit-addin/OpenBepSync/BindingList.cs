using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace OpenBepSync
{
    /// <summary>One shared parameter the plan requires, merged across all element rows of the binding list.</summary>
    public class RequiredParameter
    {
        public Guid Guid;
        public string Name = "";
        public bool IsInstance;
        public bool ScopeConflict;   // some rows say Instance, others Type
        public HashSet<string> CategoryIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> ElementTypes = new HashSet<string>();
        public int? FromLod;         // lowest "Required from LOD" across its rows; null when the list has none
    }

    /// <summary>Reads the binding list (CSV) exported by the OpenBEP Builder.</summary>
    public static class BindingList
    {
        public static List<RequiredParameter> Read(string path, out string listVersion, out List<string> warnings) =>
            ReadText(File.ReadAllText(path, Encoding.UTF8), out listVersion, out warnings);

        /// <summary>
        /// Reads the CSV text. Columns are found by name, so added columns (a project's own "LOD 351" column, say) are ignored.
        /// Levels are project-chosen: "Required from LOD" may hold 350, "LOD 351", "450 (custom)" or nothing.
        /// </summary>
        public static List<RequiredParameter> ReadText(string text, out string listVersion, out List<string> warnings)
        {
            warnings = new List<string>();
            listVersion = "";
            var rows = ParseCsv(text);
            if (rows.Count < 2) throw new InvalidDataException("The binding list is empty.");
            var head = rows[0].Select(h => h.Trim().TrimStart('﻿')).ToList();
            int Col(string name)
            {
                int i = head.FindIndex(h => string.Equals(h, name, StringComparison.OrdinalIgnoreCase));
                if (i < 0) throw new InvalidDataException("Column missing in the binding list: " + name);
                return i;
            }
            int cName = Col("Revit parameter"), cGuid = Col("GUID"), cKind = Col("Shared or built-in"),
                cScope = Col("Instance or type"), cEl = Col("Element type"), cCat = Col("Revit category ids");
            int cVer = head.FindIndex(h => h == "OpenBEP list version" || h == "CANBIM list version");
            int cLod = head.FindIndex(h => h.StartsWith("Required from LOD", StringComparison.OrdinalIgnoreCase));

            var byGuid = new Dictionary<Guid, RequiredParameter>();
            for (int r = 1; r < rows.Count; r++)
            {
                var row = rows[r];
                if (row.Count <= cCat) continue;
                if (cVer >= 0 && row.Count > cVer && listVersion == "") listVersion = row[cVer];
                if (!row[cKind].Equals("Shared", StringComparison.OrdinalIgnoreCase)) continue; // built-ins are not added; owner additions come as Shared rows with their own GUID
                if (!Guid.TryParse(row[cGuid], out var g)) { warnings.Add($"Row {r + 1}: no valid GUID for {row[cName]}"); continue; }
                bool inst = row[cScope].Equals("Instance", StringComparison.OrdinalIgnoreCase);
                if (!byGuid.TryGetValue(g, out var p))
                {
                    p = new RequiredParameter { Guid = g, Name = row[cName], IsInstance = inst };
                    byGuid[g] = p;
                }
                else if (p.IsInstance != inst)
                {
                    p.ScopeConflict = true;
                    p.IsInstance = true; // a parameter can be bound one way only; instance is the safer choice
                }
                foreach (var id in row[cCat].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                    p.CategoryIds.Add(id.Trim());
                p.ElementTypes.Add(row[cEl]);
                var lod = cLod >= 0 && row.Count > cLod ? Lod(row[cLod]) : null;
                if (lod != null && (p.FromLod == null || lod < p.FromLod)) p.FromLod = lod;
            }
            foreach (var p in byGuid.Values.Where(x => x.ScopeConflict))
                warnings.Add($"{p.Name}: instance on some elements and type on others; bound as instance.");
            return byGuid.Values.OrderBy(p => p.Name).ToList();
        }

        /// <summary>The level in a "Required from LOD" cell: the first three-digit number ("LOD 351" -> 351), or null.</summary>
        public static int? Lod(string cell)
        {
            var m = Regex.Match(cell ?? "", @"(?<!\d)\d{3}(?!\d)");
            return m.Success ? int.Parse(m.Value) : (int?)null;
        }

        /// <summary>
        /// Checks the binding list against the shared parameter file before anything is bound:
        /// every required GUID must be in the file under the same name, and OpenBEP parameters (OB_, CA_) must keep their fixed GUID.
        /// Owner additions have other names and their own GUIDs, so the GUID rule leaves them alone.
        /// </summary>
        public static List<string> Check(IEnumerable<RequiredParameter> required, IEnumerable<SharedParameterEntry> file)
        {
            var warnings = new List<string>();
            var byGuid = new Dictionary<Guid, SharedParameterEntry>();
            foreach (var e in file)
            {
                if (byGuid.ContainsKey(e.Guid)) warnings.Add($"{e.Name}: GUID {e.Guid} appears twice in the shared parameter file");
                else byGuid[e.Guid] = e;
                if (OpenBepNames.IsOpenBep(e.Name) && e.Guid != OpenBepNames.Expected(e.Name))
                    warnings.Add($"{e.Name}: GUID {e.Guid} in the shared parameter file is not the fixed OpenBEP GUID {OpenBepNames.Expected(e.Name)}");
            }
            foreach (var p in required)
            {
                if (!byGuid.TryGetValue(p.Guid, out var e)) warnings.Add($"{p.Name}: GUID {p.Guid} is not in the shared parameter file");
                else if (e.Name != p.Name) warnings.Add($"{p.Name}: the shared parameter file names GUID {p.Guid} {e.Name}");
                if (OpenBepNames.IsOpenBep(p.Name) && p.Guid != OpenBepNames.Expected(p.Name))
                    warnings.Add($"{p.Name}: GUID {p.Guid} in the binding list is not the fixed OpenBEP GUID {OpenBepNames.Expected(p.Name)}");
            }
            return warnings;
        }

        /// <summary>RFC 4180 CSV parser (quoted fields, doubled quotes, line breaks inside quotes).</summary>
        public static List<List<string>> ParseCsv(string text)
        {
            var rows = new List<List<string>>();
            var row = new List<string>();
            var f = new StringBuilder();
            bool q = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (q)
                {
                    if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { f.Append('"'); i++; }
                    else if (c == '"') q = false;
                    else f.Append(c);
                }
                else if (c == '"') q = true;
                else if (c == ',') { row.Add(f.ToString()); f.Clear(); }
                else if (c == '\n' || c == '\r')
                {
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                    row.Add(f.ToString()); f.Clear();
                    if (row.Count > 1 || row[0] != "") rows.Add(row);
                    row = new List<string>();
                }
                else f.Append(c);
            }
            if (f.Length > 0 || row.Count > 0) { row.Add(f.ToString()); rows.Add(row); }
            return rows;
        }
    }
}
