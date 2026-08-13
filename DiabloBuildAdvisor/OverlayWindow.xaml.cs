using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Controls;

namespace DiabloBuildAdvisor;

public partial class OverlayWindow : Window
{
    private const int GwlExstyle = -20, WsExTransparent = 0x20, WsExToolwindow = 0x80, WsExNoactivate = 0x08000000;
    private readonly List<OverlayStep> _steps;
    private readonly Point _logicalA, _logicalB, _screenA, _screenB;
    private int _current;
    public OverlayWindow(List<OverlayStep> steps, Point logicalA, Point logicalB, Point screenA, Point screenB)
    {
        InitializeComponent(); _steps = steps; _logicalA = logicalA; _logicalB = logicalB; _screenA = screenA; _screenB = screenB;
        Left = SystemParameters.VirtualScreenLeft; Top = SystemParameters.VirtualScreenTop; Width = SystemParameters.VirtualScreenWidth; Height = SystemParameters.VirtualScreenHeight;
        Loaded += (_, _) => { MakeClickThrough(); Render(); };
    }
    public void Next() { _current = Math.Min(_steps.Count - 1, _current + 1); Render(); }
    public void Previous() { _current = Math.Max(0, _current - 1); Render(); }
    public void ToggleOverlay() { Visibility = Visibility == Visibility.Visible ? Visibility.Hidden : Visibility.Visible; }
    private Point Map(OverlayStep s)
    {
        var dx = Math.Abs(_logicalB.X - _logicalA.X) < .001 ? 1 : _logicalB.X - _logicalA.X;
        var dy = Math.Abs(_logicalB.Y - _logicalA.Y) < .001 ? 1 : _logicalB.Y - _logicalA.Y;
        return new Point(_screenA.X + (s.LogicalX - _logicalA.X) * (_screenB.X - _screenA.X) / dx - Left, _screenA.Y + (s.LogicalY - _logicalA.Y) * (_screenB.Y - _screenA.Y) / dy - Top);
    }
    private void Render()
    {
        MarkerCanvas.Children.Clear();
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
    }
    private void MakeClickThrough() { var hwnd = new WindowInteropHelper(this).Handle; SetWindowLong(hwnd, GwlExstyle, GetWindowLong(hwnd, GwlExstyle) | WsExTransparent | WsExToolwindow | WsExNoactivate); }
    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}
