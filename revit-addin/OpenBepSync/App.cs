using System.Reflection;
using Autodesk.Revit.UI;

namespace OpenBepSync
{
    /// <summary>Adds an OpenBEP tab with four buttons: Sync parameters, Build family library, Replace materials and Create views.</summary>
    public class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication app)
        {
            const string tab = "OpenBEP";
            try { app.CreateRibbonTab(tab); } catch { /* tab already exists */ }
            var panel = app.CreateRibbonPanel(tab, "Parameters");
            var btn = new PushButtonData("OpenBepSync", "Sync\nparameters", Assembly.GetExecutingAssembly().Location, typeof(SyncCommand).FullName)
            {
                ToolTip = "Make this model's shared parameters match the OpenBEP binding list exported from the BEP Builder. Shows what will change before changing anything."
            };
            panel.AddItem(btn);
            var lib = app.CreateRibbonPanel(tab, "Library");
            lib.AddItem(new PushButtonData("OpenBepLibrary", "Build family\nlibrary", Assembly.GetExecutingAssembly().Location, typeof(LibraryCommand).FullName)
            {
                ToolTip = "Build the OpenBEP families listed in library/families.csv from Revit's metric templates, with their types and OpenBEP shared parameters. Works with no project open.",
                AvailabilityClassName = typeof(AlwaysAvailable).FullName
            });
            var mats = app.CreateRibbonPanel(tab, "Materials");
            mats.AddItem(new PushButtonData("OpenBepMaterials", "Replace\nmaterials", Assembly.GetExecutingAssembly().Location, typeof(MaterialsCommand).FullName)
            {
                ToolTip = "Find where each material is used and replace, merge, rename or remove materials (and appearance, physical and thermal assets) from a replace list. Shows every element affected first; one Undo reverses the run."
            });
            var views = app.CreateRibbonPanel(tab, "Views");
            views.AddItem(new PushButtonData("OpenBepViews", "Create\nviews", Assembly.GetExecutingAssembly().Location, typeof(CreateViewsCommand).FullName)
            {
                ToolTip = "Make plans and ceiling plans for many levels at once, named by the OpenBEP rule (N-CON-PLAN-EGRESS-L02), filed in the browser, with their templates, Title on Sheet and dependents per scope box. Shows every view first; one Undo reverses the run."
            });
            AutoTest.Hook(app);   // developer test; does nothing unless %AppData%\OpenBEP\autotest.json exists
            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication app) => Result.Succeeded;
    }
}
