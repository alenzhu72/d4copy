using System.Net;
using System.Net.Http;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DiabloBuildAdvisor;

public static class BuildParser
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true, AllowTrailingCommas = true, ReadCommentHandling = JsonCommentHandling.Skip };

    public static BuildProfile Parse(string json, string source = "本地 JSON")
    {
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        var root = Unwrap(doc.RootElement);
        var direct = JsonSerializer.Deserialize<BuildProfile>(root.GetRawText(), Options);
        if (direct is not null && (direct.WeaponDamage > 0 || direct.Gear.Count > 0)) { direct.Source = source; Normalize(direct); return direct; }

        // 宽松解析器：兼容常见 planner 使用的不同命名方式。
        var profile = new BuildProfile { Source = source };
        profile.Name = FindString(root, "name", "buildName", "title") ?? profile.Name;
        profile.Class = FindString(root, "class", "className", "characterClass") ?? profile.Class;
        profile.WeaponDamage = FindNumber(root, "weaponDamage", "weaponDmg", "baseWeaponDamage");
        profile.MainStat = FindNumber(root, "mainStat", "primaryAttribute", "mainAttribute");
        profile.AdditiveDamage = FindNumber(root, "additiveDamage", "additiveSum", "additiveBonus");
        profile.CritChance = AsRatio(FindNumber(root, "critChance", "criticalStrikeChance"));
        profile.CritDamage = AsRatio(FindNumber(root, "critDamage", "criticalStrikeDamage"));
        profile.AttackSpeed = AsRatio(FindNumber(root, "attackSpeed", "attackSpeedBonus"));
        profile.MultiplicativeAspects = (int)FindNumber(root, "multiplicativeAspects", "multiAspectsCount");
        profile.Gear = FindGear(root);
        Normalize(profile);
        if (profile.WeaponDamage == 0 && profile.MainStat == 0 && profile.Gear.Count == 0)
            throw new InvalidDataException("没有识别到装备或角色属性。请导出完整 Build JSON，或参照示例格式。");
        return profile;
    }

    public static BuildProfile ParseMaxroll(string json, int visibleProfileIndex, string source)
    {
        using var envelopeDoc = JsonDocument.Parse(json);
        var envelope = envelopeDoc.RootElement;
        if (!envelope.TryGetProperty("data", out var encoded) || encoded.ValueKind != JsonValueKind.String)
            throw new InvalidDataException("Maxroll 返回的数据格式不完整。");
        using var dataDoc = JsonDocument.Parse(encoded.GetString()!);
        var data = dataDoc.RootElement;
        var profiles = data.GetProperty("profiles").EnumerateArray().Where(x => !x.TryGetProperty("hidden", out var h) || !h.GetBoolean()).ToList();
        if (profiles.Count == 0) throw new InvalidDataException("这个 Maxroll Build 没有可见配置。");
        var selected = profiles[Math.Clamp(visibleProfileIndex, 0, profiles.Count - 1)];
        var result = new BuildProfile
        {
            Name = envelope.TryGetProperty("name", out var n) ? n.GetString() ?? "Maxroll Build" : "Maxroll Build",
            Class = envelope.TryGetProperty("class", out var c) ? c.GetString() ?? "未知职业" : "未知职业",
            Source = source
        };
        if (selected.TryGetProperty("name", out var variant) && !string.IsNullOrWhiteSpace(variant.GetString())) result.Name += " · " + variant.GetString();
        if (selected.TryGetProperty("skillBar", out var skillBar) && skillBar.ValueKind == JsonValueKind.Array)
            result.SkillBar = skillBar.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0).ToList();
        if (selected.TryGetProperty("skillTree", out var skillTree) && skillTree.TryGetProperty("steps", out var skillSteps) && skillSteps.ValueKind == JsonValueKind.Array)
        {
            var skillPosition = skillTree.TryGetProperty("position", out var skillPositionNode) && skillPositionNode.TryGetInt32(out var sp) ? Math.Clamp(sp, 0, Math.Max(0, skillSteps.GetArrayLength() - 1)) : 0;
            foreach (var step in skillSteps.EnumerateArray().Skip(skillPosition).Take(1))
                if (step.TryGetProperty("data", out var allocations) && allocations.ValueKind == JsonValueKind.Object)
                    foreach (var allocation in allocations.EnumerateObject())
                        if (int.TryParse(allocation.Name, out var nodeId) && allocation.Value.TryGetInt32(out var ranks) && ranks > 0)
                            result.Skills.Add(new SkillAllocation { NodeId = nodeId, Ranks = ranks });
        }
        if (selected.TryGetProperty("paragon", out var paragon) && paragon.TryGetProperty("steps", out var paragonSteps) && paragonSteps.ValueKind == JsonValueKind.Array && paragonSteps.GetArrayLength() > 0)
        {
            var selectedPath = paragon.TryGetProperty("position", out var pathPosition) && pathPosition.TryGetInt32(out var pp) ? Math.Clamp(pp, 0, paragonSteps.GetArrayLength() - 1) : 0;
            var path = paragonSteps[selectedPath];
            if (path.TryGetProperty("data", out var boards) && boards.ValueKind == JsonValueKind.Array)
                foreach (var board in boards.EnumerateArray())
                {
                    var plan = new ParagonBoardPlan
                    {
                        BoardId = board.TryGetProperty("id", out var boardId) ? boardId.GetString() ?? "" : "",
                        Rotation = board.TryGetProperty("rotation", out var rotation) && rotation.TryGetInt32(out var r) ? r : 0,
                        Glyph = board.TryGetProperty("glyph", out var glyph) ? glyph.GetString() ?? "" : "",
                        GlyphLevel = board.TryGetProperty("glyphLevel", out var glyphLevel) && glyphLevel.TryGetInt32(out var gl) ? gl : 0
                    };
                    if (board.TryGetProperty("position", out var position)) { plan.PositionX = position.TryGetProperty("x", out var px) ? px.GetInt32() : 0; plan.PositionY = position.TryGetProperty("y", out var py) ? py.GetInt32() : 0; }
                    if (board.TryGetProperty("nodes", out var nodes) && nodes.ValueKind == JsonValueKind.Object) plan.NodeIds = nodes.EnumerateObject().Where(x => x.Value.ValueKind == JsonValueKind.Number && x.Value.GetInt32() > 0).Select(x => int.Parse(x.Name)).ToList();
                    result.ParagonBoards.Add(plan);
                }
        }
        if (!selected.TryGetProperty("items", out var equipped) || !data.TryGetProperty("items", out var itemPool)) return result;

        var displayNames = new List<string>();
        if (envelope.TryGetProperty("search_metadata", out var metadata) && metadata.TryGetProperty("items", out var names))
            displayNames = names.EnumerateArray().Select(x => x.GetString() ?? "").ToList();
        var nameIndex = 0;
        foreach (var slotEntry in equipped.EnumerateObject())
        {
            var itemKey = slotEntry.Value.ToString();
            if (!itemPool.TryGetProperty(itemKey, out var item)) continue;
            var rawId = item.TryGetProperty("id", out var id) ? id.GetString() ?? "装备" : "装备";
            var sourceName = nameIndex < displayNames.Count && !string.IsNullOrWhiteSpace(displayNames[nameIndex])
                ? displayNames[nameIndex]
                : item.TryGetProperty("name", out var customName) && !string.IsNullOrWhiteSpace(customName.GetString()) ? customName.GetString()! : HumanizeId(rawId);
            var aspectName = "—";
            if (item.TryGetProperty("aspects", out var aspects) && aspects.ValueKind == JsonValueKind.Array && aspects.GetArrayLength() > 0)
            {
                var firstAspect = aspects[0];
                if (firstAspect.TryGetProperty("nid", out var aspectId) && aspectId.TryGetInt64(out var nid)) aspectName = ChineseLocalization.Aspect(nid);
            }
            var gear = new GearItem
            {
                Slot = MaxrollSlotName(slotEntry.Name),
                Name = ChineseLocalization.Gear(sourceName, rawId),
                Aspect = aspectName
            };
            nameIndex++;
            foreach (var groupName in new[] { "explicits", "tempered" })
                if (item.TryGetProperty(groupName, out var affixes) && affixes.ValueKind == JsonValueKind.Array)
                    foreach (var affix in affixes.EnumerateArray())
                    {
                        var nid = affix.TryGetProperty("nid", out var nidNode) ? nidNode.ToString() : "未知";
                        var value = affix.TryGetProperty("values", out var values) && values.ValueKind == JsonValueKind.Array && values.GetArrayLength() > 0 && values[0].TryGetDouble(out var v) ? v : 0;
                        gear.Affixes.Add(new Affix { Name = (groupName == "tempered" ? "精铸词条 " : "词条 ") + nid, Value = value, Unit = "" });
                    }
            result.Gear.Add(gear);
            if (item.TryGetProperty("aspects", out var aspectArray) && aspectArray.ValueKind == JsonValueKind.Array) result.MultiplicativeAspects += aspectArray.GetArrayLength();
        }
        return result;
    }

    private static string MaxrollSlotName(string id) => id switch
    {
        "4" => "头盔", "5" => "胸甲", "8" => "双手钝器", "9" => "双手劈砍", "11" => "主手", "12" => "副手",
        "13" => "手套", "14" => "裤子", "15" => "靴子", "16" => "戒指 1", "17" => "戒指 2", "18" => "护符",
        "20" => "印记", "21" => "护符石 1", "22" => "护符石 2", "23" => "护符石 3", "24" => "护符石 4", "25" => "护符石 5", "26" => "护符石 6", _ => "装备位 " + id
    };
    private static string HumanizeId(string id) => string.Join(' ', id.Split('_', StringSplitOptions.RemoveEmptyEntries).Where(x => !new[] { "Unique", "Legendary", "Generic" }.Contains(x, StringComparer.OrdinalIgnoreCase)));

    private static JsonElement Unwrap(JsonElement e)
    {
        foreach (var key in new[] { "build", "profile", "data", "planner" })
            if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty(key, out var inner) && inner.ValueKind == JsonValueKind.Object) return inner;
        return e;
    }
    private static void Normalize(BuildProfile p)
    {
        p.CritChance = AsRatio(p.CritChance); p.CritDamage = AsRatio(p.CritDamage); p.AttackSpeed = AsRatio(p.AttackSpeed);
        p.Gear ??= []; foreach (var g in p.Gear) { g.Affixes ??= []; g.Aspect ??= "—"; }
    }
    private static double AsRatio(double n) => n > 2 ? n / 100d : n;
    private static string? FindString(JsonElement root, params string[] names)
    { foreach (var p in Walk(root)) if (names.Any(n => p.Name.Equals(n, StringComparison.OrdinalIgnoreCase)) && p.Value.ValueKind == JsonValueKind.String) return p.Value.GetString(); return null; }
    private static double FindNumber(JsonElement root, params string[] names)
    { foreach (var p in Walk(root)) if (names.Any(n => p.Name.Equals(n, StringComparison.OrdinalIgnoreCase))) { if (p.Value.TryGetDouble(out var n)) return n; if (p.Value.ValueKind == JsonValueKind.String && double.TryParse(Regex.Replace(p.Value.GetString()!, "[^0-9.-]", ""), out n)) return n; } return 0; }
    private static IEnumerable<JsonProperty> Walk(JsonElement e)
    { if (e.ValueKind == JsonValueKind.Object) foreach (var p in e.EnumerateObject()) { yield return p; foreach (var x in Walk(p.Value)) yield return x; } else if (e.ValueKind == JsonValueKind.Array) foreach (var v in e.EnumerateArray()) foreach (var x in Walk(v)) yield return x; }
    private static List<GearItem> FindGear(JsonElement root)
    {
        foreach (var p in Walk(root).Where(p => new[] { "gear", "equipment", "items" }.Contains(p.Name, StringComparer.OrdinalIgnoreCase)))
            if (p.Value.ValueKind == JsonValueKind.Array) try { return JsonSerializer.Deserialize<List<GearItem>>(p.Value.GetRawText(), Options) ?? []; } catch { }
        return [];
    }
}

