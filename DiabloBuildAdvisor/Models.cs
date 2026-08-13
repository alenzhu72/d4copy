using System.Collections.ObjectModel;

namespace DiabloBuildAdvisor;

public sealed class BuildProfile
{
    public string Name { get; set; } = "未命名 Build";
    public string Class { get; set; } = "未知职业";
    public string Source { get; set; } = "本地 JSON";
    public double WeaponDamage { get; set; }
    public double MainStat { get; set; }
    public double AdditiveDamage { get; set; }
    public double CritChance { get; set; }
    public double CritDamage { get; set; }
    public double AttackSpeed { get; set; }
    public int MultiplicativeAspects { get; set; }
    public List<GearItem> Gear { get; set; } = [];
    public List<SkillAllocation> Skills { get; set; } = [];
    public List<string> SkillBar { get; set; } = [];
    public List<ParagonBoardPlan> ParagonBoards { get; set; } = [];
}

public sealed class SkillAllocation
{
    public int NodeId { get; set; }
    public int Ranks { get; set; }
    public string Name { get; set; } = "技能节点";
    public double X { get; set; }
    public double Y { get; set; }
    public string Display => $"{Name} × {Ranks}";
}

public sealed class ParagonBoardPlan
{
    public string BoardId { get; set; } = "";
    public string Name { get; set; } = "巅峰盘";
    public int Rotation { get; set; }
    public int PositionX { get; set; }
    public int PositionY { get; set; }
    public string Glyph { get; set; } = "";
    public int GlyphLevel { get; set; }
    public List<int> NodeIds { get; set; } = [];
    public string Display => $"{Name} · 旋转 {Rotation * 90}° · {NodeIds.Count} 节点";
}

public sealed class OverlayStep
{
    public string Mode { get; set; } = "技能";
    public string Group { get; set; } = "";
    public string Label { get; set; } = "";
    public int Clicks { get; set; } = 1;
    public double LogicalX { get; set; }
    public double LogicalY { get; set; }
    public string Color { get; set; } = "#FF3B30";
    public string Display => $"{Label}" + (Clicks > 1 ? $" × {Clicks}" : "");
}

public sealed class GearItem
{
    public string Slot { get; set; } = "未知部位";
    public string Name { get; set; } = "未命名装备";
    public string Aspect { get; set; } = "—";
    public List<Affix> Affixes { get; set; } = [];
    public string AffixSummary => Affixes.Count == 0 ? "—" : string.Join(" · ", Affixes.Take(2).Select(a => a.Value == 0 ? a.Name : $"{a.Name} {a.Value:0.##}{a.Unit}")) + (Affixes.Count > 2 ? $"  等 {Affixes.Count} 条" : "");
}

public sealed class Affix { public string Name { get; set; } = ""; public double Value { get; set; } public string Unit { get; set; } = "%"; }

public sealed class Suggestion
{
    public string Severity { get; set; } = "建议";
    public string Icon { get; set; } = "✦";
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public string Accent { get; set; } = "#D8A84E";
}

public sealed class GearComparison
{
    public string Slot { get; set; } = "";
    public string Current { get; set; } = "—";
    public string Target { get; set; } = "—";
    public string Status { get; set; } = "待检查";
    public string Detail { get; set; } = "";
}
