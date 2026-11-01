# 交接文档 · 2026-11-01 · preview-84

> 上一份：`HANDOFF-2026-10-31-preview-73.md`（preview-73 时代，部分内容已被本轮推翻，**以本文为准**）。
> 更早的 `HANDOFF-2026-10-31.md`（snapshot-71）、`HANDOFF-2026-10-29.md`、`HANDOVER.md` 均已过期。

---

## 0. 三十秒看懂

* 项目：**PCL Swordsman**（`PCLCE For SGU`），PCL CE 的下游分支，C# / WPF / net10.0-windows。
* 当前版本：**`27.1-preview-84(2.15.1)`，code 607**（由 snapshot-83 转预发布，代码无差异）。
* 产物：`dist/PCLCE For SGU 27.1-preview-84(2.15.1).exe`（25,738,093 B，SHA-256 `49aab141…552ece`）。
* 开发机：Android / Termux 交叉构建；测试机：Windows 10 **21H2 build 19044**（用户口述为 22H2，日志实测 19044）。
* 仓库：`Swordsman-Group-Uniform/PCL-Swordsman`（公开，`main`），上传用 SSH key `~/.ssh/id_ed25519_pcls`。
* **本轮主题**：把"自绘背景模糊"整条路线删掉，材质回到 DWM；Win10 固定方角，Win11 交给 DWM 圆角。

---

## 1. 本轮做了什么（按时间顺序）

| 版本 | 内容 | 结果 |
| --- | --- | --- |
| snapshot-74 | 自绘背景毛玻璃（截屏 + 缩小 + 窗口区域裁圆角） | ❌ 完全没有模糊（alpha 问题） |
| snapshot-75 | 加失败兜底 + 临时截图按钮 + 全链路日志 | ➖ 仍无模糊 |
| snapshot-76 | 认为"设了壁纸不该自绘"，去掉该门禁 | ❌ 仍无模糊（用户：谁家桌面不设壁纸） |
| snapshot-77 | `CreateDIBSection` + `PixelFormats.Bgr32`（GDI 不写 alpha → 32bpp DDB 被当成全透明） | ✅ 有模糊了，但是 **10 帧/秒、UI 线程、肉眼可见滞后** |
| snapshot-78 | 截屏搬独立线程，目标 16 ms/帧 + 丢帧合并 | ➖ 用户判定仍非实时 → **决定放弃自绘** |
| snapshot-79 | **删除自绘整套**；Win10 恢复 AccentBlur；Win11 改 DWM 圆角 | ⚠️ 引入两个新 bug |
| snapshot-80 | Win10 材质一律方角（按用户要求：不是 bug，是需求） | ⚠️ 同上 |
| snapshot-81 | Win11 玻璃圆角取 0，交给 DWM 圆角裁（解决"实际圆角与玻璃圆角不一致"） | ✅ 思路正确 |
| snapshot-82 | 修「系统三键闪现 + Basic 主题窗体」：不再碰非客户区 | ⚠️ 顺手删掉了 Win11 的 `Round` → 变成直角 |
| snapshot-83 | 恢复 `DWMWA_WINDOW_CORNER_PREFERENCE = Round`（只设一次，不碰非客户区） | 待用户实机验收 |
| preview-84 | 转预发布，代码与 snapshot-83 一致（功能自 snapshot-79 冻结） | 待验收 |

---

## 2. 材质与窗口外形的最终结论（**重要，别推翻**）

### 2.1 三条铁律

1. **Windows 10 材质生效时一律方角**（`ModMaterial.CornerRadius` 恒返回 0）。
   理由（有日志证据）：DWM 的 AccentBlur 按**整个窗口矩形**绘制，**不吃 `SetWindowRgn`**。
   历史日志里区域确实设上了 —— `[Material] 窗口外形：窗口区域（内缩 10px / 圆角 16px；区域包围盒 (10,10)-(890,540)）`
   —— 磨砂照样铺满整窗。既然磨砂一定是方的，就让界面也方，两者严格重合，圆角滑块在 Win10 不再生效。
   **这是需求，不是 bug，不要"修"。**
