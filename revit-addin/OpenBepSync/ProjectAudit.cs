using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;

namespace OpenBepSync
{
    /// <summary>
    /// Set up project: checks a model's names against the OpenBEP rules and writes a setup plan (levels, views and their
    /// browser parameters, view templates, phase filters, materials). Apply runs the reviewed plan in one transaction.
    /// The window and the autotest both use it; an AI or MCP can write the same plan file.
    /// </summary>
    public class ProjectAudit
    {
        public const string KLevel = "Level", KView = "View", KTemplate = "View template", KPhaseFilter = "Phase filter", KMaterial = "Material";
        public static readonly string[] Kinds = { KLevel, KView, KTemplate, KPhaseFilter, KMaterial };

        public readonly Document Doc;
        public readonly List<SetupLine> Lines = new List<SetupLine>();
        /// <summary>Per kind: how many names follow the rules, out of how many.</summary>
        public readonly Dictionary<string, (int ok, int total)> Score = new Dictionary<string, (int, int)>();
        readonly Dictionary<string, Element> byId = new Dictionary<string, Element>();

        ProjectAudit(Document doc) { Doc = doc; }

        public static ProjectAudit Run(Document doc)
        {
            var a = new ProjectAudit(doc);
            var levelNames = a.Levels();
            a.Views(levelNames);
            a.Templates();
            a.PhaseFilters();
            a.Materials();
            return a;
        }

        public string Summary() => string.Join("; ", Kinds.Where(Score.ContainsKey).Select(k => $"{k}s {Score[k].ok} of {Score[k].total}"));

        void Add(SetupLine l, Element e) { if (e != null) { l.Element = e.Id.ToString(); byId[l.Element] = e; } Lines.Add(l); }

        // ---- Levels: storeys to B1, L01, L01M, RF; reference levels flagged

        Dictionary<ElementId, string> Levels()
        {
            var levels = new FilteredElementCollector(Doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.Elevation).ToList();
            var after = levels.ToDictionary(l => l.Id, l => l.Name);
            var taken = new HashSet<string>(levels.Where(l => ViewNaming.IsLevelName(l.Name)).Select(l => l.Name));
            int ok = 0;
            foreach (var l in levels)
            {
                bool storey = l.get_Parameter(BuiltInParameter.LEVEL_IS_BUILDING_STORY)?.AsInteger() != 0;
                if (ViewNaming.IsLevelName(l.Name)) { ok++; continue; }
                var code = storey ? SetupRules.ProposeLevel(l.Name) : null;
                if (code != null && !taken.Contains(code))
                {
                    taken.Add(code); after[l.Id] = code;
                    Add(new SetupLine(KLevel, SetupAction.Rename, "", l.Name, code, "Levels: B1, L01, L01M, L02, RF"), l);
                }
                else
                    Add(new SetupLine(KLevel, SetupAction.Rename, "", l.Name, "", "Levels: B1, L01, L01M, L02, RF",
                        !storey ? "reference level: name it after its storey, such as L02_TOS" : code != null ? $"{code} is already used; type the name" : "type the name: the current one doesn't say which storey") { Apply = false }, l);
            }
            Score[KLevel] = (ok, levels.Count);
            return after;
        }

        // ---- Views: names and browser parameters

        static readonly HashSet<ViewType> Audited = new HashSet<ViewType>
            { ViewType.FloorPlan, ViewType.EngineeringPlan, ViewType.AreaPlan, ViewType.CeilingPlan, ViewType.Section, ViewType.Detail, ViewType.Elevation, ViewType.ThreeD, ViewType.DraftingView };

        static ViewKind KindOf(ViewType t) =>
            t == ViewType.CeilingPlan ? ViewKind.CeilingPlan : t == ViewType.Section || t == ViewType.Detail ? ViewKind.Section :
            t == ViewType.Elevation ? ViewKind.Elevation : t == ViewType.ThreeD ? ViewKind.Axo : t == ViewType.DraftingView ? ViewKind.Drafting : ViewKind.FloorPlan;

