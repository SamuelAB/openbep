using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Document = Autodesk.Revit.DB.Document;
using Transaction = Autodesk.Revit.DB.Transaction;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;
using TaskDialogCommonButtons = Autodesk.Revit.UI.TaskDialogCommonButtons;
using TaskDialogResult = Autodesk.Revit.UI.TaskDialogResult;

namespace OpenBepSync
{
    /// <summary>A line of the setup plan in the grid.</summary>
    public class SetupRow : INotifyPropertyChanged
    {
        public SetupLine Line;
        public bool Apply { get => Line.Apply; set { Line.Apply = value; On(nameof(Apply)); } }
        public bool CanApply => Line.Action != SetupAction.Check;
        public string Kind => Line.Kind;
        public string Action => Line.Action.ToString();
        public string Current => Line.Current;
        public string New { get => Line.New; set { Line.New = (value ?? "").Trim(); if (Line.Action == SetupAction.Rename && Line.New != "") Apply = true; On(nameof(New)); } }
        public bool CanEdit => Line.Action == SetupAction.Rename;
        public string Rule => Line.Rule;
        public string Note => Line.Note;
        public Brush Colour => Line.Action == SetupAction.Check ? Brushes.Gray : Line.Note != "" ? Brushes.DarkOrange : Brushes.Black;
        void On(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        public event PropertyChangedEventHandler PropertyChanged;
    }

    /// <summary>
    /// Set up project: a score per kind of name, then the setup plan (every rename, fill, delete or addition that
    /// brings the model to the OpenBEP rules) to review, edit, untick, export or import. Run applies the ticked lines
    /// in one transaction, so one Undo reverses it. An AI or MCP writes the same plan file; nothing changes until a person runs it here.
    /// </summary>
    public class SetupWindow : Window
    {
        readonly Document doc;
        ProjectAudit audit;
        readonly ObservableCollection<SetupRow> rows = new ObservableCollection<SetupRow>();
        readonly ICollectionView view;
        readonly WrapPanel scores = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
        readonly ComboBox show = new ComboBox { Width = 170, Margin = new Thickness(0, 0, 14, 0) };
        readonly DataGrid grid = new DataGrid();
        readonly TextBlock status = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };

        public SetupWindow(Document doc)
        {
            this.doc = doc;
            Title = "OpenBEP: set up project";
            Width = 1300; Height = 850; MinWidth = 900; MinHeight = 550;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            FontSize = 13;

            show.Items.Add("Everything");
            foreach (var k in ProjectAudit.Kinds) show.Items.Add(k + "s");
            show.SelectedIndex = 0;
            view = CollectionViewSource.GetDefaultView(rows);
            view.Filter = o => show.SelectedIndex <= 0 || ((SetupRow)o).Kind == ProjectAudit.Kinds[show.SelectedIndex - 1];
            show.SelectionChanged += (s, e) => view.Refresh();

            grid.AutoGenerateColumns = false;
            grid.CanUserAddRows = false;
            grid.ItemsSource = view;
            grid.HeadersVisibility = DataGridHeadersVisibility.Column;
            var tick = new DataGridCheckBoxColumn { Header = "Do", Binding = new Binding("Apply") { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }, Width = new DataGridLength(40) };
            tick.ElementStyle = new Style(typeof(CheckBox)) { Setters = { new Setter(IsEnabledProperty, new Binding("CanApply")) } };
            grid.Columns.Add(tick);
            grid.Columns.Add(Text("Kind", "Kind", 0.8));
            grid.Columns.Add(Text("Action", "Action", 0.6));
            grid.Columns.Add(Text("Current name", "Current", 1.8));
            grid.Columns.Add(new DataGridTextColumn { Header = "New name or value", Binding = new Binding("New") { UpdateSourceTrigger = UpdateSourceTrigger.LostFocus }, Width = new DataGridLength(1.8, DataGridLengthUnitType.Star),
                CellStyle = new Style(typeof(DataGridCell)) { Setters = { new Setter(IsEnabledProperty, new Binding("CanEdit")) } } });
            grid.Columns.Add(Text("Rule", "Rule", 1.8));
            grid.Columns.Add(Text("Note", "Note", 2));
            grid.RowStyle = new Style(typeof(DataGridRow)) { Setters = { new Setter(ForegroundProperty, new Binding("Colour")) } };

            var root = new DockPanel { Margin = new Thickness(12) };
            var head = new TextBlock { Text = "How many names follow the OpenBEP rules, by kind:", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) };
            DockPanel.SetDock(head, Dock.Top); root.Children.Add(head);
            DockPanel.SetDock(scores, Dock.Top); root.Children.Add(scores);

            var filterBar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            filterBar.Children.Add(new TextBlock { Text = "Show ", VerticalAlignment = VerticalAlignment.Center });
            filterBar.Children.Add(show);
            filterBar.Children.Add(new TextBlock { Text = "Untick a line to skip it. Type a new name where the tool can't guess one (grey lines are for you to check).", VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.Gray });
            DockPanel.SetDock(filterBar, Dock.Top); root.Children.Add(filterBar);

