using System.Reflection;
using Autodesk.Revit.UI;

namespace OpenBepSync
{
    /// <summary>Adds an OpenBEP tab with two buttons: Sync parameters and Build family library.</summary>
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
            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication app) => Result.Succeeded;
    }
}