        void Views(Dictionary<ElementId, string> levelNames)
        {
            var views = new FilteredElementCollector(Doc).OfClass(typeof(View)).Cast<View>().Where(v => !v.IsTemplate && Audited.Contains(v.ViewType)).ToList();
            var onSheet = new HashSet<ElementId>(new FilteredElementCollector(Doc).OfClass(typeof(Viewport)).Cast<Viewport>().Select(p => p.ViewId));
            var phaseCodes = Doc.Phases.Cast<Phase>().Select(p => ViewNaming.PhaseCode(p.Name)).ToList();
            var proposed = new Dictionary<ElementId, string>();
            var renames = new List<(SetupLine line, View v)>();
            var fills = new List<(SetupLine line, View v)>();
            int ok = 0;

            // Parents before dependents, so a dependent takes its parent's new name.
            foreach (var v in views.OrderBy(v => v.GetPrimaryViewId() == ElementId.InvalidElementId ? 0 : 1))
            {
                var f = new ViewFacts
                {
                    Name = v.Name, Kind = KindOf(v.ViewType), Perspective = v is View3D v3 && v3.IsPerspective, OnSheet = onSheet.Contains(v.Id),
                    Phase = Doc.GetElement(v.get_Parameter(BuiltInParameter.VIEW_PHASE)?.AsElementId() ?? ElementId.InvalidElementId) is Phase ph ? ViewNaming.PhaseCode(ph.Name) : "",
                    Level = v is ViewPlan vp && vp.GenLevel != null && levelNames.TryGetValue(vp.GenLevel.Id, out var ln) ? ln : "",
                    ViewSet = v.LookupParameter("OB_ViewSet")?.AsString() ?? "",
                    ViewType = v.LookupParameter("OB_ViewType")?.AsString() ?? "",
                };
                if (f.ViewType == "" && v.ViewType == ViewType.Detail) f.ViewType = "210-SECT-DET";
                var parent = v.GetPrimaryViewId();
                if (parent != ElementId.InvalidElementId)
                {
                    f.IsDependent = true;
                    f.ParentName = proposed.TryGetValue(parent, out var pn) ? pn : Doc.GetElement(parent)?.Name ?? "";
                    var sb = Doc.GetElement(v.get_Parameter(BuiltInParameter.VIEWER_VOLUME_OF_INTEREST_CROP)?.AsElementId() ?? ElementId.InvalidElementId);
                    f.Zone = sb != null ? ViewNaming.ZoneOf(sb.Name) : "";
                }
                var (name, set, type, why) = SetupRules.ProposeView(f, phaseCodes);
                proposed[v.Id] = name;
                bool good = true;
                if (name != v.Name) { renames.Add((new SetupLine(KView, SetupAction.Rename, "", v.Name, name, "Views: <PHASE>-<SET>-<TYPE>-<Level>", why), v)); good = false; }
                foreach (var (prm, value) in new[] { ("OB_ViewSet", ViewNaming.SetFolder(set)), ("OB_ViewType", type) })
                {
                    var p = v.LookupParameter(prm);
                    if (p == null || p.AsString() == value) continue;
                    good = false;
                    fills.Add((new SetupLine(KView, SetupAction.Fill, "", v.Name, $"{prm}={value}", "Project browser: Phase > OB_ViewSet > OB_ViewType",
                        p.IsReadOnly ? "set by the view's template: change the template" : "") { Apply = !p.IsReadOnly }, v));
                }
                if (good) ok++;
            }
            if (views.Count > 0 && views.All(v => v.LookupParameter("OB_ViewSet") == null))
                Lines.Add(new SetupLine(KView, SetupAction.Check, "", "", "", "Project browser: Phase > OB_ViewSet > OB_ViewType",
                    "OB_ViewSet and OB_ViewType are not in this model: run Sync parameters first, then check again") { Apply = false });

            var stay = views.Where(v => !renames.Any(r => r.v.Id == v.Id)).Select(v => v.Name).ToList();
            SetupRules.MakeUnique(renames.Select(r => r.line).ToList(), stay);
            foreach (var (line, v) in renames) Add(line, v);
            foreach (var (line, v) in fills) Add(line, v);
            Score[KView] = (ok, views.Count);
        }

        // ---- View templates: <Discipline>-<TYPE>-<Set>-<scale>, flagged only

