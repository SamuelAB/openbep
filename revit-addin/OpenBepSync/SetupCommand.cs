using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace OpenBepSync
{
    /// <summary>
    /// Opens the Set up project window: checks the model's names against the OpenBEP rules and proposes a setup plan
    /// (renames, browser parameters, phase filters) to review before anything changes. Projects only.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class SetupCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            var doc = data.Application.ActiveUIDocument?.Document;
            if (doc == null || doc.IsFamilyDocument) { TaskDialog.Show("OpenBEP", "Open a project first."); return Result.Cancelled; }
            var win = new SetupWindow(doc);
            new WindowInteropHelper(win).Owner = data.Application.MainWindowHandle;
            win.ShowDialog();
            return Result.Succeeded;
        }
    }
}
