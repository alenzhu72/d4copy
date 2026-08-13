using System.IO;
using System.Text.Json;

namespace DiabloBuildAdvisor;

public sealed class BuildHistoryItem
{
    public string Url { get; set; } = "";
    public string BuildName { get; set; } = "Maxroll Build";
    public string ClassName { get; set; } = "";
    public DateTime LastOpened { get; set; } = DateTime.Now;
    public string Display => BuildName;
    public string Detail => string.IsNullOrWhiteSpace(ClassName) ? LastOpened.ToString("MM-dd HH:mm") : $"{ClassName} · {LastOpened:MM-dd HH:mm}";
}

public sealed class BuildHistoryStore
{
    private readonly string _path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DiabloBuildAdvisor", "build-history.json");

    public List<BuildHistoryItem> Load()
    {
        try { return File.Exists(_path) ? JsonSerializer.Deserialize<List<BuildHistoryItem>>(File.ReadAllText(_path))?.OrderByDescending(x => x.LastOpened).ToList() ?? [] : []; }
        catch { return []; }
    }

    public List<BuildHistoryItem> Add(string url, BuildProfile profile)
    {
        var items = Load();
        items.RemoveAll(x => string.Equals(Normalize(x.Url), Normalize(url), StringComparison.OrdinalIgnoreCase));
        items.Insert(0, new BuildHistoryItem { Url = url.Trim(), BuildName = profile.Name, ClassName = profile.Class, LastOpened = DateTime.Now });
        items = items.Take(20).ToList();
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true }));
        return items;
    }

    public void Clear() { try { if (File.Exists(_path)) File.Delete(_path); } catch { } }
    private static string Normalize(string url) => url.Trim().TrimEnd('/');
}
