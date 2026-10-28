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
using Level = Autodesk.Revit.DB.Level;
using Transaction = Autodesk.Revit.DB.Transaction;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;
using TaskDialogCommonButtons = Autodesk.Revit.UI.TaskDialogCommonButtons;
using TaskDialogResult = Autodesk.Revit.UI.TaskDialogResult;

namespace OpenBepSync
{
    /// <summary>A line of the preview list.</summary>
    public class PreviewRow : INotifyPropertyChanged
    {
        bool _create;
        string _template = "", _title = "";
        public ViewLine Line;
        public bool Create { get => _create; set { _create = value; On(nameof(Create)); } }
        public bool CanCreate => Line.Ok;
        public string Name => Line.Name;
        public string Level => Line.Plan.Level;
        public string Type => Line.Plan.Type;
        public string Zone => Line.Plan.Zone;
        public string Template { get => _template; set { _template = value ?? ""; On(nameof(Template)); } }
        public string Title { get => _title; set { _title = value ?? ""; On(nameof(Title)); } }
        public string Status => Line.Status;
        public string Note => Line.Note;
        public Brush Colour => Line.Ok ? (Line.Note == "" ? Brushes.Black : Brushes.DarkOrange) : Line.Status == "exists" ? Brushes.Gray : Brushes.DarkRed;
        void On(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        public event PropertyChangedEventHandler PropertyChanged;
    }

    /// <summary>
    /// Create views: pick a phase, a drawing set and a discipline, tick levels against view types, and see every view
    /// it will make before making any. Names come from the OpenBEP rule, never typed. One Undo reverses a run.
    /// Improves on Ideate ViewCreator's wizard: one screen, the grid shows what the project already has, and the
    /// ticks save as a view plan file that the next project, or an AI, can load.
    /// </summary>
    public class CreateViewsWindow : Window
    {
        readonly Document doc;
        ViewCreator vc;

        // Everything ticked, across phases and sets; the grid shows the part for the chosen phase, set and option.
        readonly HashSet<PlannedView> plan = new HashSet<PlannedView>();
        readonly Dictionary<PlannedView, string> templateChoice = new Dictionary<PlannedView, string>();
        readonly Dictionary<PlannedView, string> titleChoice = new Dictionary<PlannedView, string>();
        readonly HashSet<string> zones = new HashSet<string>();
        readonly HashSet<PlannedView> skip = new HashSet<PlannedView>();   // unticked in the preview