2. **Windows 11 交给 DWM 圆角**：必须显式设 `DWMWA_WINDOW_CORNER_PREFERENCE = Round`（WPF 的
   `WindowChrome` 窗口在系统眼里是弹窗样式，`DEFAULT` 判定为不圆角），并且**不要**设窗口区域
   （设区域会关掉 DWM 圆角，而系统材质又不吃区域 → 就是那个"模糊溢出"）。玻璃自身的圆角取 0，
   由这条系统圆角裁出来，因此"实际圆角"与"玻璃圆角"永远同一条曲线（DPI 多少都对得上）。
3. **绝不碰非客户区**。任何 `SWP_FRAMECHANGED` / `RDW_FRAME` / 无条件 `SetWindowRgn(NULL, 重绘)`
   都会让 WPF 的 `WindowChrome` 窗口重画非客户区 → **系统标题栏三键 + Basic 主题窗体闪一帧**。
   现有保护：
   * `MaterialService.ForceRepaint` 只用 `SWP_NOACTIVATE` + 只重绘客户区；
   * `MaterialService.Clear` 用 `_dwmTouched` 判断"从没设过磨砂就直接 return"；
   * `ModMaterial.ClearShapeRegionIfNeeded` 用 `_shapeRegionApplied` 判断"没设过区域就不清"。

### 2.2 已删除 / 不要再复活的东西

| 东西 | 状态 |
| --- | --- |
| `ModBackdropBlur.cs`（自绘背景层） | **已删除** |
| `PCL.Core/Utils/OS/Window/WindowBackdropCapture.cs` | **已删除** |
| 配置项 `UiMaterialSelfDrawnBackdrop` / `UiMaterialAccentBlurOnWin10` | **已删除** |
| 设置页「自绘背景毛玻璃」「允许 Win10 旧版磨砂」「临时允许截图」 | **已删除** |
| 相关语言键（6 组 × 8 文件） | **已删除**（现在 8 个文件各 3379 键） |
| `WDA_EXCLUDEFROMCAPTURE`（启动器从截图消失） | **已删除**（自绘才会用到，不再有截图问题） |

> 用户明确表态：**自绘模糊不要再搞了**。如果将来还想做"既圆角又不溢出又真模糊"，
> 唯一还没试过的路线是 `Windows.UI.Composition` 的 `HostBackdropBrush`（Win10 1709+），
> 但它要挂 `DesktopWindowTarget`/子窗口，WPF 的 airspace 会打架（子窗口永远盖在 WPF 内容上），
> 动手前必须先跟用户确认，不要再自作主张。

---

## 3. 构建与发布

### 3.1 Termux（Android，交叉构建）

```bash
cd /data/data/com.termux/files/home/PCL-CE-dev
export DOTNET_gcServer=0
./build.sh x64 Release        # 产物：artifact-x64/ + dist/
```

必须知道的坑：

* **csc 会随机 SIGSEGV**（exit 139 / 255）→ 一定要用重试循环（本轮最多一次连挂 7 次）。
  ```bash
  for i in 1 2 3 4 5 6 7 8; do ./build.sh x64 Release > _tmp/build.log 2>&1 && { echo OK; break; }; sleep 3; done
  ```
* 日志要写到**工作区内**（`_tmp/`），`/tmp` 在 Termux 上不可写（本轮踩过）。
* 改了**语言 XAML / 资源 / 新增文件**时先 `rm -rf PCL.Core/obj PCL.Core/bin "Plain Craft Launcher 2/obj" "Plain Craft Launcher 2/bin"`。
* `plugins/package.sh` 必须**先于** `dotnet publish`（否则 `CS1566`）。
* `build.sh` / `plugins/package.ps1` 需要 **UTF-8 BOM**。
* 非正式版会把插件包塞进 `artifact-x64/plugins/`，构建前会 `rm -rf $OUT/plugins`（防残留）。

### 3.2 Windows

```powershell
powershell -ExecutionPolicy Bypass -File tools\build.ps1
```

约 30 秒；它内部会先跑 `plugins\package.ps1`。

### 3.3 版本号

`Plain Craft Launcher 2/metadata.json` → `version.suffix` + `version.code`。
开发期 `snapshot-NN`，功能冻结转 `preview-NN`，`rc-NN`，正式版无 suffix。
**code 跨阶段连续递增**（本轮 596 → 607）。改完记得同步 `CHANGELOG.md`。

