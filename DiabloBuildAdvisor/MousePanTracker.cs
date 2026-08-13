using System.Runtime.InteropServices;
using System.Text;

namespace DiabloBuildAdvisor;

/// <summary>
/// Mirrors the user's real left-button drag delta. Polling avoids input injection and
/// is more precise than estimating motion from animated screen frames.
/// </summary>
public sealed class MousePanTracker : IDisposable
{
    private readonly Action<double, double> _onPan;
    private readonly System.Threading.Timer _timer;
    private PointNative _last;
    private bool _wasDragging;
    private int _busy;

    public MousePanTracker(Action<double, double> onPan)
    {
        _onPan = onPan;
        _timer = new System.Threading.Timer(_ => Tick(), null, 250, 16);
    }

    private void Tick()
    {
        if (Interlocked.Exchange(ref _busy, 1) != 0) return;
        try
        {
            var current = new PointNative();
            var dragging = IsDiabloForeground() && (GetAsyncKeyState(0x01) & 0x8000) != 0 && GetCursorPos(out current);
            if (dragging && _wasDragging)
            {
                var dx = current.X - _last.X; var dy = current.Y - _last.Y;
                if (Math.Abs(dx) <= 200 && Math.Abs(dy) <= 200 && (dx != 0 || dy != 0)) _onPan(dx, dy);
            }
            if (dragging) _last = current;
            _wasDragging = dragging;
        }
        finally { Volatile.Write(ref _busy, 0); }
    }

    private static bool IsDiabloForeground()
    {
        var hwnd = GetForegroundWindow(); if (hwnd == IntPtr.Zero) return false;
        var title = new StringBuilder(256); GetWindowText(hwnd, title, title.Capacity); var value = title.ToString();
        return value.Contains("Diablo IV", StringComparison.OrdinalIgnoreCase) || value.Contains("暗黑破坏神IV", StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose() => _timer.Dispose();
    [StructLayout(LayoutKind.Sequential)] private struct PointNative { public int X; public int Y; }
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out PointNative point);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int maxCount);
}
