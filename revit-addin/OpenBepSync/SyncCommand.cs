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
    /// Makes the model's shared parameters match the OpenBEP binding list exported for this project:
    /// adds missing ones with their fixed GUIDs, binds them to the right categories,
    /// and (only if asked) removes OpenBEP parameters (OB_ prefix, or CA_ from before October 2026) that are no longer in the plan.
    /// Always shows a dry run first and writes a report next to the binding list.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class SyncCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            var uiapp = data.Application;
            var doc = uiapp.ActiveUIDocument?.Document;
            if (doc == null || doc.IsFamilyDocument)
            {
                TaskDialog.Show("OpenBEP", "Open a project (not a family) first.");
                return Result.Cancelled;
            }
            Application app = uiapp.Application;

            // 1. Pick the binding list; find the shared parameter file beside it.
            var csvPath = Pick("Binding list exported by the BEP Builder (CSV)|*.csv", "Choose the OpenBEP binding list");
            if (csvPath == null) return Result.Cancelled;
            var dir = Path.GetDirectoryName(csvPath) ?? "";
            // Files exported before the rename to OpenBEP are still found.
            var txtPath = new[] { "OpenBEP-shared-parameters*.txt", "OpenBEP_SharedParameters*.txt", "CANBIM-shared-parameters*.txt", "CANBIM_SharedParameters*.txt" }
                              .SelectMany(pat => Directory.GetFiles(dir, pat)).FirstOrDefault()
                          ?? Pick("Revit shared parameter file (TXT)|*.txt", "Choose the OpenBEP shared parameter file");
            if (txtPath == null) return Result.Cancelled;

            List<RequiredParameter> required;
            List<string> warnings;
            string listVersion;
            try { required = BindingList.Read(csvPath, out listVersion, out warnings); }
            catch (Exception ex) { TaskDialog.Show("OpenBEP", "Could not read the binding list: " + ex.Message); return Result.Failed; }

            // 2. Load definitions from the shared parameter file, restoring the user's file afterwards.
            var previousFile = app.SharedParametersFilename;
            var defs = new Dictionary<Guid, ExternalDefinition>();
            try
            {
                app.SharedParametersFilename = txtPath;
                var df = app.OpenSharedParameterFile();
                if (df == null) { TaskDialog.Show("OpenBEP", "Revit could not open the shared parameter file."); return Result.Failed; }
                foreach (DefinitionGroup g in df.Groups)
                    foreach (Definition d in g.Definitions)
                        if (d is ExternalDefinition ed) defs[ed.GUID] = ed;
            }
            finally { app.SharedParametersFilename = previousFile; }

            // 3. Dry run: compare the model with the plan.
            var plan = Plan.Build(doc, required, defs, warnings);
            var report = plan.Report(listVersion);
            var reportPath = Path.Combine(dir, $"OpenBEP-sync-report-{DateTime.Now:yyyyMMdd-HHmm}.csv");
            File.WriteAllText(reportPath, report, new UTF8Encoding(true));

            var td = new TaskDialog("OpenBEP parameter sync")
            {
                MainInstruction = plan.Summary(),
                MainContent = $"OpenBEP list {listVersion}. Nothing has been changed yet. Full report: {reportPath}",
                CommonButtons = TaskDialogCommonButtons.Cancel
            };
            td.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Add and bind the missing parameters", "Keeps everything already in the model.");
            if (plan.Extras.Count > 0)
                td.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, $"Add the missing ones and delete {plan.Extras.Count} OpenBEP parameters not in this plan",
                    "Deletes the parameter and every value stored in it. Non-OpenBEP parameters are never touched.");
            var choice = td.Show();
            if (choice != TaskDialogResult.CommandLink1 && choice != TaskDialogResult.CommandLink2) return Result.Cancelled;
            if (choice == TaskDialogResult.CommandLink2)
            {
                var confirm = TaskDialog.Show("OpenBEP", $"Delete {plan.Extras.Count} parameters and their values? This can be undone with Undo right after.",
                    TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No, TaskDialogResult.No);
                if (confirm != TaskDialogResult.Yes) return Result.Cancelled;
            }

            // 4. Apply in one transaction (one Undo step).
            using (var t = new Transaction(doc, "OpenBEP: sync parameters"))
            {
                t.Start();
                var done = plan.Apply(doc, app, choice == TaskDialogResult.CommandLink2);
                t.Commit();
                TaskDialog.Show("OpenBEP", done);
            }
            return Result.Succeeded;
        }

        static string Pick(string filter, string title)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = filter, Title = title };
            return dlg.ShowDialog() == true ? dlg.FileName : null;
        }
    }

    /// <summary>What the sync will do, computed without changing the model.</summary>
    public class Plan
    {
        public class Item
        {
            public RequiredParameter Req;
            public ExternalDefinition Def;
            public List<Category> Cats = new List<Category>();
            public string Action = "";   // add, extend, ok, skip
            public string Note = "";
        }
        public List<Item> Items = new List<Item>();
        public List<SharedParameterElement> Extras = new List<SharedParameterElement>();
        public List<string> Conflicts = new List<string>();
        public List<string> Warnings = new List<string>();

        public static Plan Build(Document doc, List<RequiredParameter> req, Dictionary<Guid, ExternalDefinition> defs, List<string> warnings)
        {
            var plan = new Plan { Warnings = warnings };
            var inModel = new FilteredElementCollector(doc).OfClass(typeof(SharedParameterElement)).Cast<SharedParameterElement>().ToList();
            var byGuid = inModel.ToDictionary(s => s.GuidValue, s => s);
            var wanted = new HashSet<Guid>(req.Select(r => r.Guid));
            var map = doc.ParameterBindings;

            foreach (var r in req)
            {
                var it = new Item { Req = r };
                plan.Items.Add(it);
                if (!defs.TryGetValue(r.Guid, out it.Def)) { it.Action = "skip"; it.Note = "GUID not found in the shared parameter file"; continue; }
                foreach (var id in r.CategoryIds)
                {
                    if (!Enum.TryParse(id, out BuiltInCategory bic)) { it.Note += $"{id} unknown in this Revit version; "; continue; }
                    Category c = null;
                    try { c = Category.GetCategory(doc, bic); } catch { }
                    if (c == null || !c.AllowsBoundParameters) { it.Note += $"{id} cannot take parameters; "; continue; }
                    it.Cats.Add(c);
                }
                if (it.Cats.Count == 0) { it.Action = "skip"; it.Note += "no usable category"; continue; }

                // Same name already used by a different parameter: the classic duplicate.
                foreach (var s in inModel.Where(s => s.Name == r.Name && s.GuidValue != r.Guid))
                    plan.Conflicts.Add($"{r.Name}: the model already has a shared parameter with this name but another GUID ({s.GuidValue}). Data in it will not be read as OpenBEP data.");

                if (!byGuid.TryGetValue(r.Guid, out var spe)) { it.Action = "add"; continue; }
                var eb = map.get_Item(spe.GetDefinition()) as ElementBinding;
                if (eb == null) { it.Action = "add"; it.Note += "defined but not bound; "; continue; }
                bool isInst = eb is InstanceBinding;
                if (isInst != r.IsInstance) it.Note += $"bound as {(isInst ? "instance" : "type")} in the model, plan says {(r.IsInstance ? "instance" : "type")}; left as is; ";
                var missing = it.Cats.Where(c => !eb.Categories.Contains(c)).ToList();
                it.Action = missing.Count > 0 ? "extend" : "ok";
                if (missing.Count > 0) it.Note += "adds " + string.Join(", ", missing.Select(c => c.Name)) + "; ";
            }
            plan.Extras = inModel.Where(s => (s.Name.StartsWith("OB_", StringComparison.Ordinal) || s.Name.StartsWith("CA_", StringComparison.Ordinal)) && !wanted.Contains(s.GuidValue)).ToList();
            return plan;
        }

        public string Summary()
        {
            int add = Items.Count(i => i.Action == "add"), ext = Items.Count(i => i.Action == "extend"), ok = Items.Count(i => i.Action == "ok"), skip = Items.Count(i => i.Action == "skip");
            var sb = new StringBuilder();
            sb.AppendLine($"{add} to add, {ext} to bind to more categories, {ok} already correct, {skip} skipped.");
            if (Extras.Count > 0) sb.AppendLine($"{Extras.Count} OpenBEP parameters in the model are not in this plan.");
            if (Conflicts.Count > 0) sb.AppendLine($"{Conflicts.Count} name conflicts with other parameters (see report).");
            return sb.ToString().Trim();
        }

        public string Report(string version)
        {
            string Q(string s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", new[] { "Parameter", "GUID", "Action", "Instance or type", "Categories", "Element types", "Note" }.Select(Q)));
            foreach (var i in Items)
                sb.AppendLine(string.Join(",", new[] { i.Req.Name, i.Req.Guid.ToString(), i.Action, i.Req.IsInstance ? "Instance" : "Type",
                    string.Join("; ", i.Cats.Select(c => c.Name)), string.Join("; ", i.Req.ElementTypes), i.Note.Trim().TrimEnd(';') }.Select(Q)));
            foreach (var e in Extras)
                sb.AppendLine(string.Join(",", new[] { e.Name, e.GuidValue.ToString(), "not in plan", "", "", "", "deleted only if you choose to" }.Select(Q)));
            foreach (var c in Conflicts) sb.AppendLine(string.Join(",", new[] { "", "", "conflict", "", "", "", c }.Select(Q)));
            foreach (var w in Warnings) sb.AppendLine(string.Join(",", new[] { "", "", "warning", "", "", "", w }.Select(Q)));
            return sb.ToString();
        }

        public string Apply(Document doc, Application app, bool deleteExtras)
        {
            var map = doc.ParameterBindings;
            int added = 0, extended = 0, deleted = 0, failed = 0;
            foreach (var i in Items.Where(x => x.Action == "add" || x.Action == "extend"))
            {
                var cs = app.Create.NewCategorySet();
                Definition key = i.Def;
                if (i.Action == "extend")
                {
                    var spe = SharedParameterElement.Lookup(doc, i.Req.Guid);
                    key = spe.GetDefinition();
                    if (map.get_Item(key) is ElementBinding old) foreach (Category c in old.Categories) cs.Insert(c);
                }
                foreach (var c in i.Cats) cs.Insert(c);
                ElementBinding b = i.Req.IsInstance ? (ElementBinding)app.Create.NewInstanceBinding(cs) : app.Create.NewTypeBinding(cs);
                bool ok = i.Action == "add" ? map.Insert(key, b, GroupTypeId.Data) : map.ReInsert(key, b, GroupTypeId.Data);
                if (!ok) { failed++; continue; }
                if (i.Action == "add") added++; else extended++;
            }
            if (deleteExtras)
                foreach (var e in Extras) { doc.Delete(e.Id); deleted++; }
            return $"Added {added}, bound {extended} to more categories" + (deleteExtras ? $", deleted {deleted}" : "") + (failed > 0 ? $". {failed} failed (see the report)." : ".");
        }
    }
}
