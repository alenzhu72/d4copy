using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Controls;
using System.Windows.Input;

namespace DiabloBuildAdvisor;

public partial class OverlayWindow : Window
{
    private const int GwlExstyle = -20, WsExTransparent = 0x20, WsExToolwindow = 0x80, WsExNoactivate = 0x08000000;
    private readonly List<OverlayStep> _steps;
    private readonly Point _logicalA, _logicalB, _logicalC, _screenA, _screenB, _screenC;
    private int _current;
    private double _panX, _panY;
    private double _manualX, _manualY;
    private MousePanTracker? _panTracker;
    private bool _adjusting, _dragging;
    private Point _dragStart;
    public OverlayWindow(List<OverlayStep> steps, Point logicalA, Point logicalB, Point logicalC, Point screenA, Point screenB, Point screenC)
    {
        InitializeComponent(); _steps = steps; _logicalA = logicalA; _logicalB = logicalB; _logicalC = logicalC; _screenA = screenA; _screenB = screenB; _screenC = screenC;
        Left = SystemParameters.VirtualScreenLeft; Top = SystemParameters.VirtualScreenTop; Width = SystemParameters.VirtualScreenWidth; Height = SystemParameters.VirtualScreenHeight;
        Loaded += (_, _) =>
        {
            MakeClickThrough(); Render();
            if (_steps.Count > 0 && _steps[0].Mode == "技能")
                _panTracker = new MousePanTracker((dx, dy) => Dispatcher.BeginInvoke(() => { _panX += dx; _panY += dy; Render(); }));
        };
    }
    public void Next() { _current = Math.Min(_steps.Count - 1, _current + 1); Render(); }
    public void Previous() { _current = Math.Max(0, _current - 1); Render(); }
    public void ToggleOverlay() { Visibility = Visibility == Visibility.Visible ? Visibility.Hidden : Visibility.Visible; }
    public void ToggleAdjustMode()
    {
        _adjusting = !_adjusting; _dragging = false;
        var hwnd = new WindowInteropHelper(this).Handle; var style = GetWindowLong(hwnd, GwlExstyle);
        SetWindowLong(hwnd, GwlExstyle, _adjusting ? style & ~WsExTransparent & ~WsExNoactivate : style | WsExTransparent | WsExNoactivate);
        Cursor = _adjusting ? Cursors.SizeAll : Cursors.Arrow;
        if (_adjusting) { Activate(); Focus(); }
        Render();
    }
    protected override void OnClosed(EventArgs e) { _panTracker?.Dispose(); _panTracker = null; MarkerCanvas.Children.Clear(); base.OnClosed(e); }
    private Point Map(OverlayStep s)
    {
        // Three-point affine transform corrects independent X/Y scale and shear in the game UI.
        var ux1 = _logicalB.X - _logicalA.X; var uy1 = _logicalB.Y - _logicalA.Y;
        var ux2 = _logicalC.X - _logicalA.X; var uy2 = _logicalC.Y - _logicalA.Y;
        var det = ux1 * uy2 - ux2 * uy1;
        if (Math.Abs(det) < .001) return new Point(_screenA.X + _panX - Left, _screenA.Y + _panY - Top);
        var px = s.LogicalX - _logicalA.X; var py = s.LogicalY - _logicalA.Y;
        var alpha = (px * uy2 - ux2 * py) / det; var beta = (ux1 * py - px * uy1) / det;
        var x = _screenA.X + alpha * (_screenB.X - _screenA.X) + beta * (_screenC.X - _screenA.X);
        var y = _screenA.Y + alpha * (_screenB.Y - _screenA.Y) + beta * (_screenC.Y - _screenA.Y);
        return new Point(x + _panX + _manualX - Left, y + _panY + _manualY - Top);
    }
    private void Render()
    {
        MarkerCanvas.Children.Clear();
        var byId = _steps.ToDictionary(x => x.NodeId);
        foreach (var step in _steps.Where(x => x.ParentNodeId.HasValue && byId.ContainsKey(x.ParentNodeId.Value)))
        {
            var a = Map(byId[step.ParentNodeId!.Value]); var b = Map(step);
            MarkerCanvas.Children.Add(new Line { X1 = a.X, Y1 = a.Y, X2 = b.X, Y2 = b.Y, Stroke = new SolidColorBrush(Color.FromArgb(210, 255, 35, 35)), StrokeThickness = 3 });
        }
        for (var i = 0; i < _steps.Count; i++)
        {
            var p = Map(_steps[i]); var active = i == _current; var size = active ? 25d : 14d;
            var ellipse = new Ellipse { Width = size, Height = size, Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_steps[i].Color)), Opacity = active ? .95 : .58, Stroke = Brushes.White, StrokeThickness = active ? 3 : 1 };
            Canvas.SetLeft(ellipse, p.X - size / 2); Canvas.SetTop(ellipse, p.Y - size / 2); MarkerCanvas.Children.Add(ellipse);
            if (active)
            {
                var label = new Border { Background = new SolidColorBrush(Color.FromArgb(220, 10, 10, 12)), CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 4, 8, 4), Child = new TextBlock { Text = $"{_current + 1}/{_steps.Count}  {_steps[i].Display}", Foreground = Brushes.White, FontSize = 15, FontWeight = FontWeights.SemiBold } };
                Canvas.SetLeft(label, p.X + 18); Canvas.SetTop(label, p.Y - 18); MarkerCanvas.Children.Add(label);
            }
        }
        if (_adjusting)
        {
            var notice = new Border { Background = new SolidColorBrush(Color.FromArgb(235, 80, 10, 10)), CornerRadius = new CornerRadius(6), Padding = new Thickness(14, 9, 14, 9), Child = new TextBlock { Text = "表面层对齐模式：拖动红点层 · 方向键微调 · F7 完成", Foreground = Brushes.White, FontSize = 16, FontWeight = FontWeights.Bold } };
            Canvas.SetLeft(notice, Math.Max(20, Width / 2 - 230)); Canvas.SetTop(notice, 80); MarkerCanvas.Children.Add(notice);
        }
    }
    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (!_adjusting) return; _dragging = true; _dragStart = e.GetPosition(this); CaptureMouse(); }
    private void Window_MouseMove(object sender, MouseEventArgs e) { if (!_adjusting || !_dragging) return; var p = e.GetPosition(this); _manualX += p.X - _dragStart.X; _manualY += p.Y - _dragStart.Y; _dragStart = p; Render(); }
    private void Window_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) { if (!_dragging) return; _dragging = false; ReleaseMouseCapture(); }
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (!_adjusting) return; var amount = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1;
        if (e.Key == Key.Left) _manualX -= amount; else if (e.Key == Key.Right) _manualX += amount; else if (e.Key == Key.Up) _manualY -= amount; else if (e.Key == Key.Down) _manualY += amount; else return;
        e.Handled = true; Render();
    }
    private void MakeClickThrough() { var hwnd = new WindowInteropHelper(this).Handle; SetWindowLong(hwnd, GwlExstyle, GetWindowLong(hwnd, GwlExstyle) | WsExTransparent | WsExToolwindow | WsExNoactivate); }
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}