        static readonly string[] Disciplines = { "ARCH", "STRUCT", "MEP", "MECH", "ELECT", "PLUMB", "FIRE", "REFRIG", "LIGHT", "COMM", "SECUR", "BAS", "INT", "LAND", "SITE", "CIVIL", "LOCATION", "CON" };

        public static bool IsTemplateName(string name)
        {
            foreach (var d in Disciplines)
                foreach (var t in ViewNaming.Types)
                {
                    var head = $"{d}-{t.Code}-";
                    if (name.StartsWith(head, StringComparison.Ordinal) && Regex.IsMatch(name.Substring(head.Length), @"^(Z-USER-[A-Z0-9]+|Z-WIP|[A-Z]+)-\d+$")
                        && SetupRules.SetCode(Regex.Match(name.Substring(head.Length), @"^(.+)-\d+$").Groups[1].Value) != "") return true;
                }
            return false;
        }

        void Templates()
        {
            var templates = new FilteredElementCollector(Doc).OfClass(typeof(View)).Cast<View>().Where(v => v.IsTemplate).ToList();
            int ok = 0;
            foreach (var t in templates.OrderBy(t => t.Name))
            {
                if (IsTemplateName(t.Name)) { ok++; continue; }
                Add(new SetupLine(KTemplate, SetupAction.Rename, "", t.Name, "", "View templates: <Discipline>-<TYPE>-<Set>-<scale>",
                    "type the new name, such as ARCH-PLAN-CON-100") { Apply = false }, t);
            }
            Score[KTemplate] = (ok, templates.Count);
        }

        // ---- Phase filters: Revit's own names; two deleted, Show Existing added

        void PhaseFilters()
        {
            var filters = new FilteredElementCollector(Doc).OfClass(typeof(PhaseFilter)).Cast<PhaseFilter>().ToList();
            int bad = 0;
            foreach (var f in filters.Where(f => SetupRules.PhaseFiltersToDelete.Contains(f.Name)))
            {
                bad++;
                Add(new SetupLine(KPhaseFilter, SetupAction.Delete, "", f.Name, "", "Phase filters: no drawing needs it", "views using it switch to Show Previous + New"), f);
            }
            if (!filters.Any(f => f.Name == SetupRules.PhaseFilterToAdd))
            {
                bad++;
                Lines.Add(new SetupLine(KPhaseFilter, SetupAction.Create, "", "", SetupRules.PhaseFilterToAdd, "Phase filters: existing elements that stay", "existing by category; new, demolished and temporary hidden"));
            }
            int total = filters.Count + (filters.Any(f => f.Name == SetupRules.PhaseFilterToAdd) ? 0 : 1);
            Score[KPhaseFilter] = (total - bad, total);
        }

        // ---- Materials: counted here, fixed with Replace materials

        void Materials()
        {
            var mats = new FilteredElementCollector(Doc).OfClass(typeof(Material)).Cast<Material>().ToList();
            int ok = mats.Count(m => MaterialRules.IsOpenBepMaterialName(m.Name));
            if (ok < mats.Count)
                Lines.Add(new SetupLine(KMaterial, SetupAction.Check, "", $"{mats.Count - ok} materials", "", "Materials: OB_<Class>_<Material>_<Detail>",
                    "rename and merge them with Replace materials, which shows every element each change touches") { Apply = false });
            Score[KMaterial] = (ok, mats.Count);
        }

        // ---- Apply

        /// <summary>
        /// Finds a line's element: by id, or for a plan written elsewhere, by kind and current name.
        /// </summary>
        public Element Find(SetupLine l)
        {
            if (l.Element != "" && byId.TryGetValue(l.Element, out var e) && e.IsValidObject) return e;
            if (l.Current == "") return null;
            IEnumerable<Element> pool =
                l.Kind == KLevel ? new FilteredElementCollector(Doc).OfClass(typeof(Level)).ToElements() :
                l.Kind == KView ? new FilteredElementCollector(Doc).OfClass(typeof(View)).Cast<View>().Where(v => !v.IsTemplate) :
                l.Kind == KTemplate ? new FilteredElementCollector(Doc).OfClass(typeof(View)).Cast<View>().Where(v => v.IsTemplate) :
                l.Kind == KPhaseFilter ? new FilteredElementCollector(Doc).OfClass(typeof(PhaseFilter)).ToElements() :
                l.Kind == KMaterial ? new FilteredElementCollector(Doc).OfClass(typeof(Material)).ToElements() : Enumerable.Empty<Element>();
            var found = pool.Where(x => x.Name == l.Current).ToList();
            if (found.Count != 1) return null;
            if (l.Element == "") l.Element = found[0].Id.ToString();
            byId[found[0].Id.ToString()] = found[0];
            return found[0];
        }

