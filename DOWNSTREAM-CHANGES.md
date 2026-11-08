# 下游分支改动说明

本分支基于 **PCL: CE（Plain Craft Launcher Community Edition）** 源码二次开发，
版本号规则见 [`VERSIONING.md`](VERSIONING.md)。

当前版本：**27.1.1-snapshot-86(2.15.2)**

> 括号外为下游版本号，括号内为**标注用的**上游版本号（**不追上游同步**，见 `HANDOVER.md` §0.19 / §4.1）。

## snapshot-86：四个原版贴图主题 + 深浅色主题合并

> 用户原话：「加入以下新主题：①琉璃彩…②曜石黑…③蜂蜜黄…④黏液绿…把深浅色主题合并到一起，
> 不再分开选择。旧版启动器升级的话只读取浅色主题的选项。」

- 版本号 `27.1.1-snapshot-86(2.15.2)`，`code` 612；8 个语言文件各 **3389** 键（新增 6 个键）。
- **`ColorTheme` 新增 4 个槽位**（接在 `PluginDefined=16` 之后，不动已有编号）：
  `LiuliGlass=17` / `ObsidianBlack=18` / `HoneyYellow=19` / `SlimeGreen=20`；
  都在「扩展主题」功能键之下，配色走新的逐档渐变 ramp（`_CalculateColorsHoneyAmber` /
  `…SlimeGel` / `…LiuliPrism` / `…ObsidianVoid`），并同步登记进 `IsHexTheme()`。
- **取色口径**：曜石黑 `#100C1C`（黑曜石主体色）；蜂蜜黄 `#FADE29 → #EA8E16 → #D56B2B`（蜜脾三色）；
  黏液绿按 **alpha 加权**重算（黏液块大面积半透明，`alpha ≥ 200` 口径为空）得
  `#7BCE6A` 13.7% / `#73C262` 50.0% / `#5AAA43` 7.4%；琉璃彩取玻璃三色 `#D0EAE9 / #8BC1CD / #A8D0D9`。
- **标题栏贴图**：新增 `ModTitleTextures`（运行时加载 16×16 平铺 `ImageBrush`，带同色棋盘兜底），
  下界红 / 水晶紫也从「XAML 写死 pack URI」迁过来 —— 干净 clone 里缺贴图不会再让 `InitializeComponent` 抛异常。
  曜石黑 = 黑曜石 + 右侧 20% 哭泣黑曜石；蜂蜜黄 = 蜂巢侧面 10% + 蜂巢块 40% + 蜜脾 40% + 蜂箱侧面 10%；
  黏液绿 = 黏液块铺满。
- **琉璃彩**：新增 `Modules/UI/Material/ModLiuliGlass.cs`。三块大玻璃（标题栏 / 侧边栏 / 主界面），
  内部铺原版 `Glass.png`、四周 8px 用 `GlassPaneTop.png`（不透明度 60%）、
  玻璃内部小粒子（2~5 px，上限 40%）铺满整窗；主题色是动态彩色渐变 ——
  45 fps 定时器每帧只重写 13 个画刷颜色（3 块玻璃 × 3 个色标 + 4 个粒子画刷），色相沿 OKLCH 96 秒转一圈，
  不重建资源字典、不走 `ThemeService` 全量刷新。四层不透明度在 `ModMaterial` 里**锁定 30%**，
  设置页四个滑条禁用并显示 `Setup.Ui.Material.LiuliLocked` 说明。
- **深浅色主题合并**：设置页两个下拉合并成一个「配色主题」（新键 `Setup.Ui.Theme.Color`）；
  `ThemeService.CurrentTheme` / `CurrentPluginThemeId` 只读浅色槽位、写入时镜像深色槽位；
  启动时 `SyncMergedThemeSlots()` 把浅色同步进深色（**旧版升级只读取浅色主题**），日志写
  `[Theme] 深浅色主题已合并：…`。插件请改用 `ThemeService.SetCurrentThemeSelection(theme, id)`。
- 语言键：`Setup.Ui.Theme.Color.{Liuli,Obsidian,Honey,Slime}`、`Setup.Ui.Theme.Color`、`Setup.Ui.Material.LiuliLocked`。

## preview-73：转入预发布阶段（阶段名 pre → preview）

- **阶段改名**：`VERSIONING.md` 里预发布阶段由 `pre` 统一改成 **`preview`**，示例与说明同步更新；Build 号规则不变（跨阶段连续递增）。
- **版本号**：`27.1-preview-73(2.15.1)`，`code` 596。
- **新增面向用户的 [`CHANGELOG.md`](CHANGELOG.md)**：按里程碑写「立项以来加了什么」，不列 bug 修复。
- 本版内容 = snapshot-72 的全部改动（窗口材质重做、界面铺满窗口、新徽标、启动图、玻璃标题栏透明度、有/无材质尺寸对齐、材质类型重启生效等），本轮只升版本号与补文档。
- **预览版协议**：`Modules/Base/ModPreviewAgreement.cs`，`-preview-` 版本启动后 1.5s 弹一次
  （标题「PCL Swordsman 预览版协议」，同意 / 不同意，不同意直接 `Application.Shutdown()`）；
  同意结果记 `States.UI.PreviewAgreementVersion`。**必须等入场动画跑完再弹**，否则会和
  `PanMsgBackground` 的全窗口模糊打架，表现为界面糊住、按钮点不到。
- **更新页版本号排版**：去掉 `PCL Swordsman ` 前缀、字号 20→16、`NoWrap` + 省略号 + 悬停看全
  （原来被右侧按钮挤掉，显示成 `27.1-pre…`）。
- 新增 4 个语言键 `Preview.Notice.*`，8 个语言文件各 **3381** 键。
- 产物：`artifact-x64/PCLCE For SGU 27.1-preview-73(2.15.1).exe`，25,739,368 B，SHA-256 `33172015BE3809158404FCF8D3D58E7118F35B3978B0865ED2E858D8720C9613`（`dist/` 有同哈希副本）。
- 交接书：**`HANDOFF-2026-10-31-preview-73.md`**（已更新 `README-FIRST.md` 的入口指向）。

## snapshot-72：界面铺满窗口 → 磨砂与界面等大（Win10 / Win11 同步修）；换新徽标

> 用户拍板：「那三个方案（抓屏 / 壁纸 / 混合）都不接受，还搞之前 build 的 DWM 大窗口，但起码边框大小要和启动器一致」，
> 之后补充：「圆角可以有，四个角错误模糊用户看不出来；win11 也反馈了边框 bug，请在 win11 上做到完美适配」。
> 也就是：用回**系统真实磨砂**（Win10 = AccentBlur，Win11 = 云母/亚克力）、**保留圆角**，并让磨砂范围和界面**一样大**。

### ① 为什么以前会「边框比启动器大」（这次的真因）

- 旧版 `AccentBlur`（`SetWindowCompositionAttribute`）由 DWM 按**整个窗口矩形**绘制，不听 `SetWindowRgn`；
- 而界面本体内缩了 10 px（`FormMain` 的 `PanBack.Margin="10"`，上游给阴影留的边）；
- 于是那圈 10 px 也被糊了 —— 磨砂比界面**四周各多 10 px**，圆角外面还有直角磨砂。
- snapshot-71 的处理是「默认不用 AccentBlur，改用应用内壁纸磨砂」，结果 Win10 上变成「没有磨砂」。

### ② 改法：界面铺满窗口（Win10 / Win11 同一套）

**共同根因**：DWM 画的磨砂（Win10 的 AccentBlur、Win11 的云母 / 亚克力）都铺满**整个窗口矩形**，
而界面本体内缩了 10 px（`PanBack.Margin="10"`，上游给阴影留的边）→ 四周各多出一圈 10 px 的磨砂。

- `ModMaterial.SetFullBleed`：**材质生效时**把 `PanBack.Margin` 由 `10` 改成 `0`，界面铺满整个窗口；
  关闭材质时还原成 `10`。窗口区域随之变成「内缩 0 px」——
  `[Material] 窗口外形：窗口区域（内缩 0px / 圆角 6px；区域包围盒 (0,0)-(W,H)）`，
  窗口矩形 == 界面矩形，磨砂范围与界面**完全重合**，四周不再多出一圈。**Win11 走的是同一段代码**。
- 圆角**保留**（普通模式 `UiWindowCornerRadius` 默认 6，液态玻璃默认 16），
  四个圆角的小三角里会露出一点磨砂 —— 用户确认「看不出来」，可以接受。
  同时去掉了「磨砂生效就强制直角」（`MaterialService.ForcesSquareCorners`）那套逻辑。
- Win10：云母 / Aero / 液态玻璃**一律走 AccentBlur**（`AppliedVia = "旧版 AccentBlur（界面铺满窗口，磨砂与界面等大）"`），
  不再依赖「允许旧版磨砂」勾选项；没有 DWM 材质的 Win11 21H2（22000–22620）也走这条路。
- Win11（22621+）：云母 / 亚克力照旧由 DWM 提供，另外**关掉系统那圈 1px 描边**
  （`DwmInterop.TrySetWindowBorder` → `DWMWA_BORDER_COLOR = DWMWA_COLOR_NONE`）。
  这条描边沿窗口矩形画，和我们的圆角区域对不上，正是 Win11 上「外面多一圈边」的来源；关闭材质时还原。

### ③ 徽标换成新图（`brand/new/logo-source.webp`）

- 新图整张缩到 16/32 px 时 PCL 字标太小，所以按**白色字标包围盒**裁出中间那块蓝底牌匾（`_tmp/make-logo.py`），
  让 PCL 顶满画布，再统一加 14% 圆角：
  - `Plain Craft Launcher 2/Images/icon.ico`：16/24/32/48/64/128/256 七档（exe 图标 / 窗口图标 / 启动图 / Web 接口）
  - `Plain Craft Launcher 2/Images/icon.png`：512×512（更新页、README 引用的就是它旁边的 ico）
  - `Plain Craft Launcher 2/Images/Heads/Swordsman.png`：256×256（关于页「PCL Swordsman 团队」卡片头像）
- 标题栏仍保留**矢量 PCL 字标 +「Swordsman」白底小徽标**（随主题变色，不换成蓝底位图，避免和主题色打架）。
- 版本号：`27.1-snapshot-72(2.15.1)`，`code` 595，25,739,368 B，
  SHA-256 `B27A3947862DE9CD353C5B20BA8E8B0770CA03AEA30DD01CF632C013F643D196`（`dist/` 有同哈希副本）

### ④ 圆角交给窗口区域裁 + 关闭材质时彻底撤掉系统磨砂

- **液态玻璃「边框像糊了一层透明膜」**：内容是圆角裁剪（`RectForm`）+ 系统磨砂按矩形绘制，
  两者之间的四个圆角小三角里露出的是**没上色的磨砂**。现在改成：窗口区域成功时
  `RectForm` 圆角设 **0**（内容铺满，玻璃/着色一直铺到区域边缘），圆角完全由窗口区域裁；
  区域设置失败才退回内容裁剪（`ModMaterial.SetContentCornerRadius`）。
- **高级材质改回「关闭」后边框 bug 复发**：只把 `AccentState` 置 0 时，部分 Win10 build 要等窗口重新合成才真正撤掉。
  新增 `MaterialService.Clear(hwnd)`：连发两次 AccentBlur 关闭 + `DWMWA_SYSTEM_BACKDROP_TYPE = None` +
  `SetWindowPos(SWP_FRAMECHANGED)` / `RedrawWindow` / `DwmFlush` 强制重绘，并写日志「已关闭系统窗口磨砂」。

### ④-b 「边框像糊了一层透明膜」的真凶：窗口自带的假阴影圈

> 用户原话：「观感大致是启动器里套了个去掉四角四个小正方形的矩形」——描述的就是 `FormMain.xaml` 里那圈**假阴影**。

- `FormMain.xaml` 顶部有 8 个 `Rectangle`（上下左右各 10px 的黑色渐变 + 四个 16×16 圆角暗块），
  是上游给「没有系统阴影的窗口」模拟浮起效果用的，原本画在 `PanBack.Margin="10"` 那圈**透明边距**里。
- 界面铺满窗口以后，这圈半透明黑边正好压在玻璃 / 磨砂上 → 看上去就是「边框糊了一层透明膜」，
  而且边缘带 16px 缺口 + 四个角块，形状正好是「去掉四角小正方形的矩形」。
- 修法：把这 8 个 Rectangle 收进 `Grid x:Name="PanShadow"`，
  `ModMaterial.SetFullBleed` 在铺满时 `Collapsed`、还原边距时 `Visible`。
- 验证：`PrintWindow` 截图逐像素比对 —— 修前在 x=10 / y=10 有颜色突变（黑边），修后从窗口边缘到内容完全连续。

### ④-c 有材质 / 无材质的界面尺寸对齐

- **材质类型改为「重启生效」**：`ModMaterial` 在进程内锁定类型（`_lockedKind`，首次 `ReloadOptions` 取配置值），
  改类型只在重启后生效，下拉框改动时弹提示（语言键 `Setup.Ui.Material.RestartRequired`）；参数仍然即时生效。
  备注：这台开发机装了 **DWMBlurGlass**（`D:\Program Files\DWMBG\DWMBlurGlassHost.dll` 注入 dwm.exe），
  它会自己往窗口上画模糊，`已关闭系统窗口磨砂` 执行成功也撤不干净 —— 排查「关了材质还有磨砂」时先看它。
- **尺寸对齐**：材质生效时界面铺满窗口，视觉上会比普通模式大 20px（左右/上下各 10）。
  现在铺满的同时把窗口缩小 20×20（保持中心不动），于是：
  普通模式 窗口 884×535 / 界面 864×515；材质模式 窗口 864×515 / 界面 864×515 —— **DWM 磨砂、
  普通启动器、材质启动器三者尺寸完全一致**。
  存档时用 `ModMaterial.WindowSizeCompensation` 把 20px 补回去，`WindowWidth/Height` 始终是
  「无材质时的窗口尺寸」，不会每次启动越缩越小。

### ⑤ 液态玻璃支持单独调标题栏透明度 + 启动图放大

- 新增配置 `UiMaterialGlassTitleOpacity`（0~100，**默认 0** = 标题栏和玻璃连成一片），
  玻璃面板里多一条「标题栏不透明度」滑条（复用现有文案键，不动 8 个语言文件）。
- 启动图原来用 `Images\icon.ico`，`SplashScreen` 取到的是 16×16 那帧 → 屏幕上就一个小点。
  现在换成 `Images\splash.png`：**160×160、完整原图不裁切**（带层叠边框的整块徽标）。
  注意：`icon.ico` / `icon.png` 仍然用「裁出中间蓝底牌匾」的版本 —— 那是给 16/32px 小尺寸用的，
  整图缩到那么小 PCL 字标会糊成几个点；启动图尺寸大，用完整原图。
- **启动图淡出时透明像素变黑**：WPF 的 `SplashScreen.Close(TimeSpan)` 是给整个窗口做 alpha 动画，
  位图里的透明像素会按「黑 + 全局 alpha」参与合成，于是主界面出来后那 400 ms 里徽标四周发黑。
  现在改成 `Close(TimeSpan.Zero)`（瞬间关闭），牺牲 400 ms 淡出换掉这个黑边。

### ⑥ 本轮验证到哪

- 构建期：0 error；`Win10 19045`（22H2，与用户测试机同版本）实机跑起来后日志确认
  `效果：… → …（旧版 AccentBlur（界面铺满窗口，磨砂与界面等大））`、
  `窗口外形：窗口区域（内缩 0px / 圆角 6px；区域包围盒 (0,0)-(884,535)）`，
  屏幕截图比对：磨砂边界 == 窗口边界，四周**没有**多出来的一圈，圆角保留。
- **本轮无法验证**：Windows 11 真机（这台开发机是 Win10 19045）。Win11 的改动是：
  同一段 full-bleed、`DWMWA_BORDER_COLOR` 关闭描边、云母/亚克力走 DWM。
  请在 Win11 22H2/23H2 上看三点：① 四周无外扩、无 1px 描边；② 圆角正常；③ 云母/亚克力能透出来。
- **仍待确认**：① 关闭材质后是否恢复 10 px 边距与圆角；② Win11 21H2（22000–22620）走 AccentBlur 的观感。

## 交接：snapshot-71 之后的未解决问题（Win10 磨砂仍无效果）

> 2026-10-31 交接给桌面端 DSH（可用视觉）。**snapshot-71 没有解决用户报的「22H2 上边框 bug」**，已确认原因：

- `ModWallpaperFrost.Apply()` 依赖窗口内壁纸（`FormMain.ImgBack.Background`）；
  用户未设壁纸时它是 `null`，函数直接 `Clear(); return;` → **磨砂一步没做**（`ModWallpaperFrost.cs:42`）
- 用户给的后续提示：「模糊的实现可以改了，可以复用 PCL CE 上游"高级材质"的高斯模糊算法」
- 仓库现成资产：`PCL.Core/UI/Effects/*`（采样高斯模糊）、`BlurBorder` + `BackgroundPresenter`（糊"身后视觉树"）、
  工作区 `DWMBlurGlass-master/`（C++ 注入 DWM 的真背景模糊）
- 三条候选方案（桌面壁纸抓取 / 屏幕抓取 / DWMBlurGlass 集成）与验收标准写在
  **`HANDOFF-2026-10-31.md` §9.1**；同一节 §9.2 还列了其余「需要视觉确认」的项
  （液态玻璃观感、樱花粒子密度、Faded 装饰、化学式字形、关于页头像裁切等）

## snapshot-71：Windows 10 22H2 的窗口材质改走「应用内壁纸磨砂」

> 用户：「边框Bug在22H2上仍然出现，Win10 22H2是必须适配的版本」。

### 为什么 22H2 会复现（19045 与 19044 同理）

- `DWMWA_SYSTEMBACKDROP_TYPE`（云母 / DWM 亚克力）需要 **build ≥ 22621**；
  `DWMWA_WINDOW_CORNER_PREFERENCE`（DWM 圆角）需要 **≥ 22000**。Win10 22H2 = **19045**，两条都没有；
- 唯一能做窗口磨砂的是旧版 `SetWindowCompositionAttribute` AccentBlur，而它**按整个窗口矩形绘制、不听窗口区域**，
  所以只要它生效，圆角外面的那圈磨砂就一定会溢出来 —— 之前的处理是「默认不启用它，回退纯色」，
  于是 Win10 用户要么没有磨砂，要么勾了「允许旧版磨砂」又看到边框溢出。

### 改法：Win10 云母 / Aero 默认使用**宿主自绘壁纸磨砂**

- `MaterialService.Apply`：非 22621 的云母 / Aero 不再回退 `None`，而是
  `Applied = Aero`、`AppliedVia = "应用内壁纸磨砂（Windows 10 自绘，形状可控）"`；
  磨砂糊的是**窗口内的壁纸**（`FormMain.ImgBack`），形状完全由窗口区域决定 → 圆角不再被破坏
- 新增共享工具 `ModWallpaperFrost`（`Apply` / `Clear`，幂等，只保留一份原值）：
  液态玻璃与 Win10 云母 / Aero 共用同一份实现，避免两边各自记原值导致「切回普通模式壁纸还是糊的」
- `ModMaterial`：材质生效时按需糊壁纸（液态玻璃、或 Win10 的非玻璃材质），关闭材质时还原
- **旧版 AccentBlur 与圆角二选一**：一旦旧版模糊真的生效，`ForcesSquareCorners = true`，
  `ModMaterial.CornerRadius` 直接返回 0 并记日志 —— 从根上消除「磨砂比界面大一圈、四角是方的」这个组合
- 诊断日志升级：`[Material] 效果：Aero → Aero（应用内壁纸磨砂…）｜系统 build 19045｜…`，
  下次日志能一眼看出走的哪条路、build 多少、圆角是否被强制

- 版本号：`27.1-snapshot-71(2.15.1)`，`code` 594，25,996,141 B，
  SHA-256 `1612F1EBA4EF0C7832AF36C3E1CB161F5FFDA5E314995B8BE78A956FE4FF2FCE`（`dist/` 有同哈希副本）
- 验证（构建期）：exe 内命中 `ModWallpaperFrost` / `应用内壁纸磨砂` / `ForcesSquareCorners` /
  `系统 build` / `旧版模糊生效，圆角已强制为 0`；0 error；**22H2 实机观感待日志确认**

## snapshot-70：读日志修三个真问题 + 前两轮待发的修复一起进包

> 用户给了 snapshot-69 的完整日志（`Launch-2026-10-31-164433793.log`，Win10 19044）：
> 「有问题就修复，修复完成后就可以构建了」。下面按日志逐条对。

### ① 日志实锤：插件包被自己的读句柄锁住 → 启用 / 禁用改名失败

```
[16:52:41.618] 插件 sgu.sample.test1 已启用（0 个包文件，1 个失败：
    The process cannot access the file because it is being used by another process.）
弹窗：无法切换插件状态：sgu.sample.test1.pclpkg.disabled
```

- 根因：`ZipFile.OpenRead()` 用的是 `FileShare.Read`（**不共享 Delete**）。后台扫描读清单 / 校验哈希时，
  用户在管理页点开关要 `File.Move` 改名 → 撞上共享冲突。
- 修法：`PluginPackage.OpenReadShared()` —— 用 `FileShare.ReadWrite | FileShare.Delete` 打开包，
  `ReadManifest` / `ExtractToCache` / `PluginIntegrity.ReadFileHash` / `LooksLikeAssembly` 全部改走它。
  现在可以「边扫边改名」。

### ② 日志实锤：手工改过的包报 `BadImageFormatException`，原因不可读且反复失败

```
[16:53:01.119] 加载 sgu.sample.test2 失败:System.BadImageFormatException: Bad IL format …
   HelloPlugin(4)_8746_…\HelloPlugin.dll is invalid
```

- 修法一：**发现阶段粗查 PE 头**（`PluginIntegrity.LooksLikeAssembly`，看前两字节 `MZ`），
  不合法直接 `Faulted` 并给原因「入口程序集不是有效的 .NET 程序集（x.dll），包可能已损坏或不是编译产物」；
- 修法二：**解压缓存自愈** —— `LoadCandidate` 捕获 `BadImageFormatException` 后删掉该缓存目录重新解压再试一次，
  仍失败才落 `Faulted`（日志会打「程序集格式无效，正在重建解压缓存后重试」）；
- 顺带确认**熔断是好的**：日志里 `[16:53:10.950] HelloPlugin(4).pclpkg 连续失败 3 次，已自动禁用` + 随后改名成功。

### ③ 诊断日志误导：`声明 ui.notify 的插件：无`

- 之前用 `GetProviderEntries<IPluginNotify>()` 判断，但 `ui.notify` 是**宿主提供的服务**，插件不会实现它；
- 改成看清单能力：现在会正确列出 `sgu.sample.hello`。

### ④ 一起进包的前两轮修复（之前按要求没有构建）

- 权限行展示：**小字=状态**（已允许 / 已拒绝 / 待授权），**按钮=动作**（允许 / 改为允许 / 撤回授权），不再互相打架；
- `minecraft.read` 不再自动授权：声明能力后仍要**弹一次确认框**，结果落 `permissions.json`（内存缓存，只问一次）；
- 启用**未校验**的包（单条 / 批量）：先弹 warn 确认框；
- 工具侧栏插件页面条目**并入同一单选组**：删掉外层 `PanPluginPages`、改为直接插入 `PanItem`，
  删掉手写的互斥代码，加 `_isRefreshingPluginPages` 守卫 —— 点到什么就跳到什么，永远只有一个高亮。

- 版本号：`27.1-snapshot-70(2.15.1)`，`code` 593，25,996,141 B，
  SHA-256 `981B719CF93E6D7E44A1BB0D6090991B5EDDAF5268796C78FCFD54E804680E97`（`dist/` 有同哈希副本）
- 验证（构建期）：exe 内命中 `OpenArchiveShared` / `LooksLikeAssembly` / `TryDeleteCacheDirectory` /
  `入口程序集不是有效的 .NET 程序集` / `重建解压缓存后重试` / `EnsureMinecraftGranted` / `ActionRevoke` /
  `Enable.Unverified`；随包 6 个 pclpkg 均带 `files` 哈希；0 error；**行为待下一份日志确认**

## snapshot-69：补齐 M3/M4 的剩余项（Minecraft 只读 + 事件 / 权限授权 / 熔断 / 共享目录）

> 用户：「除了市场和高危 API 都做一下」。高危（`game-argv` / `process`）与插件市场不做，其余全做。

### ① Minecraft 只读服务（M3 的最后一块）

- SDK：`PCL.PluginSdk/IPluginServices.cs` —— `IMinecraftService`（`CurrentInstance` / `Instances` / `GameDirectory`）
  与 `PluginInstanceInfo`（`Name` / `Path` / `VersionName` / `LoaderName` / `LoaderVersion` / `ModCount`）；
  新能力位 `PluginCapability.MinecraftRead = 1 << 14`
- 启动器实现 `AppMinecraftService`：从 `ModInstanceList` 的**已加载快照**取值（不主动触发扫描），
  加载器按 Forge / NeoForge / Fabric / Quilt / Cleanroom 顺序识别，`ModCount` 数 `mods` 目录里的 `.jar`
- 没声明能力时插件拿到 `EmptyMinecraftService`（字段全空，不会 null 崩）

### ② 事件补齐（M3 的「实例/下载」）

- 新事件：`InstanceSwitchedEvent`、`DownloadStartedEvent`、`DownloadFinishedEvent`
- 钩子：`ModInstanceList.McMcInstanceSelected` 的 setter 转发实例切换；
  `ModLoader.LoaderTaskbarAdd` 转发任务开始，并订阅该任务的 `OnStateChangedUi` 转发结束（Finished / Failed / Aborted）

### ③ 权限授权（M4 的「权限 UI + permissions.json」）

- `PluginPermissionStore`：`PCL\PluginData\permissions.json`，形如
  `{ "sgu.hello": { "granted": ["net-http"], "denied": ["file-game-dir"] } }`
- 需要授权的四项：`minecraft.read` / `net-http` / `file-game-dir` / `file-user-pick`
  —— **首次真正用到时**才弹确认框（`AppPluginPermissions`），拒绝过的不再问；
- 管理页「齿轮」面板顶部新增**权限区**：逐项列出「已允许 / 已拒绝 / 待授权」，点按钮切换（写入同一份 json）

### ④ 网络与文件 API（M4 的「文件/网络能力」，不含高危）

- `context.Net`：`PluginHttp`（`Get` / `Post`，超时 1–120 秒），走 **能力 + 授权** 双闸；
  未授权时返回 `StatusCode = 0` 与说明而不是抛异常
- `context.Files`：`GameDirectory`（`file-game-dir`）、`PickOpenFile` / `PickFolder`（`file-user-pick`，走系统对话框）

### ⑤ 崩溃熔断 + 共享级安装目录（M4 的剩余两条）

- **熔断**：`PluginLoader.ApplyCircuitBreaker` —— 同一插件 5 分钟内失败满 **3 次**就把包改名为 `.disabled`
  并记日志（失败时间戳存 `PCL\PluginData\PluginFaults.json`，删掉该文件即可复位）
- **共享级目录**：额外扫描 `%APPDATA%\PCLCE\Plugins`，**实例级优先**，
  同名插件只加载实例级那一份（`PluginHostManager.Initialize` 增加 `sharedDirectory` 参数）

