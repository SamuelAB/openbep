using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Document = Autodesk.Revit.DB.Document;
using ElementId = Autodesk.Revit.DB.ElementId;
using Transaction = Autodesk.Revit.DB.Transaction;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;
using TaskDialogCommonButtons = Autodesk.Revit.UI.TaskDialogCommonButtons;
using TaskDialogResult = Autodesk.Revit.UI.TaskDialogResult;

namespace OpenBepSync
{
    /// <summary>A row with a tick box, for the three lists.</summary>
    public class CheckRow : INotifyPropertyChanged
    {
        bool _checked;
        public bool Checked { get => _checked; set { if (_checked == value) return; _checked = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Checked))); } }
        public string Text { get; set; }
        public string Name;
        public ElementId Id;
        public Brush Colour { get; set; } = Brushes.Black;
        public event PropertyChangedEventHandler PropertyChanged;
    }

    /// <summary>A line of the replace list, editable in the grid.</summary>
    public class RuleRow : INotifyPropertyChanged
    {
        string _old = "", _new = "", _action = "", _note = "";
        int _uses;
        public string Old { get => _old; set { _old = value ?? ""; On(nameof(Old)); } }
        public string New { get => _new; set { _new = value ?? ""; On(nameof(New)); } }
        public string Action { get => _action; set { _action = value; On(nameof(Action)); } }
        public int Uses { get => _uses; set { _uses = value; On(nameof(Uses)); } }
        public string Note { get => _note; set { _note = value; On(nameof(Note)); } }
        void On(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        public event PropertyChangedEventHandler PropertyChanged;
    }

    /// <summary>
    /// The Replace materials window, laid out like Polynine's Find and Replace Materials:
    /// what to replace (left), what to replace it with (middle), the elements affected (right), the replace list below.
    /// Every run goes through the replace list, so what you see in the list is what changes.
    /// </summary>
    public class MaterialsWindow : Window
    {
        readonly Document doc;
        MaterialIndex ix;

        readonly ComboBox mode = new ComboBox { Width = 190, Margin = new Thickness(0, 0, 16, 0) };
        readonly CheckBox showUnused = new CheckBox { Content = "Show unused", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };
        readonly CheckBox deleteOld = new CheckBox { Content = "Delete replaced materials left unused", VerticalAlignment = VerticalAlignment.Center,
            ToolTip = "After replacing, deletes each old material that no longer has a use this tool can see. Off by default: a material used only inside a loaded family's own geometry is not seen here." };
        readonly TextBox findOld = new TextBox(), findNew = new TextBox();
        readonly ObservableCollection<CheckRow> oldRows = new ObservableCollection<CheckRow>(), newRows = new ObservableCollection<CheckRow>(), useRows = new ObservableCollection<CheckRow>();
        readonly ObservableCollection<RuleRow> rules = new ObservableCollection<RuleRow>();
        readonly ListBox oldList, newList, useList;
        readonly TextBlock oldCount = new TextBlock(), newChoice = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) },
                           useCount = new TextBlock(), status = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
        readonly DataGrid grid = new DataGrid();
        readonly HashSet<ElementId> skipOwners = new HashSet<ElementId>();
        List<ResolvedRule> resolved = new List<ResolvedRule>();

        public MaterialsWindow(Document doc)
        {
            this.doc = doc;
            Title = "OpenBEP: replace materials";
            Width = 1300; Height = 860; MinWidth = 900; MinHeight = 600;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            FontSize = 13;

            foreach (var k in new[] { "Materials", "Appearance assets", "Physical assets", "Thermal assets" }) mode.Items.Add(k);
            mode.SelectedIndex = 0;
            mode.SelectionChanged += (s, e) => { rules.Clear(); Rebuild(); };
            showUnused.Click += (s, e) => FillOld();
            findOld.TextChanged += (s, e) => FillOld();
            findNew.TextChanged += (s, e) => { FillNew(); ShowChoice(); };

            oldList = CheckList(oldRows, multi: true);
            newList = CheckList(newRows, multi: false);
            useList = CheckList(useRows, multi: true);
            newList.SelectionChanged += (s, e) => { if (newList.SelectedItem is CheckRow r) { foreach (var x in newRows) x.Checked = x == r; } ShowChoice(); };

            // Grid: the replace list. Old and New can be typed in, like a spreadsheet.
            grid.AutoGenerateColumns = false;
            grid.CanUserAddRows = true;
            grid.ItemsSource = rules;
            grid.HeadersVisibility = DataGridHeadersVisibility.Column;
            grid.Columns.Add(new DataGridTextColumn { Header = "Old name", Binding = new Binding("Old") { UpdateSourceTrigger = UpdateSourceTrigger.LostFocus }, Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
            grid.Columns.Add(new DataGridTextColumn { Header = "New name", Binding = new Binding("New") { UpdateSourceTrigger = UpdateSourceTrigger.LostFocus }, Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
            grid.Columns.Add(new DataGridTextColumn { Header = "What happens", Binding = new Binding("Action"), IsReadOnly = true, Width = new DataGridLength(90) });
            grid.Columns.Add(new DataGridTextColumn { Header = "Uses", Binding = new Binding("Uses"), IsReadOnly = true, Width = new DataGridLength(55) });
            grid.Columns.Add(new DataGridTextColumn { Header = "Note", Binding = new Binding("Note"), IsReadOnly = true, Width = new DataGridLength(3, DataGridLengthUnitType.Star) });
            grid.RowEditEnding += (s, e) => Dispatcher.BeginInvoke(new Action(Resolve));
            grid.CellEditEnding += (s, e) => Dispatcher.BeginInvoke(new Action(Resolve));

            var root = new Grid { Margin = new Thickness(12) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(3, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            for (int i = 0; i < 3; i++) root.ColumnDefinitions.Add(new ColumnDefinition());

            var top = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            top.Children.Add(new TextBlock { Text = "Replace ", VerticalAlignment = VerticalAlignment.Center });
            top.Children.Add(mode);
            top.Children.Add(showUnused);
            top.Children.Add(deleteOld);
            Put(root, top, 0, 0, 3);

            Put(root, Column("1. Tick what to replace", findOld, oldList, oldCount), 1, 0);
            Put(root, Column("2. Pick the new material, or type a new name", findNew, newList, newChoice), 1, 1);
            Put(root, Column("Elements affected (untick to leave one alone)", null, useList, useCount), 1, 2, rowSpan: 2);

            var ruleBox = new DockPanel { Margin = new Thickness(0, 10, 10, 0) };
            var ruleHead = new TextBlock { Text = "3. Replace list (type in it, or import one: Old name, New name)", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) };
            DockPanel.SetDock(ruleHead, Dock.Top);
            ruleBox.Children.Add(ruleHead);
            ruleBox.Children.Add(grid);
            Put(root, ruleBox, 2, 0, 2);

            var bar = new DockPanel { Margin = new Thickness(0, 12, 0, 0), LastChildFill = true };
            var right = new StackPanel { Orientation = Orientation.Horizontal };
            right.Children.Add(Button("Add to list", "Adds a line for each ticked material, to the new one.", AddRules));
            right.Children.Add(Button("Clear ticks", "Unticks the materials on the left.", () => { foreach (var r in oldRows) r.Checked = false; ShowUses(); }));
            right.Children.Add(Button("Remove lines", "Removes the selected lines from the replace list.", () => { foreach (var r in grid.SelectedItems.OfType<RuleRow>().ToList()) rules.Remove(r); Resolve(); }));
            right.Children.Add(Button("Clear list", "Empties the replace list.", () => { rules.Clear(); Resolve(); }));
            right.Children.Add(Button("Import list…", "Reads a CSV with Old name, New name (Polynine's Existing material, New material works too).", Import));
            right.Children.Add(Button("Export list…", "Saves the replace list as CSV, to run again on another model.", Export));
            var run = Button("Replace", "Runs the whole replace list in one step. One Undo reverses it.", Run);
            run.FontWeight = FontWeights.Bold;
            right.Children.Add(run);
            right.Children.Add(Button("Close", null, Close));
            DockPanel.SetDock(right, Dock.Right);
            bar.Children.Add(right);
            bar.Children.Add(status);
            Put(root, bar, 3, 0, 3);

            Content = root;
            Rebuild();
        }

        ReplaceKind Kind => (ReplaceKind)mode.SelectedIndex;

        void Rebuild()
        {
            Cursor = Cursors.Wait;
            try { ix = MaterialIndex.Build(doc, Kind); }
            finally { Cursor = null; }
            skipOwners.Clear();
            FillOld(); FillNew(); ShowChoice(); Resolve();
            int used = ix.Uses.Count(kv => kv.Value.Count > 0);
            status.Text = $"{ix.Names.Count} {Noun()} in this {(doc.IsFamilyDocument ? "family" : "model")}, {used} in use.";
        }

        string Noun() => Kind == ReplaceKind.Materials ? "materials" : "assets";

        void FillOld()
        {
            var ticked = new HashSet<ElementId>(oldRows.Where(r => r.Checked).Select(r => r.Id));
            oldRows.Clear();
            foreach (var kv in ix.Names.OrderBy(kv => kv.Value, StringComparer.CurrentCultureIgnoreCase))
            {
                int n = ix.Uses[kv.Key].Count;
                if (n == 0 && !showUnused.IsChecked.GetValueOrDefault()) continue;
                if (!Matches(kv.Value, findOld.Text)) continue;
                var row = new CheckRow { Id = kv.Key, Name = kv.Value, Text = $"{kv.Value}   ({n})", Checked = ticked.Contains(kv.Key),
                    Colour = n == 0 ? Brushes.Gray : Kind == ReplaceKind.Materials && !MaterialRules.IsOpenBepMaterialName(kv.Value) ? Brushes.DarkRed : Brushes.Black };
                row.PropertyChanged += (s, e) => ShowUses();
                oldRows.Add(row);
            }
            ShowUses();
        }

        void FillNew()
        {
            var picked = newRows.FirstOrDefault(r => r.Checked)?.Name;
            newRows.Clear();
            if (ix.CanRemove && Matches(MaterialRules.ByCategory, findNew.Text))
                newRows.Add(new CheckRow { Name = MaterialRules.ByCategory, Text = MaterialRules.ByCategory, Checked = picked == MaterialRules.ByCategory });
            foreach (var kv in ix.Names.OrderBy(kv => kv.Value, StringComparer.CurrentCultureIgnoreCase))
                if (Matches(kv.Value, findNew.Text))
                    newRows.Add(new CheckRow { Id = kv.Key, Name = kv.Value, Text = kv.Value, Checked = kv.Value == picked,
                        Colour = Kind == ReplaceKind.Materials && MaterialRules.IsOpenBepMaterialName(kv.Value) ? Brushes.DarkGreen : Brushes.Black });
        }

        /// <summary>The new name: the picked row, or what's typed when it matches nothing (a rename).</summary>
        string NewName() => newRows.FirstOrDefault(r => r.Checked)?.Name ?? (ix.CanRename ? findNew.Text.Trim() : "");

        void ShowChoice()
        {
            var n = NewName();
            if (n == "") { newChoice.Text = ix.CanRename ? "Nothing picked. Pick a material, or type a name that doesn't exist yet to rename." : "Nothing picked."; newChoice.Foreground = Brushes.Gray; return; }
            bool exists = ix.Names.ContainsValue(n) || n == MaterialRules.ByCategory;
            newChoice.Text = (exists ? "Replace with: " : "New name (the old one is renamed): ") + n +
                (Kind == ReplaceKind.Materials && n != MaterialRules.ByCategory && !MaterialRules.IsOpenBepMaterialName(n) ? "\nNot an OpenBEP name (OB_<Class>_<Material>_<Detail>)." : "");
            newChoice.Foreground = exists ? Brushes.Black : Brushes.DarkBlue;
        }

        /// <summary>Uses of the ticked materials, or of the whole replace list when nothing is ticked.</summary>
        void ShowUses()
        {
            var ids = oldRows.Where(r => r.Checked).Select(r => r.Id).ToList();
            if (ids.Count == 0) ids = resolved.Where(r => r.Action == RuleAction.Replace || r.Action == RuleAction.Remove).Select(r => ix.IdOf(r.Rule.Old)).Where(id => id != null).ToList();
            var uses = ids.SelectMany(id => ix.Uses[id]).ToList();
            useRows.Clear();
            foreach (var g in uses.GroupBy(u => u.Owner).OrderBy(g => g.First().OwnerLabel))
            {
                var first = g.First();
                bool locked = g.All(u => u.Locked);
                var row = new CheckRow { Id = g.Key, Text = $"{first.OwnerLabel}   [{string.Join(", ", g.Select(u => ix.Names[u.ItemId] + ": " + u.Where).Distinct())}]" + (locked ? "  (can't change here)" : ""),
                    Checked = !skipOwners.Contains(g.Key) && !locked, Colour = locked ? Brushes.Gray : Brushes.Black };
                row.PropertyChanged += (s, e) => { if (row.Checked) skipOwners.Remove(row.Id); else skipOwners.Add(row.Id); };
                useRows.Add(row);
            }
            useCount.Text = $"{useRows.Count} elements, {uses.Count} uses" + (oldRows.Any(r => r.Checked) ? " (ticked materials)" : " (whole replace list)");
        }

        void AddRules()
        {
            var to = NewName();
            var olds = oldRows.Where(r => r.Checked).Select(r => r.Name).ToList();
            if (to == "" || olds.Count == 0) { status.Text = "Tick at least one material on the left and pick or type the new one."; return; }
            foreach (var o in olds.Where(o => o != to && !rules.Any(r => r.Old == o && r.New == to)))
            {
                foreach (var clash in rules.Where(r => r.Old == o).ToList()) rules.Remove(clash);   // the newest choice wins
                rules.Add(new RuleRow { Old = o, New = to });
            }
            foreach (var r in oldRows) r.Checked = false;
            Resolve();
        }

        void Resolve()
        {
            var list = rules.Where(r => r.Old.Trim() != "" && r.New.Trim() != "").Select(r => new MaterialRule(r.Old.Trim(), r.New.Trim())).ToList();
            resolved = MaterialRules.Resolve(list, ix.Names.Values.ToList(), ix.UseCount);
            if (!ix.CanRename)
                foreach (var r in resolved.Where(r => r.Action == RuleAction.Rename)) { r.Action = RuleAction.Skip; r.Note = "no asset with this name"; }
            int i = 0;
            foreach (var row in rules)
            {
                if (row.Old.Trim() == "" || row.New.Trim() == "") { row.Action = ""; row.Note = ""; row.Uses = 0; continue; }
                var r = resolved[i++];
                row.Action = r.Action.ToString();
                row.Uses = ix.UseCount(r.Rule.Old);
                row.Note = r.Note;
            }
            ShowUses();
        }

        void Run()
        {
            Resolve();
            var live = resolved.Where(r => r.Action != RuleAction.Skip).ToList();
            if (live.Count == 0) { status.Text = "The replace list has nothing to do. Add lines first (step 3)."; return; }

            // Old id to new id. Renamed materials keep their id, so whatever merges into them points at that id.
            var renamed = live.Where(r => r.Action == RuleAction.Rename).ToDictionary(r => r.Target, r => ix.IdOf(r.Rule.Old));
            var map = new Dictionary<ElementId, ElementId>();
            foreach (var r in live.Where(r => r.Action != RuleAction.Rename))
                map[ix.IdOf(r.Rule.Old)] = r.Action == RuleAction.Remove ? ElementId.InvalidElementId : renamed.TryGetValue(r.Target, out var id) ? id : ix.IdOf(r.Target);

            int uses = map.Keys.Sum(id => ix.Uses[id].Count(u => !u.Locked && !skipOwners.Contains(u.Owner)));
            var summary = $"{live.Count(r => r.Action == RuleAction.Replace)} to replace, {live.Count(r => r.Action == RuleAction.Rename)} to rename, " +
                          $"{live.Count(r => r.Action == RuleAction.Remove)} to set to By Category; {uses} uses change. {resolved.Count - live.Count} lines skipped.";
            if (TaskDialog.Show("OpenBEP", summary + "\n\nOne Undo reverses all of it. Go ahead?", TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No, TaskDialogResult.No) != TaskDialogResult.Yes) return;

            var failures = new List<string>();
            int changed, deleted = 0;
            using (var t = new Transaction(doc, "OpenBEP: replace materials"))
            {
                t.Start();
                foreach (var r in live.Where(r => r.Action == RuleAction.Rename))
                    try { doc.GetElement(ix.IdOf(r.Rule.Old)).Name = r.Target; }
                    catch (Exception ex) { failures.Add($"Rename {r.Rule.Old} to {r.Target}: {ex.Message}"); }
                changed = ix.Apply(map, skipOwners, failures);
                if (deleteOld.IsChecked == true && Kind == ReplaceKind.Materials)
                {
                    doc.Regenerate();
                    var after = MaterialIndex.Build(doc, Kind);
                    foreach (var id in map.Keys.Where(id => after.Uses.TryGetValue(id, out var l) && l.Count == 0))
                        try { doc.Delete(id); deleted++; } catch (Exception ex) { failures.Add($"Delete {ix.Names[id]}: {ex.Message}"); }
                }
                t.Commit();
            }
            TaskDialog.Show("OpenBEP", $"Changed {changed} uses" + (deleted > 0 ? $", deleted {deleted} unused materials" : "") + "." +
                (failures.Count > 0 ? $"\n\n{failures.Count} could not change:\n" + string.Join("\n", failures.Take(15)) + (failures.Count > 15 ? "\n…" : "") : ""));
            rules.Clear();
            Rebuild();
        }

        void Import()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Replace list (CSV)|*.csv", Title = "Import a replace list" };
            if (dlg.ShowDialog(this) != true) return;
            try
            {
                var read = MaterialRules.Read(dlg.FileName, out var warnings);
                foreach (var r in read) rules.Add(new RuleRow { Old = r.Old, New = r.New });
                Resolve();
                status.Text = $"Imported {read.Count} lines." + (warnings.Count > 0 ? " " + string.Join("; ", warnings) : "");
            }
            catch (Exception ex) { status.Text = "Could not read the list: " + ex.Message; }
        }

        void Export()
        {
            var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "Replace list (CSV)|*.csv", FileName = "OpenBEP-replace-list.csv", Title = "Export the replace list" };
            if (dlg.ShowDialog(this) != true) return;
            MaterialRules.Write(dlg.FileName, rules.Where(r => r.Old.Trim() != "" && r.New.Trim() != "").Select(r => new MaterialRule(r.Old.Trim(), r.New.Trim())));
            status.Text = "Saved " + dlg.FileName;
        }

        static bool Matches(string name, string find) =>
            string.IsNullOrWhiteSpace(find) || find.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).All(w => name.IndexOf(w, StringComparison.CurrentCultureIgnoreCase) >= 0);

        /// <summary>A list of tick boxes. Space ticks or unticks every selected row; Ctrl+A selects all; drag selects several.</summary>
        static ListBox CheckList(ObservableCollection<CheckRow> rows, bool multi)
        {
            var box = new FrameworkElementFactory(typeof(CheckBox));
            box.SetBinding(ToggleButton.IsCheckedProperty, new Binding("Checked") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
            box.SetBinding(ContentControl.ContentProperty, new Binding("Text"));
            box.SetBinding(Control.ForegroundProperty, new Binding("Colour"));
            if (!multi) box.SetValue(UIElement.IsHitTestVisibleProperty, false);   // one choice: click the row
            var list = new ListBox { ItemsSource = rows, SelectionMode = multi ? SelectionMode.Extended : SelectionMode.Single, ItemTemplate = new DataTemplate { VisualTree = box } };
            VirtualizingPanel.SetIsVirtualizing(list, true);
            if (multi)
                list.PreviewKeyDown += (s, e) =>
                {
                    if (e.Key != Key.Space) return;
                    var sel = list.SelectedItems.OfType<CheckRow>().ToList();
                    if (sel.Count == 0) return;
                    bool to = !sel.All(r => r.Checked);
                    foreach (var r in sel) r.Checked = to;
                    e.Handled = true;
                };
            return list;
        }

        static FrameworkElement Column(string head, TextBox find, ListBox list, TextBlock foot)
        {
            var d = new DockPanel { Margin = new Thickness(0, 0, 10, 0) };
            var h = new TextBlock { Text = head, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) };
            DockPanel.SetDock(h, Dock.Top); d.Children.Add(h);
            if (find != null)
            {
                var f = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
                var l = new TextBlock { Text = "Find ", VerticalAlignment = VerticalAlignment.Center };
                DockPanel.SetDock(l, Dock.Left); f.Children.Add(l); f.Children.Add(find);
                DockPanel.SetDock(f, Dock.Top); d.Children.Add(f);
            }
            DockPanel.SetDock(foot, Dock.Bottom); d.Children.Add(foot);
            d.Children.Add(list);
            return d;
        }

        static Button Button(string text, string tip, Action click)
        {
            var b = new Button { Content = text, Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(6, 0, 0, 0), ToolTip = tip };
            b.Click += (s, e) => click();
            return b;
        }

        static void Put(Grid g, UIElement e, int row, int col, int colSpan = 1, int rowSpan = 1)
        {
            Grid.SetRow(e, row); Grid.SetColumn(e, col); Grid.SetColumnSpan(e, colSpan); Grid.SetRowSpan(e, rowSpan);
            g.Children.Add(e);
        }
    }
}
