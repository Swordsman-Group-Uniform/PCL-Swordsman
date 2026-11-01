# 交接书 —— PCL Swordsman（PCL CE 下游分支）

> **历史文档**：内容对应 preview-73。文中的「主题不能写进公开文档」约束已于 2026-11-01 取消，
> 主题文档现在是 `docs/PLUGIN-THEMES.md`。
> 交接时间：**2026-10-31** 晚，快照 **preview-73**（`code` 596）。
> 上一份：`HANDOFF-2026-10-31.md`（snapshot-71 时点）；更早：`HANDOFF-2026-10-29.md`、`HANDOFF-2026-10-25.md`。
> **本文档是当前状态的唯一入口**。面向用户的更新日志看 `CHANGELOG.md`，逐版工程记录看 `DOWNSTREAM-CHANGES.md`，
> 设计与历史细节看 `HANDOVER.md`。
>
> **本次交接的特殊背景**：这一轮是在 **Windows 10 19045 实机**上做的（和用户测试机同版本），
> 可以跑起来截图、逐像素比对。上一棒在 Termux 上只能"编译通过"，所以交接时欠了一批"好不好看"的判断，
> 这一轮把窗口材质那条主线补完了，但 **Windows 11 依然没有实机验证**。

---

## 0. ⛔ 死规矩：遇到任何问题，**先读日志，再动代码**

- 日志：`<exe 同级>\PCL\Log\Launch-<日期>-<时分秒毫秒>.log`
- 先看前 10 行的 `版本:` 与 `系统版本:`（Win10 1904x vs Win11 22621+ 决定窗口材质能力），再按 `[模块]` grep
- **没有日志证据不许说「已修好」**；读不到日志就去解决"怎么读到"，不要回去猜。
- 这一轮的正反例：
  - 「磨砂比界面大一圈」——盲改三轮（snapshot-35/36/37）全无效，读日志 5 分钟就定位到 10px 内边距；
  - 「液态玻璃边框像糊了一层透明膜」——用户提示"像去掉四角小正方形的矩形"后，才算到 `FormMain.xaml`
    那 8 个**假阴影** Rectangle 头上（它们躺在旧的内边距里，界面铺满后正好绕窗口一圈）；
  - 「关了材质磨砂还在」——日志里 `已关闭系统窗口磨砂` 明明是成功的，最后发现是机器上装了
    **DWMBlurGlass**（注入 dwm.exe），第三方画的模糊 App 侧撤不掉。
- 操作手册：**`docs/READ-THE-LOG.md`**

---

## 1. 一分钟速览

| 项 | 值 |
| --- | --- |
| 当前版本 | `27.1-preview-73(2.15.1)`，`metadata.json` → `suffix: preview-73` / `code: 596` |
| 阶段 | **预发布（preview）**。历史上这个阶段叫 `pre`，**现已统一为 `preview`**（`VERSIONING.md` 已改） |
| 产物 | `dist/PCLCE For SGU 27.1-preview-73(2.15.1).exe` |
| 大小 / SHA-256 | 25,739,368 B / `33172015BE3809158404FCF8D3D58E7118F35B3978B0865ED2E858D8720C9613` |
| 编译状态 | ✅ 0 error（`tools/build.ps1`，.NET SDK 10.0.403；约 30 秒一次） |
| 实测环境 | 开发机 = 用户测试机：**Windows 10 22H2（19045）**；**Win11 从未实机验证** |
| 语言键 | 8 个语言文件各 **3381** 键（必须完全对齐，构建前有重复键校验） |
| 随包插件 | `pcls.core` / `sgu.advanced-materials` / `sgu.welcome` / `sgu.pack` / `sgu.sample.hello`（`FadedGray` 只在 `dist/`） |
| 面向用户文档 | **`CHANGELOG.md`**（按里程碑写"加了什么"，**不写 bug 修复**） |

---

## 2. 这是什么 / 底线