### ⑥ 文档与示例

- `docs/PLUGIN-API.md`：新增 §5.8（Minecraft 只读）、§5.9（网络与文件）、
  事件表补三条、能力表补 `minecraft.read` 与「需授权」说明；对外文档依旧**零**主题相关内容
- 示例插件 `sgu.sample.hello`（1.1.1）能力加 `minecraft.read`，页面会显示当前实例（版本 / 加载器 / mods 数）
- 语言键 +11 → 各语言 **3370** 键

- 版本号：`27.1-snapshot-69(2.15.1)`，`code` 592，25,992,045 B，
  SHA-256 `31AF5385C4DBBA844AE8B368B31D9A8CA4F1C526A37299A776C0512A39F5B800`（`dist/` 有同哈希副本）
- 验证（构建期）：exe 内命中 `IMinecraftService` / `AppMinecraftService` / `PluginPermissionStore` /
  `permissions.json` / `InstanceSwitchedEvent` / `DownloadFinishedEvent` / `PluginFaults.json` /
  `Tools.Plugin.Permission.Title` / `PluginHttp`；`HelloPlugin.pclpkg` 能力含 `minecraft.read`；0 error；
  **实机表现待日志确认**

## snapshot-68：插件系统第三档（完整性哈希 + 包签名 + 更新检测）

> 用户：「Build67 全部正常，如果 1、2 档都做好了就做第三档」。第三档 = **包完整性 / 签名 / 更新检测**，
> 全部**不依赖插件市场**（更新源就是插件清单里自己写的 `updateUrl`）。

### ① 完整性校验（`files` 哈希，默认开启）

- 清单新增可选 `files`：包内文件名 → SHA-256（解压后内容）；
- `PCL.PluginHost/PluginIntegrity.cs` 在**安装时**（`PluginInstaller`）与**每次加载前**（`PluginLoader` 两个发现分支）
  逐文件比对，不一致 → `PluginState.Faulted` + 管理页「校验失败」，**拒绝加载**；
- 向后兼容：没写 `files` 的老插件照常加载，管理页显示「未校验」；
- 打包链路自动带哈希：新增 `tools/plugin-hash.py`，`plugins/package.sh` 打完每个包就调用它，
  所以**随包 6 个插件现在全部带哈希**（也因此给这 5 个插件升了补丁版本号，触发「按版本覆盖安装」路径）。

### ② 包签名（ECDSA P-256，可选，带信任列表）

- 清单新增可选 `signature`：`{ alg, keyId, value }`；
  载荷固定为 `<id>\n<version>\n<文件>:<sha256>…`（Ordinal 升序、哈希小写），**工具与宿主必须一致**；
- 信任列表：`PCL\PluginData\trusted-keys.json`（`{ "keys": ["<SPKI Base64>", …] }`），
  启动时 `LoadTrustedKeys()` 读入 `PluginIntegrity.TrustedPublicKeys`（必须在扫描插件前）；
- 判定：签名算法不支持 / 公钥不在信任列表 / 验签失败 → 拒绝加载，分别对应
  `BadSignature` / `UntrustedKey` / `BadSignature`；未签名则照常（「未校验」）；
- 验签同时兼容 **IEEE P1363**（.NET 默认）与 **DER/RFC 3279**（openssl 默认）两种格式；
- 新增工具 `tools/PluginSigner`（.NET 控制台，Termux 没有 openssl 也能用）：
  `genkey` / `sign` / `hash` / `verify` 四个子命令。

### ③ 更新检测（`updateUrl`，不需要市场）

- 清单新增可选 `updateUrl`，指向静态 JSON：`{ version, url, sha256?, notes? }`；
- `Modules/Plugins/PluginUpdateService.cs`：GET → 比版本（只比数字段）→ 确认框 →
  下载到临时文件 → 若声明了 `sha256` 校验整包 → `PluginInstaller.InstallPackage`（还要过 §① 的完整性）→
  `PluginHostBootstrap.Reload()` → 提示；
- 管理页列表项新增 **↻** 按钮（只有写了 `updateUrl` 的插件才显示）。

### ④ 工具与文档

- `tools/plugin-hash.py`（python，只写哈希，`package.sh` 用）、
  `tools/PluginSigner`（C#，生成密钥 / 签名 / 校验，`dotnet run --project tools/PluginSigner -- …`）；
- `docs/PLUGIN-API.md` 新增 **§9 完整性与签名**、**§10 更新检测**（对外文档依旧不含任何主题相关内容）；
- 新增语言键 +13 → 各语言 **3359** 键（完整性状态 4 个 + 更新流程 9 个）。

### 本地实测（可直接复现，不需要 Windows）

```
$ dotnet run --project tools/PluginSigner -- genkey priv.pem pub.txt
  公钥 Base64：MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAE…   keyId：e09a81b5648a…
$ dotnet run --project tools/PluginSigner -- sign Welcome.pclpkg priv.pem
  已签名：Welcome.pclpkg（1 个文件，keyId e09a81b5648a…）
$ dotnet run --project tools/PluginSigner -- verify Welcome.pclpkg trust.json
  文件哈希：通过（1 个文件）／ 签名：通过
$ dotnet run --project tools/PluginSigner -- verify Welcome.pclpkg        # 无信任列表
  签名：公钥 e09a81b5648a… 不在信任列表里
$ 篡改包内 dll 一个字节后再 verify
  !! Welcome.dll 哈希不匹配（期望 19a24afd…，实际 eb762138…）
```

- 版本号：`27.1-snapshot-68(2.15.1)`，`code` 591，25,953,645 B，
  SHA-256 `B9C36E96890E1094AB52697A1DB441C840F785CBEE27123C093EF7DC271D66E1`（`dist/` 有同哈希副本）
- 验证（构建期）：exe 内命中 `PluginIntegrity` / `PluginUpdateService` / `trusted-keys.json` /
  `Tools.Plugin.Integrity.Verified` / `Tools.Plugin.Update.Confirm` / `BtnUpdate`；
  随包 6 个 pclpkg 的 `plugin.json` 均带 `files`（各 1 个文件），签名留空待用户自签；0 error；
  **宿主侧实机表现待日志确认**

## snapshot-66 → 67：内置插件按版本升级 + 设置页/通知候选诊断日志

> 用户反馈「验证第一条失败：根本没有设置页入口」。查证过程与结论都记在这里。

- **结论一：工作区里最新的日志是 snapshot-60（10-31 12:30）**，没有 63/64/65 的日志。
  「设置 → 插件」这个入口是 **snapshot-65 才加的**（`PageSetupLeft` 新分组 + `PageSetupPlugins` 页面），
  所以装 64 及更早版本看不到它是正常的。
- **结论二（本轮真正修掉的坑）：`PackageAlreadyInstalled()` 以前只判「装过就跳过」**，
  于是升级启动器后用户机器上仍然是**旧的插件包**——插件清单里新加的能力（例如
  `ui.settings` / `ui.notify`）永远不生效，设置页就算有入口也会是空状态。
  这解释了为什么「插件能力加了但界面没反应」这类问题会反复出现。
  现在改成**比版本**：内置包版本比已装的高就覆盖安装（`PluginVersion.Compare`），
  插件数据目录（配置）不受影响；同版本或更旧仍然保留用户状态。
- 顺手加了诊断日志（`ModBase.LogLevel.Normal`，会进日志文件）：
  - 启动时：`[Plugin] 提供设置的插件：sgu.welcome(ui.settings), sgu.sample.hello(ui.settings)；声明 ui.notify 的插件：…`
    —— 一眼能看出是插件没声明能力，还是界面没接上；
  - 升级时：`[PluginHost] 内置插件 sgu.welcome 有新版本（已装 1.0.0 → 内置 1.1.0），将覆盖安装`；
  - 打开设置页时：`[Plugin] 设置页已渲染 N 个插件的设置`。

- 版本号：`27.1-snapshot-67(2.15.1)`，`code` 590，25,931,629 B，
  SHA-256 `8DF39FE39F1BD88E9BBDAA43E89A33B5060BBE02FF1986285710E7C3A50A34E1`（`dist/` 有同哈希副本）
- 验证（构建期）：exe 内命中 `Setup.Left.Item.Plugins` / `Setup.Left.Category.Plugins` /
  `TextPluginCategory` / `PageSetupPlugins` / `提供设置的插件` / `将覆盖安装`；
  随包插件清单核对：`sgu.welcome 1.1.0(ui.settings)`、`sgu.sample.hello 1.1.0(ui.settings, ui.notify)`；0 error

## snapshot-65：插件系统第二档（ui.notify + 设置页分区）

> 用户：「第二档直接做，我同时测第一档」。这一档让插件能**弹提示/确认框**，并把插件设置搬进**设置页**。

### ① `ui.notify`：插件弹提示 / 确认框

- SDK 新增 `PCL.PluginSdk/IPluginNotify.cs`：`IPluginNotify`（Info / Success / Warning / Error / `Confirm(title, message, onResult)`）
  + `PluginNotifyLevel` + 宿主侧 `IPluginNotifyHost`；`IPluginContext.Notify` 暴露给插件
- 宿主分两层（插件宿主不依赖启动器程序集）：
  - `PCL.PluginHost`：`PluginNotify` 适配器，做**能力门禁**（没声明 `ui.notify` 时只记警告、不弹）与异常隔离
  - `Plain Craft Launcher 2`：`AppPluginNotifier` —— 提示走 `HintService.Hint`，确认框走 `ModMain.MyMsgBox`，
    两者都 `ModBase.RunInUi` 切回主线程；提示前缀自动带 `[插件id]`
- `IPluginHostEnvironment` 新增 `Notifier` 成员（与 `LanguageCode` 一样由启动器注入）

### ② 设置页分区 `ui.settings`：设置 → 插件

- 把「schema → 控件行」的渲染从管理页里**抽成共享实现** `Modules/Plugins/PluginSettingsForm.cs`
  （`BuildRow` / `BuildRows`），管理页齿轮与设置页共用同一份，不会两处行为不一致
- 新页面 `Pages/PageSetup/PageSetupPlugins.xaml(.cs)`：每个声明了 `ui.settings` 的插件渲染一张卡片
  （标题 = 插件名，首行「来自插件 x（版本）」，下面按 schema 生成控件），没有插件时显示空状态卡片
- 侧栏：设置页新增「插件」分组 + 「插件」条目（`FormMain.PageSubType.SetupPlugins = 12`、
  `ModMain.frmSetupPlugins`、`PageSetupLeft.PageGet` 分支）
- 能力门禁：没声明 `ui.settings` 的插件只在「插件管理 → 齿轮」里出现，不污染设置页
- 语言键 +6 → 各语言 **3346** 键：`Setup.Left.Category.Plugins`、`Setup.Left.Item.Plugins`、
  `Setup.Plugins.Title` / `.Description` / `.Empty` / `.From`

### ③ 示例插件把两个新能力都用上

- `samples/HelloPlugin`（1.1.0）：清单能力补 `ui.settings` + `ui.notify`；
  页面里加了「弹一条提示」「弹一个确认框」两个按钮（确认框结果写日志）；
  设置里加了「启动时弹一条提示」开关（打开后启动完成会弹一条来自插件的提示）

- 版本号：`27.1-snapshot-65(2.15.1)`，`code` 588，25,927,533 B，
  SHA-256 `2A4F7AE80C9E9066DC618FF8E93A47DC98225F49CCB12EBBB27203F9E2882DEA`（`dist/` 有同哈希副本）
- 验证（构建期）：exe 内命中 `IPluginNotify` / `AppPluginNotifier` / `PageSetupPlugins` / `PluginSettingsForm` /
  `Setup.Plugins.From`；`HelloPlugin.pclpkg` 能力为
  `["config","events","ui.page","ui.sidebar","ui.settings","ui.notify"]`，DLL 内命中 `get_Notify` / `Confirm` /
  `hello.notifyOnStart`；0 error（两轮 `csc` 偶发失败，重试通过）；**实机表现待日志确认**

## snapshot-64：插件系统第一档补完（能力/清单 UI/本地化/示例）

> 用户问「插件相关还有哪些没搞的」→ 我列了三档 → 用户：「第一档」。
> 这一档是四个「小口子」，全在宿主侧 + 示例插件，不涉及市场/签名。

### ① `ui.sidebar` 名不副实 → 两者等价

- 以前 `PageToolsLeft` 只判 `PluginCapability.UiPage`，只声明 `ui.sidebar` 的插件什么也拿不到；
- 现在判 `UiPage | UiSidebar` **任一即可**，日志文案也改成「没有声明 ui.page / ui.sidebar 能力」。

### ② `manifest.ui` 终于生效（图标 + 排序）

- 新增 `SvgIconControlHelper.IconExists()`：按 `pack://…/PCL.Core;component/UI/Assets/IconPacks/<pack>/<name>.svg`
  真的去查资源是否存在 —— 之前 `HasSvgIcon()` 只判非空，写错名字会**静默渲染成空白**；
- 管理页列表项的图标改为读清单 `ui.icon`，不存在则退回 `lucide/puzzle`；
- 默认排序改为 `ui.sortOrder`（缺省 100）在前、名称在后；
- **顺手修掉三个一直空白的图标**：`MyPluginItem` 的齿轮 `lucide/settings-2`（不存在）→ `lucide/settings`；
  插件清单里的 `lucide/blend` → `lucide/layers`、`lucide/unlock` → `lucide/shield-check`、
  `lucide/message-circle-heart` → `lucide/message-circle`（已用脚本逐个校验内置图标包，现全部 OK）。

### ③ 插件文案本地化（约定 + 小工具）

- SDK 新增 `PCL.PluginSdk/PluginLocalization.cs`：`Pick(字典, 当前语言, 兜底)`，
  回落顺序 **精确语言 → 主语言 → zh-CN → 表里第一个值 → 形参兜底**（只写中文也不会空白）；
- `IPluginContext.LanguageCode` 暴露当前语言码（`IPluginHostEnvironment` 同步加成员，
  宿主实现读 `LocalizationService.CurrentLanguage.Code`，每次访问都取当前值）；
- **`LanguageChangedEvent` 现在真的会发**：宿主挂 `LocalizationService.LanguageChanged` 转发给插件。

### ④ 示例插件升级成「全功能示范」

- `samples/HelloPlugin`（1.1.0）此前只示范配置；现在一个插件能用到的接口都在里面：
  配置持久化、`IPluginSettingsProvider`（Toggle + Choice + Integer 三种控件）、
  `IPluginUiProvider`（侧栏出现「Hello 示例」页面）、三个宿主事件订阅、
  `PluginLocalization.Pick` 文案、`manifest.ui`（`lucide/sparkles`，`sortOrder = 900`）；
- 清单能力声明为 `["config","events","ui.page","ui.sidebar"]`，正好覆盖这一档改的三处门禁。

- 版本号：`27.1-snapshot-64(2.15.1)`，`code` 587，25,918,829 B，
  SHA-256 `8F5B05AD558CE266834EA3E9A807FE6BB481878BAAB631959B0C0F0ECC393A24`（`dist/` 有同哈希副本）
- 验证（构建期）：exe 内命中 `IconExists` / `LanguageCode` / `PluginLocalization` / `LanguageChangedEvent`；
  `HelloPlugin.pclpkg` 内含 `GetPages` / `GetSettings` / `lucide/sparkles` / `Hello 示例`；
  6 个插件清单的 `ui.icon` 全部通过「图标包是否存在」校验；0 error；**实机表现待日志确认**

## snapshot-63：关于页「PCL Swordsman 团队」增加 DeepSeek

- 用户要求：开发者栏加一条 **DeepSeek**，头像 `DeepSeek.jpeg`，描述
  「一条吃白饭的蓝色大肥鱼，完成了此启动器绝大多数开发任务」，并**排在其他三位之前**
  （明余游戏、Hmjmfabc、SGU 剑客群组服）。
- 实现：
  - 头像 `DeepSeek.jpeg`（1101×1080，104,726 B）原样复制为 `Images/Heads/DeepSeek.jpg`，
    与 `Mingyu.jpg` 同为「原图 + UI 里 34×34 圆形裁剪」，不重采样（省一次画质损失）；
  - `PageSetupAbout.xaml` 的团队卡片 `Grid.RowDefinitions` 由 3 行加到 **4 行**，
    DeepSeek 占 **Grid.Row=0**（文字 + 头像各一处），原三行整体下移一行；
  - 语言键 +2 → 各语言 **3340** 键：`Setup.About.Team.Name.DeepSeek`（DeepSeek）、
    `Setup.About.Team.Info.DeepSeek`（描述；zh-TW 用繁体，en-US / en-GB 用英文，ja/fr/es 按惯例放简体中文）。
- 版本号：`27.1-snapshot-63(2.15.1)`，`code` 586，25,909,101 B，
  SHA-256 `AD80C42785A1A6061BD4570A13E791734345BC49256328DD71EAE560CC3E90A9`（`dist/` 有同哈希副本）
- 验证（构建期）：exe 内命中 `DeepSeek.jpg` / `Setup.About.Team.Name.DeepSeek` / `Setup.About.Team.Info.DeepSeek`；
  语言文件 8 个均 3340 键、无重复；0 error（首轮 `csc` 段错误，重试通过）；**页面观感待实机确认**

## snapshot-61 → 62：修「进插件管理页报错」+ Faded灰装饰看不见 + FadedGray 不再内置

> 用户三条：「前往插件管理页会报错；Faded灰右上角不显示图片。还有Faded灰主题插件不要内置。」
> 先读日志（`Launch-2026-10-31-123028974.log`，Win10 19044 / snapshot-60），三条各有明确证据。

### ① 插件管理页报错 —— 日志实锤，是 Click 处理函数签名写错

```
[12:33:41.597] [ERR!] 切换分页面失败（ID 4）
System.Windows.Markup.XamlParseException: 无法从文本“BtnSettingsClose_Click”创建“Click”。
 ---> System.ArgumentException: Cannot bind to the target method because its signature is not compatible
   at PCL.PageToolsPluginManage.InitializeComponent()
   at PCL.PageToolsPluginManage..ctor()   →  PageToolsLeft.PageGet → PageChange
```

- 根因：`MyButton.Click` 的委托是 `ClickEventHandler(object sender, **MouseButtonEventArgs** e)`
  （`Controls/MyButton.xaml.cs:12`），我写成了 `(object, ModBase.RouteEventArgs)` →
  BAML 绑不上委托，整个页面的 `InitializeComponent()` 直接抛异常，表现为「点插件管理就报错」。
- 修法：改成 `(object sender, MouseButtonEventArgs e)`。**教训**：`MyButton` / `MyIconButton` /
  `MyIconTextButton` / `MyCheckBox` / `MyRadioBox` 的 Click/Check 委托各不相同，
  写 XAML 事件前先去控件源码确认签名（`RouteEventArgs` 只是部分控件的）。

### ② Faded灰 右上角看不见图

- 日志证明**链路是通的**：`[Plugin:sgu.faded-gray] 装饰图已加载：855×527`、
  `[Plugin] 已注册 1 个插件自带主题：faded.gray`、`[Plugin] 已创建插件主题 faded.gray 的标题栏装饰` ——
  控件确实创建并显示了，所以不是「没写」也不是「没加载」。
- 结论是**太淡 + 太窄**：原参数 `Opacity=0.22`、`Width=320`、`BlurEffect(3)`，
  855×527 的横图用 `UniformToFill` 塞进 320×48 → 只截到照片正中间很窄的一条，再叠 22% 透明度和模糊，
  在 `#5C5C5C` 灰色标题栏上基本看不出来。
- 修法（都在插件里，宿主不用动）：`Opacity` 0.22 → **0.34**、`Width` 320 → **420**、
  `BlurEffect` 3 → **6**（更「虚化」）、左右遮罩放宽（0.10/0.30 … 0.72/0.90 才降到 55%）。
- 顺手加了一条尺寸日志：`[Plugin:sgu.faded-gray] 装饰已就位：实际 420×48（期望 420×48），不透明度 0.34`
  —— 下次若还看不见，这条日志能立刻分清是「被裁成 0」还是「只是太淡」。

### ③ FadedGray 不内置（用户要求）

- `Plain Craft Launcher 2.csproj`：把 `FadedGray.pclpkg` 加进 `<EmbeddedResource Include="..\plugins\dist\*.pclpkg">`
  的 `Exclude` 列表 → **不再嵌进 exe**；
- `build.sh`：非正式版往 `<exe>\plugins\` 拷随包插件时**跳过** `FadedGray.pclpkg`，
  并且**拷贝前先 `rm -rf` 该目录**（否则上一次构建残留的插件包会被下一次启动自动装回去 —— 这条是本轮踩的坑）；
- 插件包仍然会产出到 `plugins/dist/FadedGray.pclpkg` 并同步到 `dist/`，**由用户手动安装**。
  已经装过的机器（含用户自己）不会被动删文件，要撤掉请手动删 `PCL\Plugins\sgu.faded-gray.pclpkg`。

- 版本号：`27.1-snapshot-62(2.15.1)`，`code` 585，25,798,509 B，
  SHA-256 `E46AB502DD097DB55B96DF9EF8D07E1FA2239803887A49ED855FC78C69E8DB46`（`dist/` 有同哈希副本）
- 验证（构建期）：exe 内 `FadedGray.pclpkg` 命中 **0**、`artifact-x64/plugins/` 只剩 5 个随包插件、
  `dist/FadedGray.pclpkg` 仍在（732,491 B）且含「装饰已就位」日志与 0.34 不透明度；
  0 error；**插件管理页与装饰观感待实机确认**

## snapshot-59 → 60：插件页面 API + 插件自带主题（Faded灰） + 对外文档去掉主题内容

> 用户三连：「插件系统…前两个」→「继续做第三项」→「测试新主题：Faded灰…主题要内置到插件『Faded灰主题』中。
> 主题 API 已完全开放，`docs/PLUGIN-THEMES.md` 也已并入公开文档。
> 前两条对应的改动（依赖/版本、事件总线、插件设置界面）见 snapshot-58；本条目是**第三项 + Faded灰主题 + 文档隔离**。

### ① 插件页面 API（`IPluginUiProvider`）

- SDK 新增 `PCL.PluginSdk/IPluginUi.cs`：`PluginPageDefinition`（Id / Title / Description / Icon /
  SortOrder / `CreatePage`）+ `IPluginUiProvider`
- 宿主新增页面容器 `PagePluginHost`（`Pages/PageTools/PagePluginHost.xaml(.cs)`，`MyPageRight`）：
  页面头（标题 / 说明 / 来自插件 X 版本）+ 可滚动内容区 + **创建失败兜底卡片**
- `PageToolsLeft` 侧栏在「插件市场」下新增动态分组 `TextPluginPages` + `PanPluginPages`：
  条目按 `ui.page` 能力从已启用插件里生成，插件被禁用 / 删除后**条目与已打开页面立刻收掉**
  （退回插件管理页）；`FormMain.PageSubType.ToolsPluginPage = 6` 作为统一宿主页
- 能力门禁真实生效：没声明 `ui.page` 的插件页面会被忽略并记日志
- 语言键 +3 → 各语言 **3338** 键：`Tools.Left.PluginPages`、`Tools.Plugin.Page.Source`、`Tools.Plugin.Page.Error`
- 示例：`sgu.pack`（SguPack 1.1.0）新增页面「SGU 插件包」，列出它提供的扩展主题与语录

### ② 插件自带主题（`IPluginCustomThemesProvider`）—— 为「Faded灰主题」插件做的后期 API

- 之前插件只能给**宿主内置槽位**换名字 / 换色；现在可以**凭空定义**主题：
  SDK 新增 `PCL.PluginSdk/IPluginThemes.cs`（`PluginCustomThemeDefinition` + `IPluginCustomThemesProvider`），
  能力位新增 `PluginCapability.ThemesCustom`（`themes.custom`）
- 宿主：`ColorTheme.PluginDefined = 16` 作为插件主题统一槽位，具体哪一个由新配置
  `UiLightPluginTheme` / `UiDarkPluginTheme`（字符串 id）决定；
  `ThemeService.SetPluginThemes()` 注册三张表（主色 / 显示名 / flavor），
  `PageSetupUI` 下拉框改成 `ThemeChoice(槽位, 插件主题 id)` 列表，
  `ModSetup.ValidateThemeSelection()` 在插件被卸载后回退龙猫蓝
- **标题栏装饰**：`PluginCustomThemeDefinition.CreateTitleDecoration` 提供的控件由宿主放进
  `PanTitlePluginDecoration`（`ContentPresenter`，右侧、不拦鼠标），切走主题立刻断开引用
- 新插件 `plugins/FadedGray`（`sgu.faded-gray`，**Faded灰主题**）：
  - 主题 `Faded灰`，主色 `#5C5C5C`，id `faded.gray`，能力 `["config","themes.custom"]`
  - 标题栏右上角一张 `Faded.jpg`（855×527，**以 `EmbeddedResource` 编进 DLL**，插件包只有 dll + plugin.json），
    `Opacity=0.22`、`OpacityMask` 左右渐隐（两侧虚化）、叠一层 `BlurEffect(3)` 做柔化
  - 插件包 732 KB（主要是那张图），非正式版构建会随 exe 内嵌并首次启动自动安装

### ③ 对外文档去掉一切主题相关内容

- 当时的用户要求：主题 API 可以开放（技术上可用），但对外文档不提主题 —— **该限制已于 2026-11-01 取消**
- `docs/PLUGIN-API.md` **整篇重写**：去掉 `themes.extended`、`IExtraThemesProvider`、`PluginThemeDefinition`、
  主题槽位白名单、`ThemeRead` 能力、主题排错行，连 `ThemeChangedEvent` 也移除（属于主题相关内容）；
  同时把依赖/版本/事件/设置/页面这些**新能力**补齐
- `docs/PLUGIN-GUIDE.md` 同样清空主题相关段落，改成「想提供数据/界面就实现这些接口」的表格（不含主题）
- 主题机制全部挪进 **`docs/PLUGIN-THEMES.md`**（2026-11-01 起已公开）：
  两套主题接口的区别、插件主题链路表、标题栏装饰约定、槽位对照、配色分派、**对外文档隔离自检清单**
- `docs/PLUGIN-SYSTEM.md` 顶部的保密标记已于 2026-11-01 移除
- 自检：`grep -in "theme\|主题" docs/PLUGIN-API.md docs/PLUGIN-GUIDE.md` **无输出**

- 版本号：`27.1-snapshot-60(2.15.1)`，`code` 583，26,531,693 B，
  SHA-256 `4808B6E323FC00532F400E862A1E6DA6BDB0D9B13687B45D62333877E8A555D8`（`dist/` 有同哈希副本）
  （snapshot-59 是中间产物：切换插件主题时槽位没变、配置事件不触发，补了手动刷新后重出 60）
- 验证（构建期）：exe 内命中 `PanTitlePluginDecoration` / `PanPluginPages` / `Tools.Plugin.Page.Source` /
  `PluginDefined` / `SetPluginThemes` / `RefreshCustomThemes` / `IPluginCustomThemesProvider` / `FadedGray.pclpkg`；
  `FadedGray.pclpkg` 内含 `faded.gray` / `Faded灰` / 内嵌 JPEG 头；0 error；**实机观感与交互待日志确认**

## snapshot-58：插件系统补齐「依赖解析 + 宿主版本校验」与「事件总线 + 插件设置界面」

