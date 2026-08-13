using System.IO;
using System.Text.Json;

namespace DiabloBuildAdvisor;

public sealed class NodeCatalogService
{
    private JsonDocument? _catalog;
    private JsonElement Root => (_catalog ??= JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "maxroll-catalog.json")))).RootElement;

    public List<OverlayStep> ResolveSkills(BuildProfile profile)
    {
        var treeName = profile.Class switch { "Barbarian" or "野蛮人" => "Barbarian", "Sorcerer" or "巫师" => "Sorcerer", "Rogue" or "游侠" => "Rogue", "Druid" or "德鲁伊" => "Druid", "Necromancer" or "死灵法师" => "Necromancer", "Spiritborn" or "魂灵师" => "Spiritborn", "Paladin" or "圣骑士" => "Paladin", _ => profile.Class };
        if (!Root.GetProperty("skillTrees").TryGetProperty(treeName, out var tree)) throw new InvalidDataException($"坐标目录中没有 {profile.Class} 的技能树。");
        var nodes = tree.GetProperty("nodes").EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object).ToDictionary(x => x.GetProperty("id").GetInt32());
        var skills = Root.GetProperty("skills");
        var result = new List<OverlayStep>();
        foreach (var allocation in profile.Skills)
        {
            if (!nodes.TryGetValue(allocation.NodeId, out var node)) continue;
            var reward = node.GetProperty("reward");
            var power = reward.TryGetProperty("power", out var p) ? p.GetString() ?? "" : "";
            var name = power;
            if (power.Length > 0 && skills.TryGetProperty(power, out var skill) && skill.TryGetProperty("name", out var skillName)) name = skillName.GetString() ?? power;
            var pos = node.GetProperty("pos");
            allocation.Name = name; allocation.X = pos.GetProperty("x").GetDouble(); allocation.Y = pos.GetProperty("y").GetDouble();
            result.Add(new OverlayStep { Mode = "技能", Group = treeName, Label = name, Clicks = allocation.Ranks, LogicalX = allocation.X, LogicalY = allocation.Y });
        }
        return result;
    }

    public List<OverlayStep> ResolveParagon(ParagonBoardPlan plan)
    {
        if (!Root.GetProperty("paragonBoards").TryGetProperty(plan.BoardId, out var board)) return [];
        plan.Name = board.TryGetProperty("name", out var name) ? name.GetString() ?? plan.BoardId : plan.BoardId;
        var width = board.GetProperty("width").GetInt32();
        var nodeArray = board.GetProperty("nodes");
        var result = new List<OverlayStep>();
        foreach (var id in plan.NodeIds)
        {
            var x = id % width; var y = id / width;
            (double rx, double ry) = plan.Rotation switch { 1 => (width - 1 - y, x), 2 => (width - 1 - x, width - 1 - y), 3 => (y, width - 1 - x), _ => (x, y) };
            var nodeType = id >= 0 && id < nodeArray.GetArrayLength() && nodeArray[id].ValueKind == JsonValueKind.String ? nodeArray[id].GetString() ?? "节点" : "节点";
            var color = nodeType.Contains("Legendary", StringComparison.OrdinalIgnoreCase) ? "#FFB020" : nodeType.Contains("Rare", StringComparison.OrdinalIgnoreCase) ? "#FFD60A" : nodeType.Contains("Socket", StringComparison.OrdinalIgnoreCase) ? "#BF5AF2" : "#FF3B30";
            result.Add(new OverlayStep { Mode = "巅峰", Group = plan.BoardId, Label = $"节点 {id}", LogicalX = rx, LogicalY = ry, Color = color });
        }
        return result;
    }
}
