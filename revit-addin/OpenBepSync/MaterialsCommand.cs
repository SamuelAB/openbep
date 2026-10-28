using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace OpenBepSync
{
    /// <summary>
    /// Opens the Replace materials window (after Polynine's Find and Replace Materials): find where materials are used,
    /// build a replace list, preview the elements affected, then replace, merge, rename or remove in one transaction.
    /// Works in projects and in the Family Editor.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class MaterialsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string message, ElementSet elements)
        {
            var doc = data.Application.ActiveUIDocument?.Document;
            if (doc == null) { TaskDialog.Show("OpenBEP", "Open a project or a family first."); return Result.Cancelled; }
            var win = new MaterialsWindow(doc);
            new WindowInteropHelper(win).Owner = data.Application.MainWindowHandle;
            win.ShowDialog();
            return Result.Succeeded;
        }
    }
}
