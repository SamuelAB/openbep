using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Plumbing;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;

namespace OpenBepSync
{
    /// <summary>
    /// Developer test, off for everyone else: it runs only when %AppData%\OpenBEP\autotest.json exists. On Revit's first idle moment it
    /// builds the library, checks every family's geometry type by type, places the showroom in a new project, exports PNGs of it,
    /// runs the parameter sync without dialogs, saves the project and closes Revit. Every dialog is answered automatically and logged.
    /// Everything is written to the "out" folder: autotest.log, the PNGs, the project, and autotest-done.txt last.
    /// </summary>
    static class AutoTest
    {
        public static string ConfigPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpenBEP", "autotest.json");

        static Dictionary<string, string> cfg;
        static string outDir;
        static StreamWriter log;
        static bool started;

        public static void Hook(UIControlledApplication app)
        {
            if (!File.Exists(ConfigPath)) return;
            try
            {
                cfg = Regex.Matches(File.ReadAllText(ConfigPath), "\"(\\w+)\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"").Cast<Match>()
                    .ToDictionary(m => m.Groups[1].Value, m => Regex.Unescape(m.Groups[2].Value), StringComparer.OrdinalIgnoreCase);
                outDir = Get("out") ?? Path.Combine(Path.GetTempPath(), "OpenBEP-autotest");
                Directory.CreateDirectory(outDir);
                File.Delete(Path.Combine(outDir, "autotest-done.txt"));
                log = new StreamWriter(Path.Combine(outDir, "autotest.log"), false, new UTF8Encoding(false)) { AutoFlush = true };
                Log("OpenBEP autotest, Revit " + app.ControlledApplication.VersionNumber + ", add-in " + typeof(AutoTest).Assembly.Location);
                app.DialogBoxShowing += OnDialog;
                app.Idling += OnIdling;
            }
            catch (Exception ex) { try { File.WriteAllText(Path.Combine(Path.GetTempPath(), "OpenBEP-autotest-error.txt"), ex.ToString()); } catch { } }
        }

        static string Get(string k) => cfg.TryGetValue(k, out var v) && v != "" ? v : null;

        static void Log(string s) { try { log?.WriteLine($"{DateTime.Now:HH:mm:ss} {s}"); } catch { } }

        static void OnDialog(object sender, DialogBoxShowingEventArgs e)
        {
            string text = e is TaskDialogShowingEventArgs td ? td.Message : e is MessageBoxShowingEventArgs mb ? mb.Message : "";
            Log($"DIALOG {e.DialogId}: {text?.Replace("\n", " | ")}");
            // Only the test project is ever open, so a "save changes?" question is answered No; everything else OK.
            int r = e.DialogId.IndexOf("Save", StringComparison.OrdinalIgnoreCase) >= 0 ? (int)TaskDialogResult.No : (int)TaskDialogResult.Ok;
            try { e.OverrideResult(r); } catch (Exception ex) { Log("could not answer the dialog: " + ex.Message); }
        }

        static void OnIdling(object sender, IdlingEventArgs e)
        {
            if (started) return;
            started = true;
            var uiapp = (UIApplication)sender;
            uiapp.Idling -= OnIdling;
            try { Run(uiapp); }
            catch (Exception ex) { Log("FAILED: " + ex); }
            Log("done");
            try { File.WriteAllText(Path.Combine(outDir, "autotest-done.txt"), DateTime.Now.ToString("s")); } catch { }
            if (Get("exit") != "no")
            {
                try { uiapp.PostCommand(RevitCommandId.LookupPostableCommandId(PostableCommand.ExitRevit)); }
                catch (Exception ex) { Log("could not exit Revit: " + ex.Message); }
            }
        }

        static void Run(UIApplication uiapp)
        {
            var app = uiapp.Application;

            // 1. Library.
            var csv = Get("familiesCsv") ?? throw new InvalidOperationException("autotest.json has no familiesCsv");
            var built = new List<(LibFamily f, string path)>();
            var res = LibraryCommand.Build(app, csv, built, out var summary);
            Log($"LIBRARY {res}: {summary.Replace("\n", " ")}");
            var report = Directory.GetFiles(Path.Combine(Path.GetDirectoryName(csv), "rfa", "Revit" + app.VersionNumber), "OpenBEP-library-report.csv").FirstOrDefault();
            if (report != null) foreach (var line in File.ReadAllLines(report).Skip(1)) Log("  report " + line);

            // 2. Each family's geometry, type by type.
            foreach (var (f, path) in built) Inspect(app, f, path);

            // 3. A fresh project from the metric template, saved in the out folder and opened.
            var rte = Get("template") ?? Path.Combine($@"C:\ProgramData\Autodesk\RVT {app.VersionNumber}\Templates\English", "DefaultMetric.rte");
            var rvt = Path.Combine(outDir, "OpenBEP-autotest.rvt");
            var nd = app.NewProjectDocument(rte);
            nd.SaveAs(rvt, new SaveAsOptions { OverwriteExistingFile = true });
            nd.Close(false);
            foreach (var b in Directory.GetFiles(outDir, "OpenBEP-autotest.0*.rvt")) { try { File.Delete(b); } catch { } }
            var uidoc = uiapp.OpenAndActivateDocument(rvt);
            var doc = uidoc.Document;
            Log("PROJECT " + rvt);

            // 4. Showroom.
            var diag = new List<string>();
            var placed = Showroom.Place(doc, built, uidoc.ActiveView, diag, out var view);
            Log("SHOWROOM " + placed.Replace("\n", " | "));
            foreach (var d in diag) Log("  placed " + d);
            if (view != null) { try { uidoc.ActiveView = view; } catch (Exception ex) { Log("could not open the view: " + ex.Message); } }

            // 5. Pictures: the 3D showroom and the level's plan.
            var ids = new List<ElementId>();
            if (view != null) ids.Add(view.Id);
            var lowest = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.Elevation).FirstOrDefault();
            var plan = new FilteredElementCollector(doc).OfClass(typeof(ViewPlan)).Cast<ViewPlan>()
                .FirstOrDefault(v => !v.IsTemplate && v.ViewType == ViewType.FloorPlan && v.GenLevel?.Id == lowest?.Id);
            if (plan != null) ids.Add(plan.Id);
            foreach (var old in Directory.GetFiles(outDir, "showroom*.png")) { try { File.Delete(old); } catch { } }
            try
            {
                var o = new ImageExportOptions
                {
                    ExportRange = ExportRange.SetOfViews, FilePath = Path.Combine(outDir, "showroom"), ZoomType = ZoomFitType.FitToPage,
                    PixelSize = 2400, FitDirection = FitDirectionType.Horizontal, ImageResolution = ImageResolution.DPI_150,
                    HLRandWFViewsFileType = ImageFileType.PNG, ShadowViewsFileType = ImageFileType.PNG
                };
                o.SetViewsAndSheets(ids);
                doc.ExportImage(o);
                foreach (var png in Directory.GetFiles(outDir, "showroom*.png")) Log("PNG " + png);
            }
            catch (Exception ex) { Log("PNG export failed: " + ex.Message); }

            // 6. Parameter sync with the school example, dry run then apply, no dialogs.
            var bindings = Get("bindings");
            if (bindings != null) Sync(app, doc, bindings);

            // 7. IFC4 export with the OpenBEP property set file, after filling the empty OpenBEP parameters with test values.
            var psets = Get("ifcPsets");
            if (psets != null) { FillTestValues(doc); ExportIfc(doc, psets); }

            doc.Save();
            Log("SAVED " + rvt);
        }