        readonly ComboBox phase = Combo(170), set = Combo(150, editable: true), option = Combo(80), discipline = Combo(90, editable: true), titles = Combo(150);
        readonly CheckBox showRef = new CheckBox { Content = "Show reference levels", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0),
            ToolTip = "Levels with Building Story off, such as L02_TOS. They rarely get their own plans." };
        readonly Grid cells = new Grid();
        readonly StackPanel zoneList = new StackPanel();
        readonly ObservableCollection<PreviewRow> rows = new ObservableCollection<PreviewRow>();
        readonly DataGrid preview = new DataGrid();
        readonly TextBlock status = new TextBlock { VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
        readonly DataGridComboBoxColumn templateCol;

        public CreateViewsWindow(Document doc)
        {
            this.doc = doc;
            Title = "OpenBEP: create views";
            Width = 1350; Height = 900; MinWidth = 900; MinHeight = 600;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            FontSize = 13;
            vc = new ViewCreator(doc);

            foreach (var p in vc.Phases) phase.Items.Add($"{ViewNaming.PhaseCode(p.Name)}  ({p.Name})");
            phase.SelectedIndex = vc.Phases.Count - 1;   // the last phase is usually the one being drawn
            foreach (var s in ViewNaming.SetCodes(Environment.UserName)) set.Items.Add(s);
            set.Text = "CON";
            foreach (var o in new[] { "", "OPT1", "OPT2", "OPT3", "OPT4" }) option.Items.Add(o);
            option.SelectedIndex = 0;
            option.ToolTip = "Adds -OPT1 to the names. Pick the option shown in each view yourself (Visibility/Graphics, Design Options): Revit doesn't let add-ins set it.";
            foreach (var d in new[] { "ARCH", "STRUCT", "MEP", "MECH", "ELECT", "PLUMB", "FIRE", "INT", "LAND", "SITE", "CIVIL", "LOCATION", "CON" }) discipline.Items.Add(d);
            discipline.Text = "ARCH";
            discipline.ToolTip = "Picks the view templates: <Discipline>-<TYPE>-<Set>-<scale>, such as ARCH-PLAN-CON-100.";
            foreach (var t in new[] { "No Title on Sheet", "Title on Sheet: English", "Title on Sheet: French" }) titles.Items.Add(t);
            titles.SelectedIndex = 1;

            phase.SelectionChanged += (s, e) => FillGrid();
            set.SelectionChanged += (s, e) => Dispatcher.BeginInvoke(new Action(FillGrid));
            set.LostFocus += (s, e) => FillGrid();
            option.SelectionChanged += (s, e) => FillGrid();
            discipline.SelectionChanged += (s, e) => Dispatcher.BeginInvoke(new Action(() => { templateChoice.Clear(); Refresh(); }));
            discipline.LostFocus += (s, e) => { templateChoice.Clear(); Refresh(); };
            titles.SelectionChanged += (s, e) => { titleChoice.Clear(); Refresh(); };
            showRef.Click += (s, e) => FillGrid();

            // Preview: one line per view, editable template and title.
            preview.AutoGenerateColumns = false;
            preview.CanUserAddRows = false;
            preview.ItemsSource = rows;
            preview.HeadersVisibility = DataGridHeadersVisibility.Column;
            var tick = new DataGridCheckBoxColumn { Header = "Make", Binding = new Binding("Create") { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }, Width = new DataGridLength(48) };
            tick.ElementStyle = new Style(typeof(CheckBox)) { Setters = { new Setter(IsEnabledProperty, new Binding("CanCreate")) } };
            preview.Columns.Add(tick);
            preview.Columns.Add(Text("View name", "Name", 2.2));
            preview.Columns.Add(Text("Level", "Level", 0.6));
            preview.Columns.Add(Text("Type", "Type", 1.1));
            preview.Columns.Add(Text("Zone", "Zone", 0.45));
            templateCol = new DataGridComboBoxColumn { Header = "Template", SelectedItemBinding = new Binding("Template"), Width = new DataGridLength(1.6, DataGridLengthUnitType.Star) };
            preview.Columns.Add(templateCol);
            preview.Columns.Add(new DataGridTextColumn { Header = "Title on Sheet", Binding = new Binding("Title"), Width = new DataGridLength(1.6, DataGridLengthUnitType.Star) });
            preview.Columns.Add(Text("Status", "Status", 0.8));
            preview.Columns.Add(Text("Note", "Note", 2));
            preview.CellEditEnding += (s, e) => Dispatcher.BeginInvoke(new Action(KeepEdits));
            preview.RowStyle = new Style(typeof(DataGridRow)) { Setters = { new Setter(ForegroundProperty, new Binding("Colour")) } };

            var root = new Grid { Margin = new Thickness(12) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(3, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.ColumnDefinitions.Add(new ColumnDefinition());
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });

            var top = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
            void Label(string s) => top.Children.Add(new TextBlock { Text = s, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 4) });
            Label("Phase"); top.Children.Add(phase);
            Label("Drawing set"); top.Children.Add(set);
            Label("Design option"); top.Children.Add(option);
            Label("Discipline"); top.Children.Add(discipline);
            top.Children.Add(titles);
            top.Children.Add(showRef);
            Put(root, top, 0, 0, 2);

            var gridBox = new DockPanel { Margin = new Thickness(0, 0, 10, 0) };
            var gridHead = new TextBlock { Text = "1. Tick views: levels down, view types across. Click a level or a type to tick its whole row or column. Grey boxes already exist.",
                FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4) };
            DockPanel.SetDock(gridHead, Dock.Top); gridBox.Children.Add(gridHead);
            gridBox.Children.Add(new ScrollViewer { Content = cells, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            Put(root, gridBox, 1, 0);

            var zoneBox = new DockPanel();
            var zoneHead = new TextBlock { Text = "2. Dependents per scope box (optional)", FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 4) };
            DockPanel.SetDock(zoneHead, Dock.Top); zoneBox.Children.Add(zoneHead);
            zoneBox.Children.Add(new ScrollViewer { Content = zoneList, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
            Put(root, zoneBox, 1, 1);

            var prevBox = new DockPanel { Margin = new Thickness(0, 10, 0, 0) };
            var prevHead = new TextBlock { Text = "3. Every view this run makes. Change a template or a title here; untick a line to skip it.", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) };
            DockPanel.SetDock(prevHead, Dock.Top); prevBox.Children.Add(prevHead);
            prevBox.Children.Add(preview);
            Put(root, prevBox, 2, 0, 2);

            var bar = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
            var right = new StackPanel { Orientation = Orientation.Horizontal };
            right.Children.Add(Button("Clear ticks", "Unticks every view in every phase and set.", () => { plan.Clear(); FillGrid(); }));
            right.Children.Add(Button("Load plan…", "Reads a view plan (CSV: Phase, Set, Type, Level, Option, Zone) and ticks its views. An AI can write this file; nothing is made until you press Create.", LoadPlan));
            right.Children.Add(Button("Save plan…", "Saves the ticked views as a view plan, to load in the next project.", SavePlan));
            var go = Button("Create", "Makes every ticked line of the list in one step. One Undo reverses it.", Run);
            go.FontWeight = FontWeights.Bold;
            right.Children.Add(go);
            right.Children.Add(Button("Close", null, Close));
            DockPanel.SetDock(right, Dock.Right);
            bar.Children.Add(right);
            bar.Children.Add(status);
            Put(root, bar, 3, 0, 2);

            Content = root;
            FillZones();
            FillGrid();
        }

        string PhaseCode => phase.SelectedIndex >= 0 ? ViewNaming.PhaseCode(vc.Phases[phase.SelectedIndex].Name) : "";
        string SetCode => (set.Text ?? "").Trim().ToUpperInvariant();
        string Option => option.SelectedItem as string ?? "";

        PlannedView Cell(Level l, ViewTypeDef t) => new PlannedView(PhaseCode, SetCode, t.Number, l.Name, Option);

        void FillZones()
        {
            zoneList.Children.Clear();
            if (vc.ScopeBoxes.Count == 0)
            {
                zoneList.Children.Add(new TextBlock { Text = "This model has no scope boxes. Name them SB-A, SB-B… and each ticked plan gets a dependent view per zone (N-CON-PLAN-L02-A).", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Gray });
                return;
            }
            foreach (var kv in vc.ScopeBoxes.OrderBy(kv => kv.Key))
            {
                var zone = kv.Key;
                var cb = new CheckBox { Content = $"{kv.Value.Name}  (zone {zone})", IsChecked = zones.Contains(zone), Margin = new Thickness(0, 2, 0, 2) };
                cb.Click += (s, e) => { if (cb.IsChecked == true) zones.Add(zone); else zones.Remove(zone); Refresh(); };
                zoneList.Children.Add(cb);
            }
        }

        /// <summary>The grid for the chosen phase, set and option: a row per level (top floor first), a column per plan type.</summary>
        void FillGrid()
        {
            cells.Children.Clear(); cells.RowDefinitions.Clear(); cells.ColumnDefinitions.Clear();
            var levels = vc.Levels.Where(l => showRef.IsChecked == true || vc.IsStorey(l)).Reverse().ToList();
            var types = ViewNaming.Types.Where(t => t.PerLevel).ToList();
            cells.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            foreach (var _ in types) cells.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            cells.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            foreach (var _ in levels) cells.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            for (int c = 0; c < types.Count; c++)
            {
                var t = types[c];
                var head = new Button { Content = new TextBlock { Text = t.Number, LayoutTransform = new RotateTransform(-90) }, ToolTip = $"{t.Label}. Click to tick or untick the whole column.",
                    Padding = new Thickness(2, 4, 2, 4), Margin = new Thickness(1), Background = Brushes.Transparent, BorderThickness = new Thickness(0), VerticalContentAlignment = VerticalAlignment.Bottom };
                head.Click += (s, e) => Toggle(levels.Select(l => Cell(l, t)));
                Put(cells, head, 0, c + 1);
            }
            for (int r = 0; r < levels.Count; r++)
            {
                var l = levels[r];
                bool rule = ViewNaming.IsLevelName(l.Name);
                var head = new Button { Content = l.Name, HorizontalContentAlignment = HorizontalAlignment.Left, Padding = new Thickness(4, 1, 8, 1), Margin = new Thickness(1),
                    Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = rule ? Brushes.Black : Brushes.DarkRed,
                    ToolTip = (rule ? "" : "Not named by the OpenBEP level rule (L02, B1, RF). Views still get made with this name.\n") + "Click to tick or untick the whole row." };
                head.Click += (s, e) => Toggle(types.Select(t => Cell(l, t)));
                Put(cells, head, r + 1, 0);
                for (int c = 0; c < types.Count; c++)
                {
                    var key = Cell(l, types[c]);
                    var name = ViewNaming.Name(key);
                    bool exists = vc.Existing.ContainsKey(name);
                    var cb = new CheckBox { IsChecked = exists || plan.Contains(key), IsEnabled = !exists, Margin = new Thickness(6, 3, 6, 3), HorizontalAlignment = HorizontalAlignment.Center,
                        ToolTip = exists ? name + " (already in the model)" : name };
                    cb.Click += (s, e) => { if (cb.IsChecked == true) plan.Add(key); else plan.Remove(key); Refresh(); };
                    Put(cells, cb, r + 1, c + 1);
                }
            }
            Refresh();
        }

        void Toggle(IEnumerable<PlannedView> keys)
        {
            var list = keys.Where(k => !vc.Existing.ContainsKey(ViewNaming.Name(k))).ToList();
            bool to = !list.All(plan.Contains);
            foreach (var k in list) { if (to) plan.Add(k); else plan.Remove(k); }
            FillGrid();
        }

        /// <summary>The ticked views plus their dependents per ticked zone.</summary>
        IEnumerable<PlannedView> Planned() =>
            plan.Concat(plan.SelectMany(p => zones.Select(z => new PlannedView(p.Phase, p.Set, p.Type, p.Level, p.Option, z))));

        List<ViewLine> lines = new List<ViewLine>();

        void Refresh()
        {
            bool fr = titles.SelectedIndex == 2;
            lines = vc.Preview(Planned(), (discipline.Text ?? "").Trim().ToUpperInvariant(), titles.SelectedIndex > 0, fr, templateChoice);
            templateCol.ItemsSource = new[] { "" }.Concat(vc.Templates.Keys.OrderBy(n => n)).ToList();
            rows.Clear();
            foreach (var l in lines)
            {
                if (titleChoice.TryGetValue(l.Plan, out var title)) l.Title = title;
                if (skip.Contains(l.Plan)) l.Create = false;
                rows.Add(new PreviewRow { Line = l, Create = l.Create, Template = l.Template, Title = l.Title });
            }
            int ok = lines.Count(l => l.Ok), exist = lines.Count(l => l.Status == "exists");
            status.Text = $"{ok} views to make, {exist} already exist, {lines.Count - ok - exist} can't be made." +
                          (vc.Levels.Any(l => vc.IsStorey(l) && !ViewNaming.IsLevelName(l.Name)) ? " Some levels are not named by the OpenBEP rule (shown in red)." : "");
        }

        /// <summary>Keeps template and title edits from the preview when the list is rebuilt.</summary>
        void KeepEdits()
        {
            foreach (var r in rows)
            {
                if (r.Template != r.Line.Template) templateChoice[r.Line.Plan] = r.Template;
                if (r.Title != r.Line.Title) titleChoice[r.Line.Plan] = r.Title;
                if (r.Line.Ok && !r.Create) skip.Add(r.Line.Plan); else skip.Remove(r.Line.Plan);
                r.Line.Template = r.Template; r.Line.Title = r.Title; r.Line.Create = r.Create;
            }
        }

        void Run()
        {
            KeepEdits();
            var go = rows.Where(r => r.Create && r.Line.Ok).Select(r => r.Line).ToList();
            if (go.Count == 0) { status.Text = "Nothing to make. Tick views in the grid first."; return; }
            int deps = go.Count(l => l.Plan.Zone != ""), noTpl = go.Count(l => l.Plan.Zone == "" && l.Template == "");
            var msg = $"Make {go.Count - deps} views" + (deps > 0 ? $" and {deps} dependents" : "") + "." + (noTpl > 0 ? $"\n{noTpl} have no template." : "") +
                      "\n\nOne Undo reverses all of it. Go ahead?";
            if (TaskDialog.Show("OpenBEP", msg, TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No, TaskDialogResult.No) != TaskDialogResult.Yes) return;

            var failures = new List<string>();
            int made;
            using (var t = new Transaction(doc, "OpenBEP: create views"))
            {
                t.Start();
                made = vc.Create(go, failures).Count;
                t.Commit();
            }
            TaskDialog.Show("OpenBEP", $"Made {made} views." + (failures.Count > 0 ? $"\n\n{failures.Count} notes:\n" + string.Join("\n", failures.Take(15)) + (failures.Count > 15 ? "\n…" : "") : ""));
            plan.Clear(); templateChoice.Clear(); titleChoice.Clear(); skip.Clear();
            vc = new ViewCreator(doc);
            FillGrid();
        }

        void LoadPlan()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "View plan (CSV)|*.csv", Title = "Load a view plan" };
            if (dlg.ShowDialog(this) != true) return;
            try
            {
                var read = ViewNaming.ReadPlan(dlg.FileName, out var warnings);
                foreach (var v in read)
                {
                    if (v.Zone == "") plan.Add(v);
                    else { plan.Add(v.Parent); zones.Add(v.Zone); }
                }
                FillZones();
                var first = read.FirstOrDefault();
                if (first != null)
                {
                    int pi = vc.Phases.FindIndex(p => ViewNaming.PhaseCode(p.Name) == first.Phase);
                    if (pi >= 0) phase.SelectedIndex = pi;
                    set.Text = first.Set;
                }
                FillGrid();
                status.Text = $"Loaded {read.Count} lines. " + status.Text + (warnings.Count > 0 ? " Skipped: " + string.Join("; ", warnings) : "");
            }
            catch (Exception ex) { status.Text = "Could not read the plan: " + ex.Message; }
        }

