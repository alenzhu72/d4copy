using Microsoft.Win32;
using System.Windows;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Input;

namespace DiabloBuildAdvisor;

public partial class MainWindow : Window
{
    private BuildProfile? _current;
    private BuildProfile? _target;
    private readonly LinkImporter _linkImporter = new();
    private readonly NodeCatalogService _nodeCatalog = new();
    private readonly CalibrationStore _calibrationStore = new();
    private List<OverlayStep> _skillSteps = [];
    private OverlayWindow? _overlay;
    private HwndSource? _source;
    private const int HotkeyToggle = 7101, HotkeyNext = 7102, HotkeyPrevious = 7103, WmHotkey = 0x0312;
    public MainWindow() { InitializeComponent(); SourceInitialized += OnSourceInitialized; Closed += (_, _) => UnregisterHotkeys(); }

    private async void ImportJson_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Build JSON (*.json)|*.json|所有文件 (*.*)|*.*", Title = "选择 Build 导出文件" };
        if (dialog.ShowDialog() != true) return;
        try { LoadProfile(BuildParser.Parse(await File.ReadAllTextAsync(dialog.FileName), Path.GetFileName(dialog.FileName))); }
        catch (Exception ex) { ShowError(ex.Message); }
    }

    private async void ImportLink_Click(object sender, RoutedEventArgs e)
    {
        try { SetBusy(true, "正在读取公开页面…"); LoadProfile(await _linkImporter.ImportAsync(UrlBox.Text.Trim())); }
        catch (Exception ex) { ShowError(ex.Message); }
        finally { SetBusy(false); }
    }

    private void SetTarget_Click(object sender, RoutedEventArgs e)
    {
        if (_current is null) { ShowError("请先导入一个 Build，再将它设为目标。"); return; }
        _target = _current; TargetText.Text = $"目标：{_target.Name} · 现在请导入你的当前 Build"; ComparisonGrid.ItemsSource = null;
        ComparisonGrid.Visibility = Visibility.Collapsed;
        StatusText.Text = "●  目标已锁定";
    }

    private void LoadProfile(BuildProfile profile)
    {
        _current = profile;
        BuildNameText.Text = $"{profile.Name} · {profile.Class} · {profile.Source}";
        WeaponText.Text = profile.WeaponDamage > 0 ? profile.WeaponDamage.ToString("N0") : "待补充"; MainStatText.Text = profile.MainStat > 0 ? profile.MainStat.ToString("N0") : "待补充";
        AdditiveText.Text = profile.AdditiveDamage > 0 ? $"+{profile.AdditiveDamage:0}%" : "待补充"; CritText.Text = profile.CritChance > 0 || profile.CritDamage > 0 ? $"{profile.CritChance:P0} / {profile.CritDamage:P0}" : "待补充";
        SuggestionsList.ItemsSource = SuggestionEngine.Generate(profile);
        CurrentGearGrid.ItemsSource = profile.Gear;
        GearCountText.Text = $"已导入 {profile.Gear.Count} 件装备 · 双击表格行可查看完整内容";
        if (_target is not null && !ReferenceEquals(profile, _target)) { ComparisonGrid.ItemsSource = SuggestionEngine.Compare(profile, _target); ComparisonGrid.Visibility = Visibility.Visible; TargetText.Text = $"当前：{profile.Name}  →  目标：{_target.Name}"; }
        StatusText.Text = $"●  已载入 {profile.Gear.Count} 件装备";
        try { _skillSteps = _nodeCatalog.ResolveSkills(profile); } catch { _skillSteps = []; }
        ParagonBoardBox.ItemsSource = profile.ParagonBoards; if (profile.ParagonBoards.Count > 0) ParagonBoardBox.SelectedIndex = 0;
        RouteStepsList.ItemsSource = _skillSteps;
        RouteSummaryText.Text = $"技能节点 {_skillSteps.Count} 个 · 技能栏 {profile.SkillBar.Count} 个 · 巅峰盘 {profile.ParagonBoards.Count} 块";
    }

    private void StartSkillOverlay_Click(object sender, RoutedEventArgs e)
    {
        if (_skillSteps.Count < 2) { ShowError("当前 Build 没有足够的技能节点数据。请导入 Maxroll Planner 链接。"); return; }
        RouteStepsList.ItemsSource = _skillSteps; StartCalibration(_skillSteps);
    }

    private void StartParagonOverlay_Click(object sender, RoutedEventArgs e)
    {
        if (ParagonBoardBox.SelectedItem is not ParagonBoardPlan plan) { ShowError("请先选择一块巅峰盘。"); return; }
        var steps = _nodeCatalog.ResolveParagon(plan); RouteStepsList.ItemsSource = steps;
        if (steps.Count < 2) { ShowError("这块巅峰盘没有足够的节点数据。"); return; }
        StartCalibration(steps);
    }

    private void StartCalibration(List<OverlayStep> steps)
    {
        var pair = FindFarthestPair(steps);
        var calibrationKey = $"{steps[0].Mode}:{steps[0].Group}:{SystemParameters.PrimaryScreenWidth}x{SystemParameters.PrimaryScreenHeight}";
        var saved = _calibrationStore.Get(calibrationKey);
        if (saved is not null && saved.ScreenWidth == SystemParameters.PrimaryScreenWidth && saved.ScreenHeight == SystemParameters.PrimaryScreenHeight &&
            MessageBox.Show("发现这个界面已保存的校准。\n\n选择“是”直接使用；选择“否”重新校准。", "使用已保存校准", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            ShowOverlay(steps, pair, saved.A, saved.B); return;
        }
        Hide();
        var calibration = new CalibrationWindow(pair.Item1, pair.Item2);
        var success = calibration.ShowDialog() == true;
        Show(); Activate();
        if (!success) return;
        _calibrationStore.Save(calibrationKey, calibration.ScreenA, calibration.ScreenB);
        ShowOverlay(steps, pair, calibration.ScreenA, calibration.ScreenB);
    }
    private void ShowOverlay(List<OverlayStep> steps, (OverlayStep, OverlayStep) pair, Point screenA, Point screenB)
    {
        _overlay?.Close(); _overlay = new OverlayWindow(steps, new Point(pair.Item1.LogicalX, pair.Item1.LogicalY), new Point(pair.Item2.LogicalX, pair.Item2.LogicalY), screenA, screenB);
        _overlay.Show(); StatusText.Text = $"●  {steps[0].Mode}红点已显示";
    }

    private static (OverlayStep, OverlayStep) FindFarthestPair(List<OverlayStep> steps)
    {
        var best = (steps[0], steps[1]); var max = -1d;
        for (var i = 0; i < steps.Count; i++) for (var j = i + 1; j < steps.Count; j++) { var dx = steps[i].LogicalX - steps[j].LogicalX; var dy = steps[i].LogicalY - steps[j].LogicalY; var d = dx * dx + dy * dy; if (d > max && Math.Abs(dx) > .01 && Math.Abs(dy) > .01) { max = d; best = (steps[i], steps[j]); } }
        return best;
    }
    private void CloseOverlay_Click(object sender, RoutedEventArgs e) { _overlay?.Close(); _overlay = null; }
    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle; _source = HwndSource.FromHwnd(hwnd); _source.AddHook(WndProc);
        RegisterHotKey(hwnd, HotkeyToggle, 0, 0x77); RegisterHotKey(hwnd, HotkeyNext, 0, 0x78); RegisterHotKey(hwnd, HotkeyPrevious, 0x0004, 0x78);
    }
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WmHotkey) return IntPtr.Zero; handled = true;
        switch (wParam.ToInt32()) { case HotkeyToggle: _overlay?.ToggleOverlay(); break; case HotkeyNext: _overlay?.Next(); break; case HotkeyPrevious: _overlay?.Previous(); break; }
        return IntPtr.Zero;
    }
    private void UnregisterHotkeys() { var hwnd = new WindowInteropHelper(this).Handle; UnregisterHotKey(hwnd, HotkeyToggle); UnregisterHotKey(hwnd, HotkeyNext); UnregisterHotKey(hwnd, HotkeyPrevious); }
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    private void SetBusy(bool busy, string? text = null) { IsEnabled = !busy; if (text is not null) StatusText.Text = text; }
    private void ShowError(string message) { StatusText.Text = "●  导入失败"; MessageBox.Show(message, "无法导入 Build", MessageBoxButton.OK, MessageBoxImage.Warning); }
}