### 3.4 本地化

8 个文件必须**键数完全一致**（当前各 **3379**）：`zh-CN`、`zh_sgu`、`zh-TW`、`en-US`、`en-GB`、
`ja-JP`、`fr-FR`、`es-ES`，位于 `PCL.Core/App/Localization/Languages/`。
重复 `x:Key` 会让整个字典加载失败（所有文字变空），`build.sh` / `build.ps1` 有重复键预检查。
XAML 里换行必须写 `&#10;`。增删键后各文件计数必须一致。

---

## 4. 常见症状 → 第一现场

| 症状 | 先看什么 |
| --- | --- |
| 材质没有效果 | 日志 `[Material] 效果：<选> → <实际>（<via>）｜系统 build <n>` 与 `窗口外形：…` |
| 磨砂比窗口大 / 四角是方的 | Win10 正常现象（见 §2.1 铁律 1）；Win11 才是 bug（检查是否被设了窗口区域） |
| 系统三键闪出来 / Basic 窗体闪一下 | 有人碰了非客户区（`SWP_FRAMECHANGED`、`RDW_FRAME`、无条件 `ClearWindowRegion`） |
| Win11 变直角 | `DWMWA_WINDOW_CORNER_PREFERENCE = Round` 没设上（看日志 `已启用 DWM 窗口圆角`） |
| 材质切换没反应 | 材质**类型**是进程内锁定的（`_lockedKind`），改类型要重启；参数是即时的 |
| 插件页报 `XamlParseException` | 事件签名不对，见 `docs/PLUGIN-DEV.md` §19.2 |
| 组件崩了想回滚 | `git log --oneline` 找 snapshot 边界，`dist/` 里有历史 exe |

**铁律：任何"已修好"的结论都必须有日志证据。** 本轮前三次"修好了"全靠猜，全错。

---

## 5. 关键文件地图

### 材质 / 窗口
* `Plain Craft Launcher 2/Modules/UI/Material/ModMaterial.cs` —— 材质总装配：取配置 → 应用材质 →
  四层不透明度 → 窗口外形（Win10 方角 / Win11 DWM 圆角）→ 悬停发光。
* `Plain Craft Launcher 2/Modules/UI/Material/ModLiquidGlass.cs` —— 大玻璃 / 竖线 / 卡片小玻璃 / 登录药丸 /
  折射环（`RefractionBorder`，默认强度 0）。
* `Plain Craft Launcher 2/Modules/UI/Material/ModOverlayMaterial.cs` —— 对话框与 Toast 的玻璃表面。
* `Plain Craft Launcher 2/Modules/UI/Material/ModWallpaperFrost.cs` —— 共享的壁纸磨砂（幂等）。
* `Plain Craft Launcher 2/Modules/UI/Material/ModTitlePetals.cs` —— 樱花主题的原版花瓣粒子。
* `PCL.Core/Utils/OS/Window/MaterialService.cs` —— Win32/DWM 调用与降级（AccentBlur / 云母 / 亚克力）。
* `PCL.Core/Utils/OS/Window/DwmInterop.cs` —— `IsBackdropSupported`（build ≥ 22621）、圆角偏好、边框色。
* `PCL.Core/Utils/OS/WindowInterop.cs` —— 窗口区域（现在只用于"设过才清"）、缩放热区等。

### 设置与配置
* `Plain Craft Launcher 2/Pages/PageSetup/PageSetupUI.xaml(.cs)` —— 个性化页（主题下拉、材质卡片与滑条）。
* `PCL.Core/App/Config.cs` —— 所有配置项（`UiMaterial*` 一组；`MaterialSelfDrawn*` 已删）。
* `PCL.Core/App/ConfigEnums.cs` —— `ColorTheme`（含 `SakuraPink12` / `NetherRed13` / `MethylOrange14` /
  `AmethystPurple15` / `PluginDefined16`）。
* `Plain Craft Launcher 2/Modules/Base/ModSetup.cs` —— 配置观察者（材质任一项改动即时重申请）。