        static void Inspect(Application app, LibFamily f, string path)
        {
            Document fd = null;
            try
            {
                fd = app.OpenDocumentFile(path);
                var fam = fd.OwnerFamily;
                var hostParam = fam.get_Parameter(BuiltInParameter.FAMILY_HOSTING_BEHAVIOR);
                Log($"FAMILY {f.Name}: category {fam.FamilyCategory?.Name}, placement {fam.FamilyPlacementType}, hosting {hostParam?.AsValueString() ?? "-"}");
                var fm = fd.FamilyManager;
                foreach (FamilyType ty in fm.Types)
                {
                    using (var t = new Transaction(fd, "inspect"))
                    {
                        t.Start();
                        fm.CurrentType = ty;
                        fd.Regenerate();
                        var forms = new FilteredElementCollector(fd).OfClass(typeof(GenericForm)).Cast<GenericForm>().ToList();
                        double vol = 0; BoundingBoxXYZ box = null;
                        foreach (var g in forms)
                        {
                            foreach (var o in g.get_Geometry(new Options()))
                            {
                                if (!(o is Solid s) || s.Volume <= 0) continue;
                                vol += s.Volume;
                                var bb = s.GetBoundingBox();
                                var mn = bb.Transform.OfPoint(bb.Min); var mx = bb.Transform.OfPoint(bb.Max);
                                box = box == null ? new BoundingBoxXYZ { Min = mn, Max = mx }
                                    : new BoundingBoxXYZ { Min = new XYZ(Math.Min(box.Min.X, mn.X), Math.Min(box.Min.Y, mn.Y), Math.Min(box.Min.Z, mn.Z)), Max = new XYZ(Math.Max(box.Max.X, mx.X), Math.Max(box.Max.Y, mx.Y), Math.Max(box.Max.Z, mx.Z)) };
                            }
                        }
                        var want = f.Types.FirstOrDefault(x => x.Name == ty.Name);
                        string size = box == null ? "NO SOLID" : $"{Mm(box.Max.X - box.Min.X):0} x {Mm(box.Max.Y - box.Min.Y):0} x {Mm(box.Max.Z - box.Min.Z):0} mm from z {Mm(box.Min.Z):0}";
                        string expect = want == null ? "" : $" (wanted {want.W:0} x {want.D:0} x {want.H:0})";
                        Log($"  type {ty.Name}: {forms.Count} forms, {UnitUtils.ConvertFromInternalUnits(vol, UnitTypeId.CubicMeters):0.000} m3, {size}{expect}");
                        t.RollBack();
                    }
                }
            }
            catch (Exception ex) { Log($"FAMILY {f.Name}: inspection failed: {ex.Message}"); }
            finally { fd?.Close(false); }
        }