- 上游：**PCL CE**（C# / WPF / net10.0-windows），真实代码基点 `dev` @ `d1149d5`（2026-10-24）
- 本源：**PCLCE For SGU**（界面名 **PCL Swordsman**）—— 剑客群组服服主团队维护的下游分支，内部分享，**非官方发布**
- 显示名统一 `Plain Craft Launcher 2 Swordsman`（窗口 Title / metadata.name / csproj / Aumid）

**底线（改代码前务必知道）**

1. 界面必须保留「下游项目声明」，不得让人误以为是官方版
2. **反馈功能已禁用**：任何文案不得引导用户去 PCL Community / PCL2-CE 提 issue（反馈邮箱：`hmjmfabc@swordsman.top`、`mingyugames@swordsman.top`）
3. 上游版权归 PCL Community 与龙腾猫跃，按 `LICENSE`（Apache-2.0）分发，保留上游声明
4. 发行版不得保留非蓝色主题与自定义主题色（`ThemeService.IsBlueTheme` 已门禁）
5. 更新 / 自动更新 / 云控公告已在代码层禁用，不要重新打开
6. 联机是 EasyTier 纯 P2P，不得引入依赖 PCL 中继 / 大厅服务器的实现
7. **对外文档不得出现任何主题相关内容**：`docs/PLUGIN-API.md` / `docs/PLUGIN-GUIDE.md` 里
   `grep -in "theme\|主题"` 必须为 0；主题只写在 `docs/PLUGIN-THEMES.md` 与 `HANDOVER.md`

---

## 3. 这一轮（snapshot-72 → preview-73）到底改了什么

> 逐条工程记录在 `DOWNSTREAM-CHANGES.md` 顶部；这里只讲**结论**和**为什么**。

### 3.1 窗口材质：从"假磨砂"回到"系统真磨砂"

- **Windows 10**：云母 / Aero / 液态玻璃**一律走旧版 `AccentBlur`**（`SetWindowCompositionAttribute`），
  它是 Win10 上唯一能糊住桌面真实内容的实现。
- **Windows 11（22621+）**：云母 / 亚克力照旧由 DWM 提供；额外用
  `DWMWA_BORDER_COLOR = DWMWA_COLOR_NONE` 关掉系统那圈 1px 描边（它沿窗口矩形画，和圆角对不上）。
- **Windows 11 21H2（22000–22620，没有云母）**：自动落到 AccentBlur，不会"选了没效果"。
- 曾经的「应用内壁纸磨砂」「实时抓屏」两条路线**代码已全部删除** —— 用户明确否决
  （抓屏要把窗口排除在截图之外，代价是启动器不出现在任何截图/录屏里）。**不要再捡回来。**

### 3.2 磨砂与界面等大（这条是整轮的核心）

- 系统磨砂都按**整个窗口矩形**绘制，而界面原本内缩 10px（`PanBack.Margin="10"`，上游给阴影留的边）
  → 四周各多一圈磨砂 = 用户一直报的「边框比启动器大」。
- 现在：**材质生效时 `PanBack.Margin` 改成 0**（`ModMaterial.SetFullBleed`），界面铺满窗口，
  窗口区域内缩 0，磨砂矩形 == 界面矩形。
- 圆角**保留**（普通 6 / 玻璃 16），由**窗口区域**负责裁（`RectForm` 圆角设 0，避免两层圆角叠出"没上色的磨砂三角"）。
- 铺满的同时把窗口缩小 20×20（保持中心），所以**有材质 / 没材质看起来一样大**：
  普通 884×535（界面 864×515）↔ 材质 864×515（界面 864×515）。
  存盘时 `ModMaterial.WindowSizeCompensation` 补回 20px，`WindowWidth/Height` 永远是"无材质时的窗口尺寸"。

### 3.3 那层"透明膜"其实是假阴影

- `FormMain.xaml` 顶部 8 个 `Rectangle`（四边 10px 黑色渐变 + 四个 16×16 圆角暗块）是上游模拟窗口阴影用的，
  原本身处 10px 内边距里；界面铺满后它压在半透明玻璃上，绕窗口一圈，形状正是"去掉四角小正方形的矩形"。
- 现在收进 `Grid x:Name="PanShadow"`，铺满时整组 `Collapsed`，还原边距时 `Visible`。

