using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace OpenBepSync
{
    /// <summary>
    /// Opens the Create views window (after Ideate ViewCreator): tick levels against view types, preview every name,
    /// template and title, then make them all in one transaction. Projects only.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CreateViewsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            var doc = data.Application.ActiveUIDocument?.Document;
            if (doc == null || doc.IsFamilyDocument) { TaskDialog.Show("OpenBEP", "Open a project first."); return Result.Cancelled; }
            var win = new CreateViewsWindow(doc);
            new WindowInteropHelper(win).Owner = data.Application.MainWindowHandle;
            win.ShowDialog();
            return Result.Succeeded;
        }
    }
}
