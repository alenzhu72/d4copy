using DiabloBuildAdvisor;

var jsonPath = args.Length > 0 ? args[0] : throw new ArgumentException("需要 Maxroll API JSON 路径");
var profile = BuildParser.ParseMaxroll(await File.ReadAllTextAsync(jsonPath), 0, "Maxroll");
if (profile.Name != "Whirlwind Barbarian · Hardcore ww barb") throw new Exception("Build 名称解析失败：" + profile.Name);
if (profile.Class != "Barbarian") throw new Exception("职业解析失败：" + profile.Class);
if (profile.Gear.Count != 19) throw new Exception($"装备数量应为 19，实际为 {profile.Gear.Count}");
if (profile.Skills.Count != 24) throw new Exception($"技能节点数量应为 24，实际为 {profile.Skills.Count}");
if (profile.SkillBar.Count != 6) throw new Exception($"技能栏数量应为 6，实际为 {profile.SkillBar.Count}");
if (profile.ParagonBoards.Count != 5) throw new Exception($"巅峰盘数量应为 5，实际为 {profile.ParagonBoards.Count}");
var catalog = new NodeCatalogService();
var skillMarkers = catalog.ResolveSkills(profile);
if (skillMarkers.Count != 24) throw new Exception($"技能红点应为 24，实际为 {skillMarkers.Count}");
var paragonMarkers = catalog.ResolveParagon(profile.ParagonBoards[0]);
if (paragonMarkers.Count != profile.ParagonBoards[0].NodeIds.Count) throw new Exception("巅峰红点坐标解析不完整");
if (profile.Gear[0].Slot != "头盔" || profile.Gear[0].Name != "强盛乔里茨的獠牙盔") throw new Exception("头盔中文名解析失败");
if (profile.Gear.First(x => x.Slot == "胸甲").Aspect != "天堂之力威能") throw new Exception("威能中文名解析失败");
Console.WriteLine($"PASS: {profile.Name} / {profile.Class} / {profile.Gear.Count} 件装备");