> 用户：「插件系统现在除了插件市场的部分可以实现了吗」→ 我盘出两处半成品（依赖 / minHostVersion 只记录不强制、
> 能力位没有运行时门禁）和两处没做（事件总线、插件设置界面）→ 用户：「前两个」。

### ① 依赖解析 + `minHostVersion` 校验

- 新增 `PCL.PluginHost/PluginVersion.cs`：版本解析 / 比较 / 范围判定。
  宿主版本 `27.1-snapshot-58(2.15.1)` 只取前导数字段比较（后缀不干扰）；
  依赖范围支持 `*`、`1.2.0`（≥）、`>=`、`>`、`<=`、`<`、`=`、`^1.2`、`~1.2`
- `PluginLoader.ReloadAll()` 重构成**四阶段**（顺序不能乱）：发现（只读清单，包不解压）→ 宿主版本 → 依赖 → 按拓扑序加载
- 判定规则（全部落成 `PluginState` + 可读原因，管理页直接显示在条目副信息里）：
  - `minHostVersion` 高于宿主 → **`Incompatible`**：`需要启动器 99.0.0 或更高（当前 …）`
  - 缺依赖 → **`Faulted`**：`缺少依赖插件 foo（需要 >=1.2.0）`
  - 依赖被禁用 / 依赖出错 / 依赖不兼容 → `Faulted`：`依赖插件 foo 已被禁用` 等
  - 依赖版本不满足 → `Faulted`：`依赖插件 foo 版本不满足：需要 ≥1.2.0，实际 1.0.0`
  - 循环依赖 → `Faulted`：`循环依赖：a → b → a`；自己依赖自己单独判定
- **加载顺序**：Kahn 拓扑排序（依赖在前，同层按名称稳定）；加载前再确认一次依赖**真的可用**，
  依赖加载失败会连带停用依赖它的插件（`依赖插件 foo 不可用（未启用或加载失败）`）
- 日志：每个失败都会打 `[PluginHost] <文件>：<原因>`；成功的加载行末尾会带 `依赖 a,b`

### ② 事件总线（`IPluginContext.Events`）

- SDK 新增 `PCL.PluginSdk/IPluginEvents.cs`：`IPluginEventBus.Subscribe<T>(handler)` + 五个宿主事件 ——
  `HostStartupCompletedEvent`、`ThemeChangedEvent`、`GameLaunchingEvent`、`GameLaunchedEvent`、`LanguageChangedEvent`
- 宿主实现 `PCL.PluginHost/PluginEventBus.cs`：**宿主 publish、插件 subscribe**；
  - 订阅需要清单里声明 **`events` 能力**，否则返回空订阅并记一条警告（这是能力位第一次真正生效）
  - 每个回调单独 try/catch → `[PluginHost] 插件 x 的事件回调抛出异常`，一个插件炸不影响别人
  - 插件卸载 / 重扫时 `UnloadAll()` 里 `Clear()` 一次退订全部，`IDisposable.Dispose()` 也能手动退订
- 宿主侧发布点：`PluginHostBootstrap.Publish<T>()`；
  - 启动完成：插件全部就绪后发 `HostStartupCompletedEvent(版本, 启动次数)`
  - 主题：`ThemeService.ColorThemeChanged` / `ColorModeChanged` → `ThemeChangedEvent`（带展示名）
  - 游戏：`ModLaunch.McLaunchStart()` 发 `GameLaunchingEvent(实例名, 路径)`，`McLaunchEnd()` 发 `GameLaunchedEvent`
- `LanguageChangedEvent` 只有类型，宿主暂未发布（等语言切换有明确钩子再接）

### ③ 插件设置界面（宿主按 schema 渲染）

- SDK 新增 `PCL.PluginSdk/PluginSettings.cs`：`PluginSettingDefinition`（Key / Title / Description / Type /
  DefaultValue / Min / Max / Options）+ `PluginSettingType`（Toggle / Text / Integer / Decimal / Choice）+
  `IPluginSettingsProvider`
- 插件只**声明结构**，读写都走它自己的 `IPluginConfig`（宿主改完立刻写进 `config.json`，插件 `Context.Config.Get` 读回）
- 管理页：`MyPluginItem` 右侧新增齿轮（只有实现了 `IPluginSettingsProvider` 才显示）→
  页面下方展开 `PanPluginSettings` 卡片，按 schema 用现成控件生成（`MyCheckBox` / `MyTextBox` / `MyComboBox` / `MySlider`），
  带防回环 guard（代码设值不触发保存）
- 语言键 +4 → 各语言 **3335** 键：`Tools.Plugin.Action.Settings`、`Tools.Plugin.Settings.Title`（`{0}` 插件名）、
  `Tools.Plugin.Settings.Empty`、`Tools.Plugin.Settings.Close`
- `PluginDescriptor` 新增 `Config`（宿主读写插件配置）与 `SettingsProvider`（`Instance as IPluginSettingsProvider`）

### ④ 顺手把 `sgu.welcome` 插件做成两个新能力的示范

- `plugins/Welcome/WelcomePlugin.cs` 升级 **1.1.0**：实现 `IPluginSettingsProvider`（开关 + 来源切换）、
  订阅 `ThemeChangedEvent`（切主题打一条日志）；`plugin.json` 的能力加入 `events`
- 设置真正生效：`GetWelcomeMessages()` 按配置现算 —— 关掉返回空池（宿主不弹），切「内置」用插件自带那几条

- 版本号：`27.1-snapshot-58(2.15.1)`，`code` 581，25,775,981 B，
  SHA-256 `0C5624B74717C971DD915B9532FBE51E808E9BECB4E22BFE5B5884067E2E07B1`（`dist/` 有同哈希副本）
- 验证（构建期）：主程序集内命中 `PanPluginSettings` / `BtnSettings` / `Tools.Plugin.Settings.Title` /
  `HostStartupCompletedEvent` / `ThemeChangedEvent` / `GameLaunchingEvent`；`PCL.PluginHost.dll` 内命中
  `依赖插件` / `循环依赖` / `需要启动器` / `缺少依赖插件` / `PluginEventBus`；`Welcome.pclpkg` 内 `GetSettings` /
  `welcome.enabled` / `welcome.source` / `ThemeChangedEvent` 均在，`capabilities = ["config","events"]`；
  0 error；**实机行为待日志确认**

## snapshot-57：修「樱花粉一片花瓣都不飘」

- 症状（用户）：「樱花粉根本没有花瓣飘落」
- 最可能的根因：**本程序是单文件发布**（`PublishSingleFile=true`），而 `ModTitlePetals.EnsureSprites()`
  一开始用的是**相对** pack URI（`new Uri("/Plain Craft Launcher 2;component/Images/Particles/cherry_0.png", UriKind.Relative)`）。
  单文件下相对 URI 要靠 `Application.ResourceAssembly` 解析，解析不到就抛异常 → 12 张精灵全部加载失败 →
  `Start()` 里 `if (Sprites.All(null)) return;` 直接返回，**连定时器都没建**，所以一片花瓣也没有。
  （仓库里其它地方加载内嵌图都用**绝对** pack URI：`ModBase.pathImage` / `ModOneClickLaunch.RenderPng`，我这次没照着写。）
- 修法（四层保险）：
  1. 改绝对 pack URI：`pack://application:,,,/Plain Craft Launcher 2;component/Images/Particles/cherry_N.png`
  2. 退一步走 `ModBase.GetResourceStream("Images/Particles/cherry_N.png")` 拿流
  3. 再退一步直接读程序集的 `.g.resources` 清单（`System.Resources.ResourceReader`，键 `images/particles/cherry_N.png`）
  4. 三样全挂 → 现画一张 **3×3 内置兜底花瓣**（原版同款两种粉），保证「至少能看到花瓣在飘」
- 另外两处加固：
  - `Start()` 里先 `host.UpdateLayout()` 强制布局，避免画布还没测量（`ActualWidth == 0`）时把 14 片粒子全堆在左上角；
    再配合原有的 `_needsSpread` 二次铺开
  - `ModTitlePetals.SetEnabled()` 整体包 try/catch（`LogLevel.Normal`）：粒子出问题不能连累标题栏其它装饰与主题切换
- 版本号：`27.1-snapshot-57(2.15.1)`，`code` 580，25,742,701 B，
  SHA-256 `D997ED7320DE73B1289866AEFD84E17FA0AC4EA0CBC1658C6B0459C436523B97`（`dist/` 有同哈希副本）
- 日志证据（都进日志文件，除最后一条是 DBG）：
  - `[Petal] 樱花飘落已启动：14 片粒子、12 张贴图`
  - `[Petal] 原版樱花粒子贴图一张都没加载成功，已改用内置 3×3 兜底花瓣（…）`
  - `[Petal] 画布就绪（260×48），粒子已铺开`
  - `[Petal] 启动樱花飘落失败`（异常详情）

## snapshot-56：改用**原版材质**，樱花换成原版樱花粒子

> 用户：「我好像说过要复用原版材质；樱花的话你就把原版的樱花粒子效果复用到右上角吧」

**原版材质（不再自己画）**

- 从原版客户端 jar 里解出（`tools/extract-vanilla-textures.py`，参数是 `client.jar` 路径）：
  - `assets/minecraft/textures/block/nether_wart_block.png` → `Images/Blocks/NetherWartBlock.png`（16×16，5 色）
  - `assets/minecraft/textures/block/amethyst_block.png` → `Images/Blocks/AmethystBlock.png`（16×16，7 色）
  - `assets/minecraft/textures/particle/cherry_0..11.png` → `Images/Particles/cherry_*.png`（12 张 **3×3** 精灵）
- 两张方块贴图现在以 **16×16 原尺寸平铺**（`ImageBrush` + `Viewport 16,16` + `Stretch=Fill`），
  不做插值放大，像素点与游戏里一致；之前手绘的 `nether_wart_block.png` / `amethyst_block.png` 已删除
- 说明：这三个主题的观感从此完全由原版贴图决定，不再受"我看不见图"影响

**樱花粉：静态樱花图 → 原版樱花粒子**

- 删掉 `Images/sakura_blossom.png` 与 `ImgTitleSakura` 静态图，改成右上角一块 **260×48 的画布** `PanTitlePetals`，
  由新模块 `Plain Craft Launcher 2/Modules/UI/ModTitlePetals.cs` 驱动
- 复刻原版（Minecraft 1.20+ 樱花树叶）的行为：随机取 12 张精灵之一、缓慢下落、左右轻摆（正弦）、缓慢自旋、
  生命周期里淡入淡出；精灵按 **3~5 倍最近邻**放大成 9~15 px，保持像素颗粒感
- 参数（都在模块顶部常量里）：同屏 14 片、下落 14~28 px/s、寿命 2.4~5.4 s、单片最大不透明度 **0.30**
  （与剑客黄钻石剑的 0.22 同一量级，只作背景点缀）
- 生命周管理：切走主题 / 窗口关闭 → `Stop()` 停掉 `DispatcherTimer` 并清空画布；窗口最小化时跳帧不计算
- 首次 Start 时画布还没布局（`ActualWidth == 0`），所以加了 `_needsSpread`：拿到有效宽度后再把粒子整体撒开

- 版本号：`27.1-snapshot-56(2.15.1)`，`code` 579，25,738,605 B，
  SHA-256 `BCF31F3E782791CAEBA7AE1D16B5D10248140DC36AF0E31506DDA88DB41EFF3F`（`dist/` 有同哈希副本）
- 验证（构建期）：主程序集内命中 `Images/Blocks/NetherWartBlock.png` / `AmethystBlock.png` /
  `Images/Particles/cherry_0.png` 等资源名 + `PanTitlePetals`；0 error；**粒子观感待实机确认**

## snapshot-54 → snapshot-55：樱花改像素画 + 三个新主题（下界红 / 甲基橙 / 水晶紫）

> 用户：「为什么樱花不是仿 Minecraft 的像素樱花？」→ 重画成像素画；
> 用户：「再做新主题：下界红（整个标题栏覆盖低透明度地狱疣块贴图）、甲基橙（Minecraft 字体写的化学式放右上角，依旧低透明度）、
> 水晶紫（整个标题栏覆盖低透明度紫水晶块纹理）」；
> 另有一条约束：「不要**为了甲基橙化学式**在启动器里内置 Minecraft 字体」→ 化学式改成**手绘像素字并烘进 PNG**，不引入任何字体文件。

**樱花改回像素画**

- `Images/sakura_blossom.png` 由矢量重绘为 **MC 画法**：24×8 格 × 6px = 144×48，逐格量化
  （覆盖率 < 0.72 的格子当深粉描边、内部按采样色填），标题栏里 `NearestNeighbor` 放大到 `Height=48`
- 构图：一枝横贯的枝条 + 主花 + 次花 + 花苞 + 飘落花瓣；合成顺序是 花 > 枝 > 空，
  否则枝条会把花瓣切掉（第一版就踩了这个坑，见 ASCII 自检输出）
- 色板仍是樱花披风那 5 个色；`Opacity` 仍是 **0.22**（与剑客黄的钻石剑一致）
- 生成脚本：`_tmp/png/make_title_art.py`（四张素材全在里面，可重跑）

**四个新素材（全部像素风，都不依赖字体文件）**

| 素材 | 尺寸 | 用法 |
| --- | --- | --- |
| `Images/sakura_blossom.png` | 144×48 | 樱花粉：标题栏右侧樱花枝条 |
| `Images/nether_wart_block.png` | 48×48（可平铺） | 下界红：整条标题栏 `ImageBrush` 平铺，`Opacity=0.28` |
| `Images/amethyst_block.png` | 48×48（可平铺） | 水晶紫：整条标题栏 `ImageBrush` 平铺，`Opacity=0.28` |
| `Images/methyl_orange_formula.png` | 159×27 | 甲基橙：右上角像素字 `C₁₄H₁₄N₃NaO₃S`，`Opacity=0.22` |

- 化学式字形是脚本里手写的 5×7 点阵（`GLYPHS` / `SUBGLYPHS`），下标用 3×5 点阵下移排版；
  **没有**内嵌 Minecraft 字体，也没读系统字体 —— 换字形只改脚本里的点阵
- 注意「Na」里的那个 `N` **不带下标**，只有 `C₁₄H₁₄N₃` 与 `O₃` 有下标（第一版给两个 N 都加了下标，已修）

**三个新主题**

| 主题 | 枚举 | 主色 | 标题栏装饰 |
| --- | --- | --- | --- |
| 下界红 | `NetherRed = 13` | `#B3392E` | `RectTitleNether`：整条标题栏铺地狱疣块贴图 |
| 甲基橙 | `MethylOrange = 14` | `#EE7B1E` | `ImgTitleFormula`：右上角化学式像素字 |
| 水晶紫 | `AmethystPurple = 15` | `#9A5CE0` | `RectTitleAmethyst`：整条标题栏铺紫水晶块贴图 |

- 三个主题都进 `IsHexTheme()`（按档位加权配色），由 `SguPack` 插件声明（`themes.extended`），
  `FormMain.RefreshTitleSword()` 现在统一管五套装饰，切主题即时显隐
- 语言键 +3 → 各语言 **3331** 键：`Setup.Ui.Theme.Color.NetherRed` / `.MethylOrange` / `.AmethystPurple`
- `plugins/SguPack/plugin.json` 的 `description` 一并补上新主题名

- 版本号：`27.1-snapshot-55(2.15.1)`，`code` 578，25,697,645 B，
  SHA-256 `EA6B6B8D9B955C813B7E0DE86734CB008D93F200EEF15F271B3B959C768B8388`（`dist/` 有同哈希副本）
- 验证（构建期）：主程序集内命中四张素材的资源名 + `RectTitleNether` / `RectTitleAmethyst` / `ImgTitleFormula`；
  `SguPack.dll` 内命中 `NetherRed` / `MethylOrange` / `AmethystPurple` / `SakuraPink` 与「下界红/甲基橙/水晶紫/樱花粉」；
  0 error；**观感待实机确认**

## snapshot-52 → snapshot-53：小弹窗真·毛玻璃 + 新主题「樱花粉」

### snapshot-52：右下角小弹窗（Toast）其实一直没有模糊

- 症状（用户）：「液态玻璃下右下角小弹窗没有模糊」→ 追问「Aero 下也没有模糊，你应该根本没写」
- **用户说得对**：`ModOverlayMaterial.ApplyToastSurface()` 之前只设了一条**半透明底色 + 一圈描边**，
  从头到尾**没有**任何模糊。对话框看起来是糊的，是因为它走的是全窗口的 `PanMsgBackground`（`BlurBorder`），
  跟卡片一样；小弹窗浮在内容之上，没有这层背景，于是只剩「换了个颜色」。
- 修法：给小弹窗自己加一层 `BlurBorder`（`MyToast.xaml` 的 `RectToastBlur`，`Grid.RowSpan=2`、
  圆角 8、`Collapsed` 常驻），材质生效时由 `ApplyToastSurface()` 打开：
  - `BlurRadius` = 材质模糊半径（液态玻璃 ×1.2）；用户把材质模糊设成 0 时仍给 14，保证「糊」得出来
  - 底色改成 **上淡下浓的竖向渐变**（`ToastSurfaceBrush`）：均匀半透明会把模糊层整个压平，渐变才能一直露出上沿的糊
  - 把 Toast 自身底色清成 `Transparent` —— 不清就会被主题底色（不透明）盖在模糊层上面
  - 关闭材质：`ClearToastSurface()` 收起模糊层、去掉描边，底色交还 `UpdateColors()`
- 日志证据（新增）：`[Material] 小弹窗毛玻璃：半径 xx px，底色 alpha 0.85（液态玻璃）`
- 版本号：`27.1-snapshot-52(2.15.1)`，`code` 575，25,689,453 B，
  SHA-256 `6B88B1133E5AFC7A6F3678DF86E308F51806BE765A187BD8DE2A55CF60542DD4`
- 验证（构建期）：产物内命中 `小弹窗毛玻璃`（UTF-16）/ `RectToastBlur` / `ToastSurfaceBrush`；0 error；
  **视觉表现待实机日志与用户确认**

### snapshot-53：新主题「樱花粉」（SakuraPink）

- 取色自用户提供的 **樱花披风纹理**（`Cherry_Blossom_Cape (texture).png`，64×32）：它只用 5 个颜色，
  全部照抄：深粉 `(251,108,196)`、中粉 `(255,148,214)`、淡粉 `(255,179,236)`、浅粉 `(254,207,249)`、近白 `(255,241,255)`
- 主题主色 **`#FF94D6`**（披风的中粉）；`ColorTheme.SakuraPink = 12`
- **专属配色 ramp `sakura-bloom`**：逐档把色相往紫红方向压 —— 樱白粉 → 樱花粉 → 桃红 → 梅红 → 深梅 → 梅紫，
  与「剑客黄」的 `golden-dusk` 同一套加权算法（`BuildWeightedColors`），只作用于 2~5 档，文字档与背景档不染
- **标题栏右上角装饰**：**自绘**樱花枝条 `Images/sakura_blossom.png`
  （144×144，矢量超采样 4×4 绘制：五瓣、花瓣带缺刻、枝条 + 花苞 + 飘落花瓣，色板就是披风那 5 个色），
  `Opacity` 与剑客黄的钻石剑**完全一致 = 0.22**，同样 `Height=144` + 居中 ⇒ 上下被 48px 标题栏裁掉；
  另配一层樱粉柔光渐变 `RectTitleSakura`
- 显隐：`FormMain.RefreshTitleSword()` 同时管两套装饰（切主题即时切换，互不干扰）
- 插件侧：`plugins/SguPack/SguPackPlugin.cs` 增加 `SakuraPink` 主题定义（`Hex` / `Flavor=sakura-bloom` / 显示名「樱花粉」）
  —— 主题仍然由插件提供（`themes.extended`），宿主只负责渲染
- 语言键 +1 → 各语言 **3328** 键：`Setup.Ui.Theme.Color.SakuraPink`（zh-CN/zh_sgu/ja/fr/es「樱花粉」、zh-TW「櫻花粉」、en-US/en-GB「Sakura Pink」）
- 生成脚本留在 `_tmp/png/make_sakura_vec.py`（可再生 / 可改构图）
- 版本号：`27.1-snapshot-53(2.15.1)`，`code` 576，25,697,645 B，
  SHA-256 `B53AA9E7D62C63A64CAAF857DF2B51D55F10C29939DB65CFC69BB391DA986761`（`dist/` 有同哈希副本）
- 验证（构建期）：0 error；产物内命中 `SakuraPink` / `sakura-bloom` / `Setup.Ui.Theme.Color.SakuraPink` /
  `#FF94D6` / `樱花粉` / `sakura_blossom`（资源名在主程序集里也命中）；**配色观感与构图待实机确认**

## snapshot-44 → snapshot-51：液态玻璃收尾与品牌名统一（合并记录）

> 这一段的用户需求是**连续追加**的（「侧边栏也要能调不透明度」→「开做液态玻璃」→「重做液态玻璃」→「补坑」→
> 「一键启动快捷方式」→「自定义信息默认值」→「实例独立设置药丸也要玻璃」→「对话框和小弹窗也要能调不透明度」
> →「徽标补上 + 小弹窗没生效」→「关闭提示的答案改一下、项目名大写」），中间几次构建共用版本号跳跃，
> **逐号对应关系没有全部落盘**（`snapshot-45` 的产物还被外部同步干扰删过一次），所以这里按**功能**记录，
> 能确定的号已经标出。产物清单见 `artifact-x64/`（41/43/44/46/48/49/50/51）。

**液态玻璃（`MaterialKind.LiquidGlass`）最终形态** —— 设计语言，别再动

- **整块启动器 = 一块大圆角玻璃**：底色 tint + 顶部 sheen + 内描边 hairline（`ModLiquidGlass`）
- **侧边栏与主界面之间 = 一条玻璃竖线**（`RectLeftShadow`），侧边栏自己**不再上材质**
- **侧边栏的每一个项 = 普通行**（snapshot-44 把这层玻璃拆掉了，恢复「整块样式」）
- **主界面的每一张卡片 = 一块小玻璃**（`MyCard` 级别）
- **正版 / 离线 / 外置药丸**与设置页「实例独立设置」药丸也是玻璃（`ApplyLoginPills` / `ApplySwitchPill`）
- 折光（`RefractionBorder`）在内容**下方**、`Source = ImgBack`，默认强度 **0**（用户要求「别开」）
- 所有透明度都进设置：`UiMaterial*Opacity` 一族 + 侧边栏 / 卡片倍率；关掉材质必须**还原 100% 与主题色**

**这一段补齐的功能**

| 功能 | 落点 |
| --- | --- |
| 侧边栏 / 卡片不透明度可调 | `Config.Main.UiMaterialSidebarOpacity` / `UiMaterialCardOpacity`（倍率语义） |
| 玻璃圆角可调 | `UiMaterialGlassCornerRadius`（默认 16）、`UiWindowCornerRadius`（默认 6） |
| 对话框 / 右下角小弹窗材质量可调 | `ModOverlayMaterial` + `UiMaterialDialogOpacity`(90) / `UiMaterialToastOpacity`(85) / `UiMaterialPillOpacity`(55) |
| 一键启动快捷方式 | `ModOneClickLaunch`：快捷方式名 = **实例名**、图标 = 该实例的 MC 图标、参数 `--launch` |
| 自定义信息默认值 | `Config.LaunchArgumentInfo` 默认 `PCL Swordsman` |
| 关于页剑客群组服徽标 | `swordsman-about.png`（992²）缩到 256² 存为 `Images/Heads/Swordsman.png`，放进「PCL Swordsman 团队」卡片第一行 |
| 小弹窗没套到液态玻璃 | `MyToast.UpdateColors()` 结尾重新调 `ModOverlayMaterial.ApplyToastSurface(this)`（`Loaded` 时机太早会被主题色覆盖） |

**snapshot-50**（`code` 573，25,689,453 B）

- 关于页「PCL Swordsman 团队」卡片 + 徽标补齐；右下角小弹窗液态玻璃修复（同上）

**snapshot-51**（`code` 574，25,689,453 B，SHA-256 `A5BFD368935616E7FABE123A46980BBDFFC24EAB2EE4A86BC98EC50605F48C2C`）

- **关闭启动器提示的答案放宽**：接受 `Plain Craft Launcher 2 Swordsman`，也接受不带「2」的
  `Plain Craft Launcher Swordsman`；比较前**去掉所有空白并转小写**（`NormalizeAnswer`），
  答对即 `States.Hint.CEMessage = false`，答错仍然提示「不太对哦……」
- **项目名小写 `swordsman` → `Swordsman`**（4 处）：`FormMain.xaml` 的窗口 `Title` +
  下游标识注释、`metadata.json` 的 `name`（关于页 `ItemAboutPcl` 的 `Title="{Binding Name}"` 读的就是它）、
  `Plain Craft Launcher 2.csproj` 的 `AssemblyTitle` / `Product`、`AumidHelper` 的 `DisplayName`
- 顺手删掉仓库根下一份构建残留的 `Plain Craft Launcher 2_nfqn41fa_wpftmp.csproj`（里面还是旧小写名）
- 验证（构建期）：产物内**小写 `Plain Craft Launcher 2 swordsman` 命中 0 次**、
  `Plain Craft Launcher 2 Swordsman` 9 次、短名 `Plain Craft Launcher Swordsman` 1 次（UTF-16 字面量）；
  0 error；**实机表现待用户日志确认**

## 27.1-snapshot-27：修复本地化重复键导致的「全部文字消失」

- 版本号：`27.1-snapshot-27(2.15.1)`，`code` 550
- 产物：`artifact-x64/PCLCE For SGU 27.1-snapshot-27(2.15.1).exe`，23,851,112 B，
  SHA-256 `F118D47F1B61DB22054A21C8C2770C10BF25627F43DC766BDA943916DEC6C4D6`（`dist/` 有同哈希副本）
- 根因：snapshot-26 给四个语言文件补 `Download.RegionBlocked.Message` 时写入了重复的 `x:Key`，
  `ResourceDictionary` 加载抛 `ArgumentException: Item has already been added`；PCL 的本地化加载失败只记一条
  `WARN`，于是界面所有文字变空。删掉重复行后恢复正常。
- 防复发：`tools/build.ps1` 与 `build.sh` 构建前校验本地化 XAML 的 `x:Key` 唯一性（重复即构建失败）；
  `tools/build.ps1` 同时补上「先跑 `plugins/package.ps1` 打包插件」这一步，与 `build.sh` 对齐。
- 验证：`_tmp/probe-lang`（WPF `XamlReader`）逐个加载 7 个语言文件全部通过，键数 3263/3263/3207/3207/248/16/2。

## 进行中（未发版）

### snapshot-43：补完三个遗留坑（⑦ 圆角可调 / ② 自定义主题拆键 / ⑨ 一键启动）

> 用户：「你不是还有几个没做的坑吗，现在补上」。对应交接书 §8 的未完成清单。

**⑦ 启动器圆角大小可调**

- 新配置 `UiWindowCornerRadius`（0~24，默认 **6**，与上游一致）与 `UiMaterialGlassCornerRadius`
  （0~24，默认 **16**，仅液态玻璃模式）
- `ModMaterial.CornerRadius` 统一出有效圆角；`ApplyCornerRadius()` 更新 `RectForm`，
  材质生效时顺带把窗口区域按新圆角重裁（否则磨砂会露出直角边）
