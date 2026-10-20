using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace OpenBepSync
{
    /// <summary>
    /// Builds the OpenBEP family library: one .rfa per family in library/families.csv, made from Revit's own metric
    /// templates, with simple parametric geometry (a box, a door panel or a glazing panel), the family's types and
    /// every OpenBEP shared parameter the manifest lists for its element type. Writes a report beside the .rfa files.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class LibraryCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            // 1. Pick library/families.csv; the manifest and the shared parameter file are in ../shared-parameters.
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "OpenBEP family list (families.csv)|*.csv", Title = "Choose library/families.csv in the OpenBEP folder" };
            if (dlg.ShowDialog() != true) return Result.Cancelled;
            var built = new List<(LibFamily f, string path)>();
            var res = Build(data.Application.Application, dlg.FileName, built, out var summary);
            TaskDialog.Show("OpenBEP family library", summary);

            // 4. With a project open, load the families and place one of each type in the "OpenBEP library" 3D view.
            var uidoc = data.Application.ActiveUIDocument;
            if (res == Result.Succeeded && built.Count > 0 && uidoc?.Document != null && !uidoc.Document.IsFamilyDocument)
            {
                var placed = Showroom.Place(uidoc.Document, built, uidoc.ActiveView, null, out var view);
                if (view != null) { try { uidoc.ActiveView = view; } catch { } }
                TaskDialog.Show("OpenBEP family library", placed);
            }
            return res;
        }

        /// <summary>Builds every family of families.csv into library/rfa/RevitNNNN with no dialog; the summary says what happened.</summary>
        public static Result Build(Application app, string csvPath, List<(LibFamily f, string path)> built, out string summary)
        {
            var libDir = Path.GetDirectoryName(csvPath) ?? "";
            var spDir = Path.Combine(Path.GetDirectoryName(libDir) ?? "", "shared-parameters");
            var manifest = Directory.Exists(spDir) ? Directory.GetFiles(spDir, "OpenBEP_Parameters_Manifest*.csv").OrderBy(f => f).LastOrDefault() : null;
            var txt = Directory.Exists(spDir) ? Directory.GetFiles(spDir, "OpenBEP_SharedParameters*.txt").OrderBy(f => f).LastOrDefault() : null;
            if (manifest == null || txt == null)
            {
                summary = "The shared-parameters folder was not found next to the library folder. Pick families.csv inside your copy of the OpenBEP repository.";
                return Result.Cancelled;
            }

            List<LibFamily> families;
            Dictionary<string, List<(Guid guid, string name, bool inst)>> paramsByElement;
            try
            {
                families = LibFamily.Read(csvPath);
                paramsByElement = ReadManifest(manifest);
            }
            catch (Exception ex) { summary = "Could not read the family list: " + ex.Message; return Result.Failed; }

            // 2. Shared parameter definitions. The OpenBEP file stays the active one until every family is built:
            // FamilyManager.AddParameter fails with definitions from a file that is no longer current.
            var previousFile = app.SharedParametersFilename;
            try { return Build(app, families, paramsByElement, txt, libDir, built, out summary); }
            finally { app.SharedParametersFilename = previousFile; }
        }

        static Result Build(Application app, List<LibFamily> families, Dictionary<string, List<(Guid guid, string name, bool inst)>> paramsByElement, string txt, string libDir, List<(LibFamily f, string path)> built, out string summary)
        {
            var defs = new Dictionary<Guid, ExternalDefinition>();
            app.SharedParametersFilename = txt;
            var df = app.OpenSharedParameterFile();
            if (df == null) { summary = "Revit could not open " + txt; return Result.Failed; }
            foreach (DefinitionGroup g in df.Groups)
                foreach (Definition d in g.Definitions)
                    if (d is ExternalDefinition ed) defs[ed.GUID] = ed;

            var outDir = Path.Combine(libDir, "rfa", "Revit" + app.VersionNumber);
            Directory.CreateDirectory(outDir);
            var templateDirs = new[] { app.FamilyTemplatePath, $@"C:\ProgramData\Autodesk\RVT {app.VersionNumber}\Family Templates\English" }
                .Where(d => !string.IsNullOrEmpty(d) && Directory.Exists(d)).Distinct().ToList();

            // 3. Build each family; one failure never stops the others.
            var report = new StringBuilder();
            string Q(string s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
            report.AppendLine(string.Join(",", new[] { "Family", "Result", "Template", "Types", "Shared parameters added", "Notes" }.Select(Q)));
            int ok = 0;
            foreach (var f in families)
            {
                var notes = new List<string>();
                string result = "failed", tplUsed = "";
                int added = 0;
                Document fam = null;
                try
                {
                    tplUsed = f.Templates.Select(t => templateDirs.Select(d => Path.Combine(d, t)).FirstOrDefault(File.Exists)).FirstOrDefault(p => p != null);
                    if (tplUsed == null) throw new FileNotFoundException("none of these templates was found: " + string.Join("; ", f.Templates));
                    fam = app.NewFamilyDocument(tplUsed);
                    added = Builder.Build(fam, f, paramsByElement.TryGetValue(f.ElementType, out var ps) ? ps : new List<(Guid, string, bool)>(), defs, notes);
                    var path = Path.Combine(outDir, f.Name + ".rfa");
                    fam.SaveAs(path, new SaveAsOptions { OverwriteExistingFile = true });
                    // Overwriting leaves Revit backups (Name.0001.rfa) beside the library; they are not part of it.
                    foreach (var b in Directory.GetFiles(outDir, f.Name + ".0*.rfa")) { try { File.Delete(b); } catch { } }
                    built.Add((f, path));
                    result = "built";
                    ok++;
                }
                catch (Exception ex) { notes.Add(ex.Message); }
                finally { if (fam != null) fam.Close(false); }
                report.AppendLine(string.Join(",", new[] { f.Name, result, Path.GetFileName(tplUsed ?? ""), string.Join("; ", f.Types.Select(t => t.Name)), added.ToString(), string.Join("; ", notes) }.Select(Q)));
            }
            var reportPath = Path.Combine(outDir, "OpenBEP-library-report.csv");
            File.WriteAllText(reportPath, report.ToString(), new UTF8Encoding(true));
            summary = $"Built {ok} of {families.Count} families in {outDir}.\nReport: {reportPath}";
            return Result.Succeeded;
        }

        /// <summary>Shared parameters per element type, from the OpenBEP parameter manifest.</summary>
        static Dictionary<string, List<(Guid guid, string name, bool inst)>> ReadManifest(string path)
        {
            var rows = BindingList.ParseCsv(File.ReadAllText(path, Encoding.UTF8));
            var head = rows[0].Select(h => h.Trim().TrimStart('\uFEFF')).ToList();
            int C(string n) { int i = head.IndexOf(n); if (i < 0) throw new InvalidDataException("Column missing in the manifest: " + n); return i; }
            int cName = C("Parameter (Revit name)"), cGuid = C("GUID"), cKind = C("Shared or built-in"), cScope = C("Instance or type"), cEl = C("Element type");
            var res = new Dictionary<string, List<(Guid, string, bool)>>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in rows.Skip(1))
            {
                if (r.Count <= cEl || !r[cKind].Equals("Shared", StringComparison.OrdinalIgnoreCase) || !Guid.TryParse(r[cGuid], out var g)) continue;
                if (!res.TryGetValue(r[cEl], out var list)) res[r[cEl]] = list = new List<(Guid, string, bool)>();
                if (!list.Any(x => x.Item1 == g)) list.Add((g, r[cName], r[cScope].Equals("instance", StringComparison.OrdinalIgnoreCase)));
            }
            return res;
        }
    }

    /// <summary>
    /// Loads the built families into the open project and places one instance of each type, a row per family
    /// (doors and windows in a short wall), with a text note under each, in a 3D view named "OpenBEP library".
    /// A rerun deletes that view and the elements it placed before, so the showroom is rebuilt rather than duplicated.
    /// </summary>
    static class Showroom
    {
        const string ViewName = "OpenBEP library", Tag = "OpenBEP library showroom";

        class Overwrite : IFamilyLoadOptions
        {
            public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues) { overwriteParameterValues = true; return true; }
            public bool OnSharedFamilyFound(Family sharedFamily, bool familyInUse, out FamilySource source, out bool overwriteParameterValues)
            { source = FamilySource.Family; overwriteParameterValues = true; return true; }
        }

        static double M(double v) => UnitUtils.ConvertToInternalUnits(v, UnitTypeId.Meters);

        static void Mark(Element e) => e?.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.Set(Tag);

        /// <summary>
        /// Places the showroom in <paramref name="doc"/> and returns a summary for the user. <paramref name="diag"/>, when given,
        /// receives one line per placed instance with its size, for the developer test.
        /// </summary>
        public static string Place(Document doc, List<(LibFamily f, string path)> built, View activeView, List<string> diag, out View3D view)
        {
            var notes = new List<string>();
            int placed = 0, loaded = 0, labels = 0;
            view = null;
            var level = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.Elevation).FirstOrDefault();
            var vft = new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>().FirstOrDefault(v => v.ViewFamily == ViewFamily.ThreeDimensional);
            if (level == null || vft == null) return "The project has no level or no 3D view type, so the families were not placed.";
            double z = level.Elevation;
            var labelsAt = new List<(XYZ at, string text)>();
            var placedIds = new List<ElementId>();

            using (var t = new Transaction(doc, "OpenBEP library showroom"))
            {
                t.Start();
                Quiet.On(t, notes);

                // The previous showroom: its view (with its text notes), walls and instances.
                var old = new FilteredElementCollector(doc).OfClass(typeof(View3D)).Cast<View3D>().Where(v => !v.IsTemplate && v.Name == ViewName).Select(v => v.Id)
                    .Concat(new FilteredElementCollector(doc).WhereElementIsNotElementType()
                        .Where(e => e.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS)?.AsString() == Tag).Select(e => e.Id))
                    .ToList();
                if (activeView != null && old.Contains(activeView.Id)) notes.Add("the OpenBEP library view was open, so the previous showroom was kept; switch to another view before rerunning");
                else if (old.Count > 0) { try { doc.Delete(old); } catch (Exception ex) { notes.Add("previous showroom not deleted: " + ex.Message); } }

                for (int row = 0; row < built.Count; row++)
                {
                    var (f, path) = built[row];
                    try
                    {
                        if (!doc.LoadFamily(path, new Overwrite(), out Family fam) || fam == null)
                            fam = new FilteredElementCollector(doc).OfClass(typeof(Family)).Cast<Family>().FirstOrDefault(x => x.Name == f.Name);
                        if (fam == null) { notes.Add(f.Name + ": not loaded"); continue; }
                        loaded++;
                        double y = -M(5) * row;
                        var symbols = fam.GetFamilySymbolIds().Select(id => (FamilySymbol)doc.GetElement(id)).OrderBy(s => s.Name).ToList();
                        Wall wall = null;
                        if (f.Kind == "door" || f.Kind == "window")
                        {
                            wall = Wall.Create(doc, Line.CreateBound(new XYZ(-M(1), y, z), new XYZ(M(4) * symbols.Count, y, z)), level.Id, false);
                            wall.get_Parameter(BuiltInParameter.WALL_USER_HEIGHT_PARAM)?.Set(M(3));
                            Mark(wall);
                            placedIds.Add(wall.Id);
                        }
                        // Face-based families (Sam's rule) are hosted on the level's own plane, so they still sit on the level.
                        bool onPlane = fam.FamilyPlacementType == FamilyPlacementType.WorkPlaneBased;
                        for (int i = 0; i < symbols.Count; i++)
                        {
                            var s = symbols[i];
                            try
                            {
                                if (!s.IsActive) { s.Activate(); doc.Regenerate(); }
                                var at = new XYZ(M(4) * i + M(1), y, z);
                                var inst = wall != null ? doc.Create.NewFamilyInstance(at, s, wall, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural)
                                    : onPlane ? doc.Create.NewFamilyInstance(level.GetPlaneReference(), at, XYZ.BasisX, s)
                                    : doc.Create.NewFamilyInstance(at, s, level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                                Mark(inst);
                                placedIds.Add(inst.Id);
                                placed++;
                                labelsAt.Add((new XYZ(at.X, y - M(1.5), z), f.Name + "\n" + s.Name));
                            }
                            catch (Exception ex) { notes.Add($"{f.Name} {s.Name}: {ex.Message}"); }
                        }
                    }
                    catch (Exception ex) { notes.Add(f.Name + ": " + ex.Message); }
                }
                doc.Regenerate();

                // The view: isometric, boxed tightly around what was placed and its labels, and locked so it can carry text notes.
                try
                {
                    double x0 = double.MaxValue, y0 = double.MaxValue, z0 = z, x1 = double.MinValue, y1 = double.MinValue, z1 = z;
                    foreach (var id in placedIds)
                    {
                        var e = doc.GetElement(id);
                        var bb = e?.get_BoundingBox(null);
                        if (bb == null) { diag?.Add($"{Describe(e)}: no bounding box"); continue; }
                        x0 = Math.Min(x0, bb.Min.X); y0 = Math.Min(y0, bb.Min.Y); z0 = Math.Min(z0, bb.Min.Z);
                        x1 = Math.Max(x1, bb.Max.X); y1 = Math.Max(y1, bb.Max.Y); z1 = Math.Max(z1, bb.Max.Z);
                        if (e is FamilyInstance fi)
                            diag?.Add($"{Describe(e)}: {Mm(bb.Max.X - bb.Min.X):0} x {Mm(bb.Max.Y - bb.Min.Y):0} x {Mm(bb.Max.Z - bb.Min.Z):0} mm, bottom {Mm(bb.Min.Z - z):0} mm above {level.Name}, host {fi.Host?.Name ?? "none"}");
                    }
                    foreach (var (at, _) in labelsAt) { x0 = Math.Min(x0, at.X - M(1)); x1 = Math.Max(x1, at.X + M(2.5)); y0 = Math.Min(y0, at.Y - M(0.6)); }
                    if (x0 > x1) { x0 = -M(2); x1 = M(10); y0 = -M(5) * built.Count; y1 = M(1); }
                    view = View3D.CreateIsometric(doc, vft.Id);
                    view.Name = ViewName;
                    view.DisplayStyle = DisplayStyle.ShadingWithEdges;
                    view.SetSectionBox(new BoundingBoxXYZ { Min = new XYZ(x0 - M(0.3), y0 - M(0.3), z0 - M(0.1)), Max = new XYZ(x1 + M(0.3), y1 + M(0.3), z1 + M(0.3)) });
                    doc.Regenerate();
                    view.SaveOrientationAndLock();
                    var textType = doc.GetDefaultElementTypeId(ElementTypeGroup.TextNoteType);
                    foreach (var (at, text) in labelsAt)
                    {
                        try { TextNote.Create(doc, view.Id, at, text, textType); labels++; }
                        catch (Exception ex) { notes.Add("text note: " + ex.Message); break; }
                    }
                }
                catch (Exception ex) { notes.Add("3D view: " + ex.Message); }

                if (t.Commit() != TransactionStatus.Committed) { notes.Insert(0, "Revit rolled the placement back."); view = null; }
            }
            return $"Loaded {loaded} families and placed {placed} types on {level.Name}, with {labels} labels, in the 3D view \"{ViewName}\"."
                + (notes.Count > 0 ? "\n\n" + string.Join("\n", notes.Take(15)) : "");
        }

        static double Mm(double ft) => UnitUtils.ConvertFromInternalUnits(ft, UnitTypeId.Millimeters);

        static string Describe(Element e) => e is FamilyInstance fi ? fi.Symbol.FamilyName + " / " + fi.Symbol.Name : e?.Name ?? "?";
    }

    /// <summary>Lets a command run from Revit's Home screen, with no project open.</summary>
    public class AlwaysAvailable : IExternalCommandAvailability
    {
        public bool IsCommandAvailable(UIApplication app, CategorySet selected) => true;
    }

    /// <summary>Deletes warnings so no dialog stops the batch; an error rolls the transaction back and is noted in the report.</summary>
    class Quiet : IFailuresPreprocessor
    {
        readonly List<string> notes;
        Quiet(List<string> n) { notes = n; }
        public static void On(Transaction t, List<string> notes)
        {
            var o = t.GetFailureHandlingOptions();
            o.SetFailuresPreprocessor(new Quiet(notes));
            o.SetClearAfterRollback(true);
            t.SetFailureHandlingOptions(o);
        }
        public FailureProcessingResult PreprocessFailures(FailuresAccessor a)
        {
            foreach (var f in a.GetFailureMessages())
            {
                if (f.GetSeverity() == FailureSeverity.Warning) { a.DeleteWarning(f); continue; }
                notes.Add(f.GetDescriptionText());
                return FailureProcessingResult.ProceedWithRollBack;
            }
            return FailureProcessingResult.Continue;
        }
    }

    public class LibType { public string Name = ""; public double W, D, H; }

    /// <summary>One family of library/families.csv (one row per type).</summary>
    public class LibFamily
    {
        public string Name = "", NameFr = "", ElementType = "", CategoryId = "", Kind = "", AssemblyCode = "", Description = "", IfcExportAs = "", IfcExportType = "";
        public List<string> Templates = new List<string>();
        public List<LibType> Types = new List<LibType>();

        public static List<LibFamily> Read(string path)
        {
            var rows = BindingList.ParseCsv(File.ReadAllText(path, Encoding.UTF8));
            var head = rows[0].Select(h => h.Trim().TrimStart('\uFEFF')).ToList();
            int C(string n) { int i = head.IndexOf(n); if (i < 0) throw new InvalidDataException("Column missing in families.csv: " + n); return i; }
            int cF = C("Family"), cFr = C("Family (FR)"), cEl = C("Element type"), cCat = C("Revit category id"), cTpl = C("Templates"), cK = C("Kind"),
                cT = C("Type"), cW = C("Width mm"), cD = C("Depth mm"), cH = C("Height mm"), cA = C("Assembly Code"), cDesc = C("Description");
            int cIfc = head.IndexOf("IfcExportAs"), cIfcT = head.IndexOf("IfcExportType");   // optional columns
            var list = new List<LibFamily>();
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            foreach (var r in rows.Skip(1).Where(r => r.Count > cDesc))
            {
                var f = list.FirstOrDefault(x => x.Name == r[cF]);
                if (f == null)
                {
                    f = new LibFamily { Name = r[cF], NameFr = r[cFr], ElementType = r[cEl], CategoryId = r[cCat], Kind = r[cK], AssemblyCode = r[cA], Description = r[cDesc],
                        Templates = r[cTpl].Split(';').Select(s => s.Trim()).Where(s => s != "").ToList() };
                    if (cIfc >= 0 && cIfc < r.Count) f.IfcExportAs = r[cIfc].Trim();
                    if (cIfcT >= 0 && cIfcT < r.Count) f.IfcExportType = r[cIfcT].Trim();
                    list.Add(f);
                }
                f.Types.Add(new LibType { Name = r[cT], W = double.Parse(r[cW], inv), D = double.Parse(r[cD], inv), H = double.Parse(r[cH], inv) });
            }
            return list;
        }
    }

    /// <summary>Geometry, parameters and types inside one new family document.</summary>
    static class Builder
    {
        static double Mm(double v) => UnitUtils.ConvertToInternalUnits(v, UnitTypeId.Millimeters);

        public static int Build(Document fam, LibFamily f, List<(Guid guid, string name, bool inst)> ps, Dictionary<Guid, ExternalDefinition> defs, List<string> notes)
        {
            var fm = fam.FamilyManager;
            int added = 0;
            var first = f.Types[0];
            using (var t = new Transaction(fam, "OpenBEP family"))
            {
                t.Start();
                Quiet.On(t, notes);
                // A Generic Model fallback template takes the family's real category.
                if (Enum.TryParse(f.CategoryId, out BuiltInCategory bic) && !new ElementId(bic).Equals(fam.OwnerFamily.FamilyCategory?.Id))
                {
                    try { fam.OwnerFamily.FamilyCategory = Category.GetCategory(fam, bic); }
                    catch (Exception ex) { notes.Add("category not set: " + ex.Message); }
                }
                if (fm.CurrentType == null) fm.NewType(first.Name);

                FamilyParameter pW = null, pD = null, pH = null;
                try
                {
                    if (f.Kind == "box") (pW, pD, pH) = Box(fam, first, notes);
                    else (pW, pD, pH) = Panel(fam, f.Kind == "window", first, notes);
                }
                catch (Exception ex) { notes.Add("geometry: " + ex.Message); }

                foreach (var p in ps)
                {
                    if (fm.get_Parameter(p.guid) != null) continue;
                    if (!defs.TryGetValue(p.guid, out var def)) { notes.Add(p.name + " not in the shared parameter file"); continue; }
                    try { fm.AddParameter(def, GroupTypeId.Data, p.inst); added++; }
                    catch (Exception ex) { notes.Add(p.name + ": " + ex.Message); }
                }
                if (t.Commit() != TransactionStatus.Committed) throw new InvalidOperationException("Revit rolled the family back: " + string.Join("; ", notes));

                // Types: the template's default type is renamed to the first type; the others are added.
                for (int i = 0; i < f.Types.Count; i++)
                {
                    var ty = f.Types[i];
                    using (var tt = new Transaction(fam, "OpenBEP type " + ty.Name))
                    {
                        tt.Start();
                        Quiet.On(tt, notes);
                        if (i == 0) { try { fm.RenameCurrentType(ty.Name); } catch { } }
                        else fm.NewType(ty.Name);
                        Set(fm, pW, Mm(ty.W)); Set(fm, pD, Mm(ty.D)); Set(fm, pH, Mm(ty.H));
                        SetText(fm, fm.get_Parameter(BuiltInParameter.UNIFORMAT_CODE), f.AssemblyCode);
                        SetText(fm, fm.get_Parameter(BuiltInParameter.ALL_MODEL_DESCRIPTION), f.Description);
                        SetText(fm, fm.get_Parameter(BuiltInParameter.ALL_MODEL_TYPE_COMMENTS), "OpenBEP generic family. Placeholder size; not a product.");
                        if (f.IfcExportAs != "") SetText(fm, IfcParam(fm, "IFC_EXPORT_ELEMENT_TYPE_AS", "IfcExportAs", notes), f.IfcExportAs);
                        if (f.IfcExportType != "") SetText(fm, IfcParam(fm, "IFC_EXPORT_PREDEFINEDTYPE_TYPE", "IfcExportType", notes), f.IfcExportType);
                        if (tt.Commit() != TransactionStatus.Committed) notes.Add("type " + ty.Name + " did not regenerate");
                    }
                }
            }
            return added;
        }

        static void Set(FamilyManager fm, FamilyParameter p, double v) { if (p != null && !p.IsDeterminedByFormula && !p.IsInstance) fm.Set(p, v); }
        static void SetText(FamilyManager fm, FamilyParameter p, string v) { if (p != null && p.StorageType == StorageType.String && !p.IsReadOnly) fm.Set(p, v ?? ""); }

        // The IFC class and predefined type for the Revit IFC exporter: Revit's own type parameters where this Revit version has them
        // (2023 and later), otherwise type text parameters with the names the exporter also reads (IfcExportAs, IfcExportType).
        static FamilyParameter IfcParam(FamilyManager fm, string bipName, string name, List<string> notes)
        {
            try
            {
                var bip = Enum.TryParse(bipName, out BuiltInParameter b) ? fm.get_Parameter(b) : null;
                return bip ?? fm.get_Parameter(name) ?? fm.AddParameter(name, GroupTypeId.Ifc, SpecTypeId.String.Text, false);
            }
            catch (Exception ex) { notes.Add(name + ": " + ex.Message); return null; }
        }

        static FamilyParameter LengthParam(FamilyManager fm, string name)
            => fm.get_Parameter(name) ?? fm.AddParameter(name, GroupTypeId.Geometry, SpecTypeId.Length, false);

        static T Named<T>(Document d, string name) where T : Element
            => new FilteredElementCollector(d).OfClass(typeof(T)).Cast<T>().FirstOrDefault(e => e.Name == name);

        static ViewPlan PlanView(Document d)
            => new FilteredElementCollector(d).OfClass(typeof(ViewPlan)).Cast<ViewPlan>().Where(v => !v.IsTemplate)
                   .OrderBy(v => v.Name == "Ref. Level" ? 0 : 1).FirstOrDefault();

        static View Elevation(Document d, params string[] names)
            => new FilteredElementCollector(d).OfClass(typeof(View)).Cast<View>()
                   .Where(v => !v.IsTemplate && v.ViewType == ViewType.Elevation)
                   .OrderBy(v => { int i = Array.IndexOf(names, v.Name); return i < 0 ? 99 : i; }).FirstOrDefault();

        /// <summary>A reference plane parallel to YZ (x = const) or XZ (y = const), made in plan.</summary>
        static ReferencePlane Vertical(Document d, View plan, string name, bool constX, double at)
        {
            var rp = Named<ReferencePlane>(d, name);
            if (rp != null) return rp;
            rp = constX ? d.FamilyCreate.NewReferencePlane(new XYZ(at, -1, 0), new XYZ(at, 1, 0), XYZ.BasisZ, plan)
                        : d.FamilyCreate.NewReferencePlane(new XYZ(-1, at, 0), new XYZ(1, at, 0), XYZ.BasisZ, plan);
            rp.Name = name;
            return rp;
        }

        static ReferencePlane Center(Document d, string name, bool normalX)
            => Named<ReferencePlane>(d, name) ?? new FilteredElementCollector(d).OfClass(typeof(ReferencePlane)).Cast<ReferencePlane>()
                   .FirstOrDefault(r => Math.Abs(Math.Abs((normalX ? XYZ.BasisX : XYZ.BasisY).DotProduct(r.Normal)) - 1) < 1e-6 && Math.Abs(r.BubbleEnd.DotProduct(normalX ? XYZ.BasisX : XYZ.BasisY)) < 1e-6);

        /// <summary>Two planes kept equal either side of the centre plane, with an overall dimension labelled by the parameter.</summary>
        static void Pair(Document d, View plan, ReferencePlane a, ReferencePlane c, ReferencePlane b, FamilyParameter p, bool alongX, double offset, List<string> notes)
        {
            try
            {
                var line = alongX ? Line.CreateBound(new XYZ(-10, offset, 0), new XYZ(10, offset, 0)) : Line.CreateBound(new XYZ(offset, -10, 0), new XYZ(offset, 10, 0));
                if (c != null)
                {
                    var eq = new ReferenceArray(); eq.Append(a.GetReference()); eq.Append(c.GetReference()); eq.Append(b.GetReference());
                    var dq = d.FamilyCreate.NewLinearDimension(plan, line, eq);
                    dq.AreSegmentsEqual = true;
                }
                var all = new ReferenceArray(); all.Append(a.GetReference()); all.Append(b.GetReference());
                var line2 = alongX ? Line.CreateBound(new XYZ(-10, offset * 1.5, 0), new XYZ(10, offset * 1.5, 0)) : Line.CreateBound(new XYZ(offset * 1.5, -10, 0), new XYZ(offset * 1.5, 10, 0));
                d.FamilyCreate.NewLinearDimension(plan, line2, all).FamilyLabel = p;
            }
            catch (Exception ex) { notes.Add($"dimension for {p.Definition.Name}: {ex.Message}"); }
        }

        static IEnumerable<PlanarFace> Faces(Element e)
        {
            var g = e.get_Geometry(new Options { ComputeReferences = true, IncludeNonVisibleObjects = false });
            foreach (var o in g)
                if (o is Solid s) foreach (Face f in s.Faces) if (f is PlanarFace pf) yield return pf;
        }

        static void Align(Document d, View v, ReferencePlane rp, Element solid, XYZ normal, List<string> notes)
        {
            // The parallel face nearest the plane, so a template whose Front is on +Y still aligns.
            var face = rp == null ? null : Faces(solid).Where(f => Math.Abs(Math.Abs(f.FaceNormal.DotProduct(normal)) - 1) < 1e-6)
                .OrderBy(f => Math.Abs((f.Origin - rp.BubbleEnd).DotProduct(normal))).FirstOrDefault();
            if (face == null || rp == null) { notes.Add("no face to align to " + rp?.Name); return; }
            try { d.FamilyCreate.NewAlignment(v, rp.GetReference(), face.Reference); }
            catch (Exception ex) { notes.Add($"align to {rp.Name}: {ex.Message}"); }
        }

        /// <summary>Free-standing box: Width (X) and Depth (Y) centred on the centre planes, Height from the reference level.</summary>
        static (FamilyParameter, FamilyParameter, FamilyParameter) Box(Document d, LibType ty, List<string> notes)
        {
            var fm = d.FamilyManager;
            var plan = PlanView(d) ?? throw new InvalidOperationException("no plan view in the template");
            double w = Mm(ty.W), dp = Mm(ty.D), h = Mm(ty.H);
            var pW = LengthParam(fm, "Width"); var pD = LengthParam(fm, "Depth"); var pH = LengthParam(fm, "Height");
            fm.Set(pW, w); fm.Set(pD, dp); fm.Set(pH, h);

            bool hadLR = Named<ReferencePlane>(d, "Left") != null, hadFB = Named<ReferencePlane>(d, "Front") != null;
            var left = Vertical(d, plan, "Left", true, -w / 2); var right = Vertical(d, plan, "Right", true, w / 2);
            var front = Vertical(d, plan, "Front", false, -dp / 2); var back = Vertical(d, plan, "Back", false, dp / 2);
            d.Regenerate();
            if (!hadLR) Pair(d, plan, left, Center(d, "Center (Left/Right)", true), right, pW, true, -dp / 2 - Mm(300), notes);
            if (!hadFB) Pair(d, plan, front, Center(d, "Center (Front/Back)", false), back, pD, false, -w / 2 - Mm(300), notes);
            d.Regenerate();

            // A template's own Left/Right/Front/Back planes (Casework) are driven by its Width/Depth but are not
            // always centred, so the profile is drawn where the planes actually are after regeneration.
            double x0 = left.BubbleEnd.X, x1 = right.BubbleEnd.X, y0 = front.BubbleEnd.Y, y1 = back.BubbleEnd.Y;
            if (x1 < x0) { var s = x0; x0 = x1; x1 = s; }
            if (y1 < y0) { var s = y0; y0 = y1; y1 = s; }
            if (x1 - x0 < 1e-6) { x0 = -w / 2; x1 = w / 2; }
            if (y1 - y0 < 1e-6) { y0 = -dp / 2; y1 = dp / 2; }
            var profile = Rect(new XYZ(x0, y0, 0), new XYZ(x1, y0, 0), new XYZ(x1, y1, 0), new XYZ(x0, y1, 0));
            var sp = SketchPlane.Create(d, Plane.CreateByNormalAndOrigin(XYZ.BasisZ, XYZ.Zero));
            var ext = d.FamilyCreate.NewExtrusion(true, profile, sp, h);
            fm.AssociateElementParameterToFamilyParameter(ext.get_Parameter(BuiltInParameter.EXTRUSION_END_PARAM), pH);
            d.Regenerate();
            Align(d, plan, left, ext, -XYZ.BasisX, notes); Align(d, plan, right, ext, XYZ.BasisX, notes);
            Align(d, plan, front, ext, -XYZ.BasisY, notes); Align(d, plan, back, ext, XYZ.BasisY, notes);
            return (pW, pD, pH);
        }

        /// <summary>
        /// Door leaf or glazing panel in the host wall's centre plane, filling the template's opening:
        /// sides locked to the template's Left and Right planes, top to its head plane, bottom to the sill plane for windows.
        /// </summary>
        static (FamilyParameter, FamilyParameter, FamilyParameter) Panel(Document d, bool window, LibType ty, List<string> notes)
        {
            var fm = d.FamilyManager;
            var plan = PlanView(d) ?? throw new InvalidOperationException("no plan view in the template");
            var elev = Elevation(d, "Exterior", "Front", "Interior") ?? throw new InvalidOperationException("no elevation view in the template");
            var pW = fm.get_Parameter("Width") ?? throw new InvalidOperationException("the template has no Width parameter");
            var pH = fm.get_Parameter("Height") ?? throw new InvalidOperationException("the template has no Height parameter");
            fm.Set(pW, Mm(ty.W)); fm.Set(pH, Mm(ty.H));
            d.Regenerate();

            var planes = new FilteredElementCollector(d).OfClass(typeof(ReferencePlane)).Cast<ReferencePlane>().ToList();
            var left = Named<ReferencePlane>(d, "Left") ?? planes.Where(IsX).OrderBy(r => r.BubbleEnd.X).First();
            var right = Named<ReferencePlane>(d, "Right") ?? planes.Where(IsX).OrderBy(r => r.BubbleEnd.X).Last();
            var flat = planes.Where(r => Math.Abs(Math.Abs(r.Normal.Z) - 1) < 1e-6).OrderBy(r => r.BubbleEnd.Z).ToList();
            var head = Named<ReferencePlane>(d, "Head") ?? flat.LastOrDefault();
            var sill = window ? (Named<ReferencePlane>(d, "Sill") ?? flat.FirstOrDefault(r => r != head)) : null;
            double x0 = left.BubbleEnd.X, x1 = right.BubbleEnd.X, z0 = sill?.BubbleEnd.Z ?? 0, z1 = head?.BubbleEnd.Z ?? Mm(ty.H);
            if (x1 < x0) { var s = x0; x0 = x1; x1 = s; }
            double th = Mm(ty.D);

            var profile = Rect(new XYZ(x0, 0, z0), new XYZ(x1, 0, z0), new XYZ(x1, 0, z1), new XYZ(x0, 0, z1));
            var sp = SketchPlane.Create(d, Plane.CreateByNormalAndOrigin(XYZ.BasisY, XYZ.Zero));
            var ext = d.FamilyCreate.NewExtrusion(true, profile, sp, th / 2);
            ext.get_Parameter(BuiltInParameter.EXTRUSION_START_PARAM).Set(-th / 2);
            d.Regenerate();
            Align(d, plan, left, ext, -XYZ.BasisX, notes); Align(d, plan, right, ext, XYZ.BasisX, notes);
            if (head != null) Align(d, elev, head, ext, XYZ.BasisZ, notes); else notes.Add("no head plane found; panel height is fixed");
            if (sill != null) Align(d, elev, sill, ext, -XYZ.BasisZ, notes);
            if (window)
            {
                var glass = new FilteredElementCollector(d).OfClass(typeof(Material)).Cast<Material>().FirstOrDefault(m => m.Name.IndexOf("Glass", StringComparison.OrdinalIgnoreCase) >= 0);
                if (glass != null) ext.get_Parameter(BuiltInParameter.MATERIAL_ID_PARAM)?.Set(glass.Id);
            }
            return (pW, null, pH);
        }

        static bool IsX(ReferencePlane r) => Math.Abs(Math.Abs(r.Normal.X) - 1) < 1e-6 && Math.Abs(r.BubbleEnd.X) > 1e-6;

        static CurveArrArray Rect(params XYZ[] p)
        {
            var loop = new CurveArray();
            for (int i = 0; i < p.Length; i++) loop.Append(Line.CreateBound(p[i], p[(i + 1) % p.Length]));
            var a = new CurveArrArray(); a.Append(loop);
            return a;
        }
    }
}
