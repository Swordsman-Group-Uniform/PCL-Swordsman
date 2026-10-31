# PCLCE For SGU 维护交接文档

> 交接时间：2026-10-25　交接版本：**27.1-snapshot-8(2.15.1)**（内部版本代号 `531`）
> 适用范围：接手 PCLCE For SGU 下游分支日常维护、功能增改与发版的人员
> **最近一次交接的时间点快照：见 [`docs/HANDOVER-2026-10-25.md`](HANDOVER-2026-10-25.md)**（品牌改名、占位页、遥测禁用、插件系统 M0/M1 都在那一份里）
> 相关文档：[`README-FIRST.md`](../README-FIRST.md)（一页导览）、[`VERSIONING.md`](../VERSIONING.md)（版本号规范）、[`DOWNSTREAM-CHANGES.md`](../DOWNSTREAM-CHANGES.md)（逐版改动记录）、[`docs/PLUGIN-SYSTEM.md`](PLUGIN-SYSTEM.md)（插件系统设计）、[`docs/FIRST-RUN-DIALOGS.md`](FIRST-RUN-DIALOGS.md)（首次启动弹窗内容 dump）

---

## 1. 项目定位（先读这段）

**PCLCE For SGU** 是 [PCL CE](https://github.com/PCL-Community/PCL2-CE)（Plain Craft Launcher Community Edition）的**下游分支**，由剑客群组服服主团队联合维护，仅供 SGU 及其相关服务器（VFH 术友汇、HCE 圣苦力怕帝国）成员内部分享使用。

必须遵守的几条底线：

1. **非官方**：本作不是 PCL Community 或 PCL 原作者龙腾猫跃的官方发布版本，界面上必须持续保留"下游项目声明"（已实现于「设置 → 软件信息」顶部卡片与「设置 → 反馈」页面）。
2. **反馈不收**：本分支**已禁用反馈功能**，任何问题都不得引导用户去 PCL Community / PCL2-CE 仓库提 issue。反馈渠道为 `hmjmfabc@swordsman.top`、`mingyugames@swordsman.top`。
3. **版权**：上游代码与素材版权归 PCL Community 与龙腾猫跃；本作按上游许可（仓库根 `LICENSE`，Apache License 2.0）分发，保留全部上游版权与许可声明。
4. **主题色**：PCL 的主题色是原作者龙腾猫跃的主要赞助项目，因此**发行版中不得保留非蓝色主题与自定义主题色**（代码已强制，见 §5.3）。

---

## 2. 当前状态速览

| 项目 | 值 |
| --- | --- |
| 下游版本号 | `27.1-snapshot-8`（`metadata.json` 的 `base` + `suffix`） |
| 完整展示版本号 | `27.1-snapshot-8(2.15.1)`（括号内为已同步的上游版本） |
| 已同步上游版本 | PCL CE `2.15.1`（实际代码基点见 §4.1：上游 dev `d1149d5`，即 2.15.1 发布后的 dev 快照） |
| 内部版本代号 | `531`（`version.code`，用于升级/降级判断） |
| 目标框架 | `net10.0-windows`（WPF，`EnableWindowsTargeting`） |
| 构建产物 | `PCLCE For SGU 27.1-snapshot-8(2.15.1).exe`（win-x64 单文件，约 23.7 MB，依赖 .NET 10 桌面运行时） |
| 源码基线 | 上游 PCL CE `dev` @ `d1149d5`（2026-10-24）的完整源码快照（无 `.git` 历史） |

---

## 3. 构建与发版

### 3.1 本地构建（唯一推荐入口）

```bash
./build.sh                # Release + x64（默认）
./build.sh ARM64          # Release + win-arm64
./build.sh x64 Debug      # 指定配置
```

`build.sh` 会：读取 `metadata.json` 拼出版本号 → `dotnet publish` 到 `artifact-<平台>/`（当前为 `artifact-x64/`）
→ 按规范重命名为 `PCLCE For SGU [版本号].exe`。

> 注意：脚本首行是 `#!/bin/sh`，会自行 `exec bash`。因为 Android/Termux 没有 `/usr/bin/env`，
> 不要改回 `#!/usr/bin/env bash`。

### 3.2 CI 构建

`.github/workflows/reusable-build.yml` 已加入同名重命名步骤，产物同样为
`PCLCE For SGU <版本号>.exe`。CI 会从 secrets 注入微软登录、CurseForge、Sentry、Naid、
联机根服务器等密钥；**本分支已不依赖这些密钥也能构建运行**（缺密钥时会自动降级，见 §5.2）。

### 3.3 构建环境备注

原维护者环境：Android Termux（aarch64）+ `pkg install dotnet-sdk-10.0`（SDK 10.0.113，`linux-bionic-arm64`），
WPF 交叉构建通过 `EnableWindowsTargeting=true` 实现，可正常产出 Windows 可执行文件。

Windows 本机（2026-10 核验，可用）：

- .NET SDK **10.0.402**（win-x64），`x64 Release` 单文件产物构建正常（0 错误）
- `build.sh` 依赖 `bash` + `python3`，Windows 上两者可能都没有：
  可直接用仓库自带的等价脚本 `tools\build.ps1`（读取同一个 `metadata.json`，
  `dotnet publish` 后按规范重命名产物），无需改 `build.sh`
  ⚠️ 该脚本必须保持 **UTF-8 with BOM** 保存：Windows PowerShell 5.1 会把无 BOM 的 UTF-8
  按系统代码页（GBK）解码，中文注释末尾的多字节序列可能吞掉换行，导致 `param` 块解析失败
- 网络受限时可把 NuGet 依赖放到本地目录并加 `-p:RestoreSources=<dir>` 离线还原
- 命令建议加 `-m:1 -p:UseSharedCompilation=false` 限制并行（脚本已内置），dotnet 进程内存峰值约 1.5 GB
- **构建只能验证编译期**：UI 实际观感、P2P 联机连通性等必须在 Windows 实机测试

### 3.4 发版前检查清单

1. `dotnet build`（或 `./build.sh ... Debug`）0 错误
2. 全部语言文件 XML 可解析（当前 7 个）：`python3 -c "import xml.etree.ElementTree as ET,glob;[ET.parse(p) for p in glob.glob('PCL.Core/App/Localization/Languages/*.xaml')]"`
   （Windows 无 python 时可用 PowerShell：`Get-ChildItem *.xaml | ForEach-Object { [xml]$x = Get-Content $_ }`）
3. `metadata.json` 的 `suffix` / `code` 已更新（见 §4）
4. 产物文件名符合规范，且 exe 内 `"upstream"` / `"suffix"` / `"code"` 字符串正确
   （单文件产物里 `metadata.json` 以明文 UTF-8 内嵌，可直接字符串搜索核对）
5. 若改动涉及界面，在 Windows 上过一遍：启动页三选项卡、设置各页、联机页、百宝箱回声洞

---

## 4. 版本号与同步上游

规范详见 `VERSIONING.md`，核心三条：

1. 完整版本号 = `年份.大更新.小更新(上游版本号)`，例如 `27.1-snapshot-8(2.15.1)`；
   实验/快照构建在中间加 `-snapshot-N` / `-experiment-N` 后缀（对应 `metadata.json` 的 `suffix`）。
2. **同步上游时下游版本号不变，只改 `upstream`**（并在必要时调 `code`）。
3. 只有下游自己加功能/修 bug 才递增外层版本号。

### 4.1 ⚠️ 先确认「真实代码基点」（2026-10 修订，务必先做）

踩过的坑：此前本仓库文档长期写着「源码基线 PCL CE `2.12.1`」。这个数字取自**上游 `metadata.json` 的
`upstream` 字段，但上游该字段的含义是「本作继承自原版 PCL2 的版本号」，从 fork 起固定为 `2.12.1`，
不随上游版本推进**（上游 `2.15.1` 与 `2.15.1-beta.1` 的 metadata 里它都是 `2.12.1`，变化的是 `base`）。

用三方文件比对核实后，本仓库的真实基点是**上游 `dev` @ `d1149d5`（2026-10-24）**，
即 2.15.1 正式版（2026-10-20，`code` 524）之后的 dev 快照：该快照与本仓库除 65 处下游改动外**逐字节相同**。

由此得到两条经验：

- 判断「已同步到哪个上游版本」**不要看上游 metadata 的 `upstream` 字段**，要做文件比对（见 §4.2）；
- 上游 tag `v2.15.1-beta.1`（2026-09-04，`code` 523）比本仓库当前的代码**更旧**，
  若按字面「同步到 beta.1」会让 73 个纯上游文件回退（丢掉 2.15.1 正式版的修复）。

本轮（`27.1-snapshot-8`）据此**只更正版本号标注、不回退/合并任何上游代码**：
把 `metadata.json` 的 `upstream` 由 `2.12.1` 改为 `2.15.1`，`suffix` → `snapshot-8`，`code` → `531`。

### 4.2 同步上游的操作步骤

本机没有 `git` 时的做法（Windows 上也适用）：把三份源码树并排放置后逐文件比对。

| 角色 | 含义 |
| --- | --- |
| `base` | 当前已知基点对应的上游 tag 源码（本例 `dev` @ `d1149d5`，可用 codeload 拉 `tar.gz`） |
| `ours` | 本工作区 |
| `theirs` | 目标上游 tag 源码 |

```powershell
# 1) 取上游源码（本机 schannel 不可用，走 node 的 TLS 栈）
node --use-system-ca _tools\gh.mjs api "/repos/PCL-Community/PCL-CE/contents/PCL.Core?ref=<tag>"   # 确认 PCL.Core 是否为 submodule 及其 commit
node --use-system-ca _tools\gh.mjs download "https://codeload.github.com/PCL-Community/PCL-CE/tar.gz/refs/tags/<tag>" "_downloads\PCL-CE-<tag>.tar.gz"
tar -xzf "_downloads\PCL-CE-<tag>.tar.gz" -C "_upstream\<tag>" --strip-components=1
```

比对要求（按文件 SHA-256 分类，排除 `bin/` `obj/` `HMCL-main/`）：

1. `ours == base` → 上游单侧改动，可直接取 `theirs`；
2. `ours != base` 且 `theirs == base` → 下游单侧改动，保留 `ours`；
3. 三者互不相同 → **冲突文件**，以 `theirs` 为底重新叠加下游改动（优先保留下游功能意图：
   禁用更新/反馈、发行版主题门禁、三选项卡登录、陶瓦联机页，同时吸收上游逻辑修正）。

> 注意 2.12.1 及更早的 tag 里 `PCL.Core` 是 **git submodule**（`tar.gz` 里是空目录），
> 需要再用 `contents/PCL.Core?ref=<tag>` 拿到 submodule commit 并单独下载；
> 而 2.14+ 的 tag 已把 `PCL.Core` 内联进主仓库。

改完后的收尾步骤：

1. 更新 `Plain Craft Launcher 2/metadata.json`：`upstream` → 新上游版本；`code` 递增；`base`/`suffix` 视情况
2. 复查本地化键（§6）：上游新增/删除的键、被下游改写过的键
3. 重新构建，按 §3.4 检查

---

## 5. 下游改动全景（相比上游 PCL CE）

> 完整逐版记录见 `DOWNSTREAM-CHANGES.md`；本节是**按主题的功能索引**，方便判断"这段代码是我们改的还是上游的"。

### 5.1 移除的官方限制

| 内容 | 关键位置 |
| --- | --- |
| 中国大陆区域限制（下载/补全/整合包安装） | 删除 `PCL.Core/App/Region/RegionPolicy.cs`、`Plain Craft Launcher 2/Modules/Minecraft/RestrictedFeaturePolicy.cs`、`PCL.Core.Test/RegionPolicyTest.cs`；移除 `ModLaunch` / `ModModpack` / `ModDownloadLib` / `PageDownloadInstall` / `PageInstanceInstall` / `PageInstanceOverall` / `PageLaunchLeft` 等 13 处调用；删除 `Lang.IsFeaturesUnrestricted` 与配置项 `SystemDebugAllowRestrictedFeature` |
| "新建离线/外置账号必须先绑定正版账号" | `ProfileUi.CanCreateOtherProfile()` 恒 `true`；删除 `PageLoginProfile` 的 `HintMicrosoft` 提示 |
| 启动前"必须登录正版账号才能启动游戏"弹窗 | `ModLaunch` 中整块正版购买/账号验证提示已删除 |

### 5.2 联网功能裁剪

| 开关 | 位置 | 含义 |
| --- | --- | --- |
| `UpdateManager.IsUpdateSupported = false` | `Plain Craft Launcher 2/Modules/Updates/UpdateManager.cs` | 关闭全部更新/自动更新/更新检测；更新服务器域名统一为 `http://127.0.0.1/`；`AnnouncementService`（云端公告＝云控）一并短路 |
| `ModBase.IsFeedbackSupported = false` | `Plain Craft Launcher 2/Modules/Base/ModBase.cs` | 关闭反馈：`CanFeedback`/`Feedback`/`_ShowFeedbackPrompt`/`PageSetupLeft.TryFeedback` 全部短路，错误弹窗不再引导去上游 |
| `LobbyController.IsTelemetryEnabled = false` | `PCL.Core/Link/Lobby/LobbyController.cs` | 不再向 `pcl2ce.pysio.online` 上报联机数据 |
| `TelemetryService.IsTelemetrySupported = false` | `PCL.Core/App/Essentials/TelemetryService.cs` | **完全禁用遥测**：Sentry 不初始化、`ReportException` 直接返回、设备环境调查不发送；启动时不再弹遥测询问框（`FormMain` 里 `!IsTelemetrySupported` 分支直接把 `SystemTelemetry` 压回 `false`）；设置页的遥测勾选框不可用且固定为未勾选 |

设置页「软件更新」与「反馈」两页改为**整页占位**：隐藏原有全部控件，只显示居中的
`MyUnavailablePlaceholder`（红色带叉箭头 + 红字），分别展示"此版本不支持更新"与下游反馈声明。
详见 §5.4。

**TerraCotta（陶瓦联机）**：上游的"联机大厅"依赖 PCL 服务器下发公告/可用性/中继，本分支已改为**不依赖任何 PCL 服务器**：

- `PCL.Core/Link/Lobby/LobbyInfoProvider.cs`：可用性/登录要求改为本地静态默认（可用、免登录、允许自定义名）；`GetUsername()` 不再依赖 Natayark ID
- `PCL.Core/Link/Lobby/LobbyService.cs`：跳过 Naid 登录与令牌刷新、跳过"必须先登录"校验
- `PCL.Core/Link/Scaffolding/EasyTier/EasyTierEntity.cs`：只连 **EasyTier 官方公共节点**（`public.easytier.top` 等），不再使用 PCL 下发的中继；`Config.Link.TerraCottaRelayMode == 0` 时纯 P2P 不连任何中继
- `Plain Craft Launcher 2/Modules/ModLink.cs`：`LobbyPrecheck()` 去掉大厅可用性与登录校验
- 房间号由 `PCL.Core/Link/Scaffolding/LobbyCodeGenerator.Generate()` **本地生成**，格式 `U/NNNN-XXXX-YYYY-ZZZZ`
- `PCL.Core/App/Secrets.cs`：`LinkServers` 内置官方公告镜像回退（当前代码已不依赖，保留兜底）

页面 `Plain Craft Launcher 2/Pages/PageTools/PageToolsGameLink.xaml(.cs)` 已**整体重写**为「陶瓦联机」控制页：
总开关 → 联机内核（EasyTier）状态与下载 → 显示名称/连接方式 → 房主（扫描局域网世界/手动端口/建房）→
房客（房间号加入）→ 当前房间（房间号、复制、玩家列表、退出）→ **房主教程 / 房客教程**（均可折叠）。

### 5.3 主题系统（重点，易踩坑）

- `ColorTheme` 枚举（`PCL.Core/App/ConfigEnums.cs`）新增：`Custom=4`、`FatFishBlue=5`(#5A67AC)、
  `TianyiBlue=6`(#66CCFF)、`SwordsmanYellow=7`(#E8B33C)、`MikuGreen=8`(#39C5BB)、
  `KaoYinGreen=10`(#33CCBB)、`IchikaBlue=11`(#33AAEE)、`MermanBlue=9`(#3366CC)
- `PCL.Core/UI/Theme/ThemeService.cs`
  - `GetCurrentThemeArgs()` 返回 `(double Hue, double LightAdjust, double ChromaAdjust)`（**色相是 double，别改回 int**，
    否则自定义色会有 ±1 偏差）
  - 新增主题通过 `_GetThemeArgsFromHex("#RRGGBB", fallback)` 把颜色换算到 OKLCH 反解参数，保证主色 `Color3` 精确等于指定色
  - `_CalculateColors()` 会分派：剑客黄走 `_CalculateColorsGoldenDusk()`（浅金→金→琥珀→暮色橙的逐档色相渐变），
    其余主题走 `_CalculateColorsDefault()`（与上游完全一致）
  - **发行版门禁**：`IsReleaseDistribution` = 版本号无 `snapshot`/`experiment` 后缀；
    `IsBlueTheme()` 标记蓝色系主题；`IsThemeSelectable()` 决定当前构建可见主题。
    **发行版只保留蓝色系主题**（天空蓝/龙猫蓝/死机蓝/肥鱼蓝/天依蓝/海人蓝/一歌蓝），
    非蓝色主题（剑客黄/初音绿/烤音绿）与自定义主题色输入行隐藏；旧配置命中不可用主题时自动回退龙猫蓝
- `Plain Craft Launcher 2/Pages/PageSetup/PageSetupUI.xaml(.cs)`
  - 主题下拉框选项由 `AllThemes` 主列表 + `ThemeColorValues` 过滤得到，**不要再用"索引 == 枚举值"的假设**
  - 自定义主题色输入框 `TextCustomColor`（正则 `^#?[0-9A-Fa-f]{6}$`）+ 预览色块 `RectCustomColor`
  - 个性化页顶部有引导语 `Setup.Ui.Basic.BlueHint`（"蓝色大肥鱼…"），配合发行版隐藏主题的策略
- `FormMain.xaml(.cs)`：`ImgTitleSword`（官方钻石剑像素图，`Opacity=0.22`，上下被标题栏裁切）、
  `RectTitleGolden`（黄昏圣光渐变）、剑的金色辉光 —— 均只在剑客黄主题显示，由 `RefreshTitleSword()` 控制

### 5.4 品牌与界面

| 内容 | 位置 |
| --- | --- |
| 标题栏 `CE` 徽标改为白底 **`Swordsman`** 文字（原 " For " 与 `swordsman.png` 图标已移除） | `FormMain.xaml`（`CELogo` 内改放 `LabCELogo`；`LabTitleFor` / `PanTitleSwordsman` / `ImgTitleSwordsman` 已删除） |
| 项目/程序显示名统一为 **`Plain Craft Launcher 2 swordsman`** | `FormMain.xaml`（窗口 `Title`）、`Plain Craft Launcher 2.csproj`（`AssemblyTitle` / `Product`）、`metadata.json`（`name`）、`PCL.Core/Utils/OS/AumidHelper.cs`（任务栏 `DisplayName`）；**`AssemblyName` 仍未改**（见 §8.1） |
| 登录区改「正版 / 离线 / 外置」三选项卡 | `PageLaunchLeft.xaml(.cs)`；外置页新增「验证服务器」下拉框 `ComboAuthServer`（默认 LittleSkin，可新建并持久化到 `LaunchAuthServerList`） |
| 开始界面「PCL CE For SGU 提示」卡片 + 屏蔽口令 | `PageLaunchRight.xaml.cs`（口令为 **`帝国娘`**，对应 `Launch.Right.CommunityHint.*`） |
| 升级后「版本说明」弹窗文案 | `Update.CommunityNotice.*`（zh-CN/zh-TW/en-US/en-GB 四份，注意同步） |
| 「设置 → 软件信息」下游项目声明卡片 | `PageSetupAbout.xaml` 顶部（`Setup.About.Downstream.*`） |
| 「反馈」侧边栏图标**沿用上游**（`SvgIcon="lucide/message-circle"`，`LogoScale=0.9`）；禁用状态改由页面内红色提示 + 条目 `ToolTip` 表达 | `PageSetupLeft.xaml`（`ItemFeedback`）、`PageSetupFeedback.xaml(.cs)` 红色提示 |
| **整页「不可用」占位控件**（红色带叉箭头 + 红字，带 1.2s 循环呼吸动画） | `Controls/MyUnavailablePlaceholder.xaml(.cs)`；被 `PageSetupFeedback`（反馈声明）与 `PageSetupUpdate`（"此版本不支持更新"）复用 |
| 反馈页禁用状态：隐藏提示条/提交卡片/状态列表/加载框，只留占位 | `PageSetupFeedback.xaml`（`PanBlocked`）+ `.cs` 的 `PageLoaded` 分支 |
| 更新页禁用状态：隐藏选项卡/更新卡/检查卡/其他选项/黄色提示，只留占位 | `PageSetupUpdate.xaml`（`PanUnsupported`）+ `.cs` 的 `ShowUnsupportedUI()` |
| 「回声洞」打字动画 | `Plain Craft Launcher 2/Modules/ModEchoCave.cs` + `PageToolsTest.xaml(.cs)`（语录列表在 `ModEchoCave.Quotes`） |
| 素材 | `Images/swordsman.png`（SGU 标识）、`Images/diamond_sword.png`（官方钻石剑 16×16 放大 9 倍，**未旋转**） |

### 5.5 新增配置项（都在 `PCL.Core/App/Config.cs`）

| 配置键 | 类型/默认 | 用途 |
| --- | --- | --- |
| `LaunchAuthServerList` | string / `""` | 外置登录验证服务器历史列表（`|` 分隔） |
| `LinkTerraCottaEnabled` | bool / `true` | 陶瓦联机总开关 |
| `LinkTerraCottaRelayMode` | int / `1` | 0=仅 P2P，1=允许 EasyTier 公共中继 |
| `UiCustomColor` | string / `#39C5BB` | 自定义主题色（`#RRGGBB`） |

### 5.6 版本号显示相关

- `PCL.Core/App/Metadata.cs`：`LauncherVersionModel` 增加 `DisplayName`（`27.1-snapshot-8(2.15.1)`）与
  `DownstreamVersionName`；`BaseName` 保持纯下游号（`27.1-snapshot-8`）
- `Basics.DisplayVersionName` / `ModBase.versionDisplayName` 供界面与日志使用；
  `versionBaseName`（User-Agent 等要求合法 token 的场合）**不能带括号**
- `PageSetupUpdate.VersionNameFormat()` 遇到带括号的完整版本号会原样返回（历史上曾把 `2.12.1` 显示成 `2 12 1`）
- `PCL.Core/App/Basics.cs`、`ModLaunch`、`ModMain`、`CrashReportExporter`、`FormMain`、`PageSetupAbout` 显示版本处均已改用 DisplayName

### 5.7 插件系统（M1 已落地）

设计全文见 [`PLUGIN-SYSTEM.md`](PLUGIN-SYSTEM.md)。当前实现：

| 位置 | 作用 |
| --- | --- |
| `PCL.PluginSdk/`（新项目） | 契约：`IPlugin` / `IPluginContext` / `IPluginConfig` / `IPluginLogger` / `PluginManifest` / `PluginCapability` / `PluginApiVersion`（=1）/ `PluginState` |
| `PCL.PluginHost/`（新项目） | 宿主：`PluginManifestReader`（校验）、`PluginLoadContext`（ALC，SDK 回落默认 ALC）、`PluginLoader`（发现/加载/卸载/启停）、`PluginStateStore`（`plugins.state.json`）、`JsonPluginConfig`（插件配置持久化）、`PluginHostManager`（门面） |
| `Plain Craft Launcher 2/Modules/Plugins/PluginHostBootstrap.cs` | 接线：实现 `IPluginHostEnvironment`（宿主版本 / 插件目录 / 日志），后台线程启动 |
| `FormMain`（`HardwareInfo.GetHardwareInfo()` 之后） | 调用 `PluginHostBootstrap.Start()` |
| `Pages/PageTools/PageToolsPluginManage.xaml(.cs)` | **完整套用「实例设置 → 模组」的管理方式**：搜索 / 打开文件夹 / 从文件安装 / 下载新插件（跳市场）/ 全选 / 筛选（全部·已启用·已禁用·出错）/ 排序 / 底部多选栏（启用·禁用·删除·取消）；列表项 `MyPluginItem`（44px、色条、图标、多选高亮） |
| `PCL.PluginHost/PluginInstaller.cs` | `.pclpkg`（zip）安装与卸载：校验清单、剥离包内顶层目录、防目录穿越、同 id 覆盖 |
| `samples/HelloPlugin/` | 示例插件（不参与解决方案构建），见 `samples/README.md` |

- 插件目录：`<启动器目录>/PCL/Plugins/<id>/`；运行数据：`PCL/PluginData/<id>/`（`config.json` + 自有文件）
- 状态：`PCL/PluginData/plugins.state.json`（用户禁用列表）
- 契约程序集**不能**由插件自带（必须回落默认 ALC），否则 `InvalidCastException`
- 调试钩子：环境变量 `PCL_FORCE_PLUGIN_PAGE=1` 会强制进入「插件管理」页（用于无人值守验证 UI）

---

## 6. 语言资源（务必同步）

新增/改写的资源键分布（**至少 zh-CN 必须有；其他语言缺失会回退到 zh-CN**，但 `PCL.Core.Test/LocalizationTest`
要求所有语言包含 zh-CN 的全部键 —— 该测试不在 CI 构建中，本地跑测试时请注意）：

| 前缀 | 说明 | 实测覆盖语言（2026-10 核对） |
| --- | --- | --- |
| `Launch.Account.Tab.*` / `Launch.Account.Auth.Server*` | 登录三选项卡、验证服务器下拉框 | **zh-CN/en-US**（zh-TW/en-GB 缺 10 键，回退 zh-CN） |
| `Setup.Update.Unsupported` | "此版本不支持更新" | zh-CN/en-US |
| `Setup.About.Downstream.*` | 下游项目声明（12 键） | zh-CN/en-US |
| `Setup.Feedback.Downstream.Disabled.*` | 反馈禁用提示（4 键） | zh-CN/en-US |
| `Setup.Ui.Theme.Color.*` / `Setup.Ui.Theme.CustomColor*` | 新主题名、自定义色 | zh-CN/zh-TW/en-US/en-GB |
| `Tools.GameLink.TerraCotta.*` / `Tools.GameLink.Tutorial.*` | 陶瓦联机页面 | zh-CN/zh-TW/en-US/en-GB |
| `Tools.Test.EchoCave.*` | 回声洞 | zh-CN/zh-TW/en-US/en-GB |
| `Launch.Right.CommunityHint.*`、`Update.CommunityNotice.*` | 改写为 SGU 文案 | zh-CN/zh-TW/en-US/en-GB |
| `Setup.Misc.System.Telemetry.Disabled` | 遥测勾选框的「已完全禁用」提示 | zh-CN/zh-TW/en-US/en-GB |

当前键数：`zh-CN` 3168 / `en-US` 3168 / `zh-TW` 3085 / `en-GB` 3085 / `fr-FR` 248 / `ja-JP` 16 / `es-ES` 2。

> **已知缺口（尚未补，属可选优化）**：`zh-TW` 与 `en-GB` 相比 `zh-CN` 各缺 **83 个键**，其中
> 28 个是下游自己加的（`Launch.Account.Tab.*` 6 个、`Launch.Account.Auth.Server*` 4 个、
> `Setup.About.Downstream.*` 12 个、`Setup.Feedback.Downstream.Disabled.*` 4 个、`Setup.Update.Unsupported` 1 个、
> `Setup.Misc.System.Telemetry.Disabled` 1 个），
> 另外 56 个是上游自身在 zh-TW/en-GB 未跟进的新键（`Instance.Resource.Mod.JarInJar.*`、`Launch.Skin.Cape.*`、
> `Setup.GameManage.Download.*` 等）。运行时缺失键会回退到 zh-CN 文案，不会崩溃。
> `ja-JP` / `fr-FR` / `es-ES` 是上游的不完整翻译，本分支未新增这些语言的键。

---

## 7. 已知问题 / 待办

1. **TerraCotta 联机未经实机验证**：代码链路（本地生成房间号 → EasyTier 网络 → 端口转发 / LAN 广播模拟）已打通并编译通过，
   但 P2P 连通性、EasyTier 首次下载、房主/房客实际进游戏均**未在 Windows 上实测**。接手后请优先验证：
   房主扫描到局域网世界 → 建房拿到房间号 → 房客加入 → 房客"多人游戏"列表出现房主世界。
   若 EasyTier 官方公共节点不可用，可在设置里选「仅 P2P」或自行替换 `EasyTierEntity._fallbackNodeLinks`。
2. **界面观感未验证**：所有 WPF 改动只能在 Windows 实机确认（输入框高度、按钮宽度、标题栏装饰等已按反馈修过多轮）。
3. **文案与实现对不上的风险**：`Update.CommunityNotice.*` 与开始界面提示里写明"砍掉了大厅、纯 P2P"，
   与当前实现（TerraCotta 纯 P2P）一致；若后续恢复任何依赖服务器的联机功能，记得同步改文案。
4. **`code` 递增会触发升级弹窗**：每次提升 `version.code`，首次启动会走一次升级流程，弹出「PCL CE For SGU 版本说明」，
   属预期行为（弹窗内容见 `docs/FIRST-RUN-DIALOGS.md`）。
5. **回声洞语录目前只有 3 条测试文案**，位于 `ModEchoCave.Quotes`，可随时增删。
6. **剑客黄配色为人工调校**：`_CalculateColorsGoldenDusk` 的 ramp 表可直接微调；黄色在亮色模式下对比度有限，改深了会显得脏。
7. **主页预设「PCL CE 公告栏」仍指向 PCL 服务器（待决策，未改）**：`PageLaunchRight.xaml.cs` 的
   `case 14` 会从 `https://s3.pysio.online/pcl2-ce/apiv2/pages/announce.xaml` 拉取主页 XAML，
   只在用户**主动**在下拉框里选了该预设时才联网，与已禁用的「云端公告 / 云控」（`AnnouncementService`）不是同一路径。
   严格按「不连任何 PCL 服务器」的口径，这个预设应当删除或改指向本地资源；本轮（snapshot-8）未改动代码，
   仅在文档中登记，等确认口径后再处理。
8. **EasyTier 内核下载镜像仍走 PCL 静态资源**：`ModLink.cs` 里的 EasyTier 压缩包地址为
   `staticassets.naids.com` / `s3.pysio.online`（与上游一致）。这是**下载内核**用的静态资源，
   不涉及大厅下发或中继；若日后要完全去 PCL 化，需要另找镜像。
9. **启动闪退 / 报 `Failure extracting contents of the application bundle`（解包缓存损坏，实测踩到）**：
   csproj 里 `IncludeNativeLibrariesForSelfExtract=true`，单文件产物启动时会先把
   `dtls_native.dll`（2,587,136 B）与 `e_sqlite3.dll`（1,911,296 B）解包到
   `%TEMP%\.net\<exe 文件名>\<bundle id>\`。若该目录下出现 **0 字节残件**
   （上次启动被中途结束、杀软拦截写入等），此后**每次启动都会在解包阶段失败**，
   表现为双击没反应、或弹出 `A .NET application failed`，Windows 事件日志里是
   `.NET Runtime` 事件 ID 1023、`Expected size:2587136 Actual size:0`。

   **与 exe 本身无关，不要重新构建**。删掉缓存目录再启动即可恢复（会重新解包一次）：

   ```powershell
   Remove-Item "$env:TEMP\.net\PCLCE For SGU 27.1-snapshot-8(2.15.1)" -Recurse -Force
   ```

   排查口诀：先看 `%TEMP%\.net\<exe 名>\` 里有没有 0 字节 dll；确认 exe 是否完好可用
   `DOTNET_BUNDLE_EXTRACT_BASE_DIR=<可写目录>` 启动一次，能正常出窗口就说明包没坏。

---

## 8. 重要约定（避免踩坑）

1. **不要改程序集名（`AssemblyName`）**：`FormMain.xaml` 等处使用
   `pack://application:,,,/Plain Craft Launcher 2;component/...` 资源 URI，改名会导致图标等资源加载失败。
   产物重命名只在构建后做（`build.sh` 已实现）。
2. **MyTextBox 最小高度 28**：控件模板内含 `MinHeight=28` 的行，设成 26 会裁掉下边缘（已修过一次）。
3. **`MyCard.CanSwap` / `IsSwapped`**：`IsSwapped=true` 表示默认收起；需要"可折叠但默认展开"就只加 `CanSwap`。
4. **`MyCheckBox.Change` 的签名是 `(object sender, bool user)`**，不是 `RouteEventArgs`。
5. **`MyTextBox` 的 `ValidatedTextChanged` 是 `RoutedEventHandler`**；校验用 `Validate()` + `ValidateRules`。
6. **`AnimatedBackgroundGrid.BackgroundBrush` 只接受 `SolidColorBrush`**，要渐变请另加叠加层（参考 `RectTitleGolden`）。
7. **HMCL 参考源码就在本工作区** `HMCL-main/`（20 MB，GPLv3）：**仅作阅读参考，严禁拷贝其代码/资源进本仓库**
   （许可不兼容）。发版/打包时不要把它带进产物或提交。
8. **上游素材与法务**：`diamond_sword.png` 由原版 Minecraft 纹理放大而来，仅作装饰用途；
   Minecraft 与 Mojang/Microsoft 无隶属关系（「软件信息 → 上游版权与法律信息」已有声明，保留即可）。
9. **`MyButton.Click` 也是自定义签名 `(object sender, MouseButtonEventArgs e)`**（`MyButton.xaml.cs` 里
   自定义的 `ClickEventHandler`），**不是** `RoutedEventArgs`。写错不会编译报错（XAML 事件是运行时按名绑定），
   而是在**打开页面时**抛 `XamlParseException：无法从文本"Xxx_Click"创建"Click"`（已踩过一次）。
   同理：`MyCheckBox.Change` 是 `(object, bool)`、`MyTextButton.Click` 需查控件定义。
10. **插件宿主的事件在后台线程触发**：`PluginHostManager.Changed` 可能来自任意线程，
    订阅方必须把**所有 UI 访问**（包括 `IsLoaded` 这种 DependencyObject 属性）放进 `ModBase.RunInUi`。
    在后台线程读 `IsLoaded` 会抛 `InvalidOperationException：调用线程无法访问此对象`（已踩过一次）。
11. **新建 C# 工程时 `System.IO` 不在隐式 using 里**（`UseWPF` 工程）：`Path`/`File`/`Directory` 需显式
    `using System.IO;`，否则 CS0103。
12. **不要用 Windows PowerShell 5.1 的 `Get-Content` / `Set-Content` 批量改带中文的源文件**：
    PS 5.1 会把无 BOM 的 UTF-8 按 GBK 读取，回写后中文注释全部乱码、还可能吞掉换行（已踩过一次，整文件重写才修好）。
    批量改文件请用编辑器/专用脚本，或显式 `-Encoding UTF8` 且确认源文件是 BOM 编码。
13. **插件目录不能在插件加载期间直接删除**：`AssemblyLoadContext.Unload()` 只是请求卸载，
    文件句柄要等 GC 真正回收才释放（实测运行期 `Directory.Delete` 必失败）。
    现在的语义是「**立即隐藏 + 下次启动彻底删**」：禁用 → 目录名写进 `PCL/PluginData/pending-delete.json`
    → `ReloadAll()` 跳过这些目录（列表里立刻不显示）→ 后台再尽力删一次 → 下次启动
    `ProcessPendingDeletes()` 真正删除并清空清单。
    该逻辑已封装在 `PluginInstaller` / `PluginLoader`，**不要绕过它直接删目录**。
14. **`FormMain.PageChange(PageType.Tools, subType)` 不会切换工具箱子页**：
    `PageChangeActual` 的 `case PageType.Tools` 会把 `subType` 强制覆盖成
    `ModMain.frmToolsLeft.pageID`（保持当前子页）。要在「工具」标签页内跳子页，必须走左侧页面，
    例如：`ModMain.frmToolsLeft.ItemPluginMarket.SetChecked(true, false, false);`
    再 `ModMain.frmToolsLeft.PageChange(PageSubType.ToolsPluginMarket);`（与用户点击等价）。
    `Download` / `Setup` 分支有专门的子页遍历逻辑，不受此影响。

---

## 9. 交接清单（接手后建议按顺序做）

- [ ] 先读 [`README-FIRST.md`](../README-FIRST.md) 与 [`docs/HANDOVER-2026-10-25.md`](HANDOVER-2026-10-25.md)（本轮交接的完整快照）
- [ ] 在 Windows 机器上 `git clone`/解压源码，`dotnet --version` 确认 SDK 10
- [ ] `./build.sh x64 Release` 跑通，确认产物名 `PCLCE For SGU 27.1-snapshot-8(2.15.1).exe`
      （Windows 上没有 `bash`/`python3` 时，改用仓库自带等价脚本 `tools\build.ps1`，见 §3.3）
- [ ] 启动器实机走查：首次启动弹窗 → 登录三选项卡（含外置验证服务器下拉框）→ 启动游戏 → 设置各页 → 百宝箱回声洞
- [ ] 验证 TerraCotta：两台机器（或本机+朋友）走一遍房主/房客流程（见 §7.1）
- [ ] 验证主题：切换各主题；确认剑客黄显示钻石剑+圣光且透明度合适
- [ ] 确认反馈页/更新页/软件信息页文案与禁用状态符合预期
- [ ] 拿到 CI secrets（可选）：微软登录、CurseForge、Sentry、Naid、联机根服务器；不注入也能正常构建
- [ ] 与剑客群组服服主团队确认发版通道与对外口径（对外只发布 SGU 内部分享版本）

---

## 10. 联系人

- 项目/内部渠道：剑客群组服服主团队
- 问题反馈邮箱：`hmjmfabc@swordsman.top`、`mingyugames@swordsman.top`
- 上游：<https://github.com/PCL-Community/PCL2-CE>（**仅作代码参考，不要向其提交本作问题**）
- 原作：<https://github.com/Meloong-Git/PCL>（作者 龙腾猫跃）

祝你接手顺利，有问题先看 `DOWNSTREAM-CHANGES.md` 的逐版记录，绝大多数改动都有说明。