- UI：个性化页「基础」卡片新增 **窗口圆角** 滑条；液态玻璃分组新增 **玻璃圆角** 滑条
- `ModSetup` 订阅两项配置；`ApplyAll()` 启动时也应用一次

**② 自定义主题色拆出独立功能键**

- 新功能键 **`themes.custom-color`**（`PluginFeatureKeys` ↔ `FeatureFlags.Keys` 两处同步，`All` 也加了）
- `ThemeService.IsCustomColorEnabled`；`IsThemeSelectable(Custom)` 改成只看这个新键，
  其余主题仍看 `themes.extended`
- `PageSetupUI` 的「自定义主题色」行改判新键
- `plugins/SguPack/plugin.json` 的 `provides` 加上 `themes.custom-color`
  → 现在可以「只给主题不给自定义色」（或反之）

**⑨ 百宝箱「一键启动」快捷方式**

- 命令行：`PCLCE For SGU.exe --launch` → `Application` 解析后由 `ModOneClickLaunch.Request()` 记录，
  `FormMain_Loaded` 里 `Initialize()`
- 触发时机：等 **实例列表加载完成** 且启动按钮 `IsEnabled` 变 true 后再调 `LaunchButtonClick()`；
  没实例 / 账号未就绪则保持主窗口（不静默失败）
- 账号策略：用**最后登录的档案**（`ProfileService.LastUsedProfile`），快捷方式因此**不需要参数**
- 百宝箱新增按钮「一键启动快捷方式」→ 选桌面 / 开始菜单 → `Files.CreateShortcut(..., "--launch")`，
  图标取自身 exe
- 语言键 +8（`Tools.Test.Shortcut.OneClick*`）×8 个语言文件 → 各 **3316 键**

- 版本号：`27.1-snapshot-43(2.15.1)`，`code` 566
- 产物：`artifact-x64/PCLCE For SGU 27.1-snapshot-43(2.15.1).exe`，25,279,853 B，
  SHA-256 `B94F2C0E56EFAF54DA36822C83DE8388C8148AA592821EC32C951E1D905C0B69`（`dist/` 有同哈希副本）
- 验证（构建期）：0 error；产物内 UTF-16 命中 `--launch` / `themes.custom-color` /
  `UiWindowCornerRadius` / `UiMaterialGlassCornerRadius` / `OneClickLaunch`；
  `plugins/dist/SguPack.pclpkg` 的 `provides` 已含 `themes.custom-color`
- ⚠️ 未实机验证：圆角滑条的观感（尤其玻璃模式 0 与 24 两端）、一键启动快捷方式的实际启动链路


### snapshot-42：液态玻璃重做（按 demo 源码对齐设计语言）

> 用户要求：「重做一下液态玻璃，工作区有 demo 源码供参考；一个改动就是**侧边栏控件不要再做成玻璃**，恢复整块样式；
> 玻璃分隔线和整块启动器是一个大玻璃、以及主界面卡片是小玻璃的设计语言**不要动**；
> 顺便**开始时正版/离线/外置的药丸也做成玻璃**。」

**参考源码**：`liquid-glass-main/`（Rust + Win32 + D3D11 + DXGI Desktop Duplication，198 KB）。
它用**桌面捕获 + 像素着色器**做折射（SDF 轮廓、位移场、RGB 色散、边缘高光 / 底部暗边、AA），
WPF 既没有桌面捕获通道也吃不到自定义着色器（只认 `fxc` 预编译的 `ps_2_0`），所以这里只搬它的**视觉配方**：

| demo 参数 | 本项目的对应做法 |
| --- | --- |
| `TOP_LIGHT_STRENGTH` / `LOWER_SHADOW_STRENGTH` | 1px 描边改成**竖向渐变**：顶边白色 specular → 中部极淡 → **底部转黑**（压暗），一条描边同时给两种光效 |
| `displacement_scale`（位移场，越大边缘挤压越强） | `UiMaterialGlassRefraction` 强度 → 缩放 Viewbox 的放大倍数 |
| 边缘 SDF + 内衰减 | 折射改成**四条边的渐变带**（绝对坐标线性遮罩，外侧不透明 → 内侧 0），比原来「一圈环带」更接近透镜，不会有硬接缝 |
| `corner_radius 32 / AA` | 圆角仍取 16（与既有设置一致），WPF 自带 AA |

**改动明细**

1. **侧边栏项不再是玻璃**：删掉 `ModLiquidGlass` 里整套「侧边栏项小玻璃」逻辑
   （`ApplySidebarItems` / `StyleItem` / `ClearItem` / `IsInSidebar` / `OnItemLoaded` 与 `MyListItem` 的 `Loaded` 挂钩），
   `MyListItem` 恢复原来的整行样式（`ColorBrushSemiTransparent` + 悬停高亮）
2. **登录药丸变成玻璃**：`ModLiquidGlass.ApplyLoginPills()` 把 `PanLoginTabs` 里的三个
   `MyRadioButton`（正版 / 离线 / 外置；它本身就是 `Border`，圆角 13.5）改成玻璃着色 + 1px 高光边，
   原值存在 `ConditionalWeakTable` 里，退出玻璃模式时还原
   ⚠️ 这些控件的 `Tag` 被用作选项卡索引，**不能**拿来存原始样式
3. **保留**：整块大玻璃（含玻璃竖线）、主界面卡片小玻璃、悬停发光
4. 参数改名：`UiMaterialSidebarItemOpacity` → **`UiMaterialPillOpacity`**（设置页滑条与语言键同步改名，
   8 个语言文件各 3304 键不变）
5. `RefractionBorder` 重写渲染：`ZoomViewbox()` 缩小 Viewbox 实现放大（不引入变换，裁剪 / 遮罩保持精确）+
   `DrawEdge()` 四条绝对坐标渐变带

- 版本号：`27.1-snapshot-42(2.15.1)`，`code` 565
- 产物：`artifact-x64/PCLCE For SGU 27.1-snapshot-42(2.15.1).exe`，25,263,469 B，
  SHA-256 `45040B52B13703326FDD6194443479F7465809C54601DD72CECB8CF4BA4B2A04`（`dist/` 有同哈希副本）
- 验证（构建期）：0 error（`csc` 段错误重试两次后通过）；`ApplyLoginPills` / `ClearPills` / `PillOpacity` /
  `DrawEdge` / `ZoomViewbox` 均在产物内；侧边栏玻璃相关符号已彻底消失
- ⚠️ 未实机验证：药丸玻璃观感、四边渐变折射的实际效果（折射默认仍为 0，需要在设置里手动调大才生效）


### snapshot-41：把用户在语料 txt 里的修改全部落地

> 用户在 `TEXT-POOLS-2026-10-29.txt` 上直接改，要求「把对它的更改全部应用到 PCL Swordsman 中」。
> 做法：把 txt 当作**新版本语料**逐项与源码对比（`difflib` 对齐 tips，逐键对比弹窗），只落地真实改动。

**① Tips 语料（`Plain Craft Launcher 2/Resources/hints/zh-CN.txt`，525 → 526 条）**

- 新增 1 条：`PCL Swordsman支持液态玻璃特效了，真的假的？`
- 修改 2 条：
  - `PCL CE 由 PCL-Community 基于 PCL 官方版开发，与龙腾猫跃没有隶属关系。`
    → `PCL Swordsman 由 SGU 基于 PCL CE开发，与龙腾猫跃和 PCL-Community 没有隶属关系。`
  - `PCL CE 支持高斯模糊了。` → `PCL Swordsman 支持Aero特效了。`
- 文件格式保持原样（每条之间一个空行、首行空行），写回后与 txt 逐条完全一致
- ⚠️ `Resources/hints/en-US.txt` 未同步（英文语料是独立翻译，用户只改了中文）；需要时再补

**② 弹窗文案（4 个键）**

| 键 | 改动 |
| --- | --- |
| `Main.Eula.Message` | `在使用 PCL CE 前…` → `在使用 PCL Swordsman 前…` |
| `Main.UpdateLog.Title` | `PCL CE 已更新至 0 1` → `PCL Swordsman 已更新至 0 1` |
| `Launch.Right.CommunityHint.Message` | 「对非SGU玩家体验可能不友好」→「对新手玩家体验可能不友好」；「请向服主团队反馈」→「请向PCLS开发团队反馈」；空行也按新版排版 |
| `Update.CommunityNotice.Body` | 整段重写：改成「PCLCE 的下游项目」口径、特性清单新增「加入了插件和插件市场」「新增若干功能」等 |

落地范围按语言文件的性质区分：

- **简体中文正文**写入 `zh-CN`、`zh_sgu`（全量副本）以及 `ja-JP`/`fr-FR`/`es-ES`（这三个本来就是简体中文副本）
- `en-US` / `en-GB` / `zh-TW` 只做**品牌替换**（`PCL CE` → `PCL Swordsman`），其余措辞保持各自的译文
  —— 用户这次改的是中文措辞与内容，翻译待后续跟进

**③ 欢迎语池**：txt 里的 9 条与插件默认池完全一致，无需改动

- 版本号：`27.1-snapshot-41(2.15.1)`，`code` 564
- 产物：`artifact-x64/PCLCE For SGU 27.1-snapshot-41(2.15.1).exe`，25,259,373 B，
  SHA-256 `2BB77410E4214CA70697706E669E34941B200885BDF06E1E7F9F80A8ACCCA863`（`dist/` 有同哈希副本）
- 验证：本地化 8 个文件各 3304 键、XML 可解析、无重复 `x:Key`；产物内已确认包含新 tips 两条、
  `在使用 PCL Swordsman 前`、`PCL Swordsman 已更新至`、`加入了插件和插件市场`


### snapshot-40：内置插件扩到三个 + 主题下拉框修复 + 新插件「欢迎语」+ 语料导出

> 用户这一轮的六件事：① 高级材质插件也内置；② 禁用 SGU 主题包后下拉框仍显示非蓝色主题（且应回退龙猫蓝）；
> ③ 新插件「欢迎语」（右下角小提示、可自定义池、默认池等概率）；④ Dump 启动 Tips / 首次弹窗 / 更新弹窗文案。

**① 高级材质 + 欢迎语也内置（`Plain Craft Launcher 2.csproj` + `PluginHostBootstrap`）**

- csproj 里 `AdvancedMaterials.pclpkg` 与 `Welcome.pclpkg` 改成与 `PCLSCore.pclpkg` 一样**无条件内嵌**；
  条件内嵌的 `*.pclpkg` glob 把这三个都 `Exclude` 掉，避免重复
- `EnsureBundledPluginsInstalled()`：正式版现在会安装这三个内置包并**默认启用**；
  `exe 同级 plugins\` 目录仍然只在非正式版自动安装
- 主页提示（`Launch.Right.CommunityHint.Message`）保持原样未删

**② 主题下拉框 bug（`PageSetupUI` + `ModSetup`）**

- 原因：下拉框绑的是 `ThemeColors`（依赖「扩展主题」功能键），停用 SGU 插件包后只改了 `SelectedIndex`，
  **没有重新绑定 `ItemsSource`**，所以非蓝色主题的名字仍然留在列表里
- 修复：新增 `RefreshThemeCombos()` —— 重建候选列表 + 校验当前选中项，`Reload()` 与功能键变化时都会调用
- 新增 `ModSetup.ValidateThemeSelection()`：订阅 `FeatureFlags.Changed`，插件的「扩展主题」被停用后，
  若当前浅色 / 深色主题不可选，**立刻回退到龙猫蓝**（默认主题）并刷新配色

**③ 新插件「欢迎语」（`plugins/Welcome/`，id `sgu.welcome`）**

- 功能键 `welcome.messages`（`PluginFeatureKeys` 与 `FeatureFlags.Keys` 两处同步）
- 插件只**提供池**（`IWelcomeMessagesProvider`），抽取与显示由宿主 `Modules/Plugins/ModWelcome.cs` 完成：
  等概率抽一条 → 替换占位符 → 用右下角小提示（`HintService`）显示，每次启动一次
- 占位符语义（用户明确）：
  | 占位符 | 含义 |
  | --- | --- |
  | `LAUNCHER_BOOT_COUNT` | PCL Swordsman 的启动次数（`States.System.StartupCount`） |
  | `TIME` | 午别：**上午 / 中午 / 下午 / 晚上**（5–11 / 12–13 / 14–17 / 18–4） |
  | `PLAYERNAME` | 玩家名，**以最后登录的档案为准**（`ProfileService.Current`）；无档案时为「冒险家」 |
- 默认池 **9 条、等概率**（原先「Also try」三条按用户要求去掉）：
  欢迎使用 PCL Swordsman！／记得常常看看插件市场有没有上新！／这是 PCL Swordsman 与你相见的第 LAUNCHER_BOOT_COUNT 次！／
  TIME 好，PLAYERNAME！／愿 PCL Swordsman 与你同在！／你爱我，我爱你，PCLS 甜蜜蜜！／
  愿你永远见不到 Crash Reports！／PLAYERNAME，今天想玩点什么？／也可以试试 PCLCE！
- 自定义池：`PCL\PluginData\sgu.welcome\welcome.txt`（一行一条，`#` 开头为注释；首次运行自动写入默认池）
- 打包：`plugins/package.sh` / `package.ps1` 的插件名单加上 `Welcome`

**④ 语料导出（`TEXT-POOLS-2026-10-29.txt`，工作区根目录）**

- 第一部分：启动 MC 时的 Tips 语料池（zh-CN，**525 条**，取自 `Resources/hints/zh-CN.txt`）
- 第二部分：弹窗文案 —— 用户协议（EULA）、遥测询问（已禁用，留档）、陶瓦联机免责声明、
  主页「PCL Swordsman 提示」卡片、每次更新的「版本说明」＋更新日志标题
- 第三部分：欢迎语默认池（方便直接对照修改）

- 版本号：`27.1-snapshot-40(2.15.1)`，`code` 563
- 产物：`artifact-x64/PCLCE For SGU 27.1-snapshot-40(2.15.1).exe`，25,259,373 B，
  SHA-256 `F0BB2AA9ACB6E29C8CAF9E3DB919B54BD16E4AFCECDD3D4F0723DBE20F98425E`（`dist/` 有同哈希副本）
- 验证（构建期）：0 error；`plugins/dist/` 5 个包（含 `Welcome.pclpkg`）且已内嵌；
  `ModWelcome` / `RefreshThemeCombos` / `ValidateThemeSelection` 均在程序集内
- ⚠️ 未实机验证：欢迎语句式与弹出时机、主题下拉框在「停用插件」后的实时刷新、正式版内置三个插件的行为

### 正式版也内置并默认启用「PCLS 核心插件」（已改代码，随 snapshot-40 一起构建）

### 正式版也内置并默认启用「PCLS 核心插件」（已改代码，**尚未构建**）

> 用户决定（2026-10-29）：「由于 HMCL 恢复了下载功能，核心组件在正式版也内置并且默认启用，那个提示不要删。」

**改动前的问题**：`PluginHostBootstrap.EnsureBundledPluginsInstalled()` 在正式版**直接 return**，
连无条件内嵌的 `PCLSCore.pclpkg` 都不会安装 —— 也就是说文档里写的「正式版内置但默认关闭」，
实际上等于「正式版内置但**根本不装**」。

**改动（`Plain Craft Launcher 2/Modules/Plugins/PluginHostBootstrap.cs`）**

- 正式版也执行 `InstallEmbeddedPackages()`：正式版只内嵌 `PCLSCore.pclpkg`（`*.pclpkg` 的 glob 带
  `Condition="'$(IncludeBundledPlugins)'=='true'"`，正式版为 false），所以只装这一个
- 删掉 `if (!pclsCoreWasInstalled && IsReleaseDistribution) PluginStateStore.SetDisabled("pcls.core", true);`
  → **默认启用**（与测试版一致）
