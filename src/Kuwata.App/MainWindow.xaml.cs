using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using Kuwata.Core.Articulator;
using Kuwata.Core.Geometry;
using Kuwata.Core.IO;
using Kuwata.Core.Method;
using Kuwata.Core.Project;
using Microsoft.Win32;
using KMesh = Kuwata.Core.Geometry.Mesh;

namespace Kuwata.App;

public partial class MainWindow : Window
{
    private KuwataProject _project = new();
    private CaseResult? _result;
    private string? _path;
    private Landmark? _picking;
    private readonly List<Visual3D> _scene = new();
    private readonly HashSet<Visual3D> _pickable = new();

    private static readonly Color UpperColor = Color.FromRgb(0xE8, 0xDF, 0xCF);
    private static readonly Color LowerColor = Color.FromRgb(0xD9, 0xC8, 0xB0);
    private static readonly Color CondyleColor = Color.FromRgb(0x9E, 0xC9, 0xE8);
    private static readonly Color SurfaceColor = Color.FromRgb(0x4C, 0xC3, 0x8A);
    private static readonly Color SpeeColor = Color.FromRgb(0xFF, 0x9F, 0x1C);
    private static readonly Color WilsonColor = Color.FromRgb(0xE0, 0x4F, 0x9F);
    private static readonly Color BroadrickColor = Color.FromRgb(0xBB, 0xBB, 0xBB);
    private static readonly Color LandmarkColor = Color.FromRgb(0xFF, 0xE0, 0x4A);
    private static readonly Color ConstructionColor = Color.FromRgb(0x7F, 0xD7, 0xFF);

    public MainWindow()
    {
        InitializeComponent();
        ProjectToUi();
        Recalculate(showErrors: false);
    }

    // ───────────── Проект ↔ интерфейс ─────────────

    private void ProjectToUi()
    {
        var p = _project;
        CaseNameBox.Text = p.CaseName;
        Select(RegistrationBox, p.Registration.ToString());
        MatrixBox.Text = p.FacebowMatrix is { } m ? string.Join(" ", m.Select(F)) : "1 0 0 0  0 1 0 0  0 0 1 0  0 0 0 1";
        CondyleFromMeshBox.IsChecked = p.CondyleCentersFromMeshes;
        BonwillBox.Text = F(p.AverageValues.BonwillSideMm);
        BalkwillBox.Text = F(p.AverageValues.BalkwillAngleDeg);
        OpTiltBox.Text = F(p.AverageValues.OcclusalPlaneToAxisOrbitalDeg);

        SspRBox.Text = F(p.Joint.SagittalPathRight);
        SspLBox.Text = F(p.Joint.SagittalPathLeft);
        BenRBox.Text = F(p.Joint.BennettRight);
        BenLBox.Text = F(p.Joint.BennettLeft);

        ObBox.Text = F(p.Manual.Overbite);
        OjBox.Text = F(p.Manual.Overjet);
        A1Box.Text = F(p.Manual.A1);
        A2Box.Text = F(p.Manual.A2);
        A3Box.Text = F(p.Manual.A3);

        IncWBox.Text = F(p.IncisorWidth);
        IncLBox.Text = F(p.IncisorLength);
        Select(PropBox, p.Proportions.ToString());
        WardBox.Text = F(p.WardRatio);

        Select(PosteriorBox, p.Broadrick.PosteriorPoint.ToString());
        RadiusBox.Text = F(p.Broadrick.RadiusMm);
        ShiftBox.Text = F(p.Broadrick.OpscShiftMm);
        WilsonBox.Text = F(p.Surface.WilsonRadiusMm);
        LcBox.Text = F(p.LongCentricMm);

        UpdatePanels();
        RefreshLandmarks();
        RefreshMeshInfo();
    }