        static void Sync(Application app, Document doc, string csvPath)
        {
            try
            {
                var required = BindingList.Read(csvPath, out var version, out var warnings);
                var dir = Path.GetDirectoryName(csvPath) ?? "";
                var txt = new[] { "OpenBEP-shared-parameters*.txt", "OpenBEP_SharedParameters*.txt" }.SelectMany(p => Directory.GetFiles(dir, p)).First();
                var previous = app.SharedParametersFilename;
                var defs = new Dictionary<Guid, ExternalDefinition>();
                try
                {
                    app.SharedParametersFilename = txt;
                    foreach (DefinitionGroup g in app.OpenSharedParameterFile().Groups)
                        foreach (Definition d in g.Definitions)
                            if (d is ExternalDefinition ed) defs[ed.GUID] = ed;
                }
                finally { app.SharedParametersFilename = previous; }
                var plan = Plan.Build(doc, required, defs, warnings);
                Log($"SYNC dry run ({version}): {plan.Summary().Replace("\n", " | ")}");
                using (var t = new Transaction(doc, "OpenBEP: sync parameters"))
                {
                    t.Start();
                    var done = plan.Apply(doc, app, false);
                    Log($"SYNC apply: {t.Commit()}: {done.Replace("\n", " | ")}");
                }
                var again = Plan.Build(doc, required, defs, new List<string>());
                Log("SYNC second dry run: " + again.Summary().Replace("\n", " | "));
            }
            catch (Exception ex) { Log("SYNC failed: " + ex.Message); }
        }