- `exe 同级 plugins\` 目录的自动安装**仍然只在非正式版生效**，避免正式版「解压即装」
- **主页提示一字未改**：`Launch.Right.CommunityHint.Message` 末尾的
  「附：如果你在使用中遇到问题，请尝试启用「PCLS 核心插件」或向开发人员反馈。」保留（用户明确要求不要删）
- 效果：正式版默认即可下载 Minecraft、使用陶瓦联机、创建离线 / 外置档案；
  用户仍可在插件管理页手动停用，停用后红镐子占位页照旧出现

**状态**：代码与文档已改，按用户要求**未构建**；下次构建（`snapshot-40` / `code` 563 或顺延）即生效。
验证方式：正式版构建后看日志 `[PluginHost] 内置插件安装成功：PCLSCore.pclpkg` 与
`已应用插件功能：unlock.region, unlock.offline-auth`（正式版不应出现「跳过随包插件自动安装」）。


> 以下改动已进代码。`snapshot-9` ~ `snapshot-12` 均已按「不改动本节、只在本节末尾追加新快照小节」的方式发布
> （本节内容自 `snapshot-9` 起就已随包发出）；顺延发版时仍把 `metadata.json` 的 `suffix`/`code` 递增即可。

### Win10 平台限制：磨砂不可裁剪 → 默认关掉，改为可选（snapshot-39）

> 用户追问：「Win10 22H2 与 Windows 11 环境暂未测试，这两个是不是正常的？」

**平台能力事实（决定一切）**

| 系统 | `DwmSetWindowAttribute(38)` 云母 / 亚克力（需 build ≥ 22621） | `DwmSetWindowAttribute(33)` 窗口圆角（需 ≥ 22000） | 可用的窗口磨砂 |
| --- | --- | --- | --- |
| **Win10 19044 / 19045（22H2）** | ❌ | ❌ | 只有旧版 `SetWindowCompositionAttribute` AccentBlur |
| Win11 22000+ | 部分（22621+ 才有材质类型） | ✅ | DWM 材质，跟随窗口形状 / 圆角 |

旧版 AccentBlur 的关键缺陷（本轮日志已证实：`SetWindowRgn` 成功，磨砂仍然溢出）：
**它按整个窗口矩形绘制，不理会窗口区域**。因此 Win10 上「真实系统磨砂」与「磨砂贴合圆角界面」**物理互斥**
——要么磨砂溢出四角变方，要么没有系统磨砂。

**snapshot-39 的处理（把选择权交给用户，默认不出 bug）**

- `Aero`：Win11 走 DWM 亚克力；Win10 **默认不再调用 AccentBlur**，而是 `Applied = None` +
  `FallbackReason`（状态行可见）→ 界面回到上游外观（圆角 6 / 内缩 10 / 无溢出磨砂）
- 设置页新增复选框「**允许 Windows 10 旧版磨砂**」（`UiMaterialAccentBlurOnWin10`，默认关；
  **只在没有 DWM 材质的系统上显示**）：勾上才调用旧版磨砂，代价是磨砂溢出圆角界面、四角变方
- `液态玻璃`：不依赖系统磨砂也能用 —— 无 DWM 材质时 `AppliedVia` 记为「宿主自绘玻璃」，
  着色 / 光泽 / hairline / 壁纸磨砂照常，只是「桌面被糊住」这一层缺失（状态行与日志都写明）
- `MaterialService` 新增 `IsWindows11`、`AppliedVia`、`MaterialOptions.AllowLegacyAccentBlur`
- 窗口外形（snapshot-37 起）：统一用 `SetClientAreaRoundedRegion` 把窗口裁成界面本体
  （客户区内缩 10px + 圆角 6，玻璃模式 16），并用 `GetWindowRgn` + `GetRgnBox` 读回包围盒写日志

- 版本号：`27.1-snapshot-39(2.15.1)`，`code` 562
- 产物：`artifact-x64/PCLCE For SGU 27.1-snapshot-39(2.15.1).exe`，25,254,765 B，
  SHA-256 `529ADB5FB652C6B0E4D546A04366542F4BC222789FD707375F217D08041F22FB`（`dist/` 有同哈希副本）
- 验证（构建期）：0 error；`AllowLegacyAccentBlur` / `IsWindows11` / `UiMaterialAccentBlurOnWin10` 在产物内；
  8 个语言文件各 3304 键
- ⚠️ **Win11 仍未实机验证**：按设计应当正常（DWM 材质跟随窗口形状），但「DWM 材质是否会被
  `SetWindowRgn` 裁掉」只能实机确认；Win10 上「磨砂与圆角二选一」是平台限制，不是待修 bug

### 磨砂框真正的元凶：AccentBlur 的「四边延伸」标志（snapshot-38）

> 用户提供 `Launch-2026-10-29-100855623.log`（snapshot-37 / code 560）后定位。

**日志给出的三个硬事实**

1. `系统版本: 10.0.19044.0` → **Windows 10 21H2**。于是
   `DwmInterop.IsBackdropSupported`（≥ 22621）与 `IsCornerPreferenceSupported`（≥ 22000）**都是 false**：
   云母 / DWM 亚克力 / DWM 圆角在这台机器上全都不可用，只剩**旧版 `SetWindowCompositionAttribute` AccentBlur**。
2. `[Material] 窗口外形：窗口区域（内缩 10px / 圆角 16px）` → `SetWindowRgn` **设置成功**（失败会打 warn）。
   即窗口确实被裁成了「内缩 10px + 圆角」的界面形状。
3. 但磨砂依旧比界面大、四角依旧方 → **AccentBlur 根本不听窗口区域**，它按窗口矩形绘制，
   而且我们给它带了 `AccentFlags = 0x20 | 0x40 | 0x80`（**四边延伸**，把模糊画到客户区之外的边框/阴影区）。

**修法**

- `MaterialService._ApplyAero`：`AccentFlags` 去掉 `0x40 | 0x80`，只留 `0x20`（仅客户区）。
  这是「磨砂比界面大一圈」最可能的直接原因。
- `Aero` / `LiquidGlass` 在 **Windows 11** 上改为优先走 DWM 亚克力（`DWMWA_SYSTEMBACKDROP_TYPE = TransientWindow`）：
  它是正规后台材质，会跟着窗口圆角 / 区域走；Windows 10 没有这个属性，才退回 AccentBlur。
- 新增 `MaterialService.AppliedVia`（实际实现方式）与两条诊断日志（`[DBG]` 级别，确认会进日志文件）：
  - `[Material] 效果：<选择> → <生效>（<实现方式>）`，降级时带原因
  - `[Material] 窗口外形：窗口区域（内缩 Npx / 圆角 Npx；区域包围盒 (x1,y1)-(x2,y2)）`
    —— 用 `GetWindowRgn` + `GetRgnBox` 读回系统真实区域，可以直接证明形状有没有生效

- 版本号：`27.1-snapshot-38(2.15.1)`，`code` 561
- 产物：`artifact-x64/PCLCE For SGU 27.1-snapshot-38(2.15.1).exe`，25,250,669 B，
  SHA-256 `F67CE42B384C180F6FF678F82271681DE308B77C58B569120BD1EF7F399458D0`（`dist/` 有同哈希副本）
- 验证（构建期）：0 error（第一次 `csc` 段错误、重试通过）；`AppliedVia` / `GetWindowRgn` / `GetRgnBox` /
  `AccentFlags` 均在产物内
- ⚠️ **已知平台限制**：如果 Win10 的 AccentBlur 连「只画在客户区」也做不到、且无视窗口区域，
  那它就无法被裁成圆角界面。届时只有两条路：① 界面铺满窗口（放弃 10px 间隙），② 不用系统模糊，
  改为宿主自绘磨砂（只磨壁纸，无壁纸时退化为着色透明）。需要用户截图确认后再定。

### 再修：磨砂框贴合 + 圆角恢复（snapshot-36 / snapshot-37）

> snapshot-35 的修法**没生效**，而且把启动器的圆角弄没了。用户实测（snapshot-35）：
> 「上一轮的问题依旧存在」「启动器的圆角怎么消失了，恢复回来，圆角大小同步上游」。

**snapshot-35 错在哪（三个错，逐个说清）**

1. **改完边距立刻读尺寸**：`PanBack.Margin = 0` 后同步调用 `FormMain_SizeChanged()`，而它读的是
   `PanBack.ActualWidth` —— 此刻布局还没跑，**读到的仍是旧尺寸**，于是 `PanForm.Width/Height` 与
   `RectForm.Rect` 全按旧尺寸下发 → 界面本体实际没有铺满窗口 → 磨砂框照旧露一圈。
   （snapshot-36 补了 `UpdateLayout()` 与 `PanBack.SizeChanged` 钩子，但见下条，方向本身也不对。）
2. **窗口区域坐标系错了**：`SetWindowRgn` 用的是**窗口坐标（含非客户区）**，我直接拿 WPF 的
   `ActualWidth/Height`（客户区 DIP）去算 → 区域整体偏移/小一圈，等于多切一条边；
   液态玻璃的「边框严重重叠」很大程度就是这个 —— 多出来的那条区域边与界面自身的圆角边不重合。
3. **圆角被挤没了**：把边距收成 0 之后，界面本体贴到窗口边缘，圆角只在窗口角上切了 6px，
   而窗口矩形外面仍是磨砂 → 视觉上就是「方角」。

**snapshot-37 的正解：一行界面布局都不动，改裁「窗口」**

- **界面边距保持上游的 10px、圆角保持上游的 6px**（`RectForm.RadiusX/Y = 6`，与上游完全一致），
  不再碰 `PanBack.Margin`、也不再调 `FormMain_SizeChanged`
- 把**窗口**裁成「界面本体」的形状：窗口区域 = 客户区**内缩 10px**、圆角 **6px**
  → 磨砂与界面完全重合、圆角与上游一致、四角不再露出直角磨砂
- 区域坐标改成 **客户区坐标**：`GetWindowRect` + `GetClientRect` + `ClientToScreen` 求非客户区偏移，
  再按内缩量平移（`SetClientAreaRoundedRegion(hwnd, radiusPx, insetPx)`）
- **缩放热区跟着移动**：窗口区域从内缩处开始，鼠标在区域外根本落不到窗口上，
  所以 `_SizeWndProc` 的 `offsetWpf` 由常量改成字段 `FormMain.ResizeOffsetWpf`，
  材质生效时设为内缩量（10）、退出时还原 6，边缘才拖得动
- 退出材质：清窗口区域 + DWM 圆角偏好回默认 + 热区偏移回 6 → 与上游行为一致
- 液态玻璃：同样只裁窗口（圆角 16），不再改边距；折射强度默认 **0**，
  强度为 0 时**连折射层元素都不创建**，只保留 1px hairline（纯描边，无填充）
- 新增排障日志（日志里搜 `[Material]`）：`窗口外形：窗口区域（内缩 Npx / 圆角 Npx）`、
  `窗口外形：DWM 圆角（窗口区域设置失败）`、`窗口外形：最大化：不裁剪`、`窗口外形：矩形窗口（未启用材质）`

- 版本号：`27.1-snapshot-37(2.15.1)`，`code` 560（snapshot-36 是「补 UpdateLayout」的中间构建，未交付）
- 产物：`artifact-x64/PCLCE For SGU 27.1-snapshot-37(2.15.1).exe`，25,250,669 B，
  SHA-256 `5C0967438AC8E38EAE8A6EFC97BEB055C91D5561C6F7B84FD64065926DF76946`（`dist/` 有同哈希副本）
- 验证（构建期）：0 error；`"suffix": "snapshot-37"` / `"code": 560`；
  `ResizeOffsetWpf` / `SetClientAreaRoundedRegion` / `ApplyWindowShape` / `LogShape` 均在程序集内
- ⚠️ 仍只能编译期验证。**如果还不贴合**，请把日志里 `[Material] 窗口外形：…` 那一行发我，
  能直接定位是「区域没设上」还是「内缩量不对」

### 修「磨砂比界面大一圈 / 无圆角」+ 修「液态玻璃边框严重重叠」（snapshot-35）

> 用户实测（snapshot-34）：
> ① 「Aero 效果下启动器边框与磨砂玻璃边框不一致，磨砂玻璃边框大小大于启动器，且没有圆角」；
> ② 「液态玻璃边框部分也有严重重叠，基本上不可用」。

**① 磨砂区与界面不重合的原因**

云母 / Aero 的后台模糊是 DWM **按整个窗口矩形**画的，而界面本体是「`PanBack` 内缩 10px + `RectForm` 圆角裁剪」：
于是磨砂在四周多出 10px 的直角边，看起来就是「磨砂框比启动器大、没有圆角」。（没有材质时那块区域是透明的，
所以这个错位一直看不出来；一开材质就暴露了。）

修法（`ModMaterial.ApplyWindowShape`，材质生效时执行、退出时完整还原）：

1. **界面铺满窗口**：`PanBack.Margin` 10 → 0，并调一次 `FormMain_SizeChanged()` 重算 `RectForm` / `PanForm` 尺寸
   —— 这样磨砂与界面就是同一块矩形
2. **窗口本身也做圆角**：Win11（build ≥ 22000）用 `DwmSetWindowAttribute(33, DWMWCP_ROUND)`，系统自带抗锯齿；
   Win10 及更早退回 `SetWindowRgn` + `CreateRoundRectRgn`（物理像素，按 DPI 换算）
3. **内容裁剪圆角与窗口外形取一致**：云母 / Aero 保持 6（≤ 系统圆角 8，不会露出缝）；液态玻璃用 16，
   并且**改用窗口区域按 16 裁**（同时把 DWM 圆角偏好设成 `DoNotRound`，避免两套圆角打架）
4. 窗口 `SizeChanged` / `StateChanged` 时重新下发（窗口区域是物理像素，缩放后必须重算）；最大化时清掉区域

新增互操作：`DwmInterop.TrySetWindowCornerPreference`（属性 33）、
`WindowInterop.SetRoundedWindowRegion` / `ClearWindowRegion`（`SetWindowRgn` + `CreateRoundRectRgn`，
成功时区域由系统接管、失败才 `DeleteObject`）；`FormMain_SizeChanged` 由 private 改 internal 以便外部重算。

**② 液态玻璃边框重叠的原因**

`RefractionBorder`（边缘折射环带）原本被**追加到 `PanForm` 的最后、`ZIndex = 9`**，也就是盖在所有内容之上：
它会把「背后的画面」重新画一遍，于是标题栏、卡片、页面内容在边缘被再叠一层 —— 看起来就是严重重叠，
而且环带宽度是 `16 + 强度`（默认 28px），覆盖面积很大。

修法：

1. 折射环带**下沉到内容之下**（插在壁纸层之后、页面之前，只压住壁纸与着色层），并且**只采集壁纸层**
   （`RefractionBorder.Source = ImgBack`），不再用 `BackgroundPresenter` 全量采集 —— 也就不会把标题栏 / 卡片采一遍
2. 折射环带只覆盖内容区那一行（标题栏那一行没有壁纸可折射），宽度收窄成 `8 + 强度 × 0.6`
3. hairline（1px 白色描边 + 顶部 specular）**拆成独立元素**，只有描边、没有填充，盖在最上层也只占 1.2px
4. **边缘折射默认值改为 0**（只磨砂不折射），需要折射时在设置里手动调大

- 版本号：`27.1-snapshot-35(2.15.1)`，`code` 558
- 产物：`artifact-x64/PCLCE For SGU 27.1-snapshot-35(2.15.1).exe`，25,246,573 B，
  SHA-256 `52F4168A3E87E9C5964A2BC723B21A858FE5D6358608F6A57E1001AC1B35D387`（`dist/` 有同哈希副本）
- 验证（构建期）：0 error；`"suffix": "snapshot-35"` / `"code": 558`；
  `ApplyWindowShape` / `UpdateWindowRegion` / `RestoreWindowShape` / `SetRoundedWindowRegion` /
  `TrySetWindowCornerPreference` 均在程序集内
- ⚠️ 仍然只能编译期验证：窗口区域裁剪在 Win10 上是**无抗锯齿**的直角圆角（Win11 走 DWM 圆角，有抗锯齿）；
  最大化 / 多显示器 / 高 DPI 下的表现需要实机确认

### 液态玻璃（⑥c）（snapshot-33 → snapshot-34）

> 用户要求：「液态玻璃就是整个启动器是一块圆角大玻璃，侧边栏跟主界面的颜色区分消失，改为用一条竖线分割
> （竖线也得是玻璃），侧边栏的每一个项和主界面的每一个卡片都是一块小玻璃，同样的，透明度都要可调」。
> 参考 <https://wei-zhang09.github.io/LiquidGlassUI/>（该项目的一句话定义：**真液态玻璃与普通 glassmorphism
> 的唯一区别是「玻璃边缘会真的把背景弯折」**，即 edge refraction；它的降级链是 折射 → 磨砂 → 实心）。

**效果形态**

| 层 | 做法 |
| --- | --- |
| 整块大玻璃 | `PanForm` 背景改成「主题背景色 × 大玻璃不透明度」；标题栏画刷强制透明（不再有独立色条）；窗口裁剪圆角 6 → **16** |
| 侧边栏 / 主界面区别消失 | `RectLeftBackground` 画刷 alpha 归零，两者共用同一层玻璃 |
| 玻璃竖线 | 复用窗口原本那条 4px 阴影 `RectLeftShadow`：宽度改 1.5、换成「两端淡、中间亮」的白色渐变 + `DropShadowEffect` 辉光，亮度由「分割线不透明度」控制 |
| 小玻璃（卡片） | `MyCard.CornerRadius` 5 → 11，`BlurBorder` 磨砂半径抬到 `模糊半径 × 1.2`，再叠一层 hairline 高光边 |
| 小玻璃（侧边栏项） | 只对**左侧边栏（`PanMainLeft` 子树）**里的 `MyListItem` 生效：加一块圆角 8、带着色 + 高光边的玻璃砖，列表页里的普通行不动 |
| 磨砂 | 有壁纸时给 `ImgBack` 上 `BlurEffect`（半径 = 模糊半径 × 1.4，12~60），退出时还原；视频背景不磨砂（太费） |
| 边缘折射 | 新控件 `PCL.Core/UI/Controls/RefractionBorder.cs`：复用 `BackgroundPresenter` 采集背后画面，**按「以中心为原点放大一点点」重采样后只画在圆角边缘环带里**，视觉上背景在边缘被透镜压缩；强度 0 即关闭 |
| 着色浮层 | 有壁纸时在上方再蒙一层「主题色 × 大玻璃不透明度」（壁纸不透明，不蒙就看不出玻璃）；没壁纸时只用 `PanForm` 背景，避免叠两遍变浑 |
| 光泽（specular） | 一块对角白色渐变浮层（左上亮 → 右下无），对应 Web 配方里的 `brightness()` 与 inset 高光 |
| hairline | 1px 白色渐变描边：顶边最亮、向下衰减、底边微光（对应 `inset 0 1px 1px rgba(255,255,255,.55)`） |

**参数（都在「设置 → 个性化 → 高级材质」，选「液态玻璃」后才出现）**

| 参数 | 配置键 | 默认 | 说明 |
| --- | --- | --- | --- |
| 大玻璃不透明度 | `UiMaterialGlassOpacity` | 45 | 整块玻璃的着色浓度（30~100，绝对 alpha） |
| 侧边栏项不透明度 | `UiMaterialSidebarItemOpacity` | 55 | 侧边栏每块小玻璃的浓度（30~100，绝对 alpha） |
| 分割线不透明度 | `UiMaterialDividerOpacity` | 55 | 玻璃竖线亮度（0~100，0 = 不画线） |
| 边缘折射 | `UiMaterialGlassRefraction` | 12 | 折射强度（0~30 像素，0 = 只有磨砂） |
| 卡片不透明度 | `UiMaterialCardOpacity` | 100 | 与云母 / Aero 模式同一个参数（在主题卡片浓度上再乘） |
| 材质模糊半径 | `UiMaterialBlurRadius` | 18 | 同时驱动壁纸磨砂与卡片磨砂 |
| 悬停发光半径 / 透明度 | `UiMaterialGlowRadius` / `UiMaterialGlowOpacity` | 8 / 35 | 与云母 / Aero 模式共用 |

- `MaterialKind` 加 `LiquidGlass = 3`；后端 `MaterialService.Apply` 里它 = **低着色的 Aero 后台模糊**（`tintScale = 0.2`）
  → 失败再试亚克力（`TransientWindow`）→ 都不行则回退纯色并在状态行写原因
- 设置页下拉框变 4 项（关闭 / 云母 / Aero / 液态玻璃）；选中液态玻璃时**隐藏**标题栏 / 主界面 / 侧边栏三个不透明度
  （玻璃模式下它们被强制为透明/统一），改显示大玻璃 / 侧边栏项 / 分割线 / 边缘折射四项
- 语言键：+10 个（`Effect.Glass`、`GlassHint`、`GlassOpacity[.ToolTip]`、`SidebarItemOpacity[.ToolTip]`、
  `DividerOpacity[.ToolTip]`、`GlassRefraction[.ToolTip]`），并把 `Hint` / `Effect.ToolTip` 改写为三种效果的说法
  → 8 个语言文件各 **3302 键**
- 版本号：`27.1-snapshot-34(2.15.1)`，`code` 557（snapshot-33 是只做了布局不含折射的中间构建，未交付）
- 产物：`artifact-x64/PCLCE For SGU 27.1-snapshot-34(2.15.1).exe`，25,242,477 B，
  SHA-256 `4E5C4AC3E931E37D9FF7AF6DCA4C7A51E299BB39B797B947588FC29E8C1D7CDD`（`dist/` 有同哈希副本）
- 验证（构建期）：0 error；产物内 `"suffix": "snapshot-34"` / `"code": 557`；`RefractionBorder` / `ModLiquidGlass` /
  `Setup.Ui.Material.Effect.Glass` / `Setup.Ui.Material.GlassRefraction` 均在 exe 内；
  `PageSetupUI.baml` 里 `MaterialSliderChange` ×12（12 个滑条全部接线）
- ⚠️ **完全不透光的点**：折射环带、hairline、光泽叠层、磨砂的观感都必须在 Windows 实机调；
  `RefractionBorder` 的环带宽度 / 强度 / 两层透明度是拍脑袋的初值，需要按实机效果微调

### 侧边栏 / 卡片不透明度也可调（补完 ⑥a/⑥b）（snapshot-32）

> 用户追加要求：「侧边栏和卡片的不透明度（卡片就是主界面上的卡片）也要支持设置」。
> snapshot-31 只能调**标题栏**与**主界面背景**两层，侧边栏与卡片还是主题给的固定浓度；本轮补齐另外两层。

**四层各自独立，互不影响**

| 层 | 载体 | 实现方式 |
| --- | --- | --- |
| 标题栏 | `PanTitle.BackgroundBrush`（主题 `ColorBrush3`，不透明） | 换成缩放 alpha 后的画刷 |
| 主界面 | `PanForm.Background`（主题 `ColorBrushBackground` 或彩色渐变） | 同上（渐变逐 stop 缩放） |
| **侧边栏** | `RectLeftBackground.BackgroundBrush`（主题 `ColorBrushTransparentBackground`） | 换成缩放 alpha 后的画刷，**用侧边栏自己的系数** |
| **卡片** | `MyCard` 的背景（同样引用 `ColorBrushTransparentBackground`） | **替换应用资源字典里的这一项**，`MyCard` 的 `SetResourceReference` 会自动重取，所有卡片一起更新 |

**语义差异（容易理解错，写清楚）**

- 侧边栏 / 卡片的主题画刷**本身就带半透明 alpha**（来自 `tone.ABackground`），所以这两个滑条是
  「在主题原始浓度上再乘一个系数」，**默认 100% = 完全保持现状**，调低才会更透
- 标题栏 / 主界面的基础画刷是全不透明的，所以它们的百分比就是**绝对 alpha**（默认 88% / 92%）
- 四者都只在「材质真的生效」时才动手；材质关闭或降级到纯色时全部恢复原状

**新增 / 改动**

- `PCL.Core/App/Config.cs`：`UiMaterialSidebarOpacity`、`UiMaterialCardOpacity`（30~100，默认 100）
- `PCL.Core/Utils/OS/Window/MaterialService.cs`：`MaterialOptions` 补 `SidebarOpacity` / `CardOpacity`
- `Modules/UI/Material/ModMaterial.cs`：新增 `ApplySurfaceOpacity(frm, sidebarFactor, cardFactor)`；
  卡片基准画刷**必须从资源字典取**并判断「当前项是不是自己写进去的那一份」，否则会层层衰减；
  侧边栏则从资源取基准、写元素，这样换主题时两层都能跟着新配色走
- `Pages/PageSetup/PageSetupUI.xaml(.cs)`：卡片里补两个滑条（现在共 8 行）；四个不透明度滑条内部量程仍是 0~70 + 30 偏移
- 语言键：+4 个（`Setup.Ui.Material.SidebarOpacity[.ToolTip]`、`Setup.Ui.Material.CardOpacity[.ToolTip]`），
  并把 `Setup.Ui.Material.Hint` 改成「标题栏、主界面、侧边栏、卡片的不透明度可以分别调节」→ 8 个语言文件各 **3292 键**
- 版本号：`27.1-snapshot-32(2.15.1)`，`code` 555
- 产物：`artifact-x64/PCLCE For SGU 27.1-snapshot-32(2.15.1).exe`，25,213,805 B，
  SHA-256 `0E0C10C5CE52FA9228D4B0BAF40A6C6F4CC279553AC3E4902DD42CFE5B6F240F`（`dist/` 有同哈希副本）
- 验证（构建期）：0 error；产物内 `"suffix": "snapshot-32"` / `"code": 555`；
  `ModMaterial.ApplySurfaceOpacity` 在程序集内；`PageSetupUI.baml` 里 `MaterialSliderChange` ×8（8 个滑条全部接线）、
  `MaterialKind_Change` ×1；8 个语言文件各 3292 键、键集一致、无重复 `x:Key`
- ⚠️ 同样只能验证编译期：四层不透明度的实际观感必须 Windows 实机确认

### 高级材质接线：设置卡片 + 窗口应用 + 悬停发光 + 默认启用（snapshot-31）

> 用户要求（2026-10-29）：「接着做高级材质剩余四步：设置 UI 卡片 + FormMain 接线 + 悬停发光 + 默认启用。
> 约束：效果不做标题栏 / 主界面区分开关，但参数要能分别调；不要动液态玻璃。」

后端在 snapshot-30 就已就绪（`DwmInterop` / `MaterialService` / `Config.System` 的 7 个 `UiMaterial*` 配置项 /
`materials.advanced` 功能键 / `plugins/AdvancedMaterials/` 骨架），本轮把它接到界面上。

**① 宿主接线**：新文件 `Plain Craft Launcher 2/Modules/UI/Material/ModMaterial.cs`

- `EnsureInstalled()` 注册四类事件：`FeatureFlags.Changed`、`PluginHostBootstrap.Ready`（插件启用 / 停用后重申请，
  **无需重启**）、`ThemeService.ColorModeChanged`、`ThemeService.ColorThemeChanged`（深浅色切换后重下发音阶与沉浸式标题栏）
- `ReloadOptions()` 把配置里的整数百分比换算成 `MaterialOptions`（55 → 0.55、88 → 0.88）
- `ApplyBackdrop()`：`MaterialService.Apply(hwnd, kind, options)` + `DwmInterop.TrySetImmersiveDarkMode(hwnd, IsDarkMode)`
- `ApplyWindowOpacity()`：**标题栏 = `PanTitle.BackgroundBrush` 的 alpha，主界面 = `PanForm.Background` 的 alpha**。
  只降背景 alpha，文字与控件不受影响；渐变背景逐 stop 缩放。基准画刷单独缓存，反复拖动滑条**不会叠加衰减**；
  材质没生效（关闭 / 系统不支持）时恢复 100%，避免「既没有材质、又把整块界面透出桌面」
- 调用时机：`FormMain.OnSourceInitialized`（句柄就绪即下发，避免启动先闪一下纯色）→ `FormMain_Loaded` →
  `ThemeManager.RefreshBackground()`（主题换完背景画刷后补一次，否则透明度会被主题刷新吃掉）→
  `ModSetup` 里的配置观察器（滑条即时生效）

**② 悬停发光**：新文件 `Plain Craft Launcher 2/Modules/UI/Material/MaterialGlow.cs`

- 用 `EventManager.RegisterClassHandler(FrameworkElement.LoadedEvent)` 全局挂钩，页面里动态创建的控件同样生效
- 只给交互型控件加效果（`MyButton` / `MyIconButton` / `MyIconTextButton` / `MyTextButton` / `MyExtraButton` /
  `MyExtraTextButton` / `MyRadioButton` / `MyRadioBox` / `MyCheckBox` / `MyListItem`），不给容器与文本块加（会很卡）
- `MouseEnter` 挂 `DropShadowEffect { ShadowDepth = 0, BlurRadius = UiMaterialGlowRadius,
  Opacity = UiMaterialGlowOpacity, Color = 主题 ColorBrush3, RenderingBias = Performance }`，`MouseLeave` 清掉；
  半径或透明度为 0、或插件被停用时完全不加。已经自带 `Effect` 的控件（壁纸模糊 `ImgBack`、标题栏钻石剑）不去抢

**③ 设置 UI**：`Pages/PageSetup/PageSetupUI.xaml(.cs)` 在个性化页「启动器」卡片后新增 `CardMaterial`

- 效果下拉（关闭 / 云母 / Aero）+ 状态行 `系统最高支持：X　当前生效：Y`，发生降级时追加 `MaterialService.FallbackReason`
- 6 个滑条：模糊半径 0~40、着色强度 0~100、标题栏不透明度 30~100、主界面不透明度 30~100、
  悬停发光半径 0~20、悬停发光透明度 0~100
  ⚠️ **两个不透明度滑条内部量程是 0~70 + 30 偏移**，写配置前会 `+30`，读配置时 `-30`，别按 0~100 直接套
- 卡片显隐由 `FeatureFlags.IsEnabled("materials.advanced")` 决定：插件停用 → 整张卡片收起（与其它插件功能一致）
- 与上游的「高级材质」（`UiBlur`，卡片背后的高斯模糊）**不是同一个东西**，本卡片标题写成
  「高级材质（窗口）」（`Setup.Ui.Material.Title`）以免混淆

**④ 默认启用**

- `plugins/AdvancedMaterials/`（id `sgu.advanced-materials`、`provides: ["materials.advanced"]`）本来就在
  `plugins/package.sh` 的打包名单里；本轮补上 `plugins/package.ps1`（Windows 侧之前漏了它，还留着已删除的 `DownloadUnlock`）
- 非正式版：`.pclpkg` 随 exe 内嵌，首次启动自动安装并启用（默认 `UiMaterialKind = 1`，即云母）
- 正式版：exe 内不嵌入，用户手动装插件后卡片才出现
- `PCL.Core/App/FeatureFlags.cs` 的 `Keys` 补上 `MaterialsAdvanced`（与 `PCL.PluginSdk.PluginFeatureKeys` 必须一致）

**⑤ 语言键**：新增 20 个 `Setup.Ui.Material.*` 键，8 个语言文件全部补齐（各 3288 键、无重复 `x:Key`）；
zh-CN / zh_sgu 为简体中文，zh-TW 为繁体，en-US / en-GB 为英文，ja / fr / es 本轮仍填简体中文原文（待真翻译）。

- 版本号：`27.1-snapshot-31(2.15.1)`，`code` 554
- 产物：`artifact-x64/PCLCE For SGU 27.1-snapshot-31(2.15.1).exe`，25,205,613 B，
  SHA-256 `C91198A8349C9FF38F7D6E40E493C56F6DE2C77572F54E09476E2274AFC66762`（`dist/` 有同哈希副本）
- 验证（构建期）：`./build.sh x64 Release` 0 error（本地化重复键校验通过，8 个语言文件各 3288 键、键集一致）；
  产物内 `"suffix": "snapshot-31"` / `"code": 554` 各 1 次；`ModMaterial` / `MaterialGlow` /
  `Setup.Ui.Material.Title` / `UiMaterialGlowRadius` / `AdvancedMaterials` 字符串均在；
  `PageSetupUI.g.cs` 已生成 `CardMaterial` / `ComboMaterialKind` / 6 个 `SliderMaterial*` 字段，
  BAML 内 `MaterialKind_Change` ×1、`MaterialSliderChange` ×6（事件接线成功）；
  四个插件包 `SguPack` / `PCLSCore` / `AdvancedMaterials` / `HelloPlugin` 均在 `plugins/dist/`、`artifact-x64/plugins/` 与 exe 内
- ⚠️ **未实机验证**：云母 / Aero 的实际观感、Aero 降级链、悬停发光强度、标题栏 / 主界面不透明度的视觉效果，
  都只能在 Windows 上确认（Termux 只能验证编译期）

### 补记：snapshot-28 ~ snapshot-30（2026-10-29 那一轮）

> 这三次构建当时没在本文档留小节（只写进了 `HANDOFF-2026-10-29.md`），这里补记。
> 版本号 / 产物大小 / SHA-256 均取自 `artifact-x64/` 里的实际文件，`"suffix"` / `"code"` 已用字符串搜 exe 核对。

| 版本 | code | 产物大小 | SHA-256 |
| --- | --- | --- | --- |
| `27.1-snapshot-28(2.15.1)` | 551 | 24,185,709 B | `18D9F43308EFBA03D20A8DB976249DF9D852FCDDB46AA35494D3DE0E21B3139F` |
| `27.1-snapshot-29(2.15.1)` | 552 | 24,185,709 B | `FC7DC3C61BD5AD5E58E1A2BC9513774981A5C42132ACB9229B0AC2BDA3F7323B` |
| `27.1-snapshot-30(2.15.1)` | 553 | 25,164,653 B | `8507DB77BD318F31D60F1C6B51E118A2FBF33C58D2F5E3D169456F0B76A8721D` |

完成的九条需求中的 8 条（逐条状态见 `HANDOFF-2026-10-29.md` §1）：

- ① 设置 → 工具 联机隐藏后，「工具」分类标题一并隐藏（`PageSetupUI.HiddenRefresh()`）
- ② 「限制解除」改名为「PCLS 核心插件」（`pcls.core`，**永远内嵌**；测试版默认启用，正式版首次安装写入
  `PluginStateStore.SetDisabled("pcls.core", true)`）；「自定义主题」仍与扩展主题共用 `themes.extended` 键
- ③ 全程序文案 `PCL CE For SGU` → `PCL Swordsman`（zh-CN / zh-TW / en-US / en-GB / zh_sgu + SguPack 语录）
- ④ 首次启用陶瓦联机弹免责声明（新配置 `LinkTerraCottaDisclaimerAccepted`，选「拒绝并停用」则开关退回）
- ⑤ 插件禁用真正生效：`DownloadPolicy.AllowDownloads` 默认改 `true`（Mod 等正常下载）、
  `AllowMinecraftDownload` = 显式值 ?? `unlock.region`；MC 下载页与陶瓦联机页改为红色占位页
  （`MyUnavailablePlaceholder`）；`DownloadUnlock` 插件删除，职责并入 PCLS 核心插件
- ⑥a/⑥b 高级材质后端就绪（见上一条的 snapshot-31）
- ⑧ 语言键按 zh-CN 全量补齐，新增语言「SGU 式中文」`zh_sgu`（`LocalizationService.SupportedLanguages` 里排在 zh-CN 之后）

未做：⑥c 液态玻璃（用户要求先不做）、⑦ 启动器圆角可调、⑨ 百宝箱「一键启动」。

### 修「禁用后再启用报缺少 plugin.json」（解包缓存残件）（snapshot-24）

> 用户实测（snapshot-23）：「禁用的启用后会直接出错确实plugin.josn怎么办」。

**原因**：缓存目录名由「基名 + 文件长度 + 修改时间」拼成，而改名不改长度也不改修改时间，
于是同一插件的 `.pclpkg` 与 `.pclpkg.disabled` 共用同一个缓存目录。禁用时该插件不被加载，
`CleanupCache()` 删这个目录；但 `AssemblyLoadContext` 释放文件有延迟，删除常常失败一半
（`plugin.json` 没了、dll 还占着）。再次启用时 `ExtractToCache()` 重新解压失败于
`Directory.Move`（目标目录还在），旧代码的 catch 只删临时目录却仍返回残缺目标 →
加载器读到没有清单的目录 → `Faulted - 缺少 plugin.json`。

**修复**（`PCL.PluginHost/PluginPackage.cs`）

- 新增 `IsUsableCache(directory)`：目录存在**且** `plugin.json` 在位才算可复用
- 残件先 `TryDeleteDirectory` 强删；删不掉（仍被占用）就改用带随机后缀的唯一目录名
- `Directory.Move` 失败时换唯一名字落盘，再失败就把（完整的）临时目录本身作为结果
- 返回前最终校验，仍不可用则抛 `IOException`（写明是哪个包的缓存缺清单），不再把坏目录交给加载器
- 版本号：`27.1-snapshot-24(2.15.1)`，`code` 547
- 产物：`artifact-x64/PCLCE For SGU 27.1-snapshot-24(2.15.1).exe`，23,847,016 B，
  SHA-256 `C70D70DD2F6B63185E10DA48558074B386B4A7007ED16ABB51C01F4CEDA4D30F`（`dist/` 有同哈希副本）
- 验证：新增 `_tmp/probe-cache-residue`（先造一个「缺 plugin.json 的同指纹缓存目录」，再连做 3 轮
  禁用→启用）18 项全部 PASS；`_tmp/probe-plugin-pkg` 重跑 23 项全部 PASS

### 修「禁用/删除只作用到一份」（同一插件多份副本）（snapshot-23）

> 用户实测（snapshot-22）：「安装正常，但是启用变成禁用后启用目录该插件仍然存在而且数量未变」。

**原因**：`InstallPackage()` 用**源文件名**当安装文件名，同一个插件从不同来源进来就变成多个文件：

```
PCL/Plugins/HelloPlugin.pclpkg           ← 内嵌资源名
PCL/Plugins/HelloPlugin.pclpkg.disabled  ← 用户点了禁用（只改名了这一份）
PCL/Plugins/hello-1.0.0.pclpkg           ← 用户从 samples/dist 拖进来
PCL/Plugins/SguPack.pclpkg
PCL/Plugins/PCL.SguPack.pclpkg           ← 内嵌资源名带 PCL. 前缀
```

`SetEnabled` 只改被点击文件的扩展名，其它同 `id` 副本仍然启用 → 列表还在、启用数量不变。
`PackageAlreadyInstalled()` 也只看文件基名，所以每次启动又按另一个名字再装一份。

**修复**

- `InstallPackage()` 目标文件名改为**用清单 `id` 命名**（`Sanitize(id) + ".pclpkg"`），落盘前调新的
  `DeleteById()` 清掉同 `id` 的其它文件 → 一个 `id` 只可能有一个包文件
- 新增 `PluginInstaller.FindPackageIds(pluginsDirectory)`；`PackageAlreadyInstalled()` 改为**按 `id` 判断**
- 新增 `PluginHostBootstrap.DeduplicatePackages()`：每次启动把同 `id` 的多份副本合成一份
  （**禁用优先**：只要有一份是 `.disabled` 就按禁用保留，保留时间最新的那份内容），
  文件名规范成 `<id>.pclpkg` / `<id>.pclpkg.disabled`，其余删除 → 老目录自愈
- `PluginInstaller.Uninstall()` / `DeleteById()` 删除该 `id` 的**全部**文件，不再只删点中的那一个
- `PluginLoader.LoadOne()` 兜底：同 `id` 已加载则跳过，日志 `[PluginHost] 跳过重复插件：xxx（同 id 已加载）`
- 版本号：`27.1-snapshot-23(2.15.1)`，`code` 546
- 产物：`artifact-x64/PCLCE For SGU 27.1-snapshot-23(2.15.1).exe`，23,847,016 B，
  SHA-256 `000BE000A4FADCEA5983BF2FC764222D754F8E9A6EAF58676304E0FD89F04D5E`（`dist/` 有同哈希副本）；
  内嵌 `snapshot-23` / `"code": 546` / `2.15.1` 各 1 次，三个插件包各 3 次
- 验证：控制台探针 `_tmp/probe-plugin-pkg` 全部 PASS（同 `id` 两份包 → 扫描只出 1 项；`DeduplicatePackages()`
  后只剩 1 个文件；禁用后 `FindPackageIds` 里该 `id` 仍唯一；删除后文件与列表项同时消失）

### 插件包化：一个插件 = 一个 `.pclpkg` 文件（snapshot-22）

> 用户报障：「PCL 不能正常启用/停用插件，并且不能正常读取 `.pclpkg` 插件文件」。

**旧的错在哪**

- 旧模型把插件当**解压目录**：`PCL/Plugins/<id>/plugin.json`。`PluginLoader.ReloadAll()` 只 `Directory.GetDirectories(...)`，
  完全不认 `.pclpkg` 文件 → 用户把包拖进 `PCL/Plugins/` 后「什么都没发生」
- 启用/禁用只写 `PCL/PluginData/plugins.state.json`（一个禁用 id 名单），与磁盘上的插件实体没有关系：
  用户以为删了/禁了，文件还在；随包插件每次启动又 `InstallPackage` 覆盖重装，禁用等于无效
- 插件程序集直接从插件目录加载 → 目录被 ALC 占着，改名/删除/覆盖经常失败，只能「登记下次启动删」

**现在的模型**

- **一个插件 = `PCL/Plugins/` 下的一个 `.pclpkg` 文件**（zip，根目录含 `plugin.json`）
- **禁用 = 文件名改成 `Foo.pclpkg.disabled`**，启用 = 改回来。文件名是启用状态的唯一真相，
  不再有 `plugins.state.json`；在资源管理器里手动改名同样生效，重启后状态一致
- 加载时把包解到 `PCL/PluginData/cache/<包名>_<长度>_<时间戳>/` 再从缓存加载程序集，
  **包文件本身始终不被占用**，所以改名、覆盖安装、删除都能立刻成功
- 删除 = 直接删包文件（`Foo.pclpkg` 与 `Foo.pclpkg.disabled` 两种都删）；真被外部进程占用时登记
  `pending-delete.json`，下次启动先删
- 老格式兼容：启动时 `MigrateDirectoriesToPackages()` 把历史 `PCL/Plugins/<id>/` 目录打成 `<id>.pclpkg` 并删目录，
  插件数据（`PCL/PluginData/<id>/`）原样保留

**安装入口**

- 拖 `.pclpkg` 进主窗口任意位置 → 弹「插件确认安装」→ 装进 `PCL/Plugins/` 并默认启用 → 提示「已安装 `Foo.pclpkg`！」
  （实现：`PageToolsPluginManage.InstallPluginPackages(files, requestConfirm: true)`，`FormMain.FileDrag` 里判定）
- 插件管理页「从文件安装」走同一个入口；文件对话框过滤 `*.pclpkg;*.pclpkg.disabled`
- 在插件管理页拖入非 `.pclpkg` 文件时提示「PCL Swordsman 无法确定适合的操作……」并拦下，
  避免被 `PageInstanceCompResource.InstallMods` 当成 Mod 装掉

**其它细节**

- `PackageAlreadyInstalled()`：exe 同级 `plugins/` 与内嵌包自动安装前，先看 `PCL/Plugins/` 里是否已有同名包
  （启用或 `.disabled` 都算），有就「保留用户当前状态」直接跳过 —— 用户禁用内置插件后不会被下次启动强行拉起
- `PluginPackage.MakeWritable()`：从只读位置（附件、共享目录、只读压缩包）复制来的包带 `ReadOnly` 属性，
  改名/删除前统一递归清除，配合 `MoveWithRetry` / `TryDeleteDirectory` 的 5 次重试
- 管理页「启用 / 禁用」聚合按钮改为逐个上报失败（弹 `Tools.Plugin.Toggle.Failed`，列出文件名与原因）；
  「打开文件夹」对包用 `explorer /select,` 选中文件本身
- 本地化：zh-CN / zh-TW / en-US / en-GB 同步新增 `Tools.Plugin.Install.Confirm.*`、`Install.Success`、
  `Install.NotAPackage`、`Toggle.Failed*`，改写 `Delete.Scheduled/Done`、`Manage.Empty*`、`Manage.Detected.Description`
- 文档：新增 `docs/PLUGIN-API.md`（接口参考 + 后续路线）、`docs/PLUGIN-GUIDE.md`（插件制作指南）、
  `plugins/package.ps1`（一行打包成 `.pclpkg`）
- 版本号：`27.1-snapshot-22(2.15.1)`，`code` 545
- 产物：`artifact-x64/PCLCE For SGU 27.1-snapshot-22(2.15.1).exe`，23,842,920 B，
  SHA-256 `6C33C66FF61320D6E5AD786AB1854AEE02BA0BE05F936A38E62640C6F49D6709`（`dist/` 有同哈希副本）；
  内嵌 `snapshot-22` / `"code": 545` / `2.15.1` 各 1 次，三个插件包各 3 次

### 标准构建定标 + `tools/build.ps1` 与 `build.sh` 等价化（2026-10-25）

- **定标**：以本工作区最新构建 `27.1-snapshot-21(2.15.1)` 为标准产物
  （`artifact-x64/PCLCE For SGU 27.1-snapshot-21(2.15.1).exe`，23,826,536 B，
  SHA-256 `CAC9CC2F59ED9D00D9300665C7E7E67A677F91758732562A877D4A6B7B995C0A`，`dist/` 有同哈希副本），
  上游 PCL CE 版本号此后仅作标注，不做追同步
- **修 bug（工具链，非启动器代码）**：`tools/build.ps1` 此前只做了「publish + 重命名」，
  与 `build.sh` 不等价，漏了两件事：
  1. 未传 `-p:IncludeBundledPlugins=<suffix 非空>` → 非正式版**没有把 `plugins\dist\*.pclpkg` 嵌入 exe**，
     用户在插件管理里看不到 SguPack / UnlockLimits / HelloPlugin（`build.sh` 产物里有，两者行为不一致）
  2. 未把产物同步到 `dist/`、也未处理 exe 同级 `plugins/`
- **补齐后**：非正式版 `IncludeBundledPlugins=true`；产物复制到 `dist/`；插件包复制到 `dist/`、
  `dist/plugins/` 与 `artifact-<平台>/plugins/`（正式版删除 exe 同级 `plugins/`），与 `build.sh` 完全对齐
- 版本号、`metadata.json`、启动器代码均未改动

### 品牌重命名 → Plain Craft Launcher 2 swordsman

- **标题栏徽标**：白色圆角底板保留，内部由 `CE` 字形（`Path`）改为文字 **`Swordsman`**
  （`FontSize=9`、`SemiBold`、`Foreground={DynamicResource ColorBrush3}` 跟随主题）；
  底板去掉固定 `Height/Width` 改为按内容自适应，并改为 `VerticalAlignment="Bottom"` +
  `Margin="10,0,0,16.6"`，使徽标底边与左侧 `PCL` 字形底部齐平
- **移除**原「 For 」文字（`LabTitleFor`）与 `swordsman.png` 图标（`PanTitleSwordsman` / `ImgTitleSwordsman`）；
  标题栏左侧只剩 `PCL` + 白底 `Swordsman` 徽标
- **项目显示名**统一为 `Plain Craft Launcher 2 swordsman`：
  - `FormMain.xaml` 窗口 `Title`
  - `Plain Craft Launcher 2.csproj` 的 `AssemblyTitle` / `Product`
  - `metadata.json` 的 `name`
  - `PCL.Core/Utils/OS/AumidHelper.cs` 的任务栏 `DisplayName`
- **`AssemblyName` 依旧保持 `Plain Craft Launcher 2`**（未改）：`pack://application:,,,/Plain Craft Launcher 2;component/...`
  这类资源 URI 直接引用程序集名，改名会导致图标等资源加载失败；exe 文件名仍在构建后重命名