            var bar = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
            var right = new StackPanel { Orientation = Orientation.Horizontal };
            right.Children.Add(Button("Check again", "Reads the model again and rebuilds the plan.", Check));
            right.Children.Add(Button("Import plan…", "Reads a setup plan (CSV: Kind, Action, Element, Current name, New name, Rule, Note), for example one written by an AI. Nothing changes until you press Run.", Import));
            right.Children.Add(Button("Export plan…", "Saves this plan as CSV, to review in Excel or hand to someone else.", Export));
            var run = Button("Run", "Applies every ticked line in one step. One Undo reverses it.", Run);
            run.FontWeight = FontWeights.Bold;
            right.Children.Add(run);
            right.Children.Add(Button("Close", null, Close));
            DockPanel.SetDock(right, Dock.Right);
            bar.Children.Add(right);
            bar.Children.Add(status);
            DockPanel.SetDock(bar, Dock.Bottom); root.Children.Add(bar);
            root.Children.Add(grid);

            Content = root;
            Check();
        }

        void Check()
        {
            Cursor = System.Windows.Input.Cursors.Wait;
            try { audit = ProjectAudit.Run(doc); } finally { Cursor = null; }
            FillRows(audit.Lines);
            status.Text = $"{audit.Lines.Count(l => l.Action != SetupAction.Check)} changes proposed.";
        }

        void FillRows(IEnumerable<SetupLine> lines)
        {
            rows.Clear();
            foreach (var l in lines) rows.Add(new SetupRow { Line = l });
            scores.Children.Clear();
            foreach (var k in ProjectAudit.Kinds.Where(audit.Score.ContainsKey))
            {
                var (ok, total) = audit.Score[k];
                var pct = total == 0 ? 100 : ok * 100 / total;
                scores.Children.Add(new Border
                {
                    BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(1), Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 0, 8, 4),
                    Child = new TextBlock { Text = $"{k}s: {ok} of {total} ({pct}%)", Foreground = pct == 100 ? Brushes.DarkGreen : pct >= 50 ? Brushes.DarkOrange : Brushes.DarkRed, FontWeight = FontWeights.SemiBold }
                });
            }
        }

        void Run()
        {
            var go = audit.Lines.Where(l => l.Apply && l.Action != SetupAction.Check && !(l.Action == SetupAction.Rename && l.New == "")).ToList();
            if (go.Count == 0) { status.Text = "Nothing ticked to do."; return; }
            var counts = string.Join(", ", go.GroupBy(l => l.Action).Select(g => $"{g.Count()} to {g.Key.ToString().ToLowerInvariant()}"));
            if (TaskDialog.Show("OpenBEP", counts + ".\n\nOne Undo reverses all of it. Go ahead?", TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No, TaskDialogResult.No) != TaskDialogResult.Yes) return;
            var failures = new List<string>();
            int done;
            using (var t = new Transaction(doc, "OpenBEP: set up project"))
            {
                t.Start();
                done = audit.Apply(go, failures);
                t.Commit();
            }
            TaskDialog.Show("OpenBEP", $"Done: {done} changes." + (failures.Count > 0 ? $"\n\n{failures.Count} could not be done:\n" + string.Join("\n", failures.Take(15)) + (failures.Count > 15 ? "\n…" : "") : ""));
            Check();
        }

        void Import()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Setup plan (CSV)|*.csv", Title = "Import a setup plan" };
            if (dlg.ShowDialog(this) != true) return;
            try
            {
                var read = SetupRules.ReadPlan(dlg.FileName, out var warnings);
                int missing = 0;
                foreach (var l in read)
                    if (l.Action != SetupAction.Create && audit.Find(l) == null) { l.Apply = false; l.Note = "not found in this model" + (l.Note == "" ? "" : "; " + l.Note); missing++; }
                audit.Lines.Clear(); audit.Lines.AddRange(read);
                FillRows(read);
                status.Text = $"Imported {read.Count} lines" + (missing > 0 ? $", {missing} not found in this model (unticked)" : "") + "." + (warnings.Count > 0 ? " Skipped: " + string.Join("; ", warnings) : "");
            }
            catch (Exception ex) { status.Text = "Could not read the plan: " + ex.Message; }
        }

        void Export()
        {
            var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "Setup plan (CSV)|*.csv", FileName = "OpenBEP-setup-plan.csv", Title = "Export the setup plan" };
            if (dlg.ShowDialog(this) != true) return;
            SetupRules.WritePlan(dlg.FileName, audit.Lines);
            status.Text = "Saved " + dlg.FileName;
        }

        static DataGridTextColumn Text(string head, string path, double star) =>
            new DataGridTextColumn { Header = head, Binding = new Binding(path), IsReadOnly = true, Width = new DataGridLength(star, DataGridLengthUnitType.Star),
                ElementStyle = new Style(typeof(TextBlock)) { Setters = { new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap) } } };

        static Button Button(string text, string tip, Action click)
        {
            var b = new Button { Content = text, Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(6, 0, 0, 0), ToolTip = tip };
            b.Click += (s, e) => click();
            return b;
        }
    }
}