    private void UiToProject()
    {
        var p = _project;
        p.CaseName = CaseNameBox.Text.Trim();
        p.Registration = Enum.Parse<RegistrationMode>(Tag(RegistrationBox));
        if (p.Registration == RegistrationMode.FacebowMatrix)
        {
            var nums = MatrixBox.Text.Split(new[] { ' ', '\t', '\r', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => Num(s, "матрица")).ToArray();
            if (nums.Length != 16) throw new FormatException("Матрица должна содержать 16 чисел.");
            p.FacebowMatrix = nums;
        }
        p.CondyleCentersFromMeshes = CondyleFromMeshBox.IsChecked == true;
        p.AverageValues.BonwillSideMm = Num(BonwillBox.Text, "сторона Бонвиля");
        p.AverageValues.BalkwillAngleDeg = Num(BalkwillBox.Text, "угол Балквилла");
        p.AverageValues.OcclusalPlaneToAxisOrbitalDeg = Num(OpTiltBox.Text, "наклон окклюзионной плоскости");

        p.Joint.SagittalPathRight = Num(SspRBox.Text, "ССП справа");
        p.Joint.SagittalPathLeft = Num(SspLBox.Text, "ССП слева");
        p.Joint.BennettRight = Opt(BenRBox.Text, "Беннет справа");
        p.Joint.BennettLeft = Opt(BenLBox.Text, "Беннет слева");

        p.Manual.Overbite = Opt(ObBox.Text, "overbite");
        p.Manual.Overjet = Opt(OjBox.Text, "overjet");
        p.Manual.A1 = Opt(A1Box.Text, "A1");
        p.Manual.A2 = Opt(A2Box.Text, "A2");
        p.Manual.A3 = Opt(A3Box.Text, "A3");

        p.IncisorWidth = Num(IncWBox.Text, "ширина резца");
        p.IncisorLength = Num(IncLBox.Text, "длина резца");
        p.Proportions = Enum.Parse<ProportionSystem>(Tag(PropBox));
        p.WardRatio = Num(WardBox.Text, "RED Уорда");

        p.Broadrick.PosteriorPoint = Enum.Parse<PosteriorSurveyPoint>(Tag(PosteriorBox));
        p.Broadrick.RadiusMm = Num(RadiusBox.Text, "радиус Бродрика");
        p.Broadrick.OpscShiftMm = Num(ShiftBox.Text, "сдвиг OPSC");
        p.Surface.WilsonRadiusMm = Num(WilsonBox.Text, "радиус Уилсона");
        p.LongCentricMm = Num(LcBox.Text, "long centric");
    }

    private static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    private static string F(double? v) => v is { } d ? F(d) : "";

    private static double Num(string s, string field) =>
        double.TryParse(s.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v)
            ? v : throw new FormatException($"Поле «{field}»: не число.");

    private static double? Opt(string s, string field) => string.IsNullOrWhiteSpace(s) ? null : Num(s, field);

    private static void Select(ComboBox box, string tag) =>
        box.SelectedItem = box.Items.Cast<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == tag) ?? box.Items[0];

    private static string Tag(ComboBox box) => (string)((ComboBoxItem)box.SelectedItem).Tag;

    private void Registration_Changed(object sender, SelectionChangedEventArgs e) => UpdatePanels();