        void SavePlan()
        {
            var dlg = new Microsoft.Win32.SaveFileDialog { Filter = "View plan (CSV)|*.csv", FileName = "OpenBEP-view-plan.csv", Title = "Save the view plan" };
            if (dlg.ShowDialog(this) != true) return;
            ViewNaming.WritePlan(dlg.FileName, Planned().OrderBy(p => p.Phase).ThenBy(p => p.Set).ThenBy(p => p.Type).ThenBy(p => p.Level).ThenBy(p => p.Zone));
            status.Text = "Saved " + dlg.FileName;
        }

        static ComboBox Combo(double width, bool editable = false) =>
            new ComboBox { Width = width, IsEditable = editable, Margin = new Thickness(0, 0, 14, 4), VerticalAlignment = VerticalAlignment.Center };

        static DataGridTextColumn Text(string head, string path, double star) =>
            new DataGridTextColumn { Header = head, Binding = new Binding(path), IsReadOnly = true, Width = new DataGridLength(star, DataGridLengthUnitType.Star),
                ElementStyle = new Style(typeof(TextBlock)) { Setters = { new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap) } } };

        static Button Button(string text, string tip, Action click)
        {
            var b = new Button { Content = text, Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(6, 0, 0, 0), ToolTip = tip };
            b.Click += (s, e) => click();
            return b;
        }

        static void Put(Grid g, UIElement e, int row, int col, int colSpan = 1)
        {
            Grid.SetRow(e, row); Grid.SetColumn(e, col); Grid.SetColumnSpan(e, colSpan);
            g.Children.Add(e);
        }
    }
}