### 3.4 液态玻璃

- 新增配置 `UiMaterialGlassTitleOpacity`（0~100，**默认 0** = 标题栏和玻璃连成一片），
  个性化页的玻璃面板里多一条「标题栏不透明度」滑条。
- 玻璃模式的大玻璃、竖线、卡片小玻璃、登录药丸、对话框 / Toast 表面都还是原来那套（`ModLiquidGlass` / `ModOverlayMaterial`）。

### 3.5 徽标与启动图

- `brand/new/logo-source.webp` 是原图。**图标**按白色字标包围盒裁出中间那块蓝底牌匾
  （`_tmp/make-logo.py`）再缩到 16/24/32/48/64/128/256 —— 整张原图缩到 16/32px 时 PCL 只剩几个像素，
  用户报过"徽标特别小"。
- **启动图**用**完整原图不裁切**（`Images/splash.png`，160×160）—— 用户明确要求启动图不许裁。
- `SplashScreen.Close(TimeSpan.Zero)`：带淡出时长时，WPF 对整个窗口做 alpha 动画，
  位图里的透明像素会按"黑 + 全局 alpha"合成，主界面出来后那 400ms 徽标四周发黑。
- 标题栏仍保留**矢量 PCL 字标 +「Swordsman」白底小徽标**（随主题变色，不换成蓝底位图）。

### 3.6 预览版协议（新增）

- `Modules/Base/ModPreviewAgreement.cs`：版本显示名里带 `-preview-` 时，启动后 **1.5 秒**弹一次协议
  （`ModMain.MyMsgBox`，标题「PCL Swordsman 预览版协议」，按钮「同意 / 不同意」）。
- **必须等窗口入场动画跑完再弹**：早期版本在动画里弹，和 `PanMsgBackground` 的全窗口模糊打架，
  表现是"界面糊住、弹窗点不到、整个启动器卡死"。
- 同意结果写 `States.UI.PreviewAgreementVersion`（完整版本号）；**不同意 → `Application.Shutdown()`**，
  这是产品要求，不给"稍后再说"。换一个预览版会重新问；快照 / 正式版不弹。

### 3.7 其它

- 更新页的当前版本号：去掉 `PCL Swordsman ` 前缀、字号 20→16、`NoWrap` + 省略号 + 悬停看全。
  之前字号太大被右侧按钮挤掉，显示成 `27.1-pre…`，看着像拼错了。
- 设置页隐藏了「允许 Windows 10 旧版磨砂」勾选项（现在 Win10 恒用 AccentBlur，没有第二条路）。

---

## 4. 怎么构建 / 发版

### 4.1 构建（Windows，唯一推荐）

```powershell
cd D:\Minecraft\PCL-CE-dev-72
powershell -ExecutionPolicy Bypass -File tools\build.ps1
```

- 前提：.NET SDK 10（开发机是 10.0.403）+ .NET 10 桌面运行时
- 脚本会依次：校验 8 个语言文件无重复键 → 打插件包（`plugins/package.ps1`）→ `dotnet publish` →
  重命名成 `PCLCE For SGU <版本>.exe` → 同步到 `dist/`
- 单次约 30 秒；产物 ~25.7 MB（单文件、框架依赖）
- **顺序很重要**：插件包必须先打，否则 exe 内嵌插件会报 `CS1566`
- `tools/build.ps1` 与 `plugins/package.ps1` **必须保持 UTF-8 with BOM**

### 4.2 版本号

- `Plain Craft Launcher 2/metadata.json`：`suffix` 递增（`preview-73` → `preview-74`），`code` +1（596 → 597）
- 阶段名：`snapshot-NN`（开发）/ **`preview-NN`（预发布，历史写法 `pre` 已废弃）** / `rc-NN` / 正式版不带后缀
- 每次发版在 `DOWNSTREAM-CHANGES.md` **顶部**加一节；面向用户的说法同步写进 `CHANGELOG.md`

### 4.3 产物校验（可直接复核）