        // Every empty OB_ parameter on the OpenBEP instances and their types gets a value, so the IFC export writes it.
        // Built-in text parameters the BEP Builder's IDS asks for that the generic families leave empty
        static readonly HashSet<string> TestBuiltIns = new HashSet<string> { "Type Mark", "Mark", "Manufacturer", "Model", "Description", "MCB Rating", "Mounting" };
        // Built-in numbers the IDS asks for that Revit creates as zero
        static readonly HashSet<string> TestNumbers = new HashSet<string> { "Mains", "MCB Rating", "Max #1 Pole Breakers", "Short Circuit Rating", "Number of Poles", "Flow" };
        // Logged for every element that has them, to show which ones Revit lets the test write
        static readonly string[] Watch = { "Light Source", "Initial Color", "Visual Light Transmittance", "Define Thermal Properties by", "Assembly Code", "Part Type",
            "Mains", "MCB Rating", "Mounting", "Max #1 Pole Breakers", "Short Circuit Rating", "Number of Poles", "Flow", "System Name" };

        static void FillTestValues(Document doc)
        {
            int n = 0;
            using (var t = new Transaction(doc, "OpenBEP: test values"))
            {
                var notes = new List<string>();
                Quiet.On(t, notes);   // a duplicate-Mark warning dialog would otherwise roll the whole fill back
                t.Start();
                Topography(doc);
                Systems(doc);
                var insts = new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>()
                    .Where(i => i.Symbol.Family.Name.StartsWith("OpenBEP_")).ToList();
                var walls = new FilteredElementCollector(doc).OfClass(typeof(Wall)).Cast<Wall>().ToList();
                // Walls get a UniFormat code the way an office standard's wall types would carry one.
                foreach (var wt in walls.Select(w => w.WallType).Distinct(new IdEq()))
                {
                    var ac = wt.get_Parameter(BuiltInParameter.UNIFORMAT_CODE);
                    if (ac != null && !ac.IsReadOnly && string.IsNullOrEmpty(ac.AsString())) { try { ac.Set("B2010"); n++; } catch (Exception ex) { Log("Assembly Code on " + wt.Name + ": " + ex.Message); } }
                }
                var elems = insts.Cast<Element>().Concat(insts.Select(i => (Element)i.Symbol))
                    .Concat(walls).Concat(walls.Select(w => (Element)w.WallType))
                    .Concat(new FilteredElementCollector(doc).OfClass(typeof(TopographySurface)).ToElements())
                    .Concat(new[] { (Element)doc.ProjectInformation }).Distinct(new IdEq()).ToList();
                foreach (var e in elems)
                {
                    var seen = Watch.Select(w => e.LookupParameter(w)).Where(p => p != null).ToList();
                    if (seen.Count > 0) Log($"  WATCH {Describe(e)}: " + string.Join("; ", seen.Select(p =>
                        $"{p.Definition.Name} {p.StorageType}{(p.IsReadOnly ? " read-only" : "")} = {(p.HasValue ? p.AsValueString() ?? p.AsString() : "(none)")}")));
                    foreach (Parameter p in e.Parameters)
                    {
                        if (p.IsReadOnly) continue;
                        string name = p.Definition.Name;
                        bool number = TestNumbers.Contains(name) && (p.StorageType == StorageType.Double && p.AsDouble() == 0 || p.StorageType == StorageType.Integer && p.AsInteger() == 0);
                        if (!number && p.HasValue && (p.StorageType != StorageType.String || p.AsString() != "")) continue;
                        if (!number && !(p.IsShared && (name.StartsWith("OB_") || name.StartsWith("CA_"))) && !(p.StorageType == StorageType.String && TestBuiltIns.Contains(name))) continue;
                        try
                        {
                            if (p.StorageType == StorageType.String) p.Set("test " + name + " " + e.Id.IntegerValue);   // unique, so no duplicate-mark warning
                            else if (p.StorageType == StorageType.Integer) p.Set(1);
                            else if (p.StorageType == StorageType.Double) p.Set(1.0);
                            else continue;
                            n++;
                        }
                        catch (Exception ex) { if (number) Log($"  could not set {name} on {Describe(e)}: {ex.Message}"); }
                    }
                }
                var r = t.Commit();
                Log($"TEST VALUES: {n} empty OpenBEP and built-in parameters filled, {r}" + string.Concat(notes.Select(x => "; " + x)));
            }
        }