    private void UpdatePanels()
    {
        if (RegistrationBox.SelectedItem is null) return;
        var mode = Enum.Parse<RegistrationMode>(Tag(RegistrationBox));
        MatrixPanel.Visibility = mode == RegistrationMode.FacebowMatrix ? Visibility.Visible : Visibility.Collapsed;
        CondyleFromMeshBox.Visibility = mode == RegistrationMode.AxisOrbital ? Visibility.Visible : Visibility.Collapsed;
        AveragePanel.Visibility = mode == RegistrationMode.AverageValues ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RefreshMeshInfo()
    {
        string S(MeshRole r, string n) => _project.Meshes.TryGetValue(r, out var m) ? $"{n}: {m.TriangleCount:N0} тр." : $"{n}: —";
        MeshesInfo.Text = string.Join("   ", S(MeshRole.Upper, "Верх"), S(MeshRole.Lower, "Низ"),
            S(MeshRole.CondyleRight, "Мыщ. R"), S(MeshRole.CondyleLeft, "Мыщ. L"));
    }

    private void RefreshLandmarks()
    {
        var selected = (LandmarkList.SelectedItem as LandmarkItem)?.Landmark;
        LandmarkList.ItemsSource = Enum.GetValues<Landmark>().Select(l =>
        {
            var v = _project.GetLandmark(l);
            return new LandmarkItem
            {
                Landmark = l,
                Title = KuwataProject.LandmarkTitle(l),
                CoordText = v is { } p ? $"мир {p}" : "не указан",
                Brush = v is null ? Brushes.Gray : Brushes.DarkGreen,
            };
        }).ToList();
        if (selected is { } s)
            LandmarkList.SelectedItem = ((List<LandmarkItem>)LandmarkList.ItemsSource).First(i => i.Landmark == s);
    }

    // ───────────── Расчёт и сцена ─────────────

    private void Compute_Click(object sender, RoutedEventArgs e) => Recalculate(showErrors: true);

    private void Recalculate(bool showErrors)
    {
        try
        {
            UiToProject();
        }
        catch (FormatException ex)
        {
            if (showErrors) MessageBox.Show(ex.Message, "Ввод", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        _result = CaseCalculator.Compute(_project);
        ReportBox.Text = Report.Build(_project, _result);
        StatusText.Text = _result.Errors.Count == 0 ? "Рассчитано" : $"Рассчитано частично: {_result.Errors.Count} этап(а) не выполнено — см. отчёт";
        RebuildScene();
    }

    private RigidTransform Display => _result?.ScanToArticulator ?? RigidTransform.Identity;

    private void Layer_Click(object sender, RoutedEventArgs e) => RebuildScene();

    private void RebuildScene()
    {
        foreach (var v in _scene) Viewport.Children.Remove(v);
        _scene.Clear();
        _pickable.Clear();
        var D = Display;
        bool inArticulator = _result?.ScanToArticulator is not null;

        void Add(Visual3D v, bool pickable = false)
        {
            Viewport.Children.Add(v);
            _scene.Add(v);
            if (pickable) _pickable.Add(v);
        }

        void AddMesh(MeshRole role, Color c, double opacity, CheckBox layer)
        {
            if (layer.IsChecked == true && _project.Meshes.TryGetValue(role, out var m))
                Add(SceneBuilder.MeshVisual(m.Transformed(D), c, opacity), pickable: true);
        }

        AddMesh(MeshRole.Lower, LowerColor, 1.0, ShowLower);
        AddMesh(MeshRole.Upper, UpperColor, 0.55, ShowUpper);
        AddMesh(MeshRole.CondyleRight, CondyleColor, 0.8, ShowCondyles);
        AddMesh(MeshRole.CondyleLeft, CondyleColor, 0.8, ShowCondyles);

        if (ShowArticulator.IsChecked == true && inArticulator)
        {
            var cr = ArticulatorFrame.CondyleCenter(Side.Right);
            var cl = ArticulatorFrame.CondyleCenter(Side.Left);
            Add(SceneBuilder.Line(cl, cr, Colors.White, 2));
            Add(SceneBuilder.Marker(cr, Colors.White, 2.5));
            Add(SceneBuilder.Marker(cl, Colors.White, 2.5));
            Add(SceneBuilder.Label(cr, "Artex R", Colors.White));
            Add(SceneBuilder.Label(cl, "Artex L", Colors.White));
            Add(new ArrowVisual3D { Point1 = new Point3D(0, 0, 0), Point2 = new Point3D(30, 0, 0), Diameter = 1, Fill = Brushes.Red });
            Add(new ArrowVisual3D { Point1 = new Point3D(0, 0, 0), Point2 = new Point3D(0, 30, 0), Diameter = 1, Fill = Brushes.LimeGreen });
            Add(new ArrowVisual3D { Point1 = new Point3D(0, 0, 0), Point2 = new Point3D(0, 0, 30), Diameter = 1, Fill = Brushes.DodgerBlue });
            foreach (var w in new[] { _result?.CondyleRightWorld, _result?.CondyleLeftWorld })
                if (w is { } c) Add(SceneBuilder.Marker(D.Apply(c), CondyleColor, 1.5));
        }
        if (ShowPlane.IsChecked == true && inArticulator)
            Add(new GridLinesVisual3D { Center = new Point3D(0, 40, 0), Normal = new Vector3D(0, 0, 1),
                Width = 140, Length = 140, MinorDistance = 5, MajorDistance = 10, Thickness = 0.1, Fill = Brushes.Gray });

        if (ShowLandmarks.IsChecked == true)
            foreach (var l in Enum.GetValues<Landmark>())
                if (_project.GetLandmark(l) is { } p)
                {
                    var q = D.Apply(p);
                    Add(SceneBuilder.Marker(q, LandmarkColor));
                    Add(SceneBuilder.Label(q, l.ToString(), LandmarkColor));
                }

        if (_result?.Scheme is { } s)
        {
            if (ShowSurface.IsChecked == true)
                Add(SceneBuilder.MeshVisual(s.Surface, SurfaceColor, 0.6, twoSided: true));
            if (ShowCurves.IsChecked == true)
                foreach (var c in s.Curves.Where(c => c.Points.Count > 1))
                {
                    var color = c.Name.Contains("Уилсона") ? WilsonColor : c.Name.Contains("Бродрика") ? BroadrickColor : SpeeColor;
                    Add(SceneBuilder.Curve(c.Points, color, c.Name.Contains("Бродрика") ? 0.3 : 0.6));
                }
            if (ShowConstruction.IsChecked == true)
                foreach (var np in s.Points)
                {
                    Add(SceneBuilder.Marker(np.Point, ConstructionColor, 1.0));
                    Add(SceneBuilder.Label(np.Point, np.Name, ConstructionColor));
                }
        }
    }

    // ───────────── Ориентиры ─────────────

    private void PickLandmark_Click(object sender, RoutedEventArgs e)
    {
        if (LandmarkList.SelectedItem is not LandmarkItem item)
        {
            MessageBox.Show("Выберите ориентир в списке.", "Ориентиры");
            return;
        }
        if (_pickable.Count == 0)
        {
            MessageBox.Show("Сначала загрузите модели (меню «Импорт»).", "Ориентиры");
            return;
        }
        _picking = item.Landmark;
        Viewport.Cursor = Cursors.Cross;
        StatusText.Text = $"Кликните по модели: {item.Title}. Esc — отмена.";
    }

    private void ClearLandmark_Click(object sender, RoutedEventArgs e)
    {
        if (LandmarkList.SelectedItem is not LandmarkItem item) return;
        _project.ClearLandmark(item.Landmark);
        RefreshLandmarks();
        Recalculate(showErrors: false);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _picking is not null)
        {
            _picking = null;
            Viewport.Cursor = null;
            StatusText.Text = "Указание ориентира отменено";
        }
        base.OnKeyDown(e);
    }

    private void Viewport_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_picking is not { } lm) return;
        var hits = Viewport.Viewport.FindHits(e.GetPosition(Viewport.Viewport));
        var hit = hits.FirstOrDefault(h => h.Visual is not null && _pickable.Contains(h.Visual));
        if (hit is null) return;
        e.Handled = true;
        // Точка на экране — в отображаемой системе; сохраняем в исходных координатах.
        var world = Display.Inverse().Apply(SceneBuilder.V(hit.Position));
        _project.SetLandmark(lm, world);
        _picking = null;
        Viewport.Cursor = null;
        RefreshLandmarks();
        Recalculate(showErrors: false);
        StatusText.Text = $"Указан: {KuwataProject.LandmarkTitle(lm)}";
    }