```powershell
Get-FileHash "dist\PCLCE For SGU 27.1-preview-73(2.15.1).exe" -Algorithm SHA256
# 33172015BE3809158404FCF8D3D58E7118F35B3978B0865ED2E858D8720C9613
```

---

## 5. 代码地图（这一轮新增 / 改动的部分已标注 ★）

```
Plain Craft Launcher 2/
├─ FormMain.xaml ★                 PanShadow（假阴影组，铺满时隐藏）、窗口区域、启动图
├─ FormMain.xaml.cs ★              窗口尺寸补偿存盘、预览版协议的 1.5s 定时器、SplashScreen 关闭方式
├─ Application.xaml.cs ★           new SplashScreen(@"Images\splash.png")
├─ Modules/Base/
│   ├─ ModPreviewAgreement.cs ★    预览版协议
│   ├─ ModBase.cs / ModSetup.cs    基础、设置观察者（材质参数变化 → ModMaterial.ApplyAll）
├─ Modules/UI/Material/
│   ├─ ModMaterial.cs ★            SetFullBleed / SetContentCornerRadius / ResizeWindow /
│   │                              WindowSizeCompensation / 玻璃标题栏不透明度
│   ├─ ModLiquidGlass.cs           大玻璃、竖线、卡片小玻璃、登录药丸
│   ├─ ModOverlayMaterial.cs       对话框 / Toast 表面
│   ├─ ModWallpaperFrost.cs        壁纸磨砂（只剩液态玻璃在用）
│   ├─ ModTitlePetals.cs           樱花粉主题的原版樱花粒子
├─ Pages/PageSetup/
│   ├─ PageSetupUI.xaml(.cs) ★     玻璃标题栏滑条、隐藏 Win10 旧版磨砂勾选
│   ├─ PageSetupUpdate.xaml(.cs) ★ 当前版本号排版
├─ Images/
│   ├─ icon.ico / icon.png ★       裁过的新徽标（7 档 / 512）
│   ├─ splash.png ★                完整原图 160×160
│   └─ Heads/Swordsman.png ★       关于页团队卡片头像（裁过的牌匾）
PCL.Core/
├─ App/Config.cs ★                 UiMaterialGlassTitleOpacity
├─ App/States.cs ★                 PreviewAgreementVersion
├─ Utils/OS/Window/MaterialService.cs ★  AccentBlur 默认启用、Clear()、不再强制直角
├─ Utils/OS/Window/DwmInterop.cs ★ TrySetWindowBorder（关 Win11 1px 描边）
├─ Utils/OS/WindowInterop.cs       SetClientAreaRoundedRegion（窗口区域 = 客户区）
├─ UI/Theme/ThemeService.cs        配色计算
├─ UI/Controls/BlurBorder.cs       背景模糊控件（卡片 / 对话框底）
└─ App/Localization/Languages/ ★   8 个 xaml，各 3381 键（新增 4 个 Preview.Notice.*）
PCL.PluginSdk/ + PCL.PluginHost/   插件契约与加载器（M0–M4）
plugins/ + samples/                随包插件与示例（package.ps1 打包 + 写 files 哈希）
tools/build.ps1                    Windows 构建入口
brand/new/logo-source.webp         徽标原图；_tmp/make-logo.py 生成图标
CHANGELOG.md ★                     面向用户的更新日志
```

---

## 6. 关键约定（踩过的坑，别再踩）

### 6.1 窗口材质（最重要的一条）

- 系统磨砂（Win10 AccentBlur / Win11 云母·亚克力）**都按整个窗口矩形绘制**；
  想要"磨砂与界面等大"，界面就必须**铺满窗口**（`PanBack.Margin = 0`），没有第二条路。
- 界面铺满 → 窗口四周的**假阴影**必须隐藏，否则就是"边框糊了一层膜"。
- 圆角交给**窗口区域**裁；`RectForm` 再裁一次会在四个角露出没上色的磨砂。
- 界面铺满 → 窗口要**缩小 20×20**，不然有材质的启动器看起来比没材质大一圈；
  存盘要补回 20px，否则每次启动都会再缩。