        static string Describe(Element e) => e is FamilyInstance fi ? fi.Symbol.FamilyName + " / " + fi.Symbol.Name : e is ElementType et ? "type " + et.Name : e.GetType().Name + " " + e.Name;

        // The exporter writes the first toposurface as the IfcSite, with its parameters, so the site's OpenBEP parameters need one.
        static void Topography(Document doc)
        {
            if (new FilteredElementCollector(doc).OfClass(typeof(TopographySurface)).Any()) return;
            try
            {
                double m = UnitUtils.ConvertToInternalUnits(1, UnitTypeId.Meters);
                var pts = new List<XYZ> { new XYZ(-60 * m, -60 * m, -m), new XYZ(60 * m, -60 * m, -m), new XYZ(60 * m, 60 * m, -m), new XYZ(-60 * m, 60 * m, -m), new XYZ(0, 70 * m, -m) };
                Log("TOPOGRAPHY created " + TopographySurface.Create(doc, pts).Id);
            }
            catch (Exception ex) { Log("TOPOGRAPHY failed: " + ex.Message); }
        }

        // Pumps and fans join a piping or duct system, so they get a System Name.
        static void Systems(Document doc)
        {
            var eq = new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>()
                .Where(i => i.Symbol.Family.Name.StartsWith("OpenBEP_") && i.MEPModel?.ConnectorManager != null).ToList();
            foreach (var i in eq)
                foreach (Connector c in i.MEPModel.ConnectorManager.Connectors)
                {
                    if (c.MEPSystem != null) continue;
                    var set = new ConnectorSet(); set.Insert(c);
                    try
                    {
                        MEPSystem s = c.Domain == Domain.DomainHvac ? (MEPSystem)doc.Create.NewMechanicalSystem(null, set, DuctSystemType.SupplyAir)
                            : c.Domain == Domain.DomainPiping ? doc.Create.NewPipingSystem(null, set, PipeSystemType.SupplyHydronic) : null;
                        if (s != null) Log($"SYSTEM {s.Name} for {Describe(i)}");
                    }
                    catch (Exception ex) { Log($"SYSTEM for {Describe(i)} failed: {ex.Message}"); }
                }
        }

        class IdEq : IEqualityComparer<Element>
        {
            public bool Equals(Element a, Element b) => a.Id == b.Id;
            public int GetHashCode(Element e) => e.Id.IntegerValue;
        }

        static void ExportIfc(Document doc, string psets)
        {
            var o = new IFCExportOptions { FileVersion = IFCVersion.IFC4, ExportBaseQuantities = true, WallAndColumnSplitting = false };
            o.AddOption("ExportUserDefinedPsets", "true");
            o.AddOption("ExportUserDefinedPsetsFileName", psets);
            o.AddOption("ExportIFCCommonPropertySets", "true");
            o.AddOption("ExportInternalRevitPropertySets", "false");
            o.AddOption("ExportSchedulesAsPsets", "false");
            o.AddOption("IncludeSiteElevation", "false");
            string name = "OpenBEP-autotest.ifc";
            try { File.Delete(Path.Combine(outDir, name)); } catch { }
            using (var t = new Transaction(doc, "OpenBEP: IFC export"))
            {
                t.Start();
                bool ok;
                try { ok = doc.Export(outDir, name, o); }
                catch (Exception ex) { Log("IFC export failed: " + ex.Message); ok = false; }
                t.RollBack();
                var f = new FileInfo(Path.Combine(outDir, name));
                Log($"IFC export {(ok ? "ok" : "returned false")}: {f.FullName} {(f.Exists ? f.Length + " bytes" : "missing")}");
            }
        }

        static double Mm(double ft) => UnitUtils.ConvertFromInternalUnits(ft, UnitTypeId.Millimeters);
    }
}