- 未改动：本地化文案里的 `PCL CE` 简称、更新页 "PCL CE <版本号>" 文本、`Resources/hints` 里解释
  PCL CE 全称的提示条目（这些属于上游文案，若要一并换名需单独评审）

### 「反馈」侧边栏图标改回上游样式

- `PageSetupLeft.xaml` 的 `ItemFeedback`：图标由下游自绘的红色「不可用」图（`Images/Icons/Unavailable.png`）
  改回上游的 `SvgIcon="lucide/message-circle"` + `LogoScale="0.9"`
- **只改了图标**：反馈仍然处于禁用状态（`ModBase.IsFeedbackSupported = false`、
  `PageSetupLeft.TryFeedback` 短路、`PageSetupFeedback` 页面内红色禁用提示、条目 `ToolTip`
  仍为 `Setup.Feedback.Downstream.Disabled.Message`），也**没有**恢复上游该条目上的「刷新」按钮
- `Images/Icons/Unavailable.png` 现已无 XAML 引用（`MyLocalModItem.xaml.cs` 里只有一行被注释掉的旧引用），
  文件保留未删

### 反馈页 / 更新页改为整页「不可用」占位

- 新增通用控件 `Controls/MyUnavailablePlaceholder.xaml(.cs)`：居中的红色**带叉箭头** + 红色说明文字。
  图标用矢量 `Path` 绘制（`ColorBrushRedLight` / `ColorBrushRedDark`），并带 1.2 秒循环「呼吸」动画
  （整体缩放 1 → 1.07、叉的透明度 0.92 → 0.45，`SineEase` 双向）
  - 仓库内**没有**可直接复用的动画素材（全仓库无 gif / apng / lottie；只有静态的
    `Images/Icons/Unavailable.png` 红圈白叉与 `Disabled.png` 灰圈白减号），故改为矢量绘制 + WPF 动画
  - 几何参数经离屏渲染逐版校对（图标 `Grid` 130×130：箭头头部 + 箭杆，叉压在箭杆末端）
- **反馈页**（`PageSetupFeedback`）：禁用状态下隐藏提示条 `HintDownstreamDisabled`、提交卡片 `PanInfo`、
  状态列表 `PanContent`、加载框 `PanLoad`，只显示占位卡片 `PanBlocked`；
  文案复用 `Setup.Feedback.Downstream.Disabled.Message`
  =「PCLCE For SGU是PCL社区版的下游项目，请勿向社区版团队提供任何反馈！」
- **更新页**（`PageSetupUpdate`）：`ShowUnsupportedUI()` 由「全部变灰禁用」改为**隐藏全部控件**
  （更新选项卡 `CardUpdateOptions`、更新卡片 `CardUpdate`、版本检查卡片 `CardCheck`、
  其他选项 `CardOtherOption`、黄色提示 `HintUpdateUnsupported`），只显示占位卡片 `PanUnsupported`；
  文案 `Setup.Update.Unsupported` =「此版本不支持更新」

### 完全禁用遥测与遥测询问弹窗

- `PCL.Core/App/Essentials/TelemetryService.cs` 新增 `IsTelemetrySupported = false`：
  - `_StartAsync` 直接返回（不初始化 Sentry、不做 STUN 探测、不上报设备环境调查）
  - `ReportException` 直接返回（日志里的错误不再发往 Sentry）
  - `_StopAsync` 不再调用 `SentrySdk.Close()`
- `FormMain`：`!TelemetryService.IsTelemetrySupported` 时**不再弹出**遥测询问框（`Main.Telemetry.*`），
  并把配置 `SystemTelemetry` 压回 `false`
- 设置 →「启动器」的「启用遥测数据收集」勾选框改为**不可用 + 0.5 透明**，固定显示为未勾选，
  提示改用新键 `Setup.Misc.System.Telemetry.Disabled`（zh-CN / zh-TW / en-US / en-GB 均已补）
- 实测：启动日志出现 `[遥测|telemetry] 遥测已在本分支被完全禁用，跳过 Sentry 初始化与设备环境调查`，
  且启动全程无遥测弹窗

### 插件系统 M0/M1：侧边栏入口 + 加载器

- **M0（UI 骨架）**：「工具」侧边栏新增灰色分组 **插件**，其下 **插件管理**（`lucide/puzzle`，与下载栏「模组」同图标）
  与 **插件市场**（`lucide/boxes`，与下载栏「Minecraft」同图标）；新增 `PageToolsPluginManage` /
  `PageToolsPluginMarket` 两个页面；`FormMain.PageSubType` 增 `ToolsPluginManage=4` / `ToolsPluginMarket=5`；
  `ModMain` 增两个页面字段
- **M1（加载器）**：新增 `PCL.PluginSdk`（契约）与 `PCL.PluginHost`（加载器）两个工程
  - 契约：`IPlugin` / `IPluginContext`（日志 / 配置 / 数据目录 / 能力）/ `IPluginConfig` / `IPluginLogger` /
    `PluginManifest` / `PluginCapability` / `PluginState` / `PluginApiVersion`（=1）
  - 宿主：清单校验（id/version/entry/apiVersion）、`AssemblyLoadContext` 隔离加载（`PCL.PluginSdk` 回落默认 ALC）、
    生命周期（Initialize → Enable → Disable → Dispose）、依赖视图、`plugins.state.json` 启停状态、
    插件配置 `config.json` 持久化、异常全兜底
  - 接线：`Modules/Plugins/PluginHostBootstrap.cs` + `FormMain` 在 `HardwareInfo` 之后后台线程 `Start()`；
    插件目录 `PCL/Plugins/<id>/`，数据目录 `PCL/PluginData/<id>/`（**snapshot-22 起改为 `PCL/Plugins/Foo.pclpkg`，见本节顶部「插件包化」**）
  - 管理页接真实状态（插件名 / 版本 / id / 状态 / 错误原因）+「重新扫描」后台执行
  - 示例插件 `samples/HelloPlugin`（不参与解决方案构建）
- 实测：装入示例插件后日志出现
  `[PluginHost] 已加载 sgu.sample.hello 1.0.0（API 1，能力 Config, Events）`，
  `PCL/PluginData/sgu.sample.hello/config.json` 的 `launchCount` 逐次 +1（跨启动持久化）
- 设计全文见 [`docs/PLUGIN-SYSTEM.md`](docs/PLUGIN-SYSTEM.md)；后续 M2（页面/侧边栏扩展）、
  M3（事件总线 + Minecraft 只读）、M4（权限/管理页完整功能）、M5（市场）待做

### 插件管理页改版（完整套用「实例设置 → 模组」的管理方式）

> 结构与交互一比一移植自 `PageInstanceCompResource.xaml(.cs)`，仅把「Mod 资源」换成「插件」。

- 新增列表项控件 `Controls/MyPluginItem.xaml(.cs)`：对齐 `MyLocalModItem` 的 44px 行
  （左侧色条 / 34px 圆角图标 / 标题 + 灰色副信息 / 右侧图标按钮），并新增**多选高亮**
- `PageToolsPluginManage` 布局完全照搬模组页：
  - **搜索框**（`MySearchBox`，按名称或 id 过滤）
  - **操作栏卡片**（`MyCard` + `WrapPanel`）：**打开文件夹**（Highlight）/ **从文件安装** /
    **下载新插件**（跳转「插件市场」页）/ **全选** / **重新扫描**
  - **列表卡片**：筛选单选（`全部 / 已启用 / 已禁用 / 出错`，标签带计数）+ 右上**排序**
    （名称 ↑ / 名称 ↓ / 按状态，循环切换）+ 列表区
  - **计数为 0 的筛选项自动隐藏**：`已禁用` / `出错` 在计数为 0 时隐藏；
    `已启用` 只要还有插件就显示（**全部启用时也显示数量**）；`全部` 始终显示
  - **当前筛选变空时自动顺延**：例如原来停在「已禁用」、随后把所有插件都启用，
    `已禁用` 计数归零 → 自动跳到「已启用」（优先级 **已启用 &gt; 已禁用 &gt; 出错 &gt; 全部**），
    并把单选高亮同步过去，不会出现「没有任何筛选被选中」或「筛选中却看不到标签」
  - **居中空状态卡片**：标题 + 分隔线 + 说明 + 「从文件安装 / 下载新插件 / 打开文件夹」三个按钮
  - **一个插件都没有时**：隐藏搜索框、操作栏（含「打开文件夹」「重新扫描」）与列表卡片，
    页面只保留「还没有发现插件」卡片（卡片内提供**从文件安装 / 下载新插件 / 打开文件夹 / 重新扫描**四个入口）
  - 「下载新插件」跳转插件市场：**不能**用 `FormMain.PageChange(PageType.Tools, …)`，
    该分支会把 `subType` 强制覆盖成 `frmToolsLeft.pageID`（等于没跳）；
    改为直接驱动工具侧边栏 `ItemPluginMarket.SetChecked(...)` + `frmToolsLeft.PageChange(...)`，
    与用户点击侧边栏等价
  - **底部悬浮多选栏**（`CardSelect`）：已选择 N 项 + **启用 / 禁用 / 删除 / 取消选择**，
    进入多选时列表底部留白由 10 改 92（与模组页一致）
- 新增 `PCL.PluginHost.PluginInstaller`：`.pclpkg`（zip）安装（校验 `plugin.json` / `id` / `entry`、
  剥离包内顶层目录、防目录穿越、同 id 覆盖安装）与按目录卸载
- 批量操作语义：启用/禁用写 `plugins.state.json` → 后台重新加载 → 回 UI 刷新；
  删除时**先禁用并重新加载**（卸载程序集、释放文件句柄）再删目录，失败项弹窗列出
- **删除健壮性（修 bug）**：插件程序集被 `AssemblyLoadContext` 加载后，`Unload()` 并不会立刻释放文件句柄
  （实测运行中直接 `Directory.Delete` 必然失败）。现在的语义是「**立即可见地消失 + 下次启动彻底删除**」：
  - 删除时先禁用 → 把目录名登记进 `PCL/PluginData/pending-delete.json`
  - `PluginLoader.ReloadAll()` 会**跳过**待删除目录并写日志，所以列表里立刻不再出现（即使文件还被占用）
  - 随后再尽力立即删一次（`GC.Collect + WaitForPendingFinalizers` 重试 5 次）；删不掉也不弹窗
  - 下次启动时由 `PluginHostBootstrap.Start()` 在**插件尚未加载**的阶段调用 `ProcessPendingDeletes()`
    真正删除，成功后自动抹掉清单
  - 删除成功只弹一条成功提示（`已删除 N 个插件，文件将在下次启动时彻底清理`），不再弹「占用失败」框- 事件签名全部按控件定义对齐：`MyButton.Click → (object, MouseButtonEventArgs)`、
  `MyIconTextButton.Click → (object, ModBase.RouteEventArgs)`、`MyRadioButton.Check → (MyRadioButton, bool)`
- 新增本地化键 30 个（`Tools.Plugin.Manage.Install/Download/SelectAll`、`Filter.*`、`Sort.*`、
  `SelectedCount`、`Action.Delete/CancelSelect`、`Install.*`、`Delete.*` 等），
  zh-CN / zh-TW / en-US / en-GB 均已补
- 实测：装载「正常 / API 不兼容 / 缺清单」三个插件时，列表分别显示 `已启用` / `不兼容（插件要求 99，
  宿主提供 1）` / `出错（缺少 plugin.json）`；在界面上点「禁用/启用」后日志出现
  `插件 sgu.sample.hello 已禁用` → `扫描完成：1 个插件目录，0 个已启用`，状态跨重启保留

## 改动一览

### 1. 移除中国大陆地区功能限制（合并补丁）

PCL: CE 曾通过区域判定限制中国大陆用户下载/安装新的 Minecraft 实例，本分支已完整移除该限制。

- 删除 `PCL.Core/App/Region/RegionPolicy.cs`（区域判定策略与其 Win32 / 注册表信号读取）
- 删除 `Plain Craft Launcher 2/Modules/Minecraft/RestrictedFeaturePolicy.cs`（功能开关封装）
- 删除 `PCL.Core.Test/RegionPolicyTest.cs`（对应单元测试）
- 删除 `ModDownload.McClientFilesAvailableLocally`（仅为限制逻辑服务的本地文件探测）
- 移除全部调用处的限制判断：
  - `ModLaunch`（启动前补全文件）
  - `ModModpack`（整合包安装 / 自动安装）
  - `ModDownloadLib`（下载实例、保存核心文件、合并安装）
  - `PageDownloadCompDetail`、`PageDownloadInstall`、`PageInstanceInstall`、`PageInstanceOverall`、`PageLaunchLeft`
- 删除 `Lang.IsFeaturesUnrestricted` 与配置项 `SystemDebugAllowRestrictedFeature`
- 删除本地化资源 `Region.Feature.Restricted` / `Region.Feature.Unknown`

### 2. 移除“必须绑定至少 1 个正版账号”的限制

- `ProfileUi.CanCreateOtherProfile()` 恒返回 `true`：新建离线 / 外置登录档案不再要求先绑定正版账号
- 移除 `PageLoginProfile` 中的“需要先进行一次正版验证才能使用其他验证方式”提示（`HintMicrosoft`）
- 移除启动前“你必须先登录正版账号才能启动游戏！”与“考虑一下正版？”弹窗（`ModLaunch` 中的正版购买提示块），
  离线账号现在可以直接启动游戏，不再被强制进入体验版或中断启动
- 同时清理对应的本地化资源（`Minecraft.Launch.AccountVerification.*`、
  `Minecraft.Launch.PurchaseHint.*`、`Minecraft.Launch.DemoMode`、
  `Launch.Account.Profile.RequireMicrosoftHint`）

### 3. 登录界面改为“正版 / 离线 / 外置”三选项卡

- `PageLaunchLeft` 顶部新增三个选项卡（复用 PCL CE 既有的 `MyRadioButton` 药丸样式与高亮配色），
  单击即可直接切换到对应的登录分页，不再需要“新建档案 → 选择验证方式”的多步对话框
- 选项卡选中状态会跟随当前登录分页 / 当前档案的验证方式自动同步
- `PageLoginAuth`（外置登录）在选项卡下方、头像上方新增一行
  `验证服务器: [下拉框]`：
  - 复用 PCL CE 既有下拉框控件 `MyComboBox`（与应用内其他下拉框完全一致的样式与交互）
  - 默认值为 LittleSkin（`https://littleskin.cn/api/yggdrasil`）
  - 下拉列表包含 LittleSkin、曾经使用过的验证服务器，以及末项“» 新建验证服务器...”
  - 选择“新建验证服务器...”会弹出既有的验证服务器输入对话框，新建的服务器会写入配置
    `LaunchAuthServerList` 并在下次启动时保留
  - 切换服务器后会自动重新探测该服务器是否支持 OAuth（Yggdrasil Connect）并切换登录方式

### 4. 版本号命名规则

格式：`年份.大更新.小更新(上游版本号)`，例如 `27.0.0(2.12.1)`。

- 年份固定为 `27`，第一版为 `27.0.0`
- 同步上游更新时**下游版本号不变**，只变更括号内的上游版本号
- 实现：
  - `metadata.json`：`version.base = "27.0.0"`（下游）、`version.upstream = "2.12.1"`（上游）
  - `Basics.DisplayVersionName` / `ModBase.versionDisplayName`：`27.0.0(2.12.1)`，用于界面与日志展示
  - `Basics.VersionName` / `ModBase.versionBaseName` / `versionDownstreamName`：`27.0.0`，
    用于 User-Agent 等要求合法 token 的场合与语义化版本解析
  - 程序集版本：`27.0.0.0`

### 5. 在代码层面禁用更新 / 自动更新 / 更新检测 / 云控

- 新增总开关 `UpdateManager.IsUpdateSupported = false`
- 所有更新服务器域名（Pysio / Naids / GitHub / Mirror 酱）改为 `http://127.0.0.1/`
- `UpdateManager.UpdateStart`、`UpdateRestart`、`GetVersionStatus`、`LoadOnlineInfo`
  （自动更新调度）全部短路，不再发起任何更新请求
- `AnnouncementService.Load`（云端公告推送，即云控入口）被短路
- 设置页“更新”界面（`PageSetupUpdate`）全部控件不可操作，并显示提示 **“此版本不支持更新”**
- 整合包导出需要“最新版启动器文件”时，改为直接复制当前可执行文件
- `ModBase.CanFeedback`、崩溃分析中的“启动器版本过旧”建议等依赖更新检测的逻辑随之稳定（不再联网）

## 构建

.NET SDK 10 构建（与 `.github/workflows/reusable-build.yml` 一致）：

```bash
dotnet publish "Plain Craft Launcher 2/Plain Craft Launcher 2.csproj" \
  -p:Configuration=Release -p:Platform=x64 \
  -p:DeleteExistingFiles=true -o ./artifact-x64 --no-self-contained
```

`-p:Platform` 可取 `x64` 或 `ARM64`（分别对应 `win-x64` / `win-arm64` 运行时标识）。
产物为单文件 `Plain Craft Launcher Community Edition.exe`，运行需安装 .NET 10 桌面运行时。

---

# 27.1-snapshot-21 更新内容

> 版本号：**27.1-snapshot-21(2.15.1)**　构建产物：`PCLCE For SGU 27.1-snapshot-21(2.15.1).exe`
> 决策来源：用户反馈「内置的插件没法在插件管理找到，且没法启用」+「Minecraft 下载限制要做成下载页屏蔽」

### ① 插件真正内置进 exe（三个包全部嵌入）