- **DWMBlurGlass 会干扰排查**：这台机器装了 `D:\Program Files\DWMBG`（注入 dwm.exe），
  它自己往窗口上画模糊，`已关闭系统窗口磨砂` 执行成功也撤不干净。
  现在材质类型改成 **重启生效**（`_lockedKind`）就是为了绕开它。

### 6.2 单文件发布下的资源加载

- **不要**用相对 pack URI；统一 `pack://application:,,,/Plain Craft Launcher 2;component/Images/…`
- `SplashScreen` 用 **ico 取到的可能是 16×16 那帧**（启动图会变成一个点）→ 用 PNG；
  且 `Close(TimeSpan)` 淡出会让透明像素发黑 → 用 `TimeSpan.Zero`

### 6.3 本地化

- 8 个文件（`zh-CN` / `zh_sgu` / `zh-TW` / `en-US` / `en-GB` / `ja-JP` / `fr-FR` / `es-ES`）**各 3381 键，必须对齐**
- 重复 `x:Key` 会让整个 ResourceDictionary 加载失败 → 全界面文字变空；构建前有校验
- 换行要在 XAML 里写 `&#10;`（不是 `\n`）；`&middot;` 这类 HTML 实体 XAML **不认**
- 代码里 `Lang.Text("Key", 参数…)`；XAML 里 `{DynamicResource Key}`
- ja / fr / es 按项目惯例直接放简体中文

### 6.4 WPF 事件签名（踩过两次 XamlParseException）

- `MyButton.Click` 是 `(object, **MouseButtonEventArgs**)`；`MyIconButton.Click` 是 `(object, EventArgs)`
- `MyIconTextButton.Click` / `MyListItem.Check` 是 `(object, **ModBase.RouteEventArgs**)`
- `MyCheckBox.Check` / `MySlider.Change` 是 `(object, **bool user**)`
- 写 XAML 事件前**先去控件源码确认签名**

### 6.5 MyListItem 单选组 / 插件

- 单选互斥**只在同一个父面板内**生效；往侧栏加动态条目必须**直接插进同一个 `PanItem`**
- 插件包文件句柄：读包一律 `PluginPackage.OpenReadShared()`
- 插件管理页刷新可能在后台线程：UI 操作都走 `ModBase.RunInUi`

### 6.6 启动早期的 UI 操作

- **不要在窗口入场动画（约 0.6s）里弹模态框**：会撞上 `PanMsgBackground` 的全窗口模糊，
  表现为界面糊住、按钮点不到。要弹就等 1.5s 后由 `DispatcherTimer` 触发（预览版协议就是这么做的）。
- `ModMain.MyMsgBox` 两个按钮时**阻塞**（`Dispatcher.PushFrame`），返回 1 = 第一个按钮、2 = 第二个。

---

## 7. 验证清单（下一轮照着跑）

1. 启动日志第一行 `版本: Publish 27.1-preview-NN` —— 确认跑的是新包
2. 材质：`[Material] 效果：… → …（旧版 AccentBlur（界面铺满窗口，磨砂与界面等大））` +
   `窗口外形：窗口区域（内缩 0px / 圆角 6px；区域包围盒 (0,0)-(864,515)）`
3. 尺寸：材质开 → 窗口 864×515；材质关 → 窗口 884×535，但**两种状态下界面都是 864×515**
4. 四周：截图比对 —— 磨砂边界 == 窗口边界，没有多余的一圈，四角是圆角
5. 关闭材质：日志出现 `已关闭系统窗口磨砂`，窗口回到 884×535（若磨砂还在，先查 DWMBlurGlass）
6. 预览版协议：`[Preview] 首次启动预览版 …，弹出预览版协议` → 同意后写
   `PCL\config.v1.yml` 的 `PreviewAgreementVersion`，重启不再弹
7. 插件：`[PluginHost] 扫描完成：…`、管理页每行「已校验」
8. 启动图：160×160 完整徽标，消失时四方不发黑

---

## 8. 明确**没做**的东西