    // ───────────── Файлы ─────────────

    private void New_Click(object sender, RoutedEventArgs e)
    {
        _project = new KuwataProject();
        _path = null;
        ProjectToUi();
        Recalculate(showErrors: false);
        Viewport.ZoomExtents();
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = $"Проект Kuwata (*{ProjectFile.Extension})|*{ProjectFile.Extension}" };
        if (dlg.ShowDialog(this) != true) return;
        _project = ProjectFile.Load(dlg.FileName);
        _path = dlg.FileName;
        ProjectToUi();
        Recalculate(showErrors: false);
        Viewport.ZoomExtents();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (_path is null) { SaveAs_Click(sender, e); return; }
        SaveTo(_path);
    }

    private void SaveAs_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Filter = $"Проект Kuwata (*{ProjectFile.Extension})|*{ProjectFile.Extension}",
            FileName = string.IsNullOrWhiteSpace(CaseNameBox.Text) ? "case" : CaseNameBox.Text,
        };
        if (dlg.ShowDialog(this) != true) return;
        SaveTo(dlg.FileName);
    }

    private void SaveTo(string path)
    {
        UiToProject();
        ProjectFile.Save(_project, path);
        _path = path;
        StatusText.Text = $"Сохранено: {path}";
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var role = Enum.Parse<MeshRole>((string)((MenuItem)sender).Tag);
        var dlg = new OpenFileDialog { Filter = "Модели (*.stl;*.obj)|*.stl;*.obj" };
        if (dlg.ShowDialog(this) != true) return;
        KMesh mesh = MeshFiles.Load(dlg.FileName);
        _project.Meshes[role] = mesh;
        RefreshMeshInfo();
        Recalculate(showErrors: false);
        Viewport.ZoomExtents();
    }

    private void ExportStl_Click(object sender, RoutedEventArgs e)
    {
        if (_result?.Scheme is null)
        {
            MessageBox.Show("Поверхность не рассчитана — см. отчёт.", "Экспорт");
            return;
        }
        var frame = Enum.Parse<ExportFrame>((string)((MenuItem)sender).Tag);
        var dlg = new SaveFileDialog { Filter = "STL (*.stl)|*.stl", FileName = frame == ExportFrame.Articulator ? "occlusal_artex.stl" : "occlusal_scan.stl" };
        if (dlg.ShowDialog(this) != true) return;
        Export.WriteSurfaceStl(dlg.FileName, _result, frame, IncludeCurvesItem.IsChecked);
        StatusText.Text = $"Экспортировано: {dlg.FileName}";
    }

    private void ExportJson_Click(object sender, RoutedEventArgs e)
    {
        if (_result is null) return;
        var dlg = new SaveFileDialog { Filter = "JSON (*.json)|*.json", FileName = "kuwata_parameters.json" };
        if (dlg.ShowDialog(this) != true) return;
        Export.WriteParametersJson(dlg.FileName, _project, _result);
        StatusText.Text = $"Экспортировано: {dlg.FileName}";
    }

    private void ExportReport_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog { Filter = "Текст (*.txt)|*.txt", FileName = "kuwata_report.txt" };
        if (dlg.ShowDialog(this) != true) return;
        File.WriteAllText(dlg.FileName, ReportBox.Text);
    }
}
