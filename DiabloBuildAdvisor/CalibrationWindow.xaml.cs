using System.Windows;
using System.Windows.Input;

namespace DiabloBuildAdvisor;
public partial class CalibrationWindow : Window
{
    private readonly OverlayStep _first, _second; private Point? _firstPoint;
    public Point ScreenA { get; private set; } public Point ScreenB { get; private set; }
    public CalibrationWindow(OverlayStep first, OverlayStep second)
    {
        InitializeComponent(); _first = first; _second = second;
        Left = SystemParameters.VirtualScreenLeft; Top = SystemParameters.VirtualScreenTop; Width = SystemParameters.VirtualScreenWidth; Height = SystemParameters.VirtualScreenHeight;
        InstructionText.Text = $"校准 1/2：点击「{first.Label}」节点中心";
    }
    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var local = e.GetPosition(this); var screen = new Point(local.X + Left, local.Y + Top);
        if (_firstPoint is null) { _firstPoint = screen; ScreenA = screen; InstructionText.Text = $"校准 2/2：点击「{_second.Label}」节点中心"; }
        else { ScreenB = screen; DialogResult = true; Close(); }
    }
    private void Window_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) { DialogResult = false; Close(); } }
}