| 缺的东西 | 说明 |
| --- | --- |
| **插件市场** | `PageToolsPluginMarket` 仍是空壳（校验/签名/打包工具已就绪） |
| 高危 API | `game-argv`（改启动参数）、`process`（起进程）—— 用户明确要求不做 |
| **Win11 实机验证** | 云母 / 亚克力、DWM 圆角、1px 描边关闭、尺寸对齐 **全部没有在 Win11 真机跑过** |
| 运行时切换材质 | 现在改类型要重启（`_lockedKind`）；根因是第三方 DWM 注入工具让运行时撤销不可靠 |
| 插件文案 i18n | 插件给的字符串只能一种语言 |
| 插件权限 UI 细节 | 只有「待授权 / 已允许 / 已拒绝 + 单条切换」，没有批量操作 |

---

## 9. 需要实机 / 视觉确认的待办

1. **Win11**：任意一台 22H2+ 机器上选云母 / Aero，确认①四周无外扩、没有 1px 描边；②圆角正常；
   ③云母能透出来；④开关材质时窗口尺寸不跳。
2. **液态玻璃观感**：大玻璃、竖线、卡片小玻璃、药丸、对话框、Toast 的层次；
   新加的「标题栏不透明度」滑条默认 0（与玻璃连成一片），拉高后是否好看没人看过。
3. **四个圆角**：材质开启时，圆角小三角里会露出一丁点磨砂（用户说"看不出来"，可以接受）。
4. **预览版协议**：同意 / 不同意的文案语气、正文长度是否合适；不同意分支只会退出应用，不会写任何记录。
5. `CHANGELOG.md` 的措辞（用户要求"给人看、写短、不提 bug 修复"）。

---

## 10. 参考文档索引

| 文档 | 用途 |
| --- | --- |
| **`docs/READ-THE-LOG.md`** | 死规矩的操作手册：日志位置、行格式、grep 配方 |
| **`CHANGELOG.md`** | 面向用户的更新日志（按里程碑，不写 bug） |
| `DOWNSTREAM-CHANGES.md` | 逐快照工程记录（**最新在最上面**，含产物哈希与验证项） |
| `HANDOVER.md` | 百科：每个子系统的设计、历史与坑 |
| `docs/PLUGIN-THEMES.md` | 内部主题机制（勿对外） |
| `docs/PLUGIN-API.md` / `docs/PLUGIN-GUIDE.md` | 对外插件接口 / 教程（不含主题内容） |
| `docs/PLUGIN-SYSTEM.md` | 插件化设计稿 + 里程碑（M0–M4 完成） |
| `VERSIONING.md` | 版本号规范（**preview 阶段名已定稿**） |
| `samples/HelloPlugin/` | 全功能示例插件 |
| `docs/FIRST-RUN-DIALOGS.md` | 首次运行的各类弹窗文案与判定 |

---

## 11. 交接时的工作区状态

- `artifact-x64/`：历史产物（snapshot-41…72、preview-73）；`dist/`：发布包目录（含 `FadedGray.pclpkg`，要手动安装）
- `PCL-2.99.0.0/`、`HMCL-main/`（GPLv3，**只许读**）、`DWMBlurGlass-master/`、`liquid-glass-main/`：参考资料
- `brand/new/logo-source.webp`：徽标原图；`_tmp/make-logo.py`：图标生成脚本
- `_tmp/shots/`：这一轮验证用的截图；`_tmp/shots/ui.ps1`：窗口截图 / 点击 / 发送按键的小工具
- `Launch-*.log`：用户提供的实测日志
- **`.git` 不可用**（工作区没有版本库），历史只能靠 `DOWNSTREAM-CHANGES.md` 与各份 `HANDOFF-*.md`

---

**交接人备注**：这一轮最值钱的经验是「**别相信"看起来"**」——
"边框比界面大"其实是 10px 内边距，"透明膜"其实是假阴影，磨砂关不掉其实是第三方注入。
三条都是先读日志/截图对齐坐标才定位的；盲改那几轮（snapshot-35…37、以及本轮一开始的壁纸抓取方案）
全部白做。**先在实机上截图 + 对数像素，再动代码。**