### 插件
* `PCL.PluginSdk/` —— 纯契约（`IPlugin`、`IPluginContext`、清单、能力、主题、本地化…）。
* `PCL.PluginHost/` —— 发现 / 清单 / ALC / 生命周期 / 权限 / 完整性 / 安装 / 更新。
* `Plain Craft Launcher 2/Modules/Plugins/` —— 启动器侧接线（Bootstrap / Services / Update / SettingsForm / Welcome）。
* `Plain Craft Launcher 2/Pages/PageTools/PageToolsPluginManage.xaml(.cs)` —— 插件管理页。
* `docs/PLUGIN-DEV.md` —— 插件开发完整文档（22 章，含主题与签名）。
* `docs/PLUGIN-THEMES.md` —— 主题机制细节。

### 文档
`README.md`（双语）、`README-FIRST.md`（§0.0 公开仓库故意没有的两样东西）、`CHANGELOG.md`、
`DOWNSTREAM-CHANGES.md`、`VERSIONING.md`、`docs/*.md`（含 `PLUGIN-DEV.md` / `PLUGIN-THEMES.md`）。

---

## 6. 待办（按优先级）

### P0 —— 需要实机验收
1. **Win11 三件事**：四角圆角、材质不溢出、玻璃高光贴着系统圆角。用户有一台 Win11 机，但从没给过 Win11 日志。
2. **Win10 启动干净**：确认第一帧没有系统三键 / Basic 窗体；确认材质生效后是方角、磨砂与界面严格重合。
3. **CPU 占用**：本轮删了自绘（原本 10~60 帧截屏），现在应该只省不费，但要确认没有回归。

### P1 —— 功能
4. **插件市场（M5 剩余唯一项）**：索引格式、搜索、远程下载安装、校验。页面 `PageToolsPluginMarket` 目前是空壳。
5. **插件权限批量操作**：管理页目前逐个授权，缺「全部允许 / 全部拒绝」。
6. **插件字符串本地化**：插件自己的文案表目前由插件自己维护（`PluginLocalization.Pick`），
   宿主不代管；若要统一体验可考虑给 SDK 加一个"插件文案目录"约定。

### P2 —— 观感遗留
7. 液态玻璃「标题栏不透明度」默认 0 的观感是否合适。
8. 樱花粒子密度、下界红/水晶紫贴图透明度、甲基橙化学式可读性。
9. `FadedGray` 标题栏装饰强度（0.34 / 宽 420 / 模糊 6）是否需要再校。
10. 关于页 DeepSeek 头像裁切、插件页与设置页的一致性。

---

## 7. 环境与外部依赖的坑

* **DWMBlurGlass** 注入在测试机上（`D:\Program Files\DWMBG`）：会干扰 DWM 相关实验，
  材质**类型**因此做成"进程内锁定、改类型需重启"。
* 测试机是 **Win10 21H2 (19044)**：没有 Mica、没有 DWM 圆角、`DWMWA_SYSTEMBACKDROP_TYPE` 不支持；
  `WDA_EXCLUDEFROMCAPTURE` 可用（19041+）。
* **单文件发布**（`PublishSingleFile=true`）会破坏**相对** pack URI → 一律用
  `pack://application:,,,/Plain Craft Launcher 2;component/…` + `ModBase.GetResourceStream` 兜底。
* 原版素材**不入公开仓库**（`.gitignore` 排除 `Images/Blocks/NetherWartBlock.png`、
  `Images/Particles/`、`plugins/FadedGray/Faded.jpg` 等）；本地构建缺这些文件时 FadedGray 的
  csproj 用了 `Condition="Exists('Faded.jpg')"`。
* 主题文档已公开（`docs/PLUGIN-THEMES.md`），不再是保留内容。
* 工作区根目录属主是 `root`，push 前已加过 `git config --global safe.directory`。
* 插件部署按**版本号比较**：同版本编辑不会传播到用户机器，改内置插件内容必须升版本号。

---

## 8. 一句话交给下一位

**材质这块别再折腾"自绘/截屏"路线，Win10 方角是既定需求，Win11 用 DWM 圆角；
任何要动窗口非客户区的代码都先看本文 §2.1 第 3 条。**
剩下的主要工程是插件市场（M5），插件 SDK 已经稳定在 API 1，开发细节全在
`docs/PLUGIN-DEV.md`。
