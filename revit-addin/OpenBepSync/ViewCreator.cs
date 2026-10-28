using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace OpenBepSync
{
    /// <summary>One view the run will make, or why it won't.</summary>
    public class ViewLine
    {
        public PlannedView Plan;
        public string Name = "";
        public Level Level;
        public string Template = "";     // template name; empty for none
        public string Title = "";        // Title on Sheet
        public ElementId ScopeBox = ElementId.InvalidElementId;
        public string Status = "";       // new, exists, or what stops it
        public bool Ok;                  // can be made
        public bool Create = true;       // ticked in the preview
        public string Note = "";
    }

    /// <summary>
    /// Makes views from a view plan: plans and ceiling plans per level, named by the OpenBEP rule, filed in the browser
    /// (OB_ViewSet, OB_ViewType), with their template, phase, phase filter, Title on Sheet, and dependents per scope box.
    /// The window previews with Preview and runs Create inside one transaction; the autotest calls the same two.
    /// </summary>
    public class ViewCreator
    {
        public readonly Document Doc;
        public readonly List<Level> Levels;                         // lowest first
        public readonly List<Phase> Phases;
        public readonly Dictionary<string, View> Templates;          // by name
        public readonly Dictionary<string, Element> ScopeBoxes;      // by zone (SB-A gives A)
        public readonly Dictionary<string, View> Existing;           // views by name
        public readonly Dictionary<string, PhaseFilter> PhaseFilters;

        public ViewCreator(Document doc)
        {
            Doc = doc;
            Levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.Elevation).ToList();
            Phases = doc.Phases.Cast<Phase>().ToList();
            var views = new FilteredElementCollector(doc).OfClass(typeof(View)).Cast<View>().ToList();
            Templates = views.Where(v => v.IsTemplate).GroupBy(v => v.Name).ToDictionary(g => g.Key, g => g.First());
            Existing = views.Where(v => !v.IsTemplate).GroupBy(v => v.Name).ToDictionary(g => g.Key, g => g.First());
            ScopeBoxes = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_VolumeOfInterest).WhereElementIsNotElementType()
                .GroupBy(e => ViewNaming.ZoneOf(e.Name)).Where(g => g.Key != "").ToDictionary(g => g.Key, g => g.First());
            PhaseFilters = new FilteredElementCollector(doc).OfClass(typeof(PhaseFilter)).Cast<PhaseFilter>().GroupBy(f => f.Name).ToDictionary(g => g.Key, g => g.First());
        }

        public bool IsStorey(Level l) => l.get_Parameter(BuiltInParameter.LEVEL_IS_BUILDING_STORY)?.AsInteger() != 0;

        public Phase PhaseOf(string code) => Phases.FirstOrDefault(p => ViewNaming.PhaseCode(p.Name) == code);

        /// <summary>Lines for the plan, parents before their dependents. Templates are matched for the discipline unless a line names one.</summary>
        public List<ViewLine> Preview(IEnumerable<PlannedView> plan, string discipline, bool titles, bool french, IDictionary<PlannedView, string> templateChoice = null)
        {
            var lines = new List<ViewLine>();
            var names = new HashSet<string>();
            foreach (var p in plan.OrderBy(p => p.Phase).ThenBy(p => p.Set).ThenBy(p => p.Type).ThenBy(p => Levels.FindIndex(l => l.Name == p.Level)).ThenBy(p => p.Option).ThenBy(p => p.Zone))
            {
                var t = ViewNaming.Type(p.Type);
                var line = new ViewLine { Plan = p, Name = ViewNaming.Name(p), Level = Levels.FirstOrDefault(l => l.Name == p.Level) };
                lines.Add(line);
                if (p.Zone == "")
                {
                    string chosen = null;
                    if (templateChoice != null && templateChoice.TryGetValue(p, out var c)) chosen = c;
                    line.Template = chosen ?? ViewNaming.MatchTemplate(Templates.Keys, discipline, p.Type, p.Set) ?? "";
                }
                if (titles) line.Title = ViewNaming.TitleOnSheet(p, french);
                if (p.Zone != "" && ScopeBoxes.TryGetValue(p.Zone, out var sb)) line.ScopeBox = sb.Id;

                if (t == null || !t.PerLevel) line.Status = "only plans and ceiling plans for now";
                else if (line.Level == null) line.Status = $"no level {p.Level} in this model";
                else if (PhaseOf(p.Phase) == null) line.Status = $"no phase with code {p.Phase}";
                else if (Existing.ContainsKey(line.Name)) { line.Status = "exists"; line.Note = "already in the model, left as it is"; }
                else if (!names.Add(line.Name)) line.Status = "twice in this list";
                else if (p.Zone != "" && line.ScopeBox == ElementId.InvalidElementId) line.Status = $"no scope box for zone {p.Zone}";
                else
                {
                    line.Ok = true;
                    line.Status = p.Zone == "" ? "new" : "new dependent";
                    var notes = new List<string>();
                    if (!ViewNaming.IsLevelName(p.Level)) notes.Add("level not named by the OpenBEP rule (L02, B1, RF)");
                    if (p.Zone == "" && line.Template == "") notes.Add($"no template {discipline}-{t.Code}-{p.Set}-<scale>");
                    line.Note = string.Join("; ", notes);
                }
                line.Create = line.Ok;
            }
            return lines;
        }

        /// <summary>Makes the ticked lines. Must run inside a transaction. Returns the views made.</summary>
        public List<View> Create(IEnumerable<ViewLine> lines, List<string> failures)
        {
            var made = new List<View>();
            var byPlan = new Dictionary<PlannedView, View>();
            var vfts = new FilteredElementCollector(Doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>().ToList();
            ViewFamilyType Vft(ViewFamily f) => vfts.Where(v => v.ViewFamily == f).OrderBy(v => v.Name == "Floor Plan" || v.Name == "Ceiling Plan" ? 0 : 1).FirstOrDefault();

            foreach (var line in lines.Where(l => l.Ok && l.Create).OrderBy(l => l.Plan.Zone == "" ? 0 : 1))
            {
                var p = line.Plan;
                try
                {
                    View v;
                    if (p.Zone == "")
                    {
                        var vft = Vft(ViewNaming.Type(p.Type).Kind == ViewKind.CeilingPlan ? ViewFamily.CeilingPlan : ViewFamily.FloorPlan)
                            ?? throw new InvalidOperationException("this model has no plan view type");
                        v = ViewPlan.Create(Doc, vft.Id, line.Level.Id);
                        var phase = PhaseOf(p.Phase);
                        v.get_Parameter(BuiltInParameter.VIEW_PHASE)?.Set(phase.Id);
                        if (line.Template != "" && Templates.TryGetValue(line.Template, out var tpl)) v.ViewTemplateId = tpl.Id;
                        if (!Controls(v, BuiltInParameter.VIEW_PHASE_FILTER) && PhaseFilters.TryGetValue(ViewNaming.DefaultPhaseFilter(p.Phase, p.Type), out var pf))
                            v.get_Parameter(BuiltInParameter.VIEW_PHASE_FILTER)?.Set(pf.Id);
                    }
                    else
                    {
                        var parent = byPlan.TryGetValue(p.Parent, out var pv) ? pv : Existing.TryGetValue(ViewNaming.Name(p.Parent), out var ev) ? ev : null;
                        if (parent == null) throw new InvalidOperationException("its parent view is not made");
                        v = Doc.GetElement(parent.Duplicate(ViewDuplicateOption.AsDependent)) as View;
                        v.get_Parameter(BuiltInParameter.VIEWER_VOLUME_OF_INTEREST_CROP)?.Set(line.ScopeBox);
                    }
                    v.Name = line.Name;
                    var notes = new List<string>();
                    SetText(v, "OB_ViewSet", ViewNaming.SetFolder(p.Set), notes);
                    SetText(v, "OB_ViewType", p.Type, notes);
                    if (line.Title != "") v.get_Parameter(BuiltInParameter.VIEW_DESCRIPTION)?.Set(line.Title);
                    if (notes.Count > 0) failures.Add($"{line.Name}: made, but {string.Join("; ", notes)}");
                    byPlan[p] = v;
                    Existing[line.Name] = v;
                    made.Add(v);
                }
                catch (Exception ex) { failures.Add($"{line.Name}: {ex.Message}"); }
            }
            return made;
        }

        /// <summary>True when the view's template controls this parameter, so the view can't change it.</summary>
        bool Controls(View v, BuiltInParameter bip)
        {
            if (v.ViewTemplateId == ElementId.InvalidElementId || !(Doc.GetElement(v.ViewTemplateId) is View t)) return false;
            var id = new ElementId(bip);
            return t.GetTemplateParameterIds().Contains(id) && !t.GetNonControlledTemplateParameterIds().Contains(id);
        }

        static void SetText(View v, string name, string value, List<string> notes)
        {
            var prm = v.LookupParameter(name);
            if (prm == null) { notes.Add($"{name} is not in this model (run Sync parameters)"); return; }
            if (prm.IsReadOnly) { if (prm.AsString() != value) notes.Add($"{name} is set by the template to {prm.AsString()}"); return; }
            prm.Set(value);
        }
    }
}
