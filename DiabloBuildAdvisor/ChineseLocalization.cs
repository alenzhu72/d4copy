namespace DiabloBuildAdvisor;

public static class ChineseLocalization
{
    private static readonly Dictionary<string, string> GearNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Tuskhelm of Joritz the Mighty"] = "强盛乔里茨的獠牙盔",
        ["Gohr's Devastating Grips"] = "戈尔的毁灭之握",
        ["Tibault's Will"] = "提博尔特之志",
        ["The Grandfather"] = "祖父",
        ["Azurewrath"] = "碧蓝怒火",
        ["Ramaladni's Magnum Opus"] = "拉玛兰迪尼的杰作",
        ["Ring of Starless Skies"] = "无星夜空之戒",
        ["Melted Heart of Selig"] = "塞利格的熔融之心",
        ["Signet of Pelghain"] = "佩尔盖恩印戒",
        ["Seal of the Golden Epiphany"] = "黄金顿悟之印",
        ["Endurant Faith"] = "不灭信仰",
        ["Runic Mail of Heavenly Strength"] = "天堂之力符文胸甲",
        ["Bonebreaker of Channeling"] = "引导碎骨锤",
        ["Runic Cleats of Anger Management"] = "怒气管理符文战靴"
    };

    private static readonly Dictionary<long, string> AspectNames = new()
    {
        [2484503] = "天堂之力威能",
        [2569782] = "战斗狂热威能",
        [2574509] = "引导威能",
        [2620618] = "怒气管理威能"
    };

    private static readonly Dictionary<string, string> RawNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Helm_Unique_Barb_100"] = "强盛乔里茨的獠牙盔",
        ["Gloves_Unique_Barb_001"] = "戈尔的毁灭之握",
        ["Pants_Unique_Generic_102"] = "提博尔特之志",
        ["S14_2HSword_Unique_Generic_001"] = "祖父",
        ["1HSword_Unique_Generic_100"] = "碧蓝怒火",
        ["1HSword_Unique_Barb_101"] = "拉玛兰迪尼的杰作",
        ["Ring_Unique_Generic_105_x2"] = "无星夜空之戒",
        ["S14_Ring_Unique_Generic_104"] = "佩尔盖恩印戒",
        ["S14_Amulet_Unique_Generic_100"] = "塞利格的熔融之心",
        ["Talisman_Seal_MythicUnique_02"] = "黄金顿悟之印"
    };

    public static string Gear(string displayName, string rawId)
    {
        if (GearNames.TryGetValue(displayName, out var translated)) return translated;
        if (RawNames.TryGetValue(rawId, out translated)) return translated;
        if (displayName.Contains(" of Heavenly Strength", StringComparison.OrdinalIgnoreCase)) return displayName.Replace(" of Heavenly Strength", "（天堂之力）", StringComparison.OrdinalIgnoreCase);
        if (displayName.Contains(" of Channeling", StringComparison.OrdinalIgnoreCase)) return displayName.Replace(" of Channeling", "（引导）", StringComparison.OrdinalIgnoreCase);
        if (displayName.Contains(" of Anger Management", StringComparison.OrdinalIgnoreCase)) return displayName.Replace(" of Anger Management", "（怒气管理）", StringComparison.OrdinalIgnoreCase);
        return displayName;
    }

    public static string Aspect(long nid) => AspectNames.TryGetValue(nid, out var name) ? name : $"传奇威能 #{nid}";
}