public static class SuggestionEngine
{
    public static List<Suggestion> Generate(BuildProfile p)
    {
        var result = new List<Suggestion>();
        if (p.AdditiveDamage > 500) { var gain = 50d / (1 + p.AdditiveDamage / 100d); result.Add(Warn("加法词条边际效应严重", $"当前加法伤害 +{p.AdditiveDamage:0}%。再增加 50% 仅带来约 {gain:0.0}% 实际提升，优先考虑攻速、暴击或 [x] 独立乘区。")); }
        if (p.CritChance > .5 && p.CritDamage < 1.2) result.Add(Info("暴击伤害偏低", $"暴击率已达 {p.CritChance:P0}，但暴击伤害仅 {p.CritDamage:P0}。戒指或武器上的暴击伤害通常有更高边际收益。"));
        if (p.MainStat > 0 && p.MainStat < 1000) result.Add(Warn("主属性不足", "主属性会提高技能伤害，也影响巅峰节点门槛。建议优先检查胸甲、武器和全属性词条。"));
        if (p.AttackSpeed > 0 && p.AttackSpeed < .15) result.Add(Info("攻速提升空间", $"当前攻速加成为 {p.AttackSpeed:P0}。若流派依赖触发频率或资源循环，可将一条低收益加法词条换为攻速。"));
        if (p.Gear.Count > 0 && p.MultiplicativeAspects < 3) result.Add(Warn("独立增伤威能较少", $"只识别到 {p.MultiplicativeAspects} 个传奇威能。请检查关键乘算威能是否遗漏或放在低倍率槽位。"));
        if (result.Count == 0) result.Add(new() { Severity = "良好", Icon = "✓", Title = "核心属性分配健康", Message = "暂未发现明显失衡。建议导入目标 Build，进一步逐部位对比词条与威能数值。", Accent = "#62C48D" });
        return result;
    }
    public static List<GearComparison> Compare(BuildProfile current, BuildProfile target)
    {
        var slots = current.Gear.Select(x => x.Slot).Union(target.Gear.Select(x => x.Slot), StringComparer.OrdinalIgnoreCase);
        return slots.Select(slot => { var a = current.Gear.FirstOrDefault(x => x.Slot.Equals(slot, StringComparison.OrdinalIgnoreCase)); var b = target.Gear.FirstOrDefault(x => x.Slot.Equals(slot, StringComparison.OrdinalIgnoreCase)); var missing = b?.Affixes.Where(t => a?.Affixes.All(x => !x.Name.Equals(t.Name, StringComparison.OrdinalIgnoreCase)) != false).Select(x => x.Name).ToList() ?? []; return new GearComparison { Slot = slot, Current = a?.Name ?? "未装备", Target = b?.Name ?? "—", Status = a is null ? "缺失" : missing.Count > 0 ? "可优化" : "达标", Detail = missing.Count > 0 ? "缺少：" + string.Join("、", missing.Take(2)) : (a?.Aspect == b?.Aspect ? "威能与核心词条匹配" : "检查威能：" + (b?.Aspect ?? "—")) }; }).ToList();
    }
    private static Suggestion Warn(string t, string m) => new() { Severity = "优先处理", Icon = "!", Title = t, Message = m, Accent = "#E06C5D" };
    private static Suggestion Info(string t, string m) => new() { Severity = "优化建议", Icon = "↗", Title = t, Message = m, Accent = "#D8A84E" };
}

