using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace OpenBepSync
{
    /// <summary>What Revit makes for a view type.</summary>
    public enum ViewKind { FloorPlan, CeilingPlan, Axo, Section, Elevation, Drafting, Camera }

    /// <summary>A drawing set: short code in the view name, full words in OB_ViewSet.</summary>
    public class ViewSetDef
    {
        public string Code, Folder, FolderFr;
        public ViewSetDef(string code, string folder, string folderFr) { Code = code; Folder = folder; FolderFr = folderFr; }
    }

    /// <summary>A view type: numbered in OB_ViewType (010-PLAN), without its number in the view name (PLAN).</summary>
    public class ViewTypeDef
    {
        public string Number, Label, LabelFr;
        public ViewTypeDef(string number, string label, string labelFr) { Number = number; Label = label; LabelFr = labelFr; }
        public string Code => Number.Substring(4);
        public ViewKind Kind =>
            Code.StartsWith("PLAN") ? ViewKind.FloorPlan : Code.StartsWith("RCP") ? ViewKind.CeilingPlan : Code.StartsWith("AXO") ? ViewKind.Axo :
            Code.StartsWith("SECT") ? ViewKind.Section : Code.StartsWith("ELEV") ? ViewKind.Elevation : Code == "3D" ? ViewKind.Camera : ViewKind.Drafting;
        /// <summary>Plans and ceiling plans: one view per level, the ones Create views makes.</summary>
        public bool PerLevel => Kind == ViewKind.FloorPlan || Kind == ViewKind.CeilingPlan;
        /// <summary>A site plan sits on a level but covers the whole site, so its name has no level: N-CON-PLAN-SITE.</summary>
        public bool LevelInName => Code != "PLAN-SITE";
    }

    /// <summary>One line of a view plan: a view to make. Zone is empty for a parent view, or the scope box zone of a dependent.</summary>
    public class PlannedView : IEquatable<PlannedView>
    {
        public string Phase = "", Set = "", Type = "", Level = "", Option = "", Zone = "";
        public PlannedView() { }
        public PlannedView(string phase, string set, string type, string level, string option = "", string zone = "")
        { Phase = phase ?? ""; Set = set ?? ""; Type = type ?? ""; Level = level ?? ""; Option = option ?? ""; Zone = zone ?? ""; }
        public PlannedView Parent => new PlannedView(Phase, Set, Type, Level, Option);
        public bool Equals(PlannedView o) => o != null && Phase == o.Phase && Set == o.Set && Type == o.Type && Level == o.Level && Option == o.Option && Zone == o.Zone;
        public override bool Equals(object o) => Equals(o as PlannedView);
        public override int GetHashCode() => (Phase + "|" + Set + "|" + Type + "|" + Level + "|" + Option + "|" + Zone).GetHashCode();
    }

    /// <summary>
    /// The OpenBEP view naming rule (docs/naming-rules.md, Views) in code: drawing sets, numbered view types,
    /// view names, template names, Title on Sheet and the view plan file. No Revit needed.
    /// </summary>
    public static class ViewNaming
    {
        // Kept the same as the tables in docs/naming-rules.md; a test compares them.
        public static readonly ViewSetDef[] Sets =
        {
            new ViewSetDef("AN", "ANALYSIS", "Analyse"),
            new ViewSetDef("BIM", "BIM MANAGEMENT", "Gestion BIM"),
            new ViewSetDef("CON", "CONSTRUCTION", "Construction"),
            new ViewSetDef("EXPORT", "EXPORT", "Exportation"),
            new ViewSetDef("PERMIT", "PERMIT", "Permis"),
            new ViewSetDef("PRES", "PRESENTATION", "Présentation"),
            new ViewSetDef("QC", "QUALITY CONTROL", "Contrôle de la qualité"),
            new ViewSetDef("WORK", "WORK", "Travail"),
            new ViewSetDef("Z-WIP", "Z-WIP", "Travail en cours"),
        };

        public static readonly ViewTypeDef[] Types =
        {
            new ViewTypeDef("000-AXO", "Axonometric views", "Vues axonométriques"),
            new ViewTypeDef("001-PLAN-GEOREF", "Georeferencing and shared coordinates", "Géoréférencement et coordonnées partagées"),
            new ViewTypeDef("002-PLAN-GRID", "Grids and levels", "Grilles et niveaux"),
            new ViewTypeDef("003-PLAN-SCOPE", "Scope boxes", "Zones de définition"),
            new ViewTypeDef("004-AXO-EXPORT", "3D views for viewers and exports", "Vues 3D pour visionneuses et exportations"),
            new ViewTypeDef("005-PLAN-SITE", "Site plan", "Plan d'implantation"),
            new ViewTypeDef("006-PLAN-CIVIL", "Civil plan", "Plan de génie civil"),
            new ViewTypeDef("007-PLAN-BPAD", "Building pad", "Plateforme du bâtiment"),
            new ViewTypeDef("009-PLAN-DEMO", "Demolition plan", "Plan de démolition"),
            new ViewTypeDef("010-PLAN", "Floor plan", "Plan d'étage"),
            new ViewTypeDef("011-PLAN-ROOF", "Roof plan", "Plan de toiture"),
            new ViewTypeDef("012-PLAN-CAD", "Plan for tracing old CAD drawings", "Plan de calque d'anciens dessins DAO"),
            new ViewTypeDef("013-PLAN-ROOM", "Room plan", "Plan des locaux"),
            new ViewTypeDef("014-PLAN-FIRE", "Fire separation plan", "Plan des séparations coupe-feu"),
            new ViewTypeDef("015-PLAN-DOOR", "Door plan", "Plan des portes"),
            new ViewTypeDef("016-PLAN-WALL", "Wall type plan", "Plan des types de murs"),
            new ViewTypeDef("017-RCP-LIGHT", "Lighting ceiling plan", "Plan de plafond, éclairage"),
            new ViewTypeDef("018-PLAN-MASSING", "Massing plan", "Plan de volumétrie"),
            new ViewTypeDef("019-PLAN-PANEL", "Panel layout plan", "Plan de calepinage des panneaux"),
            new ViewTypeDef("020-PLAN-EGRESS", "Egress plan", "Plan d'évacuation"),
            new ViewTypeDef("021-PLAN-DEPT", "Department plan", "Plan des services"),
            new ViewTypeDef("023-PLAN-LIGHT", "Lighting plan", "Plan d'éclairage"),
            new ViewTypeDef("030-RCP", "Reflected ceiling plan", "Plan de plafond réfléchi"),
            new ViewTypeDef("050-PLAN-FURN", "Furniture plan", "Plan du mobilier"),
            new ViewTypeDef("070-PLAN-STAIR", "Enlarged stair plan", "Plan agrandi d'escalier"),
            new ViewTypeDef("100-PLAN-DETL", "Detail plan", "Plan de détail"),
            new ViewTypeDef("200-SECT", "Section", "Coupe"),
            new ViewTypeDef("210-SECT-DET", "Section detail", "Coupe de détail"),
            new ViewTypeDef("300-ELEV", "Elevation", "Élévation"),
            new ViewTypeDef("400-DET", "Detail", "Détail"),
            new ViewTypeDef("410-LEGEND", "Legend drawn as a drafting view", "Légende (vue de dessin)"),
            new ViewTypeDef("500-3D", "Camera view", "Vue caméra"),
        };

        /// <summary>The drawing set codes, plus one person's set (Z-USER-SAM).</summary>
        public static IEnumerable<string> SetCodes(string userName)
        {
            foreach (var s in Sets) yield return s.Code;
            var u = Regex.Replace((userName ?? "").ToUpperInvariant(), "[^A-Z0-9]", "");
            if (u != "") yield return "Z-USER-" + u;
        }

        /// <summary>OB_ViewSet value for a set code: CON gives CONSTRUCTION; a person's set and an unknown code stay as they are.</summary>
        public static string SetFolder(string code) => Sets.FirstOrDefault(s => s.Code == code)?.Folder ?? code ?? "";

        /// <summary>The view type by its number (010-PLAN) or its code (PLAN).</summary>
        public static ViewTypeDef Type(string numberOrCode) => Types.FirstOrDefault(t => t.Number == numberOrCode || t.Code == numberOrCode);

        static readonly Dictionary<string, string> PhaseWords = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Existing"] = "EX", ["Existant"] = "EX", ["New Construction"] = "N", ["Nouvelle construction"] = "N",
        };

        /// <summary>
        /// The phase code that starts every view name: Existing is EX, New Construction is N, Phase 1 is P1.
        /// A phase named with its code first (EX Existing) gives that code. Anything else: its initials.
        /// </summary>
        public static string PhaseCode(string phaseName)
        {
            var n = (phaseName ?? "").Trim();
            if (PhaseWords.TryGetValue(n, out var c)) return c;
            var m = Regex.Match(n, @"^(?:Phase|P)\s*(\d+)$", RegexOptions.IgnoreCase);
            if (m.Success) return "P" + m.Groups[1].Value;
            m = Regex.Match(n, @"^([A-Z][A-Z0-9]{0,3})\s+\S");
            if (m.Success) return m.Groups[1].Value;
            var initials = string.Concat(Regex.Matches(n, @"[A-Za-z0-9]+").Cast<Match>().Select(w => char.ToUpperInvariant(w.Value[0])));
            return initials == "" ? "X" : initials;
        }

        /// <summary>&lt;PHASE&gt;-&lt;SET&gt;-&lt;TYPE&gt;-&lt;Level&gt;[-OPTn][-Zone], for example N-CON-PLAN-EGRESS-2ND-FLR or N-CON-PLAN-2ND-FLR-A.</summary>
        public static string Name(PlannedView v)
        {
            var t = Type(v.Type);
            var parts = new List<string> { v.Phase, v.Set, t?.Code ?? v.Type, t?.LevelInName == false ? "" : v.Level, v.Option, v.Zone };
            return string.Join("-", parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()));
        }

        // Storeys 1ST-FLR, 2ND-FLR, 12TH-FLR; the roof takes the next number, 4-ROOF; basements BSMNT-1, BSMNT-2.
        // Levels in between: the number below, a decimal, then words: 1.5-STAIR-LANDING, 1.9-US, BSMNT-1.5-LANDING.
        static readonly Regex Floor = new Regex(@"^(\d+)(ST|ND|RD|TH)-FLR$");
        static readonly Regex RoofLevel = new Regex(@"^(\d+)-ROOF$");
        static readonly Regex Basement = new Regex(@"^BSMNT-(\d+)$");
        static readonly Regex Between = new Regex(@"^(BSMNT-)?(\d+)\.(\d+)-([A-Z0-9]+(?:-[A-Z0-9]+)*)$");

        /// <summary>1ST, 2ND, 3RD, 4TH, 11TH, 21ST.</summary>
        public static string Ordinal(int n)
        {
            var end = n % 100 >= 11 && n % 100 <= 13 ? "TH" : (n % 10) switch { 1 => "ST", 2 => "ND", 3 => "RD", _ => "TH" };
            return n + end;
        }

        /// <summary>The storey code for a floor number: 2 gives 2ND-FLR.</summary>
        public static string FloorCode(int n) => Ordinal(n) + "-FLR";

        /// <summary>The floor number of a storey code (2ND-FLR gives 2), or 0.</summary>
        public static int FloorNumber(string level)
        {
            var m = Floor.Match(level ?? "");
            return m.Success && level == FloorCode(int.Parse(m.Groups[1].Value)) ? int.Parse(m.Groups[1].Value) : 0;
        }

        /// <summary>True for level names that follow the OpenBEP level rule.</summary>
        public static bool IsLevelName(string name)
        {
            if (name == null) return false;
            if (FloorNumber(name) > 0) return true;
            var m = RoofLevel.Match(name);
            if (m.Success) return int.Parse(m.Groups[1].Value) >= 2 && !name.StartsWith("0");
            m = Basement.Match(name);
            if (m.Success) return int.Parse(m.Groups[1].Value) >= 1 && m.Groups[1].Value[0] != '0';
            m = Between.Match(name);
            return m.Success && int.Parse(m.Groups[2].Value) >= 1 && m.Groups[2].Value[0] != '0';
        }

        /// <summary>The words for a level code, for Title on Sheet: 2ND-FLR is 2nd floor (2e étage), 1ST-FLR is Ground floor (Rez-de-chaussée). Empty when the name is not a level code.</summary>
        public static string LevelTitle(string level, bool french)
        {
            if (!IsLevelName(level)) return "";
            int f = FloorNumber(level);
            if (f == 1) return french ? "Rez-de-chaussée" : "Ground floor";
            if (f > 1) return french ? $"{f}e étage" : $"{Ordinal(f).ToLowerInvariant()} floor";
            if (RoofLevel.IsMatch(level)) return french ? "Toit" : "Roof";
            var m = Basement.Match(level);
            if (m.Success)
            {
                int b = int.Parse(m.Groups[1].Value);
                string[] en = { "", "First basement", "Second basement", "Third basement" }, fr = { "", "Premier sous-sol", "Deuxième sous-sol", "Troisième sous-sol" };
                return b < en.Length ? (french ? fr[b] : en[b]) : (french ? $"Sous-sol {b}" : $"Basement {b}");
            }
            m = Between.Match(level);
            var number = (m.Groups[1].Success ? (french ? "sous-sol " : "basement ") : "") + m.Groups[2].Value + (french ? "," : ".") + m.Groups[3].Value;
            if (m.Groups[4].Value == "MEZZ") return "Mezzanine " + number;
            return (french ? "Niveau " : "Level ") + number;
        }

        /// <summary>Title on Sheet: the level's words and the view type's words, so the codes never reach the drawings. "2nd floor - Egress plan".</summary>
        public static string TitleOnSheet(PlannedView v, bool french)
        {
            var t = Type(v.Type);
            var what = t == null ? "" : french ? t.LabelFr : t.Label;
            var level = t?.LevelInName == false ? "" : LevelTitle(v.Level, french);
            var title = level == "" ? what : what == "" ? level : level + " - " + what;
            return v.Zone == "" ? title : title + (french ? " - zone " : " - area ") + v.Zone;
        }

        /// <summary>The zone a scope box stands for: SB-A gives A; any other name, in capitals without spaces.</summary>
        public static string ZoneOf(string scopeBoxName)
        {
            var n = (scopeBoxName ?? "").Trim();
            if (n.StartsWith("SB-", StringComparison.OrdinalIgnoreCase)) n = n.Substring(3);
            return Regex.Replace(n.ToUpperInvariant(), @"[^A-Z0-9]+", "");
        }

        /// <summary>
        /// The view template for a view: &lt;Discipline&gt;-&lt;TYPE&gt;-&lt;Set&gt;-&lt;scale&gt; (ARCH-PLAN-CON-100).
        /// When several scales exist, the largest scale number (the least detailed) wins, as the usual one for a whole floor.
        /// Null when the model has none.
        /// </summary>
        public static string MatchTemplate(IEnumerable<string> templateNames, string discipline, string typeNumberOrCode, string set)
        {
            var code = Type(typeNumberOrCode)?.Code ?? typeNumberOrCode;
            var head = $"{discipline}-{code}-{set}-";
            return templateNames
                .Where(n => n.StartsWith(head, StringComparison.Ordinal) && Regex.IsMatch(n.Substring(head.Length), @"^\d+$"))
                .OrderByDescending(n => int.Parse(n.Substring(head.Length)))
                .FirstOrDefault();
        }

        /// <summary>
        /// The phase filter a view gets when no template sets one: the demolition plan shows previous and demolished elements,
        /// views of the existing phase show it complete, every other view shows previous and new (docs/naming-rules.md, Phase filters).
        /// </summary>
        public static string DefaultPhaseFilter(string phaseCode, string typeNumberOrCode)
        {
            if (Type(typeNumberOrCode)?.Code == "PLAN-DEMO") return "Show Previous + Demo";
            return phaseCode == "EX" ? "Show Complete" : "Show Previous + New";
        }

        // ---- The view plan file: one line per view, written by people, the add-in or an AI, reviewed in the add-in before anything is made.

        public static readonly string[] PlanColumns = { "Phase", "Set", "Type", "Level", "Option", "Zone" };

        public static List<PlannedView> ReadPlanText(string text, out List<string> warnings)
        {
            warnings = new List<string>();
            var rows = BindingList.ParseCsv(text ?? "").Where(r => r.Any(c => c.Trim() != "") && !r[0].TrimStart().StartsWith("#")).ToList();
            var result = new List<PlannedView>();
            if (rows.Count == 0) return result;
            var head = rows[0].Select(h => h.Trim().TrimStart('﻿')).ToList();
            int[] col = PlanColumns.Select(c => head.FindIndex(h => string.Equals(h, c, StringComparison.OrdinalIgnoreCase))).ToArray();
            int start = 1;
            if (col[0] < 0 || col[1] < 0 || col[2] < 0) { col = new[] { 0, 1, 2, 3, 4, 5 }; start = 0; }
            for (int r = start; r < rows.Count; r++)
            {
                string Cell(int i) => col[i] >= 0 && col[i] < rows[r].Count ? rows[r][col[i]].Trim() : "";
                var v = new PlannedView(Cell(0), Cell(1), Cell(2), Cell(3), Cell(4), Cell(5));
                var t = Type(v.Type);
                if (v.Phase == "" || v.Set == "" || v.Type == "") { warnings.Add($"Line {r + 1}: needs a phase, a set and a type"); continue; }
                if (t == null) { warnings.Add($"Line {r + 1}: {v.Type} is not an OpenBEP view type"); continue; }
                v.Type = t.Number;
                if (t.PerLevel && v.Level == "") { warnings.Add($"Line {r + 1}: a {t.Label.ToLowerInvariant()} needs a level"); continue; }
                if (!result.Contains(v)) result.Add(v);
            }
            return result;
        }

        public static List<PlannedView> ReadPlan(string path, out List<string> warnings) => ReadPlanText(File.ReadAllText(path, Encoding.UTF8), out warnings);

        public static string WritePlanText(IEnumerable<PlannedView> views)
        {
            string Q(string s) => s.IndexOfAny(new[] { ',', '"', '\n' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
            var sb = new StringBuilder(string.Join(",", PlanColumns)).Append("\r\n");
            foreach (var v in views)
                sb.Append(string.Join(",", new[] { v.Phase, v.Set, v.Type, v.Level, v.Option, v.Zone }.Select(Q))).Append("\r\n");
            return sb.ToString();
        }

        public static void WritePlan(string path, IEnumerable<PlannedView> views) => File.WriteAllText(path, WritePlanText(views), new UTF8Encoding(true));
    }
}
