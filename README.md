# 庇护所配装助手

Windows 原生的《暗黑破坏神 IV》Build 导入、属性诊断与目标配装对比工具。

## 运行

```powershell
dotnet run --project .\DiabloBuildAdvisor\DiabloBuildAdvisor.csproj
```

首次体验可导入 `DiabloBuildAdvisor/sample-build.json`。发布单文件程序：

```powershell
dotnet publish .\DiabloBuildAdvisor\DiabloBuildAdvisor.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## JSON 格式

解析器支持示例中的规范格式，也会递归识别 `weaponDmg`、`mainAttribute`、`additiveSum`、`criticalStrikeChance` 等常见别名。外部网站页面结构会变化，因此网页链接导入属于尽力解析；网站改版时请优先使用 JSON 导出。

## 说明

建议引擎基于边际收益和规则诊断，不替代游戏内实测。赛季机制、职业被动和技能条件会影响最终伤害，后续可在 `SuggestionEngine` 中按职业和赛季扩展规则。