- snapshot-19 只是把 `.pclpkg` 放在产物目录的 `plugins\` 里，用户只拷 exe 时不生效（=「插件管理里找不到」）
- 现在非正式版构建会把插件包**作为程序集资源嵌进 exe**：
  - `Plain Craft Launcher 2.csproj`：`<EmbeddedResource Include="..\plugins\dist\*.pclpkg" Condition="'$(IncludeBundledPlugins)'=='true'" />`
  - `build.sh`：先打包插件 → 再 `dotnet publish -p:IncludeBundledPlugins=$([ -n suffix ] && echo true || echo false)`
  - `PluginHostBootstrap.InstallEmbeddedPackages()`：启动时把内嵌的 `*.pclpkg` 释放到临时目录并用 `PluginInstaller.InstallPackage` 装进 `PCL\Plugins`，随后 `Initialize()` 扫描加载
  - `EnsureBundledPluginsInstalled()` 仍保留 exe 同级 `plugins\` 目录的兼容路径（手动放置也能装）
- **内置三个插件**：`SguPack`（SGU 插件包）、`UnlockLimits`（限制解除）、`HelloPlugin`（samples 里的示例插件，用于验证插件系统本身）

### ② Minecraft 下载页改为「中国大陆地区屏蔽」

- `PageDownloadClient`：未启用 `unlock.region` 时，整页换成与反馈页同一个占位控件
  `MyUnavailablePlaceholder`（红色镐子 + 红叉 + 横杠，入场动画同 MyLoading 错误态），下方文案：
  > 应相关法律法规要求，PCL Swordsman 无法在中国大陆地区提供 Minecraft 下载服务，红豆泥斯密马赛！
- 启用「限制解除」插件后，`FeatureFlags.IsEnabled("unlock.region")` 为真，页面恢复正常
- 该判定**不做 IP 归属地检测**：只看插件是否提供 `unlock.region`，行为确定、可复现、可离线验证
- 新增本地化键 `Download.RegionBlocked.Message`（zh-CN / zh-TW / en-US / en-GB）

### ③ 限制解除插件升级到 1.1.0

- 说明与日志改为「解除下载页的中国大陆地区屏蔽，离线 / 外置档案不再要求先绑定正版账号」
- 仍为纯声明插件（不含行为代码），解除逻辑全部在宿主

### ④ 插件管理页与宿主初始化的竞态

- 新增 `PluginHostBootstrap.Ready` 事件（插件加载完 + 功能开关应用完时触发）
- `PageToolsPluginManage` 订阅它，触发后重新订阅 `PluginHostManager.Changed` 并刷新列表，
  修掉「启动后立刻打开插件管理 → 列表一直空白（插件其实已经装好了）」的竞态

### ⑤ 版本号

- `suffix` → `snapshot-21`，`code` `543` → `544`

---

# 27.1-snapshot-19 更新内容

> 版本号：**27.1-snapshot-19(2.15.1)**　构建产物：`PCLCE For SGU 27.1-snapshot-19(2.15.1).exe`
> 决策来源：用户要求「被屏蔽的功能改为由插件实现，做成 SGU 插件包 + 限制解除两个插件；
> 除正式版外都不需要功能屏蔽」

### ① 功能屏蔽从「构建类型」改为「插件是否提供」

- 新增核心功能开关 `PCL.Core/App/FeatureFlags.cs`：启动时由**已启用插件**的
  `plugin.json → provides` 填充，宿主据此决定要不要展示被屏蔽的功能
- 插件 SDK 扩展（`PCL.PluginSdk`）：
  - `PluginFeatureKeys`：`themes.extended` / `echo-cave` / `unlock.region` / `unlock.offline-auth`
  - `IExtraThemesProvider`（提供主题名称 / 主色 / 特殊配色风格）、`IEchoCaveQuotesProvider`（提供语录）
  - 清单新增 `provides` 数组
- 插件宿主（`PCL.PluginHost`）新增 `ProvidedFeatures` 与 `GetProviders<T>()`
- 关键原则：**实现仍留在宿主**（主题引擎、回声洞卡片、登录/下载判定），插件只声明功能并提供数据，
  因此渲染、门禁与安全检查依旧由宿主掌握

### ② 两个插件

| 插件 | id | 提供 | 内容 |
| --- | --- | --- | --- |
| **SGU 插件包** | `sgu.pack` | `themes.extended`、`echo-cave` | 剑客黄（`#E8B33C`，`golden-dusk` 神圣黄昏配色）、初音绿（`#39C5BB`）、烤音绿（`#33CCBB`）+ 回声洞 12 条语录；后续更新只改插件 |
| **限制解除** | `sgu.unlock-limits` | `unlock.region`、`unlock.offline-auth` | 解除中国大陆地区相关下载限制；解除「必须先绑定正版账号才能创建离线 / 外置档案」的前置要求（纯声明，不含行为代码） |

- 源码位于 `plugins/SguPack/`、`plugins/UnlockLimits/`，打包脚本 `plugins/package.sh`（Linux/Termux）与 `plugins/package.ps1`（Windows）
  （用 python3 的 zipfile 打包，Termux 无 zip 命令）
- 宿主侧接入：`ThemeService.IsExtendedThemesEnabled` 改看功能开关、主题主色与显示名优先取插件定义；
  `ModEchoCave.SetQuotes()` 接插件语录、百宝箱回声洞卡片按开关显示；
  `ProfileUi.CanCreateOtherProfile()` 恢复「必须先有正版档案」的上游口径，由 `unlock.offline-auth` 解除

### ③ 随包分发策略（沿用「预览版 B / 正式版 A」）

- `build.sh` 现在会先构建插件包，然后：
  - **非正式版**（`suffix` 非空，即 snapshot / pre / rc / experiment / hotfix）：
    把 `.pclpkg` 放进产物目录的 `plugins/`，启动器首次运行**自动安装并启用**
  - **正式版**（`suffix` 为空）：产物目录不带插件，插件包只放到 `dist/` 与 `dist/plugins/`，
    由用户手动安装
- 这样正式版本体不含非蓝色主题、回声洞等被屏蔽功能（满足原作者赞助方政策），
  同时不牺牲非正式版的测试便利

### ④ 版本号

- `suffix` → `snapshot-19`，`code` `541` → `542`

---

# 27.1-snapshot-18 更新内容

> 版本号：**27.1-snapshot-18(2.15.1)**　构建产物：`PCLCE For SGU 27.1-snapshot-18(2.15.1).exe`
> 决策来源：用户反馈「联机还是有问题」并提供现场日志 `Launch-2026-10-25-153700004.log`

### ① 陶瓦联机：修掉写错的 EasyTier 参数（这是联机一直不通的总根因）

日志里的内核输出是决定性的：

```
EasyTier Core 已退出（退出码 2）。最后的输出：
error: unexpected argument '--disable-sys-hole-punching' found
  tip: a similar argument exists: '--disable-sym-hole-punching'
```

- **参数名拼错**：`EasyTierEntity` 传的是 `--disable-sys-hole-punching`，EasyTier **没有这个参数**
  （正确写法是 `disable-sym-hole-punching`，sym = symmetric）。注意同一仓库的旧联机实现
  `PCL.Core/Link/EasyTier/ETController.cs` 里拼写是**对的**，只有 TerraCotta 这一份写错了
- **触发条件**：`TryPunchSym`（界面「端口猜测 NAT」）为 `false` 时会追加这个参数，而**它默认就是 false**，
  于是默认配置下内核一启动就以退出码 2 死掉
- **后果链条**：房主点「创建房间」→ 内核秒退，但建房间流程只看 `Launch()` 的返回值（进程确实起来了）
  → 界面显示创建成功并给出房间号；房客加入后只能看到**自己**一个节点，永远等不到房主 → 卡在「正在加入……」
- 修复：改成 `disable-sym-hole-punching`，并加注释说明为什么会踩

### ② 房主侧增加内核存活检查（防止「假成功」）

- `ScaffoldingFactory.CreateServer` 在启动内核后等待 2.5 秒，若内核已退出则直接抛错并附上
  内核输出（`WaitCoreExited` / `GetCoreOutputTail`）。这样参数不被版本接受、缺依赖等问题
  会在**创建房间这一步**就暴露，而不是让房主拿着一个假的房间号干等

### ③ 顺带确认（snapshot-14 起累积的效果）

- 日志里 `仍在等待房主的 EasyTier 节点出现（已等待 93 秒，最多 180 秒）` 说明 snapshot-16 的
  超时/进度日志在正常工作；`Et Cli` 的 JSON 解析也已能正常读出节点（`Find player: 7b8a278f…`）
- 公共节点接口 `uptime.easytier.cn` 在本机 DNS 解析失败（`DNS resolution failed`），程序按设计
  回落到 6 个兜底中继继续工作，不影响结论

### 版本号

- `suffix` → `snapshot-18`，`code` `540` → `541`（`base` 仍 `27.1`，`upstream` 仍标注 `2.15.1`）

---

# 27.1-snapshot-17 更新内容

> 版本号：**27.1-snapshot-17(2.15.1)**　构建产物：`PCLCE For SGU 27.1-snapshot-17(2.15.1).exe`
> 决策来源：用户反馈「还报错」，并提供了现场日志 `Launch-2026-10-25-152936703.log`

### 正版登录：修掉「设备码窗口根本没弹」的真凶（`\p{XDigit}` 正则）

日志给出的异常链是决定性的：

```
[Profile] 未配置 Microsoft OAuth Client ID，使用内置的微软第一方客户端 ID（00000000402b5328）+ live.com 旧版流程。
System.TypeInitializationException: The type initializer for 'MicrosoftProvider' threw an exception.
 ---> System.Text.RegularExpressions.RegexParseException: Invalid pattern
      '^\p{XDigit}{8}-\p{XDigit}{4}-\p{XDigit}{4}-\p{XDigit}{4}-\p{XDigit}+$' at offset 11. Unknown property 'XDigit'.
   at PCL.Core.Minecraft.Profile.Authentication.MicrosoftProvider..cctor()
```

- **根因**：`\p{XDigit}` 是 **Java / ICU 的正则写法**（从 RaphiMC/MinecraftAuth 的 Java 源码里照抄过来的），
  **.NET 不支持**这个 Unicode 属性名。它被写在 `static readonly` 字段的初始化器里，
  于是**类型初始化器抛异常** → `new MicrosoftProvider()` 直接抛 `TypeInitializationException`
  → 登录在「拿到 client id」之后立刻失败，**设备码弹窗根本没有机会出现**
- **修复**：删掉这条正则，改用 `Guid.TryParseExact(clientId, "D", out _)` 判断 Client ID 形式
  （GUID → Entra ID v2；否则 → live.com 旧版）。**不手写正则，也就没有任何解析期风险**
- 已用独立小程序验证分类结果：`00000000402b5328` / `000000004C12AE6F` → 旧版流程；
  `8f8c1f75-31e4-4bd2-a2f4-0d1e5c1dc0a2` → Entra ID v2；乱码 → 旧版流程（兜底不会崩）

### 版本号

- `suffix` → `snapshot-17`，`code` `539` → `540`（`base` 仍 `27.1`，`upstream` 仍标注 `2.15.1`）

---

# 27.1-snapshot-16 更新内容

> 版本号：**27.1-snapshot-16(2.15.1)**　构建产物：`PCLCE For SGU 27.1-snapshot-16(2.15.1).exe`
> 决策来源：用户「100% 没法注册应用了，都没卡，所以还是用你那个方法，但仍有 Bug，没修」

### 正版登录：重新启用微软第一方 ID + live.com 旧版设备码流程（并修掉三个可疑点）

snapshot-15 曾把登录代码整体回滚到上游；本次按用户决定重新启用第一方路线，
`MicrosoftProvider.cs` 在 snapshot-14 基础上**多修了三处**：

| 修复 | 说明 |
| --- | --- |
| **旧档案刷新失败不再阻断登录** | `AuthenticateAsync` 里若带 `RefreshToken`（已有微软档案）而刷新失败，原逻辑直接抛出、本次登录就结束了。旧档案的刷新令牌往往来自另一套流程/另一个客户端 ID，必然刷新失败。现在只要还有设备码处理器，就记 Warn 并**改用全新的设备码登录** |
| **补上「账号类型不支持」提示** | 旧版流程只认**个人**微软账号。轮询返回 `invalid_grant`/`access_denied`/`unauthorized_client` 时，错误详情里追加一句：单位/学校账号（`@xxx.onmicrosoft.com`）与未创建 Xbox 档案的账号都无法通过这一步 |
| **网络失败给出可操作提示** | 连不上 `login.live.com` 时抛出 `network_error`，提示把 `login.live.com`、`login.microsoftonline.com` 设为直连（加速/代理常拦这两个域） |
| 日志更细 | 记录所选流程与 Client ID、设备码响应（Debug 级含完整 JSON）、设备码与验证地址、轮询错误、Xbox 票据前缀与每种前缀的失败原因（含 HTTP 状态码 + 响应体） |
| Xbox 票据前缀 | 仍为 `t=`（第一方）→ `d=` → 无前缀 三档兜底，成功的那一档写进日志 |

### 版本号

- `suffix` → `snapshot-16`，`code` `538` → `539`（`base` 仍 `27.1`，`upstream` 仍标注 `2.15.1`）

---

# 27.1-snapshot-15 更新内容

> 版本号：**27.1-snapshot-15(2.15.1)**　构建产物：`PCLCE For SGU 27.1-snapshot-15(2.15.1).exe`
> 决策来源：用户要求「正版登录相关代码回滚到 snapshot-8 的状态」

### 正版（Microsoft）登录：代码回到 snapshot-8 = 上游原样

- `PCL.Core/Minecraft/Profile/Authentication/MicrosoftProvider.cs` 与
  `Utils/XboxUtils.cs` 已**逐字节还原为上源源码**（md5 `dbc6e74a…`、`399d14ec…`，与
  `PCL-CE-dev.zip` 内的上游快照一致），snapshot-12 的 live.com 旧版流程、`t=`/`d=`/无前缀兜底、
  以及 snapshot-14 的 XboxUtils 错误详情增强**全部撤销**
- 正版登录区块的其余文件（`ProfileService.cs`、`ProfileManagement.cs`、`MojangUtils.cs`、
  `PageLoginMs.xaml.cs`、`MyMsgLogin.xaml.cs`）经 diff 确认**本来就与上游一致**，无需改动
- 因此行为回到 snapshot-8：`Secrets.MSOAuthClientId` 为空时抛
  `Microsoft OAuth client id is not configured.`。要启用正版登录，仍需自建 Azure 应用并在构建时注入
  `PCL_MS_CLIENT_ID`（见 `docs/MICROSOFT-OAUTH-SETUP.md` §2~§3），**代码无需改动**

### 版本号

- `suffix` → `snapshot-15`，`code` `537` → `538`（`base` 仍 `27.1`，`upstream` 仍标注 `2.15.1`）

---

# 27.1-snapshot-14 更新内容

> 版本号：**27.1-snapshot-14(2.15.1)**　构建产物：`PCLCE For SGU 27.1-snapshot-14(2.15.1).exe`
> 决策来源：用户明确要求「完全无法创建租户，还是搞微软第一方 ID + live.com 旧版流程」

### 正版（Microsoft）登录：启用微软第一方 ID + live.com 旧版设备码流程

**背景**：上游 PCL CE 的正版登录依赖他们 CI 里注入的 `MS_CLIENT_ID`（自建 Azure 应用），本仓库没有；
而用户侧无法创建 Azure 租户（`AADSTS50020`，`admin.microsoft.com` 直接 401，SGU 租户无管理员角色）。
因此改走微软第一方（Title）客户端 ID 路线，**无需注册任何应用即可登录个人微软账号**。

实现要点（`PCL.Core/Minecraft/Profile/Authentication/MicrosoftProvider.cs`）：

| 项目 | 说明 |
| --- | --- |
| 流程分流 | 按 Client ID 形式自动选择：GUID（自建应用）→ Entra ID v2 设备码；非 GUID（内置 `00000000402b5328`）→ live.com 旧版设备码 |
| Client ID 解析 | 调用方传入 → 环境变量 `PCL_MS_CLIENT_ID` → 构建期注入的 `MS_CLIENT_ID` → 内置第一方 ID |
| 旧版设备码 | `POST https://login.live.com/oauth20_connect.srf`（`response_type=device_code`、`scope=service::user.auth.xboxlive.com::MBI_SSL`），返回 `verification_uri=https://www.microsoft.com/link` |
| 令牌换取/刷新 | `POST https://login.live.com/oauth20_token.srf`（`grant_type=device_code` 轮询 / `refresh_token` 刷新） |
| **Xbox 票据前缀** | **第一方客户端用 `t=`**，自建应用用 `d=`（依据 RaphiMC/MinecraftAuth 的 `isTitleClientId() ? "t=" : "d="`）。snapshot-12 漏了 `t=`、只用 `d=` 与无前缀重试，这是那一版失败的很可能原因；现在按 `t=` → `d=` → 无前缀 顺序兜底 |
| 诊断日志 | 记录所选流程与 Client ID、设备码与验证地址、Xbox 认证成功时使用的票据前缀；失败时逐个前缀打印原因 |
| 错误详情 | `XboxUtils.AuthenticateAsync` 任何非 2xx 都抛出 `IdentityModelAuthenticationException`，附带 HTTP 状态码与响应体（上游只在 XSTS 失败时带详情，Xbox Live 失败只抛无上下文的 `HttpRequestException`） |

风险提示（写在本仓库文档里，交接必需）：

- 该链路依赖**微软对第三方复用第一方客户端的默许**，不是官方支持路径；微软若关停，
  所有用户会同时无法登录，且无法通过配置补救（届时应回到自建 Azure 应用路线，见
  `docs/MICROSOFT-OAUTH-SETUP.md`）
- 第一方流程只支持**个人微软账号**；单位/学校账号（如 `@xxx.onmicrosoft.com`）无法登录，这与 Minecraft 本身的账号体系一致

### 版本号

- `suffix` → `snapshot-14`，`code` `536` → `537`（`base` 仍 `27.1`，`upstream` 仍标注 `2.15.1`）

---

# 27.1-snapshot-13 更新内容

> 版本号：**27.1-snapshot-13(2.15.1)**　构建产物：`PCLCE For SGU 27.1-snapshot-13(2.15.1).exe`
> 来源：用户实测反馈「正版还是没修好」「陶瓦联机现在卡正在加入」「占位图标动画不对、红叉少一条横杠」

### ① 正版（Microsoft）登录：**按用户要求回滚到上游原始代码**

- `PCL.Core/Minecraft/Profile/Authentication/MicrosoftProvider.cs` 与
  `Utils/XboxUtils.cs` 已**逐字节还原为上源源码**（取自 `PCL-CE-dev.zip` 中的上游快照，md5
  `dbc6e74a…`、`399d14ec…`），删除了 snapshot-11/12 里下游自加的
  「按 Client ID 形式自动切换 live.com / Entra ID v2 流程」「RpsTicket 双形式重试」
  「XboxUtils 错误详情」等全部改动
- 同时删除下游自加的配置项 `SystemMsClientId`
- 因此上游的原始行为恢复：**`Secrets.MSOAuthClientId` 为空时会抛
  `Microsoft OAuth client id is not configured.`**。这是上游设计——客户端 ID 由 CI 密钥注入：
  ```bash
  # 构建时把密钥烧进程序集（EnvironmentInteropGenerator 读取 PCL_* 环境变量）
  PCL_WRITE_SECRET=1 PCL_MS_CLIENT_ID=<你的 Azure 应用 GUID> ./build.sh x64 Release
  ```
  上游 PCL CE 官方构建用的就是他们自己的 Azure 应用 ID，本仓库没有该密钥，
  所以**在没有自有 Client ID 之前，正版登录不可用**（这是预期行为，不再由下游改动绕过）

### ② 陶瓦联机「加入房间」卡在「正在加入……」：改成会真正失败并说明原因

- **根因**：`EasyTierEntity.CheckEasyTierStatusAsync` 用「重试 300 次」当超时，而不是按时间。
  每次 `easytier-cli` 调用在拿不到结果时会一直等到超时（上游 5 秒、snapshot-12 放宽到 15 秒），
  于是 300 次 × 每次十几秒 ≈ **十几分钟甚至更久的「正在加入……」**；如果 EasyTier 内核启动即崩溃，
  它会一直在这个循环里等下去，因为没人检查进程是否还活着
- 修复：
  - 等待改为**按时间计**：`ReadyTimeout = 180 秒`，超时立刻返回失败（界面不会再无限转圈）
  - **内核退出即快速失败**：新增 `_coreExited` 标志 + `HasCoreExited`，内核一旦退出，
    立即中止等待并记录退出码与最后输出
  - **捕获 EasyTier 内核输出**：`easytier-core.exe` 改为重定向 stdout/stderr 并异步持续读取，
    在内存里保留尾部（约 40 KB）。内核启动失败、参数不兼容、端口占用等原因现在会**直接写进日志**
  - 日志里新增周期提示：「仍在等待房主的节点出现（已等待 N 秒，最多 180 秒）」「已发现房主但延迟 N ms 不达标」
  - 公共中继列表增强：除 6 个兜底地址外，再向 EasyTier 官方节点接口
    （`uptime.easytier.cn`，最多等 4 秒）取一份实时节点列表，最多取 8 个中继下发；
    实际使用的地址会写进日志。中继是双方唯一的碰面途径，多给几个候选能显著提高成功率
  - `ScaffoldingFactory.CreateClientAsync` 现在会检查 `Launch()` 的返回值（原来忽略了），启动失败直接抛错

### ③ 反馈 / 更新页占位图标：补齐横杠 + 复用下载报错页的动画

- **缺的那条横杠**：`MyLoading` 的错误态里除了镐子与红叉，还有一个
  `Rectangle Width=25 Height=2 Fill=Foreground VerticalAlignment=Bottom HorizontalAlignment=Left`
  的下划线，之前的占位控件漏掉了，现已补上
- **动画换成下载报错页那一套**：删掉下游自造的「呼吸动画」（整体缩放 1→1.07 + 叉透明度起伏），
  改为逐条复用 `MyLoading.ErrorAnimation` 的入场动画：
  1. 前景色 300 ms 由主题色渐变为 `ColorBrushRedLight`
  2. 红叉淡入（100 ms）
  3. 红叉从 0.6 倍弹性放大回原尺寸（400 ms，`AniEaseOutBack`）
- 图形绑定方式也与 `MyLoading` 一致：`Foreground` 由控件自身 DP 提供，
  镐子 / 红叉 / 横杠 / 说明文字全部 `{Binding Foreground, ElementName=PanBack}`，
  因此颜色渐变对整组元素同时生效

### ④ 版本号

- `suffix` → `snapshot-13`，`code` `535` → `536`（`base` 仍 `27.1`，`upstream` 仍标注 `2.15.1`）

---

# 27.1-snapshot-12 更新内容

> 版本号：**27.1-snapshot-12(2.15.1)**　构建产物：`PCLCE For SGU 27.1-snapshot-12(2.15.1).exe`
> 来源：用户实测反馈「EasyTier 那个问题没修，而且正版还是无法登录」+ 官方开源部分（`PCL-2.99.0.0`）比对

### ① 正版（Microsoft）账号登录：修掉 `AADSTS700016`

- **现象**：登录报
  `认证失败: unauthorized_client - AADSTS700016: Application with identifier '00000000402b5328' was not found in the directory '9188040d-6c67-4c5b-b112-36a304b66dad'`
- **根因**：`00000000402b5328` 是**微软第一方（Title）客户端 ID**，只适用于 `login.live.com` 的旧版设备码流程。
  snapshot-11 把它送进了 Entra ID v2 端点（`login.microsoftonline.com/consumers/oauth2/v2.0/devicecode`），
  该租户（consumers）里没有这个应用，必然 700016。**v2 端点只接受自行注册的 GUID 客户端 ID**
- **修复**：`MicrosoftProvider` 按 Client ID 形式自动选择流程
  | Client ID 形式 | 流程 | 端点 | 权限范围 |
  | --- | --- | --- | --- |
  | GUID（`8f8c1f75-…`，自行注册的 Azure 应用） | Entra ID v2 设备码 | `login.microsoftonline.com/consumers/oauth2/v2.0/*` | `XboxLive.signin offline_access` |
  | 其他（内置 `00000000402b5328`） | live.com 旧版设备码 | `login.live.com/oauth20_connect.srf`、`oauth20_token.srf` | `service::user.auth.xboxlive.com::MBI_SSL` |
- 已用 `curl` 实测：内置 ID 请求 `oauth20_connect.srf` 正常返回 `user_code` / `device_code` /
  `verification_uri=https://www.microsoft.com/link` / `interval=5` / `expires_in=900`；未授权时令牌端点返回
  `authorization_pending`（与 `MyMsgLogin` 的轮询分支匹配）
- 旧版流程的 Xbox Live 用户认证会依次尝试 `d=<accessToken>` 与裸 token 两种 `RpsTicket` 形式，避免前缀差异导致失败
- 想改用自有 Azure 应用：填配置项 `SystemMsClientId`（或环境变量 `PCL_MS_CLIENT_ID`、CI 密钥 `MS_CLIENT_ID`）即可，无需改代码
- 附带修复诊断：`XboxUtils.AuthenticateAsync` 任何非 2xx 都会抛 `IdentityModelAuthenticationException` 并附 HTTP 状态码与响应体（原先只有 XSTS 失败才带详情，Xbox Live 失败只会得到无上下文的 `HttpRequestException`）

### ② 陶瓦联机：修掉 `Failed to get EasyTier player list info`

- **根因**：`EasyTierEntity._GetPlayersAsync` 用 `JsonCompat.ParseNode(output)` 直接解析 CLI 输出。
  只要输出不是合法 JSON（EasyTier Core 的 RPC 端口还没起来时 CLI 会打印错误、日志混进 stdout、退出码非 0），
  就会抛 `JsonException` → 被通用 catch 记成这条 Error → 玩家表整体失败 → 房客永远找不到房主
- **修复**：
  - 新增 `_ExtractPeerNodes()`：整段解析失败后截取第一段 `[...]` / `{...}` 再试；兼容 `{ "peers": [...] }`、
    `{ "data": [...] }` 等包装形式；输出为空时单独给出「还没连上 RPC 端口」的提示
  - `ETPeerInfo` 去掉全部 `required` 标记（缺字段不再让整表解析失败），旧 `ETInfoProvider` 同步做空值防护
  - 节点解析改为 `_TryParsePeer()`：字段名多别名（`hostname`/`host_name`/`name`、`lat_ms`/`latency_ms`/`ping`、
    `loss_rate`/`loss`、`nat_type`/`nat`、`version`/`easytier_version`）+ 大小写不敏感 + 数值宽松解析
  - CLI 读取超时 5s → 15s（Windows Defender 首次扫描刚下载的 exe 很容易超过 5s），超时按 Warn 记录并继续重试
  - 日志增强：记录完整命令行、退出码、原始输出；每 10 秒汇报「仍在等待房主节点 / 已发现房主但延迟不达标」；
    超时后提示可改为「允许公共中继」再试
  - **新增「联机诊断」按钮**（陶瓦联机 → 高级设置）：一键汇总 EasyTier 三个文件是否存在、当前 `easytier-core` 进程与其命令行、
    联机开关/连接方式/协议偏好、当前联机状态与房间号、以及日志里最近 60 条 `EasyTier` / `Et Cli` / `Lobby` 相关行，
    **自动复制到剪贴板**并弹出预览。下次出问题时直接让用户发这段内容即可
  - 解析逻辑已用本机实测的 EasyTier 2.6.4 `easytier-cli -o json peer` 真实输出 + 6 组异常样例（日志混入、
    `{ "peers": [...] }` 包装、字段名变体、`12ms`/`1.5%` 单位、CLI 报错文本、空输出）单独验证通过

### ③ 同步官方开源部分的「百宝箱 → 内存优化」

- **先说结论**：官方开源部分（`PCL-2.99.0.0`）里 **`PageOtherTest.MemoryOptimize` / `MemoryOptimizeInternal` 的实现是被替换过的**
  （内容为「为便于维护，开源内容中不包含百宝箱功能」），所以「同步」= **移植全部接线 + 按官方行为自行实现优化内核**
