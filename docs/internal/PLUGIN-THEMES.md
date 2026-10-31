# 主题机制内部说明（**仅内部使用，不随社区文档发行**）

> 适用版本：`27.1-snapshot-60(2.15.1)` 及以后 · 契约 API 版本 **1**
>
> **阅读约束**：本文记录的是**主题相关**的插件接口与实现细节。
> 按用户要求（2026-10-31）：主题 API 可以对社区开放（技术上可用、插件能用），
> 但**对外发行的 API 文档（`docs/PLUGIN-API.md` / `docs/PLUGIN-GUIDE.md`）不得提及任何主题相关内容**。
> 因此主题机制只写在这份内部文档与 `HANDOVER.md` / `DOWNSTREAM-CHANGES.md` 里。
> 删改 `PLUGIN-API.md` 时**不要**把主题接口加回去。

---

## 1. 两套「主题插件」接口，别混

| 接口 | 槽位 | 能力键 | 能做的事 |
| --- | --- | --- | --- |
| `IExtraThemesProvider.GetThemes()` | 必须是宿主**内置**槽位（`ColorTheme` 枚举名） | `themes.extended`（功能键 `provides`） | 给内置槽位换**名字 / 主色 / 配色风格**；不能凭空加主题 |
| `IPluginCustomThemesProvider.GetCustomThemes()` | 插件自定义字符串 id | `themes.custom`（能力） | **凭空定义**任意多个主题，可带自己的标题栏装饰 |

- 内置槽位白名单（`PluginThemeDefinition.Key`，区分大小写）：
  `SwordsmanYellow`、`SakuraPink`、`NetherRed`、`MethylOrange`、`AmethystPurple`、`MikuGreen`、
  `KaoYinGreen`、`TianyiBlue`、`MermanBlue`、`IchikaBlue`。
- 插件自带主题全部落在 `ColorTheme.PluginDefined = 16` 这一个槽位上，
  具体是哪个由配置的 `UiLightPluginTheme` / `UiDarkPluginTheme`（字符串 id）决定。

## 2. 插件自带主题（`IPluginCustomThemesProvider`）

```csharp
public sealed class PluginCustomThemeDefinition
{
    public string Id { get; set; }                                        // 全局唯一，建议 插件id.主题名
    public string DisplayName { get; set; }                               // 主题下拉框显示名
    public string Hex { get; set; }                                       // 主色 #RRGGBB
    public string? Description { get; set; }
    public string? Flavor { get; set; }                                   // golden-dusk / sakura-bloom，可空
    public int SortOrder { get; set; } = 100;
    public Func<FrameworkElement>? CreateTitleDecoration { get; set; }    // 标题栏装饰，可空
}
```

宿主侧链路：

| 环节 | 位置 | 说明 |
| --- | --- | --- |
| 收集 | `PluginHostBootstrap.RefreshCustomThemes(manager)` | 能力门禁 `themes.custom`；id 重复只留第一个；异常只记日志 |
| 注册 | `ThemeService.SetPluginThemes(...)` | 三张表：hex / 显示名 / flavor + 一份有序列表 |
| 取主色 | `ThemeService._ThemeHex(ColorTheme.PluginDefined, …)` | 按 `CurrentPluginThemeId` 查表；查不到用兜底色 |
| 取风格 | `ThemeService._ThemeFlavor(...)` | 同上有 flavor 时走专属 ramp，否则按档位加权 |
| 可选性 | `ThemeService.IsThemeSelectable(PluginDefined)` | 有任意插件主题即可选（不受 `themes.extended` 限制） |
| 下拉框 | `PageSetupUI.ThemeChoices` | `ThemeChoice(Theme, PluginThemeId)` 列表；选插件主题时同时写槽位与 id |
| 校验 | `ModSetup.ValidateThemeSelection()` | 插件主题被卸载 → 回退龙猫蓝并清空 id |
| 标题栏 | `FormMain.RefreshPluginThemeDecoration()` | 按 id 取 `CreateTitleDecoration`，创建一次后缓存，切走即断开引用 |

### 2.1 标题栏装饰约定

- 宿主把插件给的控件放进 `PanTitlePluginDecoration`（`ContentPresenter`，右侧、`IsHitTestVisible=False`）；
- **尺寸 / 对齐 / 透明度全部由插件自己定**（宿主不做任何修饰），宿主只保证「跟随主题显隐」「切换主题重建」；
- 建议叠 `OpacityMask` 做左右渐隐、加轻微 `BlurEffect` 做柔化（`FadedGray` 插件即这么写）；
- 装饰图别放插件包外部文件，直接 `EmbeddedResource` 编进 DLL，用 `Assembly.GetManifestResourceStream` 读 ——
  这样插件包只需 `dll + plugin.json`，也绕开宿主单文件发布的 pack URI 问题。

### 2.2 现成的例子

| 插件 | 主题 | 说明 |
| --- | --- | --- |
| `plugins/SguPack`（`IExtraThemesProvider`） | 剑客黄 / 樱花粉 / 下界红 / 甲基橙 / 水晶紫 / 初音绿 / 烤音绿 | 占内置槽位，只给数据 |
| `plugins/FadedGray`（`IPluginCustomThemesProvider`） | `Faded灰`（`#5C5C5C`，id `faded.gray`） | **插件自带主题** + 内嵌 `Faded.jpg` 标题栏装饰（左右渐隐 + 轻模糊），能力 `["config","themes.custom"]` |

## 3. 宿主内置主题槽位（供对照）

`ColorTheme`（`PCL.Core/App/ConfigEnums.cs`）：
`SkyBlue=0`、`CatBlue=1`、`DeathBlue=2`、`HmclBlue=3`、`Custom=4`、`FatFishBlue=5`、`TianyiBlue=6`、
`SwordsmanYellow=7`、`MikuGreen=8`、`MermanBlue=9`、`KaoYinGreen=10`、`IchikaBlue=11`、
`SakuraPink=12`、`NetherRed=13`、`MethylOrange=14`、`AmethystPurple=15`、**`PluginDefined=16`**。

配色分派（`ThemeService._CalculateColors`）：

```
剑客黄 / golden-dusk flavor      → _CalculateColorsGoldenDusk（黄昏金渐变）
樱花粉 / sakura-bloom flavor     → _CalculateColorsSakuraBloom（樱粉 → 梅紫）
其余十六进制主题（含 PluginDefined）→ _CalculateColorsFromHex（按档位加权，主色精确等于指定色）
上游预设主题                      → _CalculateColorsDefault
```

**新增主题槽位时必查**：`GetCurrentThemeArgs()` 的 switch（少一个 case 会在 Release 下抛
`IndexOutOfRangeException`）、`IsHexTheme()`、`IsBlueTheme()`、`PageSetupUI.ThemeColors` 的显示名 case。

## 4. 与「对外文档」的隔离检查

- `docs/PLUGIN-API.md`、`docs/PLUGIN-GUIDE.md` 里**不得**出现 `theme` / `Themes` / `主题` 字样
  （包括 `ThemeChangedEvent` 事件名——它属于主题相关内容，只在本文件和 `HANDOVER.md` 里写）。
  自检：`grep -in "theme\|主题" docs/PLUGIN-API.md docs/PLUGIN-GUIDE.md` 应该没有输出。
- 主题相关的类型（`PluginThemeDefinition`、`PluginCustomThemeDefinition`、
  `IExtraThemesProvider`、`IPluginCustomThemesProvider`、`PluginCapability.ThemesCustom`）
  在 `PCL.PluginSdk` 里是**正常公开的**（编译期不给社区设障），只是不在对外文档里介绍。