        /// <summary>Runs the ticked lines. Must run inside a transaction. Returns how many lines changed something.</summary>
        public int Apply(IEnumerable<SetupLine> lines, List<string> failures)
        {
            var go = lines.Where(l => l.Apply && l.Action != SetupAction.Check).ToList();
            int done = 0;

            // Renames in two passes, so names can swap (Level 2 to L01 while Level 1 becomes L00) without a clash.
            var renames = go.Where(l => l.Action == SetupAction.Rename && l.New != "").Select(l => (l, e: Find(l))).ToList();
            foreach (var (l, e) in renames.Where(r => r.e == null)) failures.Add($"{l.Kind} {l.Current}: not found");
            var live = renames.Where(r => r.e != null).ToList();
            foreach (var (l, e) in live)
                try { e.Name = "OBTMP_" + e.Id; } catch (Exception ex) { failures.Add($"{l.Kind} {l.Current}: {ex.Message}"); }
            foreach (var (l, e) in live)
                try { e.Name = l.New; done++; }
                catch (Exception ex) { failures.Add($"{l.Kind} {l.Current} to {l.New}: {ex.Message}"); try { e.Name = l.Current; } catch { } }

            foreach (var l in go.Where(l => l.Action == SetupAction.Fill))
            {
                var e = Find(l);
                var eq = l.New.IndexOf('=');
                if (e == null || eq < 1) { failures.Add($"{l.Kind} {l.Current}: not found or no Parameter=value"); continue; }
                var p = e.LookupParameter(l.New.Substring(0, eq));
                if (p == null || p.IsReadOnly || !p.Set(l.New.Substring(eq + 1))) failures.Add($"{l.Current}: could not set {l.New}");
                else done++;
            }

            foreach (var l in go.Where(l => l.Action == SetupAction.Delete))
            {
                var e = Find(l);
                if (e == null) { failures.Add($"{l.Kind} {l.Current}: not found"); continue; }
                try
                {
                    if (e is PhaseFilter)
                    {
                        // Views and templates using it move to Show Previous + New first; Revit would otherwise pick one.
                        var fallback = new FilteredElementCollector(Doc).OfClass(typeof(PhaseFilter)).FirstOrDefault(f => f.Name == "Show Previous + New");
                        if (fallback != null)
                            foreach (var v in new FilteredElementCollector(Doc).OfClass(typeof(View)).Cast<View>())
                            {
                                var p = v.get_Parameter(BuiltInParameter.VIEW_PHASE_FILTER);
                                if (p != null && !p.IsReadOnly && p.AsElementId() == e.Id) p.Set(fallback.Id);
                            }
                    }
                    Doc.Delete(e.Id); done++;
                }
                catch (Exception ex) { failures.Add($"{l.Kind} {l.Current}: {ex.Message}"); }
            }

            foreach (var l in go.Where(l => l.Action == SetupAction.Create))
            {
                try
                {
                    if (l.Kind != KPhaseFilter) { failures.Add($"{l.Kind} {l.New}: only phase filters can be created here"); continue; }
                    var f = PhaseFilter.Create(Doc, l.New);
                    f.SetPhaseStatusPresentation(ElementOnPhaseStatus.Existing, PhaseStatusPresentation.ShowByCategory);
                    f.SetPhaseStatusPresentation(ElementOnPhaseStatus.New, PhaseStatusPresentation.DontShow);
                    f.SetPhaseStatusPresentation(ElementOnPhaseStatus.Demolished, PhaseStatusPresentation.DontShow);
                    f.SetPhaseStatusPresentation(ElementOnPhaseStatus.Temporary, PhaseStatusPresentation.DontShow);
                    done++;
                }
                catch (Exception ex) { failures.Add($"{l.Kind} {l.New}: {ex.Message}"); }
            }
            return done;
        }
    }
}