- 移植的接线（与官方一一对应）：
  - 配置项 `LaunchArgumentRam`（设置 → 启动 → 内存分配 → 「启动游戏前进行内存优化」）
  - 配置项 `VersionRamOptimize`（版本独立设置 → 内存 → 跟随全局设置 / 开启 / 关闭）
  - 启动加载项 `ModLaunch.McLaunchMemoryOptimize`（`ProgressWeight = 30`，官方同款进度推进节奏，启用时不阻塞补全文件）
  - 命令行参数 `--memory`：静默执行内存优化后直接退出
  - 自定义事件 `内存优化`（已从 `EventTypeMapper.UnsupportedTypes` 移除，`EventType.MemoryOptimize` 落地）
  - 开启时弹出官方同款提醒（延长启动耗时 / 机械硬盘会造成短暂卡顿；非管理员再追加「需要管理员权限」说明）
- 自行实现的优化内核 `PCL.Core/Utils/OS/MemoryOptimizer.cs`：
  1. `AdjustTokenPrivileges` 提升 `SeProfileSingleProcessPrivilege`、`SeIncreaseQuotaPrivilege`、`SeDebugPrivilege`
  2. `NtSetSystemInformation(SystemMemoryListInformation, …)` 清空系统备用内存列表（standby list；指令编号 4/5 两种资料写法都尝试）
  3. `psapi!EmptyWorkingSet` 逐个裁剪进程工作集（权限不足的进程自动跳过）
  4. `GlobalMemoryStatusEx` 前后对比，日志与提示给出释放量
- **与「清理垃圾」的区别（别搞混）**：内存优化**不删除任何文件**，只回收物理内存；「清理垃圾」才会删缓存与日志
- 入口有两处：更多 → 百宝箱 → 「内存优化」按钮、以及启动前自动执行

### ④ 拖入网页链接 → 一键添加外置登录（对齐 HMCL）

- `FormMain.FrmMain_Drop` 新增 http(s) 链接分支：`AuthServerResolver` 依次尝试
  「已知站点映射 → 链接本身 → 链接 + `/api/yggdrasil` → 站点根 + `/api/yggdrasil`」，
  并遵循 authlib-injector 的 `X-Authlib-Injector-API-Location` 响应头，最终以「返回含 `meta` / `signaturePublickey` 的 JSON」为有效判据
- 例：把 `https://manual.littlesk.in/yggdrasil/` 拖进启动器 → 自动解析为 `https://littleskin.cn/api/yggdrasil`，
  确认后记录进验证服务器下拉框并切到外置登录页
- 新增文案：`Main.FileDrag.AuthlibUrlResolving` / `.AuthlibUrlFound` / `.AuthlibUrlUnrecognized`（zh-CN / zh-TW / en-US / en-GB）

### ⑤ 版本号

- `Plain Craft Launcher 2/metadata.json`：`suffix` → `snapshot-12`，`code` `534` → `535`（`base` 仍为 `27.1`，`upstream` 仍标注 `2.15.1`）

---

# 27.1-snapshot-8 更新内容

> 版本号：**27.1-snapshot-8(2.15.1)**　构建产物：`PCLCE For SGU 27.1-snapshot-8(2.15.1).exe`
> 本轮性质：**版本号标注更正 + 重新发版，不含任何代码合并或功能变更**

### ① 更正「已同步上游版本」标注：`2.12.1` → `2.15.1`

- `Plain Craft Launcher 2/metadata.json`：`upstream` 由 `2.12.1` 改为 `2.15.1`；
  `base` 保持 `27.1`，`suffix` → `snapshot-8`，`code` 530 → `531`
- 界面/日志展示版本号随之变为 `27.1-snapshot-8(2.15.1)`；程序集版本仍为 `27.1.0.0`（四段格式不变）

### ② 为什么原来的 `2.12.1` 是错的（重要，避免再次踩坑）

- 上游 PCL CE 的 `metadata.json` 里也有一个 `upstream` 字段，但它的含义是
  **「本作继承自原版 PCL2 的版本号」**，从 fork 起固定为 `2.12.1`，**不随上游版本推进**
  （上游 `2.15.1`、`2.15.1-beta.1` 的 metadata 里它都是 `2.12.1`，变的是 `base` 字段）
- 先前把上游 metadata 的 `upstream` 当成了「上游启动器版本」，因此本分支一直标注为 `(2.12.1)`
- 三方文件比对核实：本分支真实代码基点是**上游 `dev` @ `d1149d5`（2026-10-24）**，
  即 2.15.1 正式版（2026-10-20，`code` 524）之后的 dev 快照
  ——该快照与本分支源码除 65 处下游改动外**逐字节相同**

### ③ 本轮没有做的事（以及原因）

- **没有**「回退到 `v2.15.1-beta.1`」：该 tag（2026-09-04，`code` 523）比当前代码**更旧**，
  照字面回退会让 73 个纯上游文件退回旧版（含 2.15.1 正式版对 Tooltip / 动画 / 网络层 / 内核互操作的修复，
  以及 `#3673`「中国大陆地区禁用 MC 安装」——而本分支正是删掉该限制的一方）
- **没有**改动任何下游功能开关（更新/反馈/云控仍禁用、发行版主题门禁不变、陶瓦联机仍为纯 P2P）
- 版本号更正的方法与「下次真正需要同步上游时怎么做」已写入
  [`docs/HANDOVER.md`](docs/HANDOVER.md) §4.1 / §4.2

### ④ 新增 Windows 构建脚本 `tools/build.ps1`

- `build.sh` 依赖 `bash` + `python3`，Windows 上通常没有；新增等价的 PowerShell 脚本，
  逻辑一致（读 `metadata.json` → `dotnet publish` → 重命名产物），不改动 `build.sh` 本身
- 该脚本必须保持 **UTF-8 with BOM**：Windows PowerShell 5.1 对无 BOM 的 UTF-8 会按 GBK 解码，
  中文注释可能吞掉换行并导致 `param` 块解析失败（已实测踩到）
- 已在 Windows + .NET SDK 10.0.402 上用该脚本跑通，产物 `23,657,253` 字节

---

# 27.1-experiment-2 更新内容

> 版本号：**27.1-experiment-2(2.12.1)**（下游 `27.1-experiment-2` + 上游 `2.12.1`）
> 构建产物：`PCLCE For SGU 27.1-experiment-2(2.12.1).exe`

### ① 设置 → 更新：不可选的半透明灰白

- 更新设置页中全部选项（更新通道、自动更新、Mirror 酱 CDK、检查更新、查看更新日志）均不可操作
- 控件统一呈现为不可选的半透明灰白色（`IsEnabled = false` + `Opacity = 0.5`，即 PCL 既有的
  `ColorBrushGray5 / ColorBrushGray6` 禁用配色叠加半透明效果）
- 页面顶部保留红色/黄色提示「此版本不支持更新」

### ② 恢复「联机大厅」

- `PCL.Core/App/Secrets.cs`：`LinkServers` 在未注入 `LINK_SERVER_ROOT` 密钥时回退到内置的官方
  公共联机根服务器（`https://s3.pysio.online/pcl2-ce`、`https://staticassets.naids.com/resources/pclce`）
- 由此启用的最小化联网模块：大厅公告/可用状态与中继列表接口（`/api/link/v2/*`）、
  EasyTier CLI 下载（官方静态资源镜像）与大厅 Scaffolding 连接；更新、云端公告等功能仍保持禁用
- 说明：大厅的中继节点（`et.pclc.cc`）与用户所在网络相关，需网络可达才能建立 P2P 连接；
  联机数据上报所需的 `TelemetryKey` 未注入，属于**可选**上报，缺失时会自动跳过且不影响联机

### ③ 首次启动弹窗内容 dump

- 新增 [`docs/FIRST-RUN-DIALOGS.md`](docs/FIRST-RUN-DIALOGS.md)：按启动顺序完整列出
  环境警告、协议授权、遥测、社区版说明、更新日志、主页重置、联机大厅条款等弹窗的
  触发条件、标题、正文原文、按钮、代码位置与**重新触发方法**
- 本次构建将内部版本代号提升到 `525`，因此从上一版升级时会重新走一次升级流程，
  可借此再次看到「PCL CE 版本说明」与「更新日志」弹窗

### ④ 禁用「反馈」

- 新增总开关 `ModBase.IsFeedbackSupported = false`
- 设置侧边栏「反馈」选项卡图标改为红色带叉图标（`Images/Icons/Unavailable.png`），
  并附带提示气泡
- 反馈页顶部显示红色提示：**PCLCE For SGU是PCL社区版的下游项目，请勿向社区版团队提供任何反馈！**
  「前往反馈」按钮变为不可点击的「反馈功能已禁用」；不再请求上游 GitHub Issues 列表
- `ModBase.CanFeedback / Feedback / _ShowFeedbackPrompt` 与 `PageSetupLeft.TryFeedback`
  全部短路，错误提示不再引导用户向上游社区提交反馈

### ⑤「软件信息」页新增下游项目声明

- `PageSetupAbout` 顶部新增「下游项目声明」卡片，明确写出：
  **PCLCE For SGU是由剑客群组服服主团队联合维护的PCL社区版的下游项目，PCL：CE的版权归PCL Community所有**，
  并补充：本作非 PCL Community / 龙腾猫跃官方发布、上游不承担责任、反馈渠道说明、
  许可与版权（Apache-2.0 与上游第三方许可）说明，附上游源码 / 原作 / 官网按钮

### ⑥ 标题栏「PCLCE For SGU」标识

- 顶部 PCL CE 徽标后新增 **「 For 」** 文字与 `swordsman.png` 标识
- 图片随程序集以资源形式打包（`Plain Craft Launcher 2/Images/swordsman.png`），
  以 22px 高的白色圆角底板承载（与 CE 徽标同款白色圆角底板保持设计一致），
  保证在蓝色标题栏上清晰可读

### ⑦ 构建产物命名规范

- 新增 [`build.sh`](build.sh)：统一构建入口（`./build.sh [x64|ARM64] [Release|Debug]`），
  构建完成后按规范重命名为 `PCLCE For SGU [版本号].exe`
- `.github/workflows/reusable-build.yml` 增加同名重命名步骤，CI 产物同样遵循该规范

---

# 27.1-snapshot-7 更新内容

> 版本号：**27.1-snapshot-7(2.12.1)**　构建产物：`PCLCE For SGU 27.1-snapshot-7(2.12.1).exe`

- 钻石剑装饰**不再旋转**：直接使用官方 16×16 纹理整数倍放大（9× → 144×144），保持原本的对角像素造型
- 钻石剑**透明度进一步降低**：`Opacity` 0.46 → 0.22（更淡，仅作为标题栏背景点缀）
- 标题栏装饰尺寸随之调整（`Height=144`，右对齐，上下被标题栏裁切）

---

# 27.1-snapshot-6 更新内容

> 版本号：**27.1-snapshot-6(2.12.1)**　构建产物：`PCLCE For SGU 27.1-snapshot-6(2.12.1).exe`

### 钻石剑装饰改用官方纹理

- `Images/diamond_sword.png` 改为由**官方钻石剑 16×16 纹理**旋转 45° 横置而来（逐像素旋转 + 方块铺陈，
  无空洞），尺寸 304×160，不再使用手绘图
- 标题栏尺寸调整为 `Height=120`（约 228×120），右对齐、上下被标题栏裁切充当背景

### 「剑客黄」主题：神圣黄昏配色 + 渐变

- **专属调色板**：`ThemeService._CalculateColorsGoldenDusk()` 为剑客黄单独生成 11 档颜色，
  按「浅奶油金 → 浅金 → 金 → 暖金 → 琥珀 → 深琥珀 → 暮色橙 → 深暮紫橙」逐档偏移色相并微调明度 / 色度，
  形成黄昏渐变观感；其他主题仍走统一算法（配色完全不变）
- **标题栏圣光渐变**：剑客黄启用时叠加一层由透明过渡到「暖金 → 金沙 → 淡金」的对角线性渐变
- **钻石剑辉光**：装饰图增加淡金色 `DropShadowEffect` 光晕，强化「神圣」观感
- 以上装饰仅在剑客黄主题下显示，切换主题即时显隐（`FormMain.RefreshTitleSword()`）

---

# 27.1-snapshot-5 更新内容

> 版本号：**27.1-snapshot-5(2.12.1)**　构建产物：`PCLCE For SGU 27.1-snapshot-5(2.12.1).exe`

### 主题

- **修复主题下拉框不显示扩展主题**：`PageSetupUI.ThemeColorValues` 之前漏掉了新增主题（补丁未生效），现已改为
  「主列表 + 按规则过滤」的结构
- 新增主题（均按 #RRGGBB 经 OKLCH 反解参数生成，主色即指定色值）：
  | 主题 | 颜色 | 发行版可用 |
  | --- | --- | --- |
  | 肥鱼蓝 | `#5A67AC` | ✅ |
  | 天依蓝 | `#66CCFF` | ✅ |
  | 海人蓝 | `#3366CC` | ✅ |
  | 一歌蓝 | `#33AAEE` | ✅ |
  | 剑客黄 | `#E8B33C` | ❌ |
  | 初音绿 | `#39C5BB` | ❌ |
  | 烤音绿 | `#33CCBB` | ❌ |
  | 自定义 | 用户输入 | ❌ |
- **发行版只保留蓝色系主题**：`ThemeService.IsThemeSelectable()` = 快照版/实验版全开；发行版仅保留
  天空蓝、龙猫蓝、死机蓝与上述蓝色系扩展主题，非蓝色主题与自定义主题色输入行一并隐藏
  （主题色是原作者龙腾猫跃的主要赞助项目）
- **剑客黄专属装饰**：选择「剑客黄」时，标题栏右侧显示一条横向半透明钻石剑（`Images/diamond_sword.png`，
  自绘像素图），位置从工具栏一路延伸到最小化按钮，上下两端被标题栏裁切以充当背景；切换主题时自动显隐

---

# 27.1-snapshot-4 更新内容

> 版本号：**27.1-snapshot-4(2.12.1)**　构建产物：`PCLCE For SGU 27.1-snapshot-4(2.12.1).exe`

### 界面修复

- **输入框显示不全**：`MyTextBox` 模板内含 `MinHeight=28` 的行，此前给陶瓦联机的「显示名称」「房间号」、
  以及个性化的「自定义主题色」设置了 26px 高，导致下边缘被裁切 —— 统一改为 28px（下拉框同步对齐）
- **「此版本不支持更新」按钮文字被挡**：软件更新页的检查按钮列宽由固定 80px 改为自动 + 最小 140px，
  并放宽文字内边距，长文案不再被裁切
- **版本号显示异常**（`2.12.1` 显示成 `2 12 1`）：`PageSetupUpdate.VersionNameFormat` 原先会把最后一个 `-`
  之后的内容按 beta/rc 规则改造，遇到 `27.1-snapshot-4(2.12.1)` 这种带括号的完整版本号时误伤；
  现在检测到括号即原样显示
- **房客教程无法折叠**：房主 / 房客教程卡片均启用 `CanSwap`，默认展开、可点击折叠

### 主题

- 新增主题 **肥鱼蓝（#5A67AC）** 与 **天依蓝（#66CCFF）**，与原有主题一样通过 OKLCH 反解参数生成
- 个性化页面「官方快照版」引导文案改为：
  「蓝色大肥鱼吃掉了其他主题！现在只有肥鱼蓝了！想要逃离蓝色大肥鱼，请使用官方快照版！」
- **发行版隐藏扩展主题**：`ThemeService.IsReleaseDistribution`（版本号无 snapshot / experiment 等后缀时判定为发行版）
  为真时，主题列表只保留原有的天空蓝 / 龙猫蓝 / 死机蓝，并隐藏自定义主题色输入行 ——
  主题色是原作者龙腾猫跃的主要赞助项目，发行版中不保留该功能；快照版 / 实验版仍完整可用

---

# 27.1-snapshot-3 更新内容

> 版本号：**27.1-snapshot-3(2.12.1)**　构建产物：`PCLCE For SGU 27.1-snapshot-3(2.12.1).exe`

### ① 主题色支持自定义（#RRGGBB）

- 新增配色主题选项「自定义」（`ColorTheme.Custom`），亮色 / 深色配色均可单独选择
- 新增配置项 `UiCustomColor`（默认 `#39C5BB`），「设置 → 个性化」中新增「自定义主题色」输入框与颜色预览色块，
  输入 `#RRGGBB` 后即时生效（正则校验 `^#?[0-9A-Fa-f]{6}$`）
- 实现方式：把用户颜色转换到 OKLCH，取其色相并按明度 / 色度反解出 `lightAdj` / `chromaAdj`
  （`ThemeService._InvertAdjustLinear`），使生成的主色 `Color3` 与该颜色一致；色相改为 `double` 以保证精确还原
- 原有天空蓝 / 龙猫蓝 / 死机蓝（以及愚人节 HMCL 蓝）配色参数完全不变

### ② 弃用联机大厅，改为 TerraCotta 陶瓦联机（EasyTier 纯 P2P）

- **核心**：`LobbyInfoProvider` 改用本地静态默认值（可用、无需登录、允许自定义用户名），
  不再依赖 PCL 联机大厅公告服务器；`LobbyController.IsTelemetryEnabled = false` 关闭联机数据上报；
  `LobbyService` 跳过 Natayark ID 登录与令牌刷新；`ModLink.LobbyPrecheck` 去掉大厅可用性 / 登录校验
- **中继**：新增配置 `LinkTerraCottaRelayMode`（0 = 仅 P2P，1 = 允许公共中继，默认 1）。
  EasyTier 只使用 EasyTier 官方公共节点（`public.easytier.top` 等），不再使用 PCL 下发的联机中继；
  房间号由 `LobbyCodeGenerator.Generate()` 在本地生成，不经过任何服务器
- **页面**：原「联机大厅」页面重写为「陶瓦联机」控制页 —— 联机总开关（`LinkTerraCottaEnabled`）、
  联机内核（EasyTier）状态与下载、显示名称、连接方式（公共中继 / 仅 P2P）、房主建房（扫描局域网世界 / 手动端口）、
  房客加入（房间号）、当前房间（房间号、复制、玩家列表、退出），并新增**房主教程**与**房客教程**两个栏目
- 侧边栏条目「大厅」改名为「陶瓦联机」

### ③ 恢复「回声洞」

- 「百宝箱」页面底部新增「回声洞」方框，以逐字打字动画展示语录，每 3.5 秒自动轮换，
  右下「换一句」可手动切换，并显示「第 N / M 句」
- 语录列表位于 `Modules/ModEchoCave.cs` 的 `Quotes`，当前收录：
  「帝国之辉，终不熄灭的光！」「Welcome to the SGU！」「PCL CE For SGU！」

---

# 27.1-snapshot-9 更新内容

> 版本号：**27.1-snapshot-9(2.15.1)**　构建产物：`PCLCE For SGU 27.1-snapshot-9(2.15.1).exe`（`artifact-x64/` 与 `dist/` 各一份）

### 修复

- **「剑客黄」等十六进制主题导致浅色模式下所有控件发白（看起来像半透明）**
  根因：`_GetThemeArgsFromHex` 为了"让 Color3 等于指定色"反解出的 `lightAdj`（剑客黄 +0.52、天依蓝 +0.54）
  被整条明度阶梯套用，深色文字档 Color1 从 0.35 被提到 0.69~0.70，与白底几乎没有对比。
  现改为 `BuildWeightedColors()` **按档位加权**：Color3 全量、Color2/4 部分、文字档 1 与背景档 5~8 几乎不动，
  主色仍精确等于指定色；顺带修复天依蓝 / 一歌蓝 / 初音绿 / 烤音绿同类问题。
- **更新页 / 反馈页占位图标显示错误**：`MyUnavailablePlaceholder` 不再手绘箭头+叉，
  改用 PCL 自带状态图标 `Images/Icons/Unavailable.png`（112×112、HighQuality），呼吸动画改为缩放 + 透明度。
- **语言页**：「让 PCL CE 走向国际化」横幅卡片与「前往翻译平台」「提交 Pull Request」按钮删除；
  该页重建为单张「语言」卡片（界面语言 + 区域格式两个下拉框）。
- **zh-TW / en-GB 补齐 27 个下游新增语言键**（剩余缺键均为上游自身未跟进的部分，运行时回退 zh-CN）。

### 清理

- **下线「设置 → 联机」选项卡**（与「工具 → 陶瓦联机」重复）：删除 `PageSetupLeft` 的 `ItemGameLink`、
  `PageSetupGameLink` 页面与 `ModMain.frmSetupGameLink` 字段；其独有的 5 个 EasyTier 选项
  （协议偏好 / 延迟优先 / 端口猜测 / IPv6 / CLI 输出）迁入陶瓦联机页新增的「高级设置（EasyTier）」卡片，配置键不变。
- `PCLCE-old/`（154 MB 旧代码副本）移出工作区。
- 交接文档收敛：根目录 `HANDOVER.md` 为唯一长期文档，另两份移入 `docs/archive/`。
- 产物目录统一：`artifact-<平台>/` 为构建输出，`dist/` 为发布包目录，`build.sh` / `tools/build.ps1` 构建后自动同步。

---

# 27.1-snapshot-10 更新内容

> 版本号：**27.1-snapshot-10(2.15.1)**　构建产物：`PCLCE For SGU 27.1-snapshot-10(2.15.1).exe`

- **占位图标换成 PCL 原生的「红镐子 + 叉」**：复用 `MyLoading` 失败态的 `PathPickaxe` 与 `PathError` 几何
  （`ColorBrushRedLight`），与 Minecraft 下载报错页完全一致；替换掉上一版误用的 `Icons/Unavailable.png`
- **CurseForge 源优雅降级**：构建未注入 `CURSEFORGE_API_KEY` 时，搜索请求自动跳过 CurseForge 并收敛为仅 Modrinth，
  避免整页"加载失败"
- **回滚 TLS/DoH 相关改动**：「Minecraft 原版无法下载 · 证书链错误」经确认为测试机把系统日期改到 4 月 1 日所致，
  与代码无关；`HostConnectionHandler` / `NetworkService` / `Requester` 恢复上游行为，不留无谓改动

---

# 27.1-snapshot-11 更新内容

> 版本号：**27.1-snapshot-11(2.15.1)**　构建产物：`PCLCE For SGU 27.1-snapshot-11(2.15.1).exe`

- **正版登录修复**：`MicrosoftProvider` 多级解析 Client ID（配置 / 环境变量 / 构建密钥 / 内置公开 ID `00000000402b5328`），
  不再出现「Microsoft OAuth client id is not configured」
- **陶瓦联机房客加入失败修复**：EasyTier CLI 的 `ping` / `loss` 字段改为宽松解析（`12ms`、`1.5%`、`-` 均可），
  解析失败时记录原始输出；主机名解析加边界检查
- **显示名称不再强制填写**：留空自动使用当前游戏账号名（兜底 `Player`），输入框提示中显示实际生效名称
- 加入房间失败时追加 NAT/公共中继方向的提示

## 27.1-snapshot-26：下载页整体关闭（复用「设置 → 反馈」的显示）

> 用户反馈（对 snapshot-25）：「你写的UI有问题，请直接复用“设置”里面的“反馈”的显示，并且关闭下载功能」。

- **整页关闭**：`DownloadPolicy` 新增 `AllowDownloads`（默认 `false`）作为下载功能的唯一总开关，`AllowMinecraftDownload` 改为它的别名（插件照旧写哪个都行）。
- **下载页只剩占位**：`PageDownloadLeft.PageGet()` 在 `!AllowDownloads` 时直接返回新页面 `Pages\PageDownload\PageDownloadBlocked.xaml`；左侧列表保留，点任何一项都是同一个占位页。
- **复用反馈页的显示**：占位页结构照抄「设置 → 反馈」被禁用时的写法 —— `MyCard`（`MinHeight=300`）里放 `MyUnavailablePlaceholder`，文案键 `Download.Disabled.Message`，不再自己拼一套 UI。
- **状态变化即时生效**：`PageDownloadLeft` 订阅 `DownloadPolicy.Changed`，插件启用/禁用后自动 `RefreshPage()` 重新装载当前分页（不需要重启，也不会停在旧占位页）。
- 版本号：`27.1-snapshot-26(2.15.1)`，`code` 549
- 产物：`artifact-x64/PCLCE For SGU 27.1-snapshot-26(2.15.1).exe`，23,851,112 B，
  SHA-256 `FC905905557E0A965727422240E775794FE28F3FF9AC2285032BD28B3F6CC5D6`（`dist/` 有同哈希副本）
- 插件「下载限制解除」保持不变（仍为 `dist\DownloadUnlock.pclpkg`），启用后 `AllowDownloads=true`，下载页与限制前完全一致。

## 27.1-snapshot-25：Minecraft 下载限制 + 「下载限制解除」插件

- 默认构建禁止下载 Minecraft：下载页「原版游戏」与加载器列表被占位提示覆盖（文案照 2.99.0.0 的语气改写），所有下载入口统一由 `DownloadPolicy.CheckMinecraftDownloadAllowed()` 守卫。
- 整合包下载不再自动安装，改为保存 `.mrpack` / `.zip` 到默认下载文件夹，完成时提示保存路径。
- 解除限制做成独立插件 `plugins\DownloadUnlock\`（id `sgu.download-unlock`，显示名「下载限制解除」）：插件在 `Enable()` / `Disable()` 里用反射改写 `PCL.DownloadPolicy` 的两个静态属性并触发 `Changed`。启动器不感知插件，没有任何「检测到插件就放行」的分支。
- 该插件只发布到 `dist\DownloadUnlock.pclpkg`，刻意不放进 `plugins\dist\`，避免非正式版构建自动内嵌 + 自动安装导致「默认就不受限」。
- 细节见 [`docs/DOWNLOAD-LIMIT.md`](docs/DOWNLOAD-LIMIT.md)。

## 维护交接

维护交接说明见 [`docs/HANDOVER.md`](docs/HANDOVER.md)：项目定位与底线、构建发版流程、版本号与同步上游步骤、
下游改动全景（按主题索引）、语言资源约定、已知问题与踩坑清单。


## 构建产物

原维护者构建环境：Android Termux（aarch64）+ .NET SDK 10.0.113，`EnableWindowsTargeting` 交叉构建 WPF。
最近一次（27.1-snapshot-8）在 **Windows + .NET SDK 10.0.402** 上构建通过。

| 版本 | 产物 | 说明 |
| --- | --- | --- |
| 27.1-snapshot-8 | `artifact-x64/PCLCE For SGU 27.1-snapshot-8(2.15.1).exe` | 单文件发布（23,657,253 字节 / ≈22.6 MiB），`win-x64` Release，依赖 .NET 10 桌面运行时 |
| 27.1-snapshot-7 | `artifact-x64/PCLCE For SGU 27.1-snapshot-7(2.12.1).exe` | 上一版 |
| 27.1-experiment-2 | `artifact-x64/PCLCE For SGU 27.1-experiment-2(2.12.1).exe` | 单文件发布（约 23 MB） |

推荐使用统一脚本构建：

```bash
./build.sh            # Release + x64
./build.sh ARM64      # Release + win-arm64
```

构建过程中 `.NET` 进程内存峰值约 1.5 GB，未超过 3.5 GB 限制。