public sealed class LinkImporter
{
    private readonly HttpClient _http = new(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All }) { Timeout = TimeSpan.FromSeconds(15) };
    public async Task<BuildProfile> ImportAsync(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")) throw new ArgumentException("请输入有效的 https 分享链接。");
        if (!new[] { "maxroll.gg", "mobalytics.gg", "d4builds.gg" }.Any(d => uri.Host.Equals(d, StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith('.' + d, StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("目前链接导入仅允许 Maxroll、Mobalytics 和 D4Builds 域名。");
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 DiabloBuildAdvisor/1.1");
        if (uri.Host.EndsWith("maxroll.gg", StringComparison.OrdinalIgnoreCase) && uri.AbsolutePath.StartsWith("/d4/planner/", StringComparison.OrdinalIgnoreCase))
        {
            var plannerId = uri.AbsolutePath["/d4/planner/".Length..].Trim('/');
            if (string.IsNullOrWhiteSpace(plannerId) || !Regex.IsMatch(plannerId, "^[a-zA-Z0-9]+$")) throw new ArgumentException("Maxroll Planner 分享 ID 无效。");
            var visibleIndex = int.TryParse(uri.Fragment.TrimStart('#'), out var selected) ? Math.Max(0, selected - 1) : 0;
            var apiJson = await _http.GetStringAsync("https://planners.maxroll.gg/profiles/d4/" + plannerId);
            return BuildParser.ParseMaxroll(apiJson, visibleIndex, "Maxroll");
        }
        var html = await _http.GetStringAsync(uri);
        foreach (var pattern in new[] { "<script[^>]+id=[\"']__NEXT_DATA__[\"'][^>]*>(.*?)</script>", "<script[^>]+type=[\"']application/ld\\+json[\"'][^>]*>(.*?)</script>" })
            foreach (Match m in Regex.Matches(html, pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase)) try { return BuildParser.Parse(WebUtility.HtmlDecode(m.Groups[1].Value), uri.Host); } catch { }
        throw new InvalidDataException("页面可以访问，但未找到可识别的 Build 数据。该网站可能更新了页面结构；请改用其 JSON 导出文件。");
    }
}
