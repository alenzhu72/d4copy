using System.Windows;
using System.Windows.Input;

namespace DiabloBuildAdvisor;
public partial class CalibrationWindow : Window
{
    private readonly OverlayStep _first, _second, _third; private int _stage;
    public Point ScreenA { get; private set; } public Point ScreenB { get; private set; } public Point ScreenC { get; private set; }
    public CalibrationWindow(OverlayStep first, OverlayStep second, OverlayStep third)
    {
        InitializeComponent(); _first = first; _second = second; _third = third;
        Left = SystemParameters.VirtualScreenLeft; Top = SystemParameters.VirtualScreenTop; Width = SystemParameters.VirtualScreenWidth; Height = SystemParameters.VirtualScreenHeight;
        InstructionText.Text = $"校准 1/3：点击基础技能「{first.Label}」中心";
    }
    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var local = e.GetPosition(this); var screen = new Point(local.X + Left, local.Y + Top);
        if (_stage == 0) { ScreenA = screen; _stage = 1; InstructionText.Text = $"校准 2/3：点击基础技能「{_second.Label}」中心"; }
        else if (_stage == 1) { ScreenB = screen; _stage = 2; InstructionText.Text = $"校准 3/3：点击基础技能「{_third.Label}」中心"; }
        else { ScreenC = screen; DialogResult = true; Close(); }
    }
    private void Window_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) { DialogResult = false; Close(); } }
}
