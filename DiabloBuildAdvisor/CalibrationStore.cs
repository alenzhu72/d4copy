using System.IO;
using System.Text.Json;
using System.Windows;

namespace DiabloBuildAdvisor;

public sealed class CalibrationStore
{
    private readonly string _path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DiabloBuildAdvisor", "calibration.json");
    private Dictionary<string, SavedCalibration> Load()
    {
        try { return File.Exists(_path) ? JsonSerializer.Deserialize<Dictionary<string, SavedCalibration>>(File.ReadAllText(_path)) ?? [] : []; } catch { return []; }
    }
    public SavedCalibration? Get(string key) { var all = Load(); return all.TryGetValue(key, out var value) ? value : null; }
    public void Save(string key, Point a, Point b, Point c)
    {
        var all = Load(); all[key] = new SavedCalibration { AX = a.X, AY = a.Y, BX = b.X, BY = b.Y, CX = c.X, CY = c.Y, ScreenWidth = SystemParameters.PrimaryScreenWidth, ScreenHeight = SystemParameters.PrimaryScreenHeight };
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!); File.WriteAllText(_path, JsonSerializer.Serialize(all, new JsonSerializerOptions { WriteIndented = true }));
    }
}
public sealed class SavedCalibration
{
    public double AX { get; set; } public double AY { get; set; } public double BX { get; set; } public double BY { get; set; }
    public double CX { get; set; } public double CY { get; set; }
    public double ScreenWidth { get; set; } public double ScreenHeight { get; set; }
    public Point A => new(AX, AY); public Point B => new(BX, BY); public Point C => new(CX, CY);
}
