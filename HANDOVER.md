# PCLCE For SGU 维护交接文档

> **交接时间**：2026-10-25（2026-10-25 二次修订）　**交接版本**：`27.1-snapshot-11(2.15.1)`（内部版本代号 `534`）
> **适用范围**：接手本下游分支日常维护、功能增改与发版的人员
> **这是唯一的交接文档**：项目定位、构建发版、上游同步、下游改动索引、插件系统、已知问题、踩坑清单、接手清单都在这里。
> 补充材料（只在对应场景查阅）：[`VERSIONING.md`](../VERSIONING.md)（版本号规范）、[`DOWNSTREAM-CHANGES.md`](../DOWNSTREAM-CHANGES.md)（逐版改动记录）、
> [`docs/PLUGIN-SYSTEM.md`](PLUGIN-SYSTEM.md)（插件系统完整设计）、[`docs/FIRST-RUN-DIALOGS.md`](FIRST-RUN-DIALOGS.md)（首次启动弹窗内容）、[`samples/README.md`](../samples/README.md)（如何编译安装插件）

---

## 0. 五分钟速览（新接手先看这一节）

> **交接时点：`27.1-snapshot-41(2.15.1)` / `code` 564（2026-10-29）**。
> 最新交接书是 `HANDOFF-2026-10-29.md`（已按 snapshot-41 整篇重写），本文件是**常驻文档**：
> 项目全景、构建发版、上游同步、按主题的下游改动索引、插件系统设计、踩坑与历史。
> 出任何问题先读日志（见 §0.2 红线表第 0 条与 `docs/READ-THE-LOG.md`）。


### 0.1 这是什么

- 上游：**PCL CE**（<https://github.com/PCL-Community/PCL-CE>，C# / WPF / `net10.0-windows`）
- 本项目：**PCLCE For SGU** —— 剑客群组服服主团队维护的下游分支，仅供 SGU 及相关服务器（VFH 术友汇、HCE 圣苦力怕帝国）成员内部分享，**非官方发布**
- 真实代码基点：上游 `dev` @ `d1149d5`（2026-10-24，即 2.15.1 正式版之后的 dev 快照），除 65 处下游改动外与上游逐字节相同（见 §4.1）

### 0.25 本地化重复键导致「全部文字消失」的修复（27.1-snapshot-27）

- 现象：snapshot-26 启动后界面**所有文字空白**。根因是我给四个语言文件补 `Download.RegionBlocked.Message` 时插了第二份，
  `ResourceDictionary` 加载抛 `ArgumentException: Item has already been added`，而 `PCL.Core` 的本地化加载失败只写一条
  `WARN … 本地化 启动失败`，于是整个 `Lang.Text()` 全部返回空。
- 修复：删掉四个语言文件里重复的那一行；同一 id 的键现在各只有一份（zh-CN / en-US 3263 键，zh-TW / en-GB 3207 键）。
- 防复发：`tools\build.ps1` 与 `build.sh` 在构建前校验所有 `PCL.Core\App\Localization\Languages\*.xaml` 的 `x:Key` 唯一性，
  重复键直接让构建失败；`tools\build.ps1` 同时补上了「先跑 `plugins\package.ps1` 打插件包」这一步（与 `build.sh` 对齐）。
- 验证：`_tmp\probe-lang` 用 WPF `XamlReader` 逐个加载 7 个语言文件，`ResourceDictionary.Count` 分别为 3263/3263/3207/3207/248/16/2，**ALL PASS**。
- 产物：`artifact-x64/PCLCE For SGU 27.1-snapshot-27(2.15.1).exe`，23,851,112 B，SHA-256 `F118D47F1B61DB22054A21C8C2770C10BF25627F43DC766BDA943916DEC6C4D6`。

### 0.24 下载页整体关闭（27.1-snapshot-26）

- `DownloadPolicy.AllowDownloads` 是下载功能的**总开关**（默认 `false`）；`AllowMinecraftDownload` 现在是它的别名，插件写哪个都等价。
- `PageDownloadLeft.PageGet()` 在总开关关闭时直接返回占位页 `Pages\PageDownload\PageDownloadBlocked.xaml`，因此下载页的**所有子页**（原版、加载器、Mod、整合包、资源包、光影、世界……）都只剩这一个占位页。
- 占位页照搬「设置 → 反馈」被禁用时的写法：`MyCard`（`MinHeight=300`）里放 `MyUnavailablePlaceholder`，文案键 `Download.Disabled.Message`。
- `PageDownloadLeft` 订阅 `DownloadPolicy.Changed` 并 `RefreshPage()`，插件启用/禁用后界面立即切换，无需重启。
- 产物：`artifact-x64/PCLCE For SGU 27.1-snapshot-26(2.15.1).exe`，23,851,112 B，SHA-256 `FC905905557E0A965727422240E775794FE28F3FF9AC2285032BD28B3F6CC5D6`。
  **该版本有「界面全部文字空白」的缺陷（本地化重复键），已被 §0.25 的 snapshot-27 取代，不要再分发。**

### 0.23 下载限制（27.1-snapshot-25）

- `Plain Craft Launcher 2\Modules\Base\DownloadPolicy.cs` 是唯一开关：`AllowMinecraftDownload` / `InstallModpackAfterDownload` 默认 `false`，外加 `Changed` 事件与 `GetDownloadFolder()` / `GetSavePath()` / `CheckMinecraftDownloadAllowed()`。（snapshot-26 起新增总开关 `AllowDownloads`，见 §0.24；`AllowMinecraftDownload` 已变为它的别名。）
- 下载页（原版游戏、加载器）显示占位提示；`ModDownloadLib` 十个入口统一守卫；整合包下载改为保存 `.mrpack` 到下载文件夹。
- 解除限制的唯一途径是插件 `plugins\DownloadUnlock`（`sgu.download-unlock`，「下载限制解除」）用反射改写上述两个属性；启动器不读插件清单、不判断插件是否启用，没有后门分支。
- 该插件只出 `dist\DownloadUnlock.pclpkg`，**不进 `plugins\dist\`**（否则非正式版自动内嵌 + 自动安装，默认就不受限了）。
- 详见 `docs\DOWNLOAD-LIMIT.md`；`dist\DownloadUnlock.pclpkg` 需用户手动安装并启用。

### 0.2 红线（写进代码了，别破坏；第 0 条是流程红线）

> **2026-10-25 补充（snapshot-19/20）**：非蓝色主题、回声洞、限制解除已经**插件化**
> （见 §13）。红线的新表述是：**正式版本体不内置这些插件**（`build.sh` 按 `metadata.json`
> 的 `suffix` 是否为空自动决定），因此正式版仍然"看不到非蓝色主题"；
> 而快照 / 预发布等非正式版会把三个插件嵌进 exe 并自动安装，方便测试。

| # | 红线 | 体现位置 |
| --- | --- | --- |
| **0** | **遇到任何问题，先读日志再动代码**：日志在 `<exe 同级>\PCL\Log\Launch-<日期>-<时分秒毫秒>.log`；先读前 30 行的 `版本:` / `系统版本:`，再按 `[模块]` 标签 grep；没有日志证据不许说「已修好」，读不到日志要先解决「怎么读到」（`chmod 644` / 让用户贴内容）。操作手册见 `docs/READ-THE-LOG.md` | 2026-10-29 snapshot-35/36/37 三轮靠猜修「磨砂框比界面大」全部无效、snapshot-38 读日志 5 分钟定位 |
| 1 | 界面必须保留**下游项目声明**，不得让人误以为是官方版 | 设置→软件信息顶部卡片、设置→反馈页 |
| 2 | **反馈功能已禁用**：任何文案都不得引导用户去 PCL Community / PCL2-CE 提 issue | `ModBase.IsFeedbackSupported = false` |
| 3 | 上游代码与素材版权归 PCL Community 与龙腾猫跃，按仓库 `LICENSE`（Apache-2.0）分发，保留全部上游声明 | `LICENSE`、`metadata.json.licenses` |
| 4 | **发行版不得保留非蓝色主题与自定义主题色**（主题色是原作者主要赞助项目） | `ThemeService.IsThemeSelectable()` 门禁 |
| 5 | 更新 / 自动更新 / 更新检测 / 云端公告（云控）已在**代码层禁用**，不要重新打开 | `UpdateManager.IsUpdateSupported = false` |
| 6 | 联机用 **EasyTier 纯 P2P（TerraCotta）**，不得引入依赖 PCL 中继/大厅服务器的实现 | `LobbyInfoProvider` / `LobbyService` / `EasyTierEntity` |
| 7 | `HMCL-main/` 是 **GPLv3 参考源码**：只许阅读，严禁拷贝任何代码/资源进本仓库 | 同目录 `HMCL-main/` |

### 0.3 马上跑起来（Windows）

```powershell
# 需要 .NET SDK 10（本机验证 10.0.402）
powershell -ExecutionPolicy Bypass -File tools\build.ps1
# 产物：artifact-x64\PCLCE For SGU 27.1-snapshot-23(2.15.1).exe
```

运行需要 **.NET 10 桌面运行时**。首次启动会弹一系列对话框（协议、版本说明等），每个弹窗的原文与重触发方法见 `docs/FIRST-RUN-DIALOGS.md`。

> ⚠️ `tools/build.ps1` **必须保持 UTF-8 with BOM** 保存：Windows PowerShell 5.1 会把无 BOM 的 UTF-8 按 GBK 解码，
> 中文注释末尾的多字节序列可能吞掉换行，导致 `param` 块解析失败（已实测踩过）。

### 0.4 不要碰的东西

| 不要动 | 原因 |
| --- | --- |
| `Plain Craft Launcher 2.csproj` 的 `AssemblyName` | `pack://application:,,,/Plain Craft Launcher 2;component/...` 资源 URI 依赖它，改名会让图标等资源加载失败（见 §8.1） |
| `UpdateManager.IsUpdateSupported` / `ModBase.IsFeedbackSupported` / `TelemetryService.IsTelemetrySupported` | 三个 `false` 是产品决定 |
| `ThemeService` 的发行版主题门禁 | 涉及原作者赞助（见 §5.3） |
| 插件目录的手写删除 | 必须走 `PluginInstaller`（ALC 文件占用，见 §8.13） |
| `HMCL-main/` | GPLv3，只读参考，不进产物、不进提交 |

### 0.5 现在还没做完的事（一屏看完）

1. ~~版本号未提升~~：`snapshot-9` / `code 532` 已提升；正式发版（去掉 `-snapshot-N` 后缀）时会自动隐藏非蓝色主题（见 §5.3）
2. **插件系统只做到 M1**：M2 页面扩展点 / M3 事件总线 + MC 只读 / M4 权限 / M5 插件市场未做（见 §11）
3. **陶瓦联机（TerraCotta）从未在 Windows 双机实测**（见 §7.1）
4. **所有界面改动只验证到「编译通过 + 启动通过」**，观感必须实机确认（见 §7.2）
5. **本地化仍缺键**：`zh-TW` / `en-GB` 各比 `zh-CN` 少 83 个键（运行时回退，不崩，见 §6）
6. **插件市场页是空壳**（见 §7.11）

### 0.6 目录速览

```
Plain Craft Launcher 2/   WPF 启动器主程序（上游 + 下游改动）
PCL.Core/                 核心库（上游 + 下游改动）
PCL.PluginSdk/            ★ 插件契约：IPlugin / IPluginContext / PluginManifest / PluginCapability
PCL.PluginHost/           ★ 插件加载器：ManifestReader / LoadContext / Loader / Installer / StateStore
samples/                  ★ 示例插件（含可安装的 .pclpkg）
tools/build.ps1           ★ Windows 构建脚本（注意 BOM）
docs/                     交接与设计文档
HMCL-main/                （本目录可能不含）GPLv3 参考源码，只读
```

---

## 0.7 2026-10-25 二次修订（snapshot-9）

| 问题 | 处理 |
| --- | --- |
| 「剑客黄」等十六进制主题会让浅色模式下**所有控件像半透明** | 根因：`_GetThemeArgsFromHex` 反解出的 `lightAdj` 被**整条明度阶梯**套用，把深色文字档（Color1/2）一起提亮到 0.69~0.70，与白底几乎无对比。改为**按档位加权**（`BuildWeightedColors`）：Color3 全量应用、2/4 部分、文字档 1 与背景档 5~8 几乎不动，主色仍精确等于指定色；剑客黄的黄昏渐变在此基础上叠加逐档色相偏移 |
| 「设置 → 联机」与「工具 → 陶瓦联机」功能重复 | 下线设置侧选项卡（`PageSetupLeft` 的 `ItemGameLink`、`PageSetupGameLink` 页面与 `Main.frmSetupGameLink` 字段全部删除）；其独有的 5 个 EasyTier 选项（协议偏好 / 延迟优先 / 端口猜测 / IPv6 / CLI 输出）迁入陶瓦联机页新增的「高级设置（EasyTier）」卡片，配置键不变 |
| 更新页 / 反馈页的「带红叉」占位图标显示错误 | `MyUnavailablePlaceholder` 不再手绘矢量箭头+叉，改为直接复用 PCL 自带状态图标 `Images/Icons/Unavailable.png`（红圈白叉，112×112，HighQuality），保留呼吸动画（缩放 1→1.07 + 透明度 0.92→0.55） |
| 语言页「让 PCL CE 走向国际化」横幅及两个按钮 | 删除整块横幅卡片（含「前往翻译平台」「提交 Pull Request」按钮）；该页重建为单张「语言」卡片：界面语言 + 区域格式两个下拉框（原横幅卡片里同时承载了这两个下拉框，删除时已重建，勿再整块删除） |
| zh-TW / en-GB 缺下游新增语言键 | 已补齐 27 个下游键；剩余各缺 56 个键均为**上游自身**在 zh-TW/en-GB 未跟进的键（运行时回退 zh-CN） |
| 工作区内 `PCLCE-old/`（154 MB 旧代码副本）未记录 | 已确认不在交接包内且无引用，移出工作区（备份在 `~/PCLCE-old-backup`，如需归档请勿放回源码树） |
| 交接文档三份并存 | 收敛为根目录 `HANDOVER.md` 一份；另两份移入 `docs/archive/` |
| 构建产物目录口径不一（`artifact-x64/` vs `dist/`） | `artifact-<平台>/` 为构建输出，`dist/` 为发布包目录；`build.sh` / `tools/build.ps1` 构建完成后自动同步一份到 `dist/` |

---

## 0.8 2026-10-25 三次修订（snapshot-10）

| 问题 | 处理 |
| --- | --- |
| 更新页 / 反馈页占位图标仍然不对 | 正确素材是 **`MyLoading` 失败态的红镐子 + 叉**（`PathPickaxe` + `PathError` 的几何与配色 `ColorBrushRedLight`，也就是 Minecraft 下载报错页上那个图标）。`MyUnavailablePlaceholder` 已改为直接复用这两个 Path（同一份 Data、同一套旋转/缩放），保留呼吸动画（缩放 1→1.07、叉透明度 0.9→0.45） |
| 社区资源页「经常性加载失败」 | 本分支构建**不注入 CurseForge API Key**，`api.curseforge.com` 必然 403。已做优雅降级：`ModComp.SearchParam.GetCurseForgeAddress()` 无 Key 时直接跳过该源；`PageComp` 把「全部 / CurseForge」自动收敛为「仅 Modrinth」，不再让整页报错 |
| 「Minecraft 原版无法下载 / 证书链错误」 | **不是代码问题**：测试机为验证愚人节彩蛋把系统时间改成 4 月 1 日，服务器证书校验因此失败。相关 TLS/DoH「修复」已全部回滚（`HostConnectionHandler` / `NetworkService` / `Requester` 回到上游行为）。**排查口诀：先看系统时间与时区**，再看代理/证书拦截 |

> ⚠️ 愚人节（4 月 1 日）会影响主题列表与标题栏：`ThemeColorValues` 会额外追加 `HmclBlue`、`_AprilFoolLogic` 会改写配色/标题，测试主题相关改动时**请先确认系统日期不是 4 月 1 日**，否则看到的现象不可信。

---

## 0.9 2026-10-25 四次修订（snapshot-11）

| 问题 | 处理 |
| --- | --- |
| 正版（Microsoft）账号无法登录：「Microsoft OAuth client id is not configured」 | 本分支构建不注入 `PCL_MS_CLIENT_ID`。`MicrosoftProvider` 改为多级解析：调用方传入 → 配置项 `SystemMsClientId` → 环境变量 `PCL_MS_CLIENT_ID` → 构建期密钥 → **内置微软公开客户端 ID**（`00000000402b5328`，社区启动器通用的个人账号 + XboxLive.signin 设备码流程）。要换成自有 Azure 应用时，填配置/环境变量/CI 密钥任一即可 |
| 陶瓦联机「加入房间」失败：`Failed to get EasyTier player list info` | 根因是 `_ConvertPeerToPlayer` 用 `Convert.ToDouble`（跟随当前区域性）解析 EasyTier CLI 的 `ping`/`loss`；CLI 输出形如 `12ms`、`1.5%`、`-` 时直接抛异常 → 整张玩家表被判为失败 → 房客永远找不到房主。已改为 `_ParseMetric()`：正则抽数字 + `InvariantCulture`，并兼容 `-`/空值；主机名匹配加空值保护，CLI 原始输出在解析失败时写入日志 |
| 「显示名称」留空会直接拒绝联机 | `ModLink.LobbyPrecheck()` 删掉了「用户名与 Natayark 用户名皆为空」的强制拦截；留空时自动回退「当前游戏账号名 → Player」，并在输入框提示里显示实际生效的名称 |
| 加入失败缺少可操作提示 | 新增 `Tools.GameLink.TerraCotta.Guest.JoinFailed.Nat` 提示：双方都在对称 NAT 后时，把「连接方式」改为「允许公共中继」再试，并确认两侧内核都是「已就绪」 |

---

## 0.10 2026-10-25 五次修订（snapshot-12）

| 问题 | 处理 |
| --- | --- |
| 正版登录报 `AADSTS700016: Application with identifier '00000000402b5328' was not found in the directory '9188040d-…'` | 根因：`00000000402b5328` 是**微软第一方（Title）客户端 ID**，只能配合 `login.live.com` 的旧版设备码流程使用；snapshot-11 把它送进了 Entra ID v2 端点（`login.microsoftonline.com/consumers`），必然 700016。`MicrosoftProvider` 现按 Client ID 形式自动选流程：GUID 形式（自行注册的应用）走 v2 + `XboxLive.signin offline_access`；非 GUID（内置第一方 ID）走 `https://login.live.com/oauth20_connect.srf` + `service::user.auth.xboxlive.com::MBI_SSL`，令牌在同一域名 `oauth20_token.srf` 换取/刷新。已用 curl 实测该端点对内置 ID 正常下发 `user_code`/`verification_uri`（`https://www.microsoft.com/link`）。同时 Xbox 用户认证对旧版令牌会依次尝试 `d=<token>` 与裸 token 两种 `RpsTicket` 形式 |
| 正版登录失败时看不到真实原因 | `XboxUtils.AuthenticateAsync` 原来只在 XSTS 失败时附带错误详情，Xbox Live 用户认证失败会抛出无上下文的 `HttpRequestException`。现在任何非 2xx 都抛 `IdentityModelAuthenticationException`，并附 HTTP 状态码与响应体（含 `XErr` 解析）；`MicrosoftProvider` 还会在日志里打印所选流程与 Client ID |
| 陶瓦联机仍报 `Failed to get EasyTier player list info` | 上一轮只修了数值解析，本轮补掉真正的崩点：`JsonCompat.ParseNode` 遇到**任何非 JSON 输出**（EasyTier Core 尚未起 RPC、CLI 把日志混进 stdout、退出码非 0）都会抛 `JsonException`，被通用 catch 记成这条 Error。现在改为 `_ExtractPeerNodes()`：先整段解析、失败后截取第一段 `[...]`/`{...}` 再试，并兼容 `{ "peers": [...] }` 之类包装；`ETPeerInfo` 去掉全部 `required`（缺字段不再整表失败）；解析改为 `_TryParsePeer()` 手工取字段，字段名多别名 + 大小写不敏感；CLI 超时 5s → 15s 并按 Warn 记录；新增「主机已发现但延迟不达标」「仍在等待房主节点」等周期日志，超时后给出可操作建议 |
| 联机问题下次怎么定位 | 陶瓦联机 → 高级设置新增 **「联机诊断」** 按钮：汇总 EasyTier 三件套文件状态、`easytier-core` 进程与命令行、开关/连接方式/协议偏好、联机状态与房间号、日志最近 60 条 `EasyTier`/`Et Cli`/`Lobby` 行，自动复制到剪贴板并弹窗预览。让用户复现失败后点一下、贴回内容即可 |
| 官方开源部分里的「百宝箱 → 内存优化」 | **官方开源部分只有壳**：`PageOtherTest.MemoryOptimize` / `MemoryOptimizeInternal` 的实现被替换成「为便于维护，开源内容中不包含百宝箱功能」，但**接线全在**（`LaunchArgumentRam`、`VersionRamOptimize`、启动加载项、`--memory` 参数、`EventType.内存优化`）。本轮把接线照着官方源码逐条移植，并按官方行为**自行实现**优化内核：提升 `SeProfileSingleProcessPrivilege` / `SeIncreaseQuotaPrivilege` / `SeDebugPrivilege` → `NtSetSystemInformation(SystemMemoryListInformation)` 清空备用内存列表（编号 4/5 两种资料写法都试） → `EmptyWorkingSet` 逐个裁剪进程工作集 → 用 `GlobalMemoryStatusEx` 统计释放量。与「清理垃圾」完全不同：**不删除任何文件** |
| 内存优化落地位置 | 新增 `PCL.Core/Utils/OS/MemoryOptimizer.cs`（内核）；`PageToolsTest.MemoryOptimize/MemoryOptimizeInternal`（与官方同名入口）+ 百宝箱「内存优化」按钮；`ModLaunch.McLaunchMemoryOptimize` 启动前加载项（`ProgressWeight = 30`，全局开关 + 版本三态 0/1/2）；设置 → 启动 → 内存分配卡片新增复选框（开启时弹官方同款提醒，非管理员追加说明）；实例设置 → 内存 新增「跟随全局设置/开启/关闭」下拉；`--memory` 参数静默执行；`EventType.内存优化` 自定义事件已启用（从 `EventTypeMapper.UnsupportedTypes` 移除） |
| 拖入网页链接一键添加外置登录（对齐 HMCL） | `FormMain.FrmMain_Drop` 新增 http(s) 链接分支：`AuthServerResolver.LooksLikeUrl` → 后台 `ResolveAsync`（已知站点映射 + 链接本身 + `/api/yggdrasil` 候选 + `X-Authlib-Injector-API-Location` 响应头 + `meta.serverName`/`signaturePublickey` 校验，单次探测 8s 超时）→ 识别成功确认后 `RememberAuthServer` + `draggedAuthServer` → 切到启动页外置登录。`https://manual.littlesk.in/yggdrasil/` 这类说明页会被解析到 `https://littleskin.cn/api/yggdrasil` |

> 上一轮（snapshot-11）遗留的「正版登录 / 陶瓦联机」两项，本轮已按实测证据修掉；若用户侧仍复现，请先取 `PCL\Log\` 下的
> `Profile`（微软登录流程与 Client ID）与 `EasyTier`/`Et Cli`（CLI 原始输出）两段日志。

---

## 0.11 2026-10-25 六次修订（snapshot-13）

| 问题 | 处理 |
| --- | --- |
| **正版登录怎么才能用**（必读） | 不需要改代码：注册一个免费的 Azure 应用并在构建时注入客户端 ID 即可，完整步骤见 **`docs/MICROSOFT-OAUTH-SETUP.md`**（含权限配置、注入命令、注入验证、常见 AADSTS/XSTS 错误对照表） |
| 用户明确要求：正版登录不要下游乱改，**回滚到上游原始代码** | `MicrosoftProvider.cs`、`Utils/XboxUtils.cs` 已逐字节还原为上源源码（源头：`~/PCL-CE-dev.zip` 内的上游快照），并删除下游自加配置项 `SystemMsClientId`。**恢复后上游行为是 `Secrets.MSOAuthClientId` 为空即抛 `Microsoft OAuth client id is not configured.`**——上游客户端 ID 由 CI 秘钥注入，本仓库没有，所以正版登录在拿到自有 Azure 应用 ID 前不可用。需要时用 `PCL_WRITE_SECRET=1 PCL_MS_CLIENT_ID=<GUID> ./build.sh x64 Release` 把 ID 烧进程序集（`EnvironmentInteropGenerator` 会读 `PCL_*` 环境变量），**不要再改 Provider 代码** |
| 陶瓦联机「加入房间」永远停在「正在加入……」 | 根因：等待就绪用「重试 300 次」而不是按时间算，而每次 `easytier-cli` 调用拿不到结果都会耗尽自己的超时（15 秒），300 × 十几秒 ≈ 十几分钟；若内核启动即崩溃则永远等不到。修复：①`ReadyTimeout = 180 秒` 按时间超时；②新增 `_coreExited`/`HasCoreExited`，内核退出立即失败并记录退出码；③`easytier-core.exe` 改为重定向 stdout/stderr 持续读取，保留尾部输出写进日志（启动失败原因终于可见）；④日志每 10 秒汇报等待进度；⑤公共中继除 6 个兜底地址外再取 `uptime.easytier.cn` 实时节点（最多等 4 秒、最多 8 个），实际下发地址写日志；⑥`ScaffoldingFactory` 检查 `Launch()` 返回值 |
| 反馈/更新页占位图标：动画不对、红叉下面少一条横杠 | ①补上 `MyLoading` 错误态里那条 `Rectangle Width=25 Height=2` 下划线；②删掉下游自造的呼吸动画，改为逐条复用 `MyLoading.ErrorAnimation`：前景色 300ms 渐变到 `ColorBrushRedLight` → 红叉淡入 100ms → 红叉从 0.6 倍弹性放大（400ms，`AniEaseOutBack`）；③`Foreground` 改为控件自身 DP（`SetResourceReference("ColorBrush3")` 初始化），镐子/红叉/横杠/文字统一 `{Binding Foreground, ElementName=PanBack}` |

---

## 0.12 2026-10-25 七次修订（snapshot-14）

| 问题 | 处理 |
| --- | --- |
| 上游正版登录需要自建 Azure 应用，而用户**无法创建租户**（`AADSTS50020`、`admin.microsoft.com` 401、SGU 租户无管理员角色） | 按用户决定改走**微软第一方客户端 ID + `login.live.com` 旧版设备码流程**：`MicrosoftProvider` 按 Client ID 形式自动分流（GUID → Entra ID v2；非 GUID → live.com）；Client ID 依次取 环境变量 `PCL_MS_CLIENT_ID` → 构建期 `MS_CLIENT_ID` → 内置 `00000000402b5328`。旧版流程 scope 为 `service::user.auth.xboxlive.com::MBI_SSL`，设备码在 `oauth20_connect.srf`、令牌/刷新在 `oauth20_token.srf` |
| snapshot-12 的旧版流程为什么失败 | **票据前缀错了**：第一方（Title）客户端 ID 的 `RpsTicket` 必须是 **`t=`** 前缀，`d=` 只适用于自建应用（依据 RaphiMC/MinecraftAuth 的 `isTitleClientId() ? "t=" : "d="`）。snapshot-12 只试了 `d=` 与无前缀，两个都不对。现在按 `t=` → `d=` → 无前缀 兜底，并把所用前缀写进日志 |
| 失败时看不到原因 | `XboxUtils.AuthenticateAsync` 任何非 2xx 都抛 `IdentityModelAuthenticationException`（含 HTTP 状态码 + 响应体）；`MicrosoftProvider` 在构造时记录所选流程与 Client ID，登录过程记录设备码、验证地址、Xbox 票据前缀与逐步失败原因 |
| **风险（交接必读）** | 第一方 ID 路线依赖微软默许，**不受官方支持**；若被关停，全体用户同时失效且无法配置补救 → 届时应改用自建 Azure 应用（`docs/MICROSOFT-OAUTH-SETUP.md` §2~§3，代码已支持 GUID 客户端 ID 自动走 v2 流程，无需再改代码）。另：第一方流程只支持**个人微软账号**，单位/学校账号无法登录 |

---

## 0.13 2026-10-25 八次修订（snapshot-15）

| 问题 | 处理 |
| --- | --- |
| 用户要求「正版登录相关代码回滚到 snapshot-8 的状态」 | snapshot-8 阶段本分支**尚未改动过登录协议层**，所以该状态 ≡ 上游原样。`MicrosoftProvider.cs` / `Utils/XboxUtils.cs` 已逐字节还原（md5 `dbc6e74a…` / `399d14ec…`），live.com 旧版流程、`t=`/`d=`/无前缀兜底、XboxUtils 错误详情增强全部撤销；其余登录文件（`ProfileService`、`ProfileManagement`、`MojangUtils`、`PageLoginMs`、`MyMsgLogin`）经 diff 确认本就和上游一致 |
| 现在正版登录什么状态 | 回到上游行为：`Secrets.MSOAuthClientId` 为空 → `Microsoft OAuth client id is not configured.`。启用方式只有一条——自建 Azure 应用 + 构建时注入 `PCL_MS_CLIENT_ID`（`docs/MICROSOFT-OAUTH-SETUP.md` §2~§3），**代码不用改** |
| 第一方 ID 路线还保留着吗 | **不保留**（代码已回滚）。`docs/MICROSOFT-OAUTH-SETUP.md` §6 已改回「备选方案（未启用）」，其中记录了 `t=` 前缀这一关键结论，将来若微软政策放宽或用户拿不到租户需要重新启用时可直接照做 |

---

## 0.14 2026-10-25 九次修订（snapshot-16）

| 问题 | 处理 |
| --- | --- |
| 用户确认「100% 没法注册应用（没卡）」，要求重新启用第一方 ID 路线，但明确表示「仍有 Bug，没修」 | `MicrosoftProvider` 重新启用 live.com 旧版设备码流程（第一方 Client ID `00000000402b5328`、scope `MBI_SSL`、Xbox 票据 `t=`）。相比 snapshot-14 多修三处：①已有档案刷新失败时不再中断登录，改为直接走全新设备码登录；②旧版流程的 `invalid_grant`/`access_denied`/`unauthorized_client` 错误追加「只支持个人微软账号」的可操作提示；③连不上 `login.live.com` 时给 `network_error` 并提示把两个微软域名设直连 |
| 复查结论（重要，接手别再踩） | 该流程的协议细节已逐项核对：设备码 `oauth20_connect.srf`（curl 实测可返回 user_code/verification_uri）、令牌 `oauth20_token.srf`（实测可返回 `authorization_pending`）、`XboxProperty` 的 `AuthMethod=RPS`/`SiteName`/`RelyingParty` 默认值均正确、XSTS 与 Minecraft 段沿用上游。**唯一与上游不同的关键点是 `t=` 前缀**（见 RaphiMC/MinecraftAuth 的 `isTitleClientId()` 分支） |
| 若仍然失败，第一步要查什么 | ①账号类型：`@xxx.onmicrosoft.com` 之类的单位/学校账号**必然失败**，必须用个人微软账号；②日志里 `微软登录：使用 live.com 旧版设备码流程` 是否出现；③`Xbox Live 用户认证失败（票据前缀：…）` 后面的 HTTP 状态码与响应体；④是否到了 `minecraft_owned`/XSTS 阶段（说明微软侧已通过，问题在账号档案） |
| 与上游的偏离（交接必读） | 正版登录已不是上游原样：`MicrosoftProvider.cs`、`Utils/XboxUtils.cs` 为下游改动版。上游每次同步后**必须重新应用这两处**，否则会退回 `Microsoft OAuth client id is not configured.` |

---

## 0.15 2026-10-25 十次修订（snapshot-17）

| 问题 | 处理 |
| --- | --- |
| 正版登录报错、**设备码弹窗根本不出现**（用户提供现场日志） | 日志显示 `System.TypeInitializationException → RegexParseException: Unknown property 'XDigit'`。`MicrosoftProvider` 里用 `\p{XDigit}{8}-…` 判断 Client ID 是否为 GUID，而 **`\p{XDigit}` 是 Java/ICU 写法，.NET 不支持**（这是从 RaphiMC/MinecraftAuth 的 Java 源码照抄来的）。它位于 `static readonly` 字段初始化器，于是**类型初始化器抛异常** → `new MicrosoftProvider()` 抛 `TypeInitializationException` → 连设备码弹窗都弹不出来。已改为 `Guid.TryParseExact(id, "D", out _)`，彻底不用正则 |
| **踩坑备忘（务必记住）** | ①**不要从 Java 项目照抄正则**：`\p{XDigit}`、`\p{Alpha}`、`\p{Alnum}`、`\p{Punct}` 等是 Java/ICU 属性，.NET 只认 `\p{Lu}`/`\p{Nd}` 这类 Unicode 类别；②**认证路径上不要放会抛异常的静态字段初始化器**——一旦抛 `TypeInitializationException`，表现是「功能完全没反应/流程还没开始就失败」，日志里也只会是一句泛泛的「登录失败」；③判断 GUID 用 `Guid.TryParseExact`，别手写正则 |
| 调试方法论（这次为什么能一次定位） | 让用户从「设置 → 日志 → 导出日志」把日志丢过来，比逐个猜测错误码快得多。日志里 `[Profile]` 前缀的行能看到所选流程、设备码与验证地址、Xbox 票据前缀、各步 HTTP 状态码 |

---

## 0.16 2026-10-25 十一次修订（snapshot-18）

| 问题 | 处理 |
| --- | --- |
| **陶瓦联机一直连不上**（用户现场日志证据） | 根因是 `EasyTierEntity` 把 EasyTier 参数拼错了：传 `--disable-sys-hole-punching`，而 EasyTier 2.6.4 只认 `--disable-sym-hole-punching`（sym = symmetric），内核直接 `error: unexpected argument` 以退出码 2 退出。`TryPunchSym` 默认 false → **默认配置下每次联机内核都秒退**。已改正参数名。注意同仓库旧实现 `PCL.Core/Link/EasyTier/ETController.cs` 拼写是正确的，只有 TerraCotta 那份错了——同步上游时要复查 |
| 房主「创建房间成功」其实是假成功 | 建房间只看 `Launch()` 返回值，内核随后秒退时界面仍显示成功并给出房间号，房客必然等不到房主。已加 `WaitCoreExited(2.5s)`：内核启动后 2.5 秒内退出则直接抛错并附内核输出，问题在「创建房间」阶段就暴露 |
| 联机排障工具 | 陶瓦联机 → 高级设置 → **联机诊断**：可导出 EasyTier 三件套文件状态、`easytier-core` 进程与命令行、开关/中继/协议、联机状态与房间号、日志里最近 60 条 `EasyTier`/`Et Cli`/`Lobby` 行 |
| 排障经验 | ①EasyTier 内核的 stdout/stderr 现在会被捕获进日志（snapshot-12 起），它自己会明确告诉你哪个参数不对；②`uptime.easytier.cn`（公共节点列表）在部分网络下 DNS 不通，属正常回落，不是故障；③「只能看到自己一个节点」= 对方根本不在网络里，先查房主侧内核是否真的活着 |

---

## 0.17 2026-10-25 十二次修订（snapshot-19）

| 主题 | 处理 |
| --- | --- |
| **政策变更：功能屏蔽改由插件控制** | 新增 `PCL.Core/App/FeatureFlags.cs`（运行期功能开关），由**已启用插件的 `provides`** 填充。被屏蔽功能（非蓝色主题、自定义主题色、回声洞、限制解除）不再由「构建类型」判定，而是「是否装了提供该功能的插件」。实现仍留在宿主，插件只提供数据与声明 |
| SDK/Host 扩展 | `PluginFeatureKeys`（4 个功能键）、`IExtraThemesProvider` / `IEchoCaveQuotesProvider`、清单 `provides`；宿主新增 `PluginHostManager.ProvidedFeatures` 与 `GetProviders<T>()`；`PluginHostBootstrap.ApplyProvidedFeatures()` 负责把功能键写进 `FeatureFlags` 并把主题定义 / 主题名 / 语录交给宿主 |
| 两个插件 | `plugins/SguPack`（`sgu.pack`：剑客黄 `golden-dusk` / 初音绿 / 烤音绿 + 回声洞语录）、`plugins/UnlockLimits`（`sgu.unlock-limits`：`unlock.region` + `unlock.offline-auth`，纯声明）。打包脚本 `plugins/package.sh`（python3 zipfile，Termux 无 zip） |
| 随包策略 | 非正式版：`.pclpkg` 放进产物目录 `plugins/`，首次运行自动安装启用；正式版：不带插件，插件包只放 `dist/` 与 `dist/plugins/` 供手动安装。判定依据是 `metadata.json` 的 `suffix` 是否为空 |
| ⚠️ 恢复的限制 | `ProfileUi.CanCreateOtherProfile()` 恢复上游口径：**必须先有一个正版档案**才能创建离线 / 外置档案，由 `unlock.offline-auth` 解除。**中国地区下载限制在开源部分没有对应代码**（原要求提到的是合并补丁，可能作用于闭源部分或已随服务端下线），因此 `unlock.region` 目前只是声明位，宿主暂无可解除的实现 |
| ⚠️ 与 `docs/PLUGIN-SYSTEM.md` §8.4 的冲突 | 该节原写「插件即使拿到能力也不能让非蓝色主题在发行版可见」。政策变更后这条不再成立：**由插件提供即视为可用**。安全性改由「正式版不随包附带插件」保证，详见 §8.4 的修订说明 |
| 维护提醒 | 主题 / 语录的增删改现在**只改插件**（`plugins/SguPack/SguPackPlugin.cs`），不必动宿主；功能键拼写必须与 `PluginFeatureKeys` / `FeatureFlags.Keys` 一致，否则功能不会生效 |

---

## 0.18 2026-10-25 十三次修订（snapshot-21）

| 主题 | 处理 |
| --- | --- |
| 「内置插件在插件管理里找不到、无法启用」 | 根因：snapshot-19 只是把 `.pclpkg` 放在产物目录 `plugins\` 里，用户只复制 exe 时自然什么都没有。现在非正式版会把插件包**嵌入 exe 资源**（`IncludeBundledPlugins=true` → `EmbeddedResource ..\plugins\dist\*.pclpkg`），启动时 `PluginHostBootstrap.InstallEmbeddedPackages()` 释放到临时目录并装进 `PCL\Plugins`，随后由 `Initialize()` 扫描加载并自动启用；exe 同级 `plugins\` 目录作为兼容路径保留 |
| 内置哪几个插件 | `SguPack`（SGU 插件包）、`UnlockLimits`（限制解除）、`HelloPlugin`（samples 示例，用来验证插件系统本身）。**非正式版全部内置**；正式版一个都不带（插件包只放 `dist/` 与 `dist/plugins/`）。`DownloadUnlock`（下载限制解除）**故意例外**：只发布到 `dist/DownloadUnlock.pclpkg`，不进 `plugins/dist/`，否则非正式版会自动装上、默认就不受限 |
| Minecraft 下载限制 | `PageDownloadClient` 在未启用 `unlock.region` 时整页替换为 `MyUnavailablePlaceholder`（与反馈页同一个红色镐子 + 红叉 + 横杠，动画同 MyLoading 错误态），文案「应相关法律法规要求，PCL Swordsman 无法在中国大陆地区提供 Minecraft 下载服务，红豆泥斯密马赛！」。启用「限制解除」插件即恢复 |
| 判定方式 | **不做 IP 归属地检测**，只看插件是否提供 `unlock.region` 功能键——行为确定、可复现、可离线验证 |
| 限制解除插件 | 升级到 `1.1.0`：声明 `unlock.region`（下载页屏蔽）+ `unlock.offline-auth`（离线/外置档案前置正版），仍是纯声明插件，解除逻辑全在宿主 |

---

## 0.19 2026-10-25 十四次修订（接手基线：snapshot-21 定标）

> 口径变更：**以本工作区的最新构建 `27.1-snapshot-21(2.15.1)` 为标准产物**，
> 不再追 PCL CE 上游版本号（`metadata.json.upstream` 仅作标注，见 §4.1）。

| 主题 | 处理 |
| --- | --- |
| 接手核查 | 源码树在 snapshot-21（`code` 544），但 `dist/` 只有 snapshot-19/20；用 `tools/build.ps1` 重新构建 `x64 Release` 后补齐 |
| ⚠️ 修正 `tools/build.ps1` | 旧版与 `build.sh` **不等价**：漏了 `-p:IncludeBundledPlugins=<suffix 非空>`（导致非正式版**没嵌入** `plugins\dist\*.pclpkg`，插件管理里看不到内置插件）、漏了同步 `dist/` 与 exe 同级 `plugins/`。已按 `build.sh` 补齐三件事：嵌入插件包、复制产物到 `dist/`、复制插件包到 `dist/`+`dist/plugins/`+（非正式版）`artifact-<平台>/plugins/` |
| 标准构建产物 | `artifact-x64/PCLCE For SGU 27.1-snapshot-21(2.15.1).exe`，23,826,536 B，SHA-256 `CAC9CC2F59ED9D00D9300665C7E7E67A677F91758732562A877D4A6B7B995C0A`；`dist/` 同名副本哈希一致 |
| 产物自检 | 内嵌 `snapshot-21` / `"code": 544` / `2.15.1` 各 1 次；`此版本不支持更新`、`下游项目声明` 在；`TerraCotta` 313 次；三插件包 `SguPack`/`UnlockLimits`/`HelloPlugin` 各 3 次（已嵌入）；`snapshot-7`/`"code": 531`/`2.12.1` 均 0 次 |
| 工作区根快捷方式 | `PCLCE For SGU 27.1-snapshot-21(2.15.1).lnk` → 指向上述 exe，工作目录 `artifact-x64`（**必须显式指定工作目录**，否则启动器会在新目录另起一套配置） |
| 未做（有意） | 不做上游代码合并；`PCL-2.99.0.0/`（上游 `PCL.Core` submodule 解压出的嵌套完整 PCL，含 `uninstall.exe` / `PCL.exe` / LGPL 组件）按裁决**保留但暂不使用**，且**不得参与编译与打包** |

---

## 0.20 2026-10-25 十五次修订（snapshot-22：插件包化）

> 用户报障：「PCL 不能正常启用/停用插件，并且不能正常读取 `.pclpkg` 插件文件」。
> 根因是旧模型把插件当**解压目录**（`PCL/Plugins/<id>/plugin.json`），拖进来的 `.pclpkg` 根本没人认；
> 启用/禁用靠 `plugins.state.json` 里的名单，删目录、换文件名都对不上。

| 主题 | 处理 |
| --- | --- |
| 新模型 | **一个插件 = `PCL/Plugins/` 下的一个 `.pclpkg` 文件**（zip 包）。扫描、加载、显示、删除全部以包为单位（`PCL.PluginHost/PluginPackage.cs`，新增） |
| 热开关 | 禁用 = 把 `Foo.pclpkg` 改名为 `Foo.pclpkg.disabled`；启用 = 改回来。**不再写** `plugins.state.json`。手动在资源管理器改名同样生效 |
| 拖入安装 | 拖入 `.pclpkg` → 弹「插件确认安装」（标题同主题色、灰字副文、确定/取消）→ 装进 `PCL/Plugins/` 并默认启用 → 提示「已安装 <文件名>！」。复用主页面拖 `.jar` 的「Mod 确认安装」按钮样式（`MyMsgBox`，`Button1 = "确定"` / `Button2 = "取消"`） |
| 非包文件拦截 | 拖入非 `.pclpkg` 且在插件管理页时，提示 `PCL Swordsman 无法确定适合的操作…` 并**直接返回**，避免被 `PageInstanceCompResource.InstallMods` 当 Mod 装掉 |
| 删除 | 直接从 `PCL/Plugins/` 删掉 `Foo.pclpkg` / `Foo.pclpkg.disabled`（老目录形式也一并清）。文件被占用时登记 `pending-delete.json`，下次启动先删 |
| 老目录迁移 | 启动时 `MigrateDirectoriesToPackages()` 把历史遗留的 `PCL/Plugins/<id>/` 目录打成 `<id>.pclpkg` 并删目录，数据目录不动 |
| 内置插件不覆盖用户选择 | `PackageAlreadyInstalled()` 守卫：`plugins/` 目录里只要存在同名包（启用或 `.disabled`）就跳过自动安装，用户手动禁用后不会被下次启动强行拉起 |
| 只读包 | 从只读位置（压缩包/共享目录/附件）复制来的包在改名、覆盖、删除前自动清 `ReadOnly`（`PluginPackage.MakeWritable`），并带 5 次重试 |
| 管理页 | 「启用 / 禁用」聚合按钮改为逐个上报失败原因；「打开文件夹」对方括号包用 `explorer /select` 选中文件本身；空状态与副信息文案改为 `.pclpkg` 说法 |
| 文档 | 新增 [`docs/PLUGIN-API.md`](docs/PLUGIN-API.md)（接口参考 + 后续路线）与 [`docs/PLUGIN-GUIDE.md`](docs/PLUGIN-GUIDE.md)（插件制作指南）；新增 `plugins/package.ps1` 打包脚本 |
| 产物 | `artifact-x64/PCLCE For SGU 27.1-snapshot-22(2.15.1).exe`，23,842,920 B，SHA-256 `6C33C66FF61320D6E5AD786AB1854AEE02BA0BE05F936A38E62640C6F49D6709`（`dist/` 同哈希）；`code` 545；详见 `DOWNSTREAM-CHANGES.md` |

---

## 0.21 2026-10-25 十六次修订（snapshot-23：同一插件多份副本导致「禁用/删除不生效」）

> 用户实测反馈（用 snapshot-22）：「安装正常，但是启用变成禁用后启用目录该插件仍然存在而且数量未变」。

**根因**：安装时的目标文件名取自**源文件名**而不是插件 `id`。同一个插件从不同来源进来就会变成多个文件：

```
PCL/Plugins/
  HelloPlugin.pclpkg          ← 内嵌包安装（资源名）
  HelloPlugin.pclpkg.disabled ← 用户点了禁用（只改名了这一份）
  hello-1.0.0.pclpkg          ← 用户从 samples/dist 拖进来
  SguPack.pclpkg
  PCL.SguPack.pclpkg          ← 内嵌包（资源名带 PCL. 前缀）
  UnlockLimits.pclpkg
  UnlockLimits.pclpkg.disabled
```

`SetEnabled` 只改被点击的那一个文件的后缀，其它同 `id` 副本仍然启用 → 列表里还看得见、启用数量不变，看起来就是「禁用无效」。删除同理只删一份；`PackageAlreadyInstalled()` 也只看文件基名，所以每次启动又会把内嵌包按另一个名字再装一份。

| 主题 | 处理 |
| --- | --- |
| 安装文件名 | `PluginInstaller.InstallPackage()` 改为**用清单里的 `id` 命名**（`Sanitize(id) + .pclpkg`），并在落盘前调 `DeleteById()` 清掉同 `id` 的其它文件 |
| 判重 | 新增 `PluginInstaller.FindPackageIds(pluginsDirectory)`；`PluginHostBootstrap.PackageAlreadyInstalled()` 改为**按 `id` 判断**，不再看文件名。`plugins\` 里的旧命名包导入后会被规范成 `<id>.pclpkg` |
| 启动自愈 | 新增 `PluginHostBootstrap.DeduplicatePackages()`：把同 `id` 的多份副本合成一份，**禁用优先**（只要有一份是 `.disabled` 就按禁用保留，宁可少加载也不擅自把用户关掉的东西又打开），保留时间最新的那份内容，文件名规范成 `<id>.pclpkg` / `<id>.pclpkg.disabled`，其余删除。用户现有目录不需要手工清理，启动一次即自愈 |
| 加载去重 | `PluginLoader.LoadOne()` 兜底：同 `id` 已加载则跳过并记日志 `[PluginHost] 跳过重复插件：xxx（同 id 已加载）` |
| 删除 | `PluginInstaller.Uninstall()` / 新增 `DeleteById()` 都删掉该 `id` 的**全部**文件（启用 + 禁用两种名字），不再只删点中的那一个 |
| 版本 | `metadata.json`：`suffix` snapshot-22 → **snapshot-23**，`code` 545 → **546** |
| 产物 | `artifact-x64/PCLCE For SGU 27.1-snapshot-23(2.15.1).exe`，23,847,016 B，SHA-256 `000BE000A4FADCEA5983BF2FC764222D754F8E9A6EAF58676304E0FD89F04D5E`（`dist/` 同哈希）；`code` 546；详见 `DOWNSTREAM-CHANGES.md` |
| 验证 | 控制台探针 `_tmp/probe-plugin-pkg`（直接引用 `PCL.PluginHost.dll`）全部 PASS：同 `id` 两份包 → 扫描只出 1 项、`DeduplicatePackages()` 后只剩 1 个文件、禁用后该 `id` 在 `FindPackageIds` 里仍唯一、删除后文件与列表项同时消失。GUI 仍未实测（单实例检测挡住） |

---

## 0.22 2026-10-25 十七次修订（snapshot-24：缓存残件导致「禁用后再启用报缺少 plugin.json」）

> 用户实测反馈（用 snapshot-23）：「禁用的启用后会直接出错确实plugin.josn怎么办」。
> 前一轮修的「多份副本」确实生效了（日志 `[PluginHost] 已合并 8 个重复的插件包副本`），但接着暴露出这个新问题。

**现象**（`PCL/Log/Launch-2026-10-25-224742226.log`）：

```
[22:47:44] [PluginHost] 已合并 8 个重复的插件包副本        ← snapshot-23 的去重正常
[22:48:02] [PluginHost] 插件 sgu.unlock-limits：sgu.unlock-limits.pclpkg.disabled → sgu.unlock-limits.pclpkg
[22:48:03] [PluginHost] sgu.unlock-limits.pclpkg：Faulted - 缺少 plugin.json   ← 启用后立刻出错
[22:48:04] 已应用插件功能：themes.extended, echo-cave                       ← unlock.region / unlock.offline-auth 丢了
```

包文件本身是好的（`tar -tf sgu.unlock-limits.pclpkg` 里 `plugin.json` + `SguUnlockLimits.dll` 都在），所以问题在**解包缓存**这一层。

**根因**：缓存目录名是「基名_长度_修改时间」。改名不改长度、也不改修改时间，所以启用/禁用两份名字映射到**同一个缓存目录**。禁用时该插件不再被加载，`CleanupCache()` 会删掉这个目录；但 `AssemblyLoadContext` 释放文件有延迟，递归删除常常只删掉一部分（`plugin.json` 先没了，dll 还被占用留着）。再次启用时：

1. `ExtractToCache()` 看到目录已存在但清单缺失 → 走重新解压；
2. 解压到临时目录后 `Directory.Move(temp, target)` 因 target 残留目录存在而抛异常；
3. 旧代码的 catch 分支发现 target 存在就只删临时目录，然后**照样 `return target`**；
4. 加载器拿到一个没有 `plugin.json` 的目录 → `Faulted - 缺少 plugin.json`。

**修法**（`PCL.PluginHost/PluginPackage.cs`，`ExtractToCache` 重写收尾逻辑）：

| 步骤 | 处理 |
| --- | --- |
| 复用判断 | 新增 `private static bool IsUsableCache(string directory)`：目录存在**且**清单文件在位才复用 |
| 残件处理 | 目录在但不可用时先 `TryDeleteDirectory` 强力清理；清理失败（文件仍被占用）则改用带随机后缀的唯一目录名，绝不返回残缺目录 |
| Move 失败兜底 | 目标不可用 → 换第二个唯一名字落盘；连改名都失败就把临时目录本身当结果（它本身就是完整解压出来的） |
| 最终校验 | 返回前再验一次 `IsUsableCache`，仍不可用则抛 `IOException`（错误信息写明是哪个包的缓存缺清单），不再把问题推给加载器 |
| 版本 | `metadata.json`：`suffix` snapshot-23 → **snapshot-24**，`code` 546 → **547** |
| 产物 | `artifact-x64/PCLCE For SGU 27.1-snapshot-24(2.15.1).exe`，23,847,016 B，SHA-256 `C70D70DD2F6B63185E10DA48558074B386B4A7007ED16ABB51C01F4CEDA4D30F`（`dist/` 同哈希） |
| 验证 | 新增回归探针 `_tmp/probe-cache-residue`：先手工造一个「缺 plugin.json 的同指纹缓存目录」再加载，然后连做 3 轮「禁用→启用」，**18 项全部 PASS**；原有 `_tmp/probe-plugin-pkg` 也重跑，**23 项全部 PASS** |

**教训**：缓存目录的「是否可复用」不能只看目录是否存在，必须看**内容是否完整**；`Directory.Move` 失败后也绝不能假定目标已经就绪。

---

## 1. 项目定位（先读这段）

**PCLCE For SGU** 是 [PCL CE](https://github.com/PCL-Community/PCL2-CE)（Plain Craft Launcher Community Edition）的**下游分支**，由剑客群组服服主团队联合维护，仅供 SGU 及其相关服务器（VFH 术友汇、HCE 圣苦力怕帝国）成员内部分享使用。

必须遵守的几条底线：

1. **非官方**：本作不是 PCL Community 或 PCL 原作者龙腾猫跃的官方发布版本，界面上必须持续保留"下游项目声明"（已实现于「设置 → 软件信息」顶部卡片与「设置 → 反馈」页面）。
2. **反馈不收**：本分支**已禁用反馈功能**，任何问题都不得引导用户去 PCL Community / PCL2-CE 仓库提 issue。反馈渠道为 `hmjmfabc@swordsman.top`、`mingyugames@swordsman.top`。
3. **版权**：上游代码与素材版权归 PCL Community 与龙腾猫跃；本作按上游许可（仓库根 `LICENSE`，Apache License 2.0）分发，保留全部上游版权与许可声明。
4. **主题色**：PCL 的主题色是原作者龙腾猫跃的主要赞助项目，因此**发行版中不得保留非蓝色主题与自定义主题色**（代码已强制，见 §5.3）。
5. **联机**：只在 EasyTier 纯 P2P 路线上做，**不要引入任何依赖 PCL 中继 / 大厅服务器的实现**（见 §5.2）。

---

## 2. 当前状态速览

| 项目 | 值 |
| --- | --- |
| 下游版本号 | `27.1-snapshot-23`（`metadata.json` 的 `base` + `suffix`） |
| 完整展示版本号 | `27.1-snapshot-23(2.15.1)`（括号内为已标注的上游版本） |
| 上游版本标注 | PCL CE `2.15.1`（`metadata.json.upstream`；**代码基点**见 §4.1） |
| 内部版本代号 | `546`（`version.code`，用于升级/降级判断；递增会触发一次版本说明弹窗） |
| 程序显示名 | `Plain Craft Launcher 2 swordsman`（**`AssemblyName` 仍是 `Plain Craft Launcher 2`**） |
| 目标框架 | `net10.0-windows`（WPF，`EnableWindowsTargeting`），win-x64 自包含单文件 |
| 构建产物 | `artifact-x64/PCLCE For SGU 27.1-snapshot-23(2.15.1).exe`（23,847,016 B，SHA-256 `000BE000…4D5E`；`build.sh`/`build.ps1` 都会再同步一份到 `dist/`） |
| **基线口径** | **以本工作区这个构建为标准**；上游 PCL CE 版本号只作标注，不追同步（§0.19、§4.1） |
| 源码基线 | 上游 PCL CE `dev` @ `d1149d5`（2026-10-24）的完整源码快照（无 `.git` 历史） |
| 验证等级 | 编译 ✅ / 启动 ✅ / 内嵌资源 ✅ / 界面观感 ❌ 未复测 / 联机连通性 ❌ 未复测 |

### 2.1 最近一轮（2026-10-25）改了什么

| 改动 | 一句话 | 详细位置 |
| --- | --- | --- |
| 品牌改名 | 标题栏 CE 徽标 → 白底 `Swordsman`；项目名统一为 `Plain Craft Launcher 2 swordsman` | §5.4 |
| 反馈页占位 | 禁用时整页只显示「红色带叉箭头 + 红字」占位控件 | §5.4 |
| 更新页占位 | 整页只显示「此版本不支持更新」 | §5.4 |
| 遥测彻底禁用 | Sentry 不初始化、不弹询问框、设置项置灰 | §5.2 |
| 插件侧边栏 | 工具页新增灰色标题「插件」+ 插件管理 / 插件市场 | §5.7、§11 |
| 插件系统 M1 | SDK 契约 + 加载器 + 完整管理界面 + 示例插件 | §11 |

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

Windows 上没有 `bash` / `python3` 时，用等价脚本（**已完成与 `build.sh` 的等价化，见 §0.19**）：

```powershell
powershell -ExecutionPolicy Bypass -File tools\build.ps1          # Release + x64
powershell -ExecutionPolicy Bypass -File tools\build.ps1 -Configuration Debug
powershell -ExecutionPolicy Bypass -File tools\build.ps1 -RestoreSources <离线feed目录>
```

`build.ps1` 与 `build.sh` 必须保持同样的三件事，改动其一就同步另一：
非正式版（`suffix` 非空）`-p:IncludeBundledPlugins=true` 把 `plugins\dist\*.pclpkg` 嵌入 exe；
产物同步一份到 `dist/`；插件包同步到 `dist/`、`dist/plugins/`，非正式版另放 exe 同级 `plugins/`。

### 3.2 CI 构建

`.github/workflows/reusable-build.yml` 已加入同名重命名步骤，产物同样为
`PCLCE For SGU <版本号>.exe`。CI 会从 secrets 注入微软登录、CurseForge、Sentry、Naid、
联机根服务器等密钥；**本分支已不依赖这些密钥也能构建运行**（缺密钥时会自动降级，见 §5.2）。

### 3.3 构建环境备注

原维护者环境：Android Termux（aarch64）+ `pkg install dotnet-sdk-10.0`（SDK 10.0.113，`linux-bionic-arm64`），
WPF 交叉构建通过 `EnableWindowsTargeting=true` 实现，可正常产出 Windows 可执行文件。

Windows 本机（2026-10 核验，可用）：

- .NET SDK **10.0.402**（win-x64），`x64 Release` 单文件产物构建正常（0 错误，23,847,016 字节 = snapshot-23 标准构建）
- `build.sh` 依赖 `bash` + `python3`，Windows 上两者可能都没有 → 用 `tools\build.ps1`
  ⚠️ 该脚本必须保持 **UTF-8 with BOM** 保存：Windows PowerShell 5.1 会把无 BOM 的 UTF-8
  按系统代码页（GBK）解码，中文注释末尾的多字节序列可能吞掉换行，导致 `param` 块解析失败
- 网络受限时可把 NuGet 依赖放到本地目录并加 `-p:RestoreSources=<dir>` 离线还原
- 命令建议加 `-m:1 -p:UseSharedCompilation=false` 限制并行（脚本已内置），dotnet 进程内存峰值约 1.5 GB
- **构建只能验证编译期**：UI 实际观感、P2P 联机连通性等必须在 Windows 实机测试
- 构建/启动期间启动器会清理 `%TEMP%`，若与其他工具的临时目录共用会互相干扰

### 3.4 发版前检查清单

1. `dotnet build`（或 `./build.sh ... Debug`）0 错误
2. 全部语言文件 XML 可解析（当前 7 个）：
   `python3 -c "import xml.etree.ElementTree as ET,glob;[ET.parse(p) for p in glob.glob('PCL.Core/App/Localization/Languages/*.xaml')]"`
   （Windows 无 python 时用 PowerShell，**必须指定 UTF8 读取**：`[IO.File]::ReadAllText($p,[Text.Encoding]::UTF8)` 后 `[xml]`；
   用 `Get-Content -Raw` 不指定编码会按 GBK 解码而误报解析失败）
3. `metadata.json` 的 `suffix` / `code` 已更新（见 §4）
4. 产物文件名符合规范，且 exe 内 `"upstream"` / `"suffix"` / `"code"` 字符串正确
   （单文件产物里 `metadata.json` 以明文 UTF-8 内嵌，可直接字符串搜索核对）
5. 抽查关键字符串：`Swordsman`、`此版本不支持更新`、`下游项目声明`、`TerraCotta`
6. **非正式版**还要抽查三个内置插件包已嵌入：`SguPack`、`UnlockLimits`、`HelloPlugin` 各出现 3 次；
   若为 0，说明漏了 `-p:IncludeBundledPlugins=true`（`build.sh` 有、`build.ps1` 曾漏，见 §0.19）
7. 若改动涉及界面，在 Windows 上过一遍：启动页三选项卡、设置各页（反馈/更新应为占位页）、联机页、百宝箱回声洞、插件管理页、Minecraft 下载页（未启用 `unlock.region` 时是占位页）

---

## 4. 版本号与同步上游

规范详见 `VERSIONING.md`，核心四条：

1. 完整版本号 = `年份.大更新[.小更新][-阶段-Build号](上游版本号)`，例如 `27.1-snapshot-18(2.15.1)`。
   阶段后缀写在 `metadata.json` 的 `suffix` 里，取值只有五种：
   `snapshot-N`（快照）/ `pre-N`（预发布）/ `rc-N`（备选发布）/ `experiment-N`（实验）/ `hotfix-N`（热修，配合 `base = 27.x.y`）；
   **正式版 `suffix` 留空**（版本号形如 `27.1` / `27.1.1`）。
2. **Build 号每次构建前 +1**，同时 `code` 也 +1；Build 号**跨阶段连续递增**，不重置。
3. **同步上游时下游版本号不变，只改 `upstream`**。
4. 只有下游自己发正式版时才递增外层版本号（小更新 +1；不兼容改动时大更新 +1、小更新归零）。

`metadata.json` 是**版本号唯一来源**：

```json
{ "name": "Plain Craft Launcher 2 swordsman",
  "version": { "base": "27.1", "upstream": "2.15.1", "suffix": "snapshot-18", "code": 541 } }
```

`AssemblyVersion` 保持 4 段格式（`27.1.0.0`），不要动。

### 4.1 ⚠️ 先确认「真实代码基点」（2026-10 修订，务必先做）

踩过的坑：此前本仓库文档长期写着「源码基线 PCL CE `2.12.1`」。这个数字取自**上游 `metadata.json` 的
`upstream` 字段，但上游该字段的含义是「本作继承自原版 PCL2 的版本号」，从 fork 起固定为 `2.12.1`，
不随上游版本推进**（上游 `2.15.1` 与 `2.15.1-beta.1` 的 metadata 里它都是 `2.12.1`，变化的是 `base`）。

用三方文件比对核实后，本仓库的真实基点是**上游 `dev` @ `d1149d5`（2026-10-24）**，
即 2.15.1 正式版（2026-10-20，`code` 524）之后的 dev 快照：该快照与本仓库除 65 处下游改动外**逐字节相同**。

由此得到两条经验：

- 判断「已同步到哪个上游版本」**不要看上游 metadata 的 `upstream` 字段**，要做文件比对（见 §4.2）；
- 上游 tag `v2.15.1-beta.1`（2026-09-04，`code` 523）比本仓库当前的代码**更旧**，
  若按字面「同步到 beta.1」会让 73 个纯上游文件回退（丢掉 2.15.1 正式版的修复，含 #3673「中国大陆地区禁用 MC 安装」）。

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

下游改过的文件清单见 §5（重点：`ModLaunch.cs`、`ModDownloadLib.cs`、`PageSetupUI.xaml`、`ThemeService.cs`、
`UpdateManager.cs`、`ProfileUi.cs`、`Secrets.cs`、`Lobby*.cs`、`EasyTierEntity.cs`、`PageToolsGameLink.xaml(.cs)`、
`PageSetupFeedback` / `PageSetupUpdate`、4 个语言文件）。

改完后的收尾步骤：

1. 更新 `Plain Craft Launcher 2/metadata.json`：`upstream` → 新上游版本；`code` 递增；`base`/`suffix` 视情况
2. 复查本地化键（§6）：上游新增/删除的键、被下游改写过的键（`Update.CommunityNotice.*`、`Launch.Right.CommunityHint.*` 等）
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
`MyUnavailablePlaceholder`（红色带叉箭头 + 红字），分别展示"此版本不支持更新"与下游反馈声明。详见 §5.4。

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
  `KaoYinGreen=10`(#33CCBB)、`IchikaBlue=11`(#33AAEE)、`MermanBlue=9`(#3366CC)、
  **`SakuraPink=12`(#FF94D6)**（取色自樱花披风纹理，5 个色板色全部照抄）、
  **`NetherRed=13`(#B3392E)**、**`MethylOrange=14`(#EE7B1E)**、**`AmethystPurple=15`(#9A5CE0)**

- `PCL.Core/UI/Theme/ThemeService.cs`
  - `GetCurrentThemeArgs()` 返回 `(double Hue, double LightAdjust, double ChromaAdjust)`（**色相是 double，别改回 int**，
    否则自定义色会有 ±1 偏差）
  - 新增主题通过 `_GetThemeArgsFromHex("#RRGGBB", fallback)` 把颜色换算到 OKLCH 反解参数，保证主色 `Color3` 精确等于指定色
  - `_CalculateColors()` 会分派：剑客黄走 `_CalculateColorsGoldenDusk()`（浅金→金→琥珀→暮色橙的逐档色相渐变），
    樱花粉走 `_CalculateColorsSakuraBloom()`（樱白粉→樱花粉→桃红→梅红→梅紫），
    其余「十六进制主题」走 `_CalculateColorsFromHex()`，上游预设主题走 `_CalculateColorsDefault()`（与上游完全一致）
  - 插件想自带配色风格时给 `PluginThemeDefinition.Flavor` 填 `golden-dusk` / `sakura-bloom`；
    `IsHexTheme()` 里**别忘了加新主题**，否则它会掉进上游预设分支、主色不等于你指定的颜色
  - **发行版门禁**：`IsReleaseDistribution` = 版本号无 `snapshot`/`experiment` 后缀；
    `IsBlueTheme()` 标记蓝色系主题；`IsThemeSelectable()` 决定当前构建可见主题。
    **发行版只保留蓝色系主题**（天空蓝/龙猫蓝/死机蓝/肥鱼蓝/天依蓝/海人蓝/一歌蓝），
    非蓝色主题（剑客黄/初音绿/烤音绿）与自定义主题色输入行隐藏；旧配置命中不可用主题时自动回退龙猫蓝
- `Plain Craft Launcher 2/Pages/PageSetup/PageSetupUI.xaml(.cs)`
  - 主题下拉框选项由 `AllThemes` 主列表 + `ThemeColorValues` 过滤得到，**不要再用"索引 == 枚举值"的假设**
  - 自定义主题色输入框 `TextCustomColor`（正则 `^#?[0-9A-Fa-f]{6}$`）+ 预览色块 `RectCustomColor`
  - 个性化页顶部有引导语 `Setup.Ui.Basic.BlueHint`（"蓝色大肥鱼…"），配合发行版隐藏主题的策略
- `FormMain.xaml(.cs)`：标题栏装饰由 `RefreshTitleSword()` 统一管，**五套互斥**：
  - 剑客黄 → `ImgTitleSword`（官方钻石剑像素图，`Opacity=0.22`，上下被标题栏裁切）+ `RectTitleGolden`（黄昏圣光）+ 金色辉光
  - 樱花粉 → `PanTitlePetals`（右上角 260×48 画布，跑**原版樱花粒子**：`Images/Particles/cherry_0..11.png`
    12 张 3×3 精灵，逻辑在 `Modules/UI/ModTitlePetals.cs`）+ `RectTitleSakura`（樱粉柔光）
  - 下界红 → `RectTitleNether`（整条标题栏 `ImageBrush` 平铺**原版** `Images/Blocks/NetherWartBlock.png`，
    16×16 原尺寸，`Opacity=0.28`）
  - 甲基橙 → `ImgTitleFormula`（右上角像素字化学式 `C₁₄H₁₄N₃NaO₃S`，`methyl_orange_formula.png` 159×27，`Opacity=0.22`）
  - 水晶紫 → `RectTitleAmethyst`（整条标题栏平铺**原版** `Images/Blocks/AmethystBlock.png`，16×16 原尺寸，`Opacity=0.28`）
  - **方块 / 粒子一律复用原版材质**（`tools/extract-vanilla-textures.py <client.jar>` 从原版客户端 jar 解出）；
    化学式是自绘像素字（`tools/make-methyl-orange-formula.py`，5×7 点阵），**不内嵌任何字体文件**

#### 单文件发布下加载内嵌图片（踩过）

本程序 `PublishSingleFile=true`。**不要**用相对 pack URI（`new Uri("/Assembly;component/Images/…", UriKind.Relative)`）
去加载 `Images/` 下的内嵌图 —— 单文件下 `Application.ResourceAssembly` 解析不到，`EndInit()` 直接抛异常，
表现是「一点反应都没有」（`ModTitlePetals` 的樱花粒子就这么一片都没飘出来过）。
统一用绝对 pack URI：`pack://application:,,,/Plain Craft Launcher 2;component/Images/…`
（仓库里 `ModBase.pathImage` / `ModOneClickLaunch.RenderPng` / `MinecraftServer` 都是这个写法），
兜底再走 `ModBase.GetResourceStream("Images/…")` 或程序集 `.g.resources` 清单。

#### 插件页面与插件自带主题（snapshot-59 起）

- **插件页面**：`IPluginUiProvider`（能力 `ui.page`）→ 宿主 `PagePluginHost` 统一容器 +
  `PageToolsLeft` 动态侧栏分组 + `FormMain.PageSubType.ToolsPluginPage`。
  插件给 `CreatePage` 工厂（每次新建控件），宿主管侧栏、滚动、来源标注、出错兜底。
- **插件自带主题**：`IPluginCustomThemesProvider`（能力 `themes.custom`）→ 统一落在
  `ColorTheme.PluginDefined = 16`，具体 id 存在 `UiLightPluginTheme` / `UiDarkPluginTheme`；
  `CreateTitleDecoration` 给标题栏装饰，宿主放进 `PanTitlePluginDecoration`。
- **文档隔离（用户硬要求）**：主题相关内容**只能**写内部文档
  （`docs/PLUGIN-THEMES.md`、`HANDOVER.md`、`DOWNSTREAM-CHANGES.md`）；
  `docs/PLUGIN-API.md` / `docs/PLUGIN-GUIDE.md` 里不得出现 `theme` / `主题` 字样
  （包括 `ThemeChangedEvent`）。改完自检：`grep -in "theme\|主题" docs/PLUGIN-API.md docs/PLUGIN-GUIDE.md` 必须无输出。
- 现成例子：`plugins/SguPack`（页面）、`plugins/FadedGray`（自带主题 `Faded灰` + 内嵌 `Faded.jpg` 装饰）。

#### 插件系统：依赖 / 版本 / 事件 / 设置（snapshot-58 起）

- **扫描四阶段**（`PCL.PluginHost/PluginLoader.cs`，顺序不能乱）：发现（只读清单、包不解压）→
  宿主版本（`minHostVersion` 不满足 → `Incompatible`）→ 依赖（缺依赖 / 被禁用 / 版本不满足 / 循环 →
  `Faulted` + 可读原因）→ **按拓扑序加载**（依赖在前；加载前再确认依赖真的可用，失败会连带停用依赖方）
- 版本比较在 `PluginVersion.cs`：宿主版本只取**前导数字段**（`27.1-snapshot-58(2.15.1)` → `27.1`），
  依赖范围支持 `*` / `1.2.0`（≥）/ `>=` / `>` / `<=` / `<` / `=` / `^1.2` / `~1.2`
- **事件总线**（`IPluginContext.Events`）：宿主 publish、插件 subscribe，需要清单声明 **`events`** 能力；
  事件类型在 `PCL.PluginSdk/IPluginEvents.cs`；每个回调单独 try/catch，插件卸载时 `Clear()` 全部退订。
  宿主发布点：启动完成（`PluginHostBootstrap`）、主题变化（`ThemeService` 两个事件）、游戏启动
  （`ModLaunch.McLaunchStart` / `McLaunchEnd`）
- **插件设置界面**：插件实现 `IPluginSettingsProvider` 声明 `PluginSettingDefinition` 列表，
  宿主管页在条目上显示齿轮、按 schema 用现成控件渲染（`MyCheckBox`/`MyTextBox`/`MyComboBox`/`MySlider`），
  读写一律走插件自己的 `IPluginConfig`（`PluginDescriptor.Config`）
- 示范插件：`plugins/Welcome`（1.1.0）同时用到设置与事件，改这两块时先看它

### 5.4 品牌与界面

| 内容 | 位置 |
| --- | --- |
| 标题栏 `CE` 徽标改为白底 **`Swordsman`** 文字（原 " For " 与 `swordsman.png` 图标已移除） | `FormMain.xaml`（`CELogo` 内改放 `LabCELogo`：`Grid.Column="1"`、`Margin="10,0,0,16.6"`、`Padding="3.5,0.5"`、`CornerRadius="3"`、`FontSize="9"` SemiBold、`Foreground={DynamicResource ColorBrush3}`；`LabTitleFor` / `PanTitleSwordsman` / `ImgTitleSwordsman` 已删除） |
| 项目/程序显示名统一为 **`Plain Craft Launcher 2 Swordsman`**（原为小写 `swordsman`，snapshot-51 修正） | `FormMain.xaml`（窗口 `Title`）、`Plain Craft Launcher 2.csproj`（`AssemblyTitle` / `Product`）、`metadata.json`（`name`）、`PCL.Core/Utils/OS/AumidHelper.cs`（任务栏 `DisplayName`）；**`AssemblyName` 仍未改**（见 §8.1） |
| 登录区改「正版 / 离线 / 外置」三选项卡 | `PageLaunchLeft.xaml(.cs)`；外置页新增「验证服务器」下拉框 `ComboAuthServer`（默认 LittleSkin，可新建并持久化到 `LaunchAuthServerList`） |
| 开始界面「PCL Swordsman 提示」卡片 + 屏蔽口令 | `PageLaunchRight.xaml.cs`（口令为 **`Plain Craft Launcher 2 Swordsman`**，也接受不带「2」的 `Plain Craft Launcher Swordsman`；忽略大小写与所有空白，对应 `Launch.Right.CommunityHint.*`） |
| 升级后「版本说明」弹窗文案 | `Update.CommunityNotice.*`（zh-CN/zh-TW/en-US/en-GB 四份，注意同步） |
| 「设置 → 软件信息」下游项目声明卡片 | `PageSetupAbout.xaml` 顶部（`Setup.About.Downstream.*`） |
| 「反馈」侧边栏图标**沿用上游**（`SvgIcon="lucide/message-circle"`，`LogoScale=0.9`）；禁用状态改由页面内占位页表达 | `PageSetupLeft.xaml`（`ItemFeedback`）、`PageSetupFeedback.xaml(.cs)` |
| **整页「不可用」占位控件**（淡红上箭头 + 深红叉，1.2s 循环呼吸动画：缩放 1→1.07、叉透明度 0.9→0.45） | `Controls/MyUnavailablePlaceholder.xaml(.cs)`；被 `PageSetupFeedback`（反馈声明）与 `PageSetupUpdate`（"此版本不支持更新"）复用 |
| 反馈页禁用状态：隐藏提示条/提交卡片/状态列表/加载框，只留占位（`PanBlocked`） | `PageSetupFeedback.xaml` + `.cs` 的 `PageLoaded` 分支 |
| 更新页禁用状态：隐藏选项卡/更新卡/检查卡/其他选项/黄色提示，只留占位（`PanUnsupported`） | `PageSetupUpdate.xaml` + `.cs` 的 `ShowUnsupportedUI()` |
| 「回声洞」打字动画 | `Plain Craft Launcher 2/Modules/ModEchoCave.cs` + `PageToolsTest.xaml(.cs)`（语录列表在 `ModEchoCave.Quotes`） |
| 素材 | `Images/swordsman.png`（SGU 标识）、`Images/diamond_sword.png`（官方钻石剑 16×16 放大 9 倍，**未旋转**） |

### 5.5 新增配置项（都在 `PCL.Core/App/Config.cs`）

| 配置键 | 类型/默认 | 用途 |
| --- | --- | --- |
| `LaunchAuthServerList` | string / `""` | 外置登录验证服务器历史列表（`\|` 分隔） |
| `LinkTerraCottaEnabled` | bool / `true` | 陶瓦联机总开关 |
| `LinkTerraCottaRelayMode` | int / `1` | 0=仅 P2P，1=允许 EasyTier 公共中继 |
| `UiCustomColor` | string / `#39C5BB` | 自定义主题色（`#RRGGBB`） |
| `LaunchArgumentRam` | bool / `false` | 启动游戏前进行内存优化（`VersionRamOptimize = 0` 时的全局默认） |
| `VersionRamOptimize` | int / `0` | 版本独立设置：0 跟随全局、1 开启、2 关闭 |
| `LinkTerraCottaDisclaimerAccepted` | bool / `false` | 陶瓦联机免责声明是否已被同意（拒绝则把总开关退回关闭） |
| `UiMaterialKind` | int / `1` | 高级材质效果：0=关闭、1=云母（Win11 22H2+）、2=Aero 模糊（`Config.System` 组） |
| `UiMaterialBlurRadius` | int / `18` | 材质模糊半径 0~40 |
| `UiMaterialTintStrength` | int / `55` | 材质着色强度 0~100（55 = 0.55） |
| `UiMaterialTitleOpacity` | int / `88` | **标题栏**背景不透明度 30~100（88 = 0.88） |
| `UiMaterialMainOpacity` | int / `92` | **主界面**背景不透明度 30~100（92 = 0.92） |
| `UiMaterialSidebarOpacity` | int / `100` | **侧边栏**背景不透明度 30~100 —— 系数语义：在主题原始浓度上再乘，100 = 保持现状 |
| `UiMaterialCardOpacity` | int / `100` | **卡片**背景不透明度 30~100 —— 同样是系数语义，100 = 保持现状 |
| `UiMaterialGlassOpacity` | int / `45` | **液态玻璃**：整块玻璃的着色不透明度 30~100（绝对 alpha） |
| `UiMaterialSidebarItemOpacity` | int / `55` | 液态玻璃：侧边栏每块小玻璃不透明度 30~100 |
| `UiMaterialDividerOpacity` | int / `55` | 液态玻璃：侧边栏与主界面之间玻璃竖线亮度 0~100（0 = 不画） |
| `UiMaterialGlassRefraction` | int / `12` | 液态玻璃：边缘折射强度 0~30 像素（0 = 只有磨砂） |
| `UiMaterialGlowRadius` | int / `8` | 悬停发光半径 0~20（0 = 关闭发光） |
| `UiMaterialGlowOpacity` | int / `35` | 悬停发光透明度 0~100（35 = 0.35） |

### 5.6 版本号显示相关

- `PCL.Core/App/Metadata.cs`：`LauncherVersionModel` 增加 `DisplayName`（形如 `27.1-snapshot-18(2.15.1)`）与
  `DownstreamVersionName`；`BaseName` 保持纯下游号（`27.1-snapshot-23`）
- `Basics.DisplayVersionName` / `ModBase.versionDisplayName` 供界面与日志使用；
  `versionBaseName`（User-Agent 等要求合法 token 的场合）**不能带括号**
- `PageSetupUpdate.VersionNameFormat()` 遇到带括号的完整版本号会原样返回（历史上曾把 `2.12.1` 显示成 `2 12 1`）
- `PCL.Core/App/Basics.cs`、`ModLaunch`、`ModMain`、`CrashReportExporter`、`FormMain`、`PageSetupAbout` 显示版本处均已改用 DisplayName

### 5.7 插件系统（M1 已落地，详见 §11）

新增两个工程 `PCL.PluginSdk` / `PCL.PluginHost`、宿主接线 `Modules/Plugins/PluginHostBootstrap.cs`、
工具侧边栏「插件」分类、完整插件管理页 `Pages/PageTools/PageToolsPluginManage.xaml(.cs)` 与列表项
`Controls/MyPluginItem.xaml(.cs)`、`.pclpkg` 安装器 `PCL.PluginHost/PluginInstaller.cs`、示例插件 `samples/HelloPlugin/`。
**完整设计、接口签名、里程碑与数据文件约定见 §11 与 `docs/PLUGIN-SYSTEM.md`。**

### 5.8 高级材质与液态玻璃（snapshot-30 后端 / 31~34 接线 / 35~39 修窗口外形与平台限制）

| 层 | 位置 | 说明 |
| --- | --- | --- |
| 互操作 | `PCL.Core/Utils/OS/Window/DwmInterop.cs` | `DwmSetWindowAttribute(hwnd, 38, 云母/亚克力)`、属性 20 沉浸式深色标题栏；`IsBackdropSupported` = build ≥ 22621；全部 `try/catch` 兜住 `DllNotFound` |
| 后端 | `PCL.Core/Utils/OS/Window/MaterialService.cs` | `Apply(hwnd, kind, options)` 返回**实际生效**效果；降级链 云母 →（不支持）Aero →（失败）纯色；Aero 走 `SetWindowCompositionAttribute` + `ACCENT_ENABLE_BLURBEHIND`，alpha 由「模糊半径 + 着色强度」合成（上限 0x75） |
| 宿主 | `Plain Craft Launcher 2/Modules/UI/Material/ModMaterial.cs` | 读配置 → `MaterialService.Apply` → **四层表面的 alpha**（标题栏 `PanTitle.BackgroundBrush`、主界面 `PanForm.Background`、侧边栏 `RectLeftBackground.BackgroundBrush`、卡片 = 资源 `ColorBrushTransparentBackground`）→ 通知悬停发光。材质没生效时四层全部恢复原状 |
| 发光 | `Plain Craft Launcher 2/Modules/UI/Material/MaterialGlow.cs` | 全局 `Loaded` 类处理器挂钩交互控件，`MouseEnter` 挂 `DropShadowEffect(ShadowDepth=0)`、`MouseLeave` 清掉；颜色取主题 `ColorBrush3` |
| 设置 | `Plain Craft Launcher 2/Pages/PageSetup/PageSetupUI.xaml(.cs)` | 个性化页 `CardMaterial`：效果下拉 + 状态行 + 6 个滑条；卡片按 `materials.advanced` 显隐 |
| 插件 | `plugins/AdvancedMaterials/` | 只声明 `materials.advanced` + 打日志，无渲染代码 |

设计约束（用户 2026-10-29 定）：**效果不做标题栏 / 主界面的区分开关**（要开就两处一起开），
但**不透明度等参数分别可调**。液态玻璃（⑥c）明确「先不做」，别在本轮动它。

> 不透明度的两种语义别搞混：**标题栏 / 主界面**的基础画刷不透明，百分比就是绝对 alpha（默认 88 / 92）；
> **侧边栏 / 卡片**用的是主题的半透明画刷 `ColorBrushTransparentBackground`，百分比是**在主题浓度上再乘的系数**（默认 100 = 现状）。
> 卡片的实现是「替换应用资源字典里的那一项」，所以基准画刷要从资源里取、且要判断「当前项是不是自己写进去的那一份」，否则层层衰减。

**液态玻璃（`MaterialKind.LiquidGlass = 3`）** —— 新文件 `Modules/UI/Material/ModLiquidGlass.cs`
+ `PCL.Core/UI/Controls/RefractionBorder.cs`：

| 要点 | 说明 |
| --- | --- |
| 整块玻璃 | `PanForm` 背景 = 主题色 × `UiMaterialGlassOpacity`；标题栏画刷 alpha 归零；`RectForm` 圆角 6 → 16 |
| 侧边栏合并 | `RectLeftBackground` alpha 归零，侧边栏与主界面共用一层玻璃；分割靠 `RectLeftShadow` 改成 1.5px 玻璃竖线（`UiMaterialDividerOpacity`） |
| 小玻璃 | 卡片：圆角 11 + 磨砂抬高 + hairline；侧边栏项：只处理 `PanMainLeft` 子树内的 `MyListItem`，加圆角 8 的着色玻璃砖 |
| 磨砂 | 有壁纸时给 `ImgBack` 上 `BlurEffect`（半径 = `UiMaterialBlurRadius` × 1.4，12~60），退出还原；视频背景不磨砂 |
| 边缘折射 | `RefractionBorder` 复用 `BackgroundPresenter` 采集背面，按「以中心放大一点点」重采样后只画在圆角环带里 = 透镜压缩；`UiMaterialGlassRefraction = 0` 关闭 |
| 降级链 | Aero 后台模糊（系统着色只留 1/5）→ 亚克力 `TransientWindow` → 纯色（状态行写原因） |

> **窗口外形对齐（snapshot-35 起，必读）**：云母 / Aero 的后台模糊按**整个窗口矩形**绘制，
> 而界面本体内缩 10px + 圆角裁剪，材质一开就会露出「比界面大一圈、四角是直角」的磨砂框。
> 现在材质生效时 `ModMaterial.ApplyWindowShape()` 会把 `PanBack.Margin` 收成 0，并给窗口做圆角：
> Win11 用 `DwmSetWindowAttribute(33, DWMWCP_ROUND)`（有抗锯齿），Win10 退回 `SetWindowRgn`；
> 窗口缩放 / 状态变化时重下发，退出材质时完整还原。
> **液态玻璃的折射环带必须放在内容之下**（只压住壁纸层，`Source = ImgBack`），放上面会把标题栏与卡片再画一遍 → 边缘重叠。

> 为什么不用真位移着色器：WPF 只吃 `fxc` 预编译的 `ps_2_0` 字节码（仓库里只有 `AdaptiveBlur.ps` 一个），
> Termux 上没法编译新的 `.ps`。所以折射用「环带重采样」近似，效果方向一致（边缘压缩/位移），
> 但没有真正的像素级位移贴图。要做到和参考库（SVG 位移贴图 / AGSL shader）完全一致，需要 Windows 上有 fxc 再补一个 `.ps`。

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
| `Tools.Left.PluginCategory` / `Tools.Left.PluginManage` / `Tools.Left.PluginMarket` | 工具侧边栏「插件」分类与两项 | zh-CN/zh-TW/en-US/en-GB |
| `Tools.Plugin.*`（约 60 键：筛选/排序/状态/安装/删除/空状态） | 插件管理页与市场页 | zh-CN/en-US（zh-TW/en-GB 部分缺，回退 zh-CN） |

当前键数（2026-10-29 实测，8 个文件**完全对齐**、无重复 `x:Key`）：**各 3302 键**
（`zh-CN` / `zh_sgu` / `zh-TW` / `en-US` / `en-GB` / `ja-JP` / `fr-FR` / `es-ES`）。

> 2026-10-29 那一轮把缺口全部补齐了：`en-US` +1、`en-GB` +57、`zh-TW` +56、`ja-JP` +3252、`fr-FR` +3020、
> `es-ES` +3266，并新增语言「SGU 式中文」`zh_sgu`（= zh-CN 全量副本，排在 zh-CN 之后）。
> ⚠️ **`ja-JP` / `fr-FR` / `es-ES` 里补进去的是简体中文原文**（上游这几份本来就是不完整翻译），真翻译仍待做；
> 本轮新增的 `Setup.Ui.Material.*`（20 键）也是如此：zh-CN / zh_sgu 中文、zh-TW 繁体、en-US / en-GB 英文。

---

## 7. 已知问题 / 待办

1. **TerraCotta 联机未经实机验证**：代码链路（本地生成房间号 → EasyTier 网络 → 端口转发 / LAN 广播模拟）已打通并编译通过，
   但 P2P 连通性、EasyTier 首次下载、房主/房客实际进游戏均**未在 Windows 上实测**。接手后请优先验证：
   房主扫描到局域网世界 → 建房拿到房间号 → 房客加入 → 房客"多人游戏"列表出现房主世界。
   若 EasyTier 官方公共节点不可用，可在设置里选「仅 P2P」或自行替换 `EasyTierEntity._fallbackNodeLinks`。
2. **界面观感未验证**：所有 WPF 改动只能在 Windows 实机确认（输入框高度、按钮宽度、标题栏装饰、占位页动画、插件页布局等）。
3. **文案与实现对不上的风险**：`Update.CommunityNotice.*` 与开始界面提示里写明"砍掉了大厅、纯 P2P"，
   与当前实现（TerraCotta 纯 P2P）一致；若后续恢复任何依赖服务器的联机功能，记得同步改文案。
4. **`code` 递增会触发升级弹窗**：每次提升 `version.code`，首次启动会走一次升级流程，弹出「PCL CE For SGU 版本说明」，
   属预期行为（弹窗内容见 `docs/FIRST-RUN-DIALOGS.md`）。
5. **回声洞语录目前只有 3 条测试文案**，位于 `ModEchoCave.Quotes`，可随时增删。
6. **剑客黄配色为人工调校**：`_CalculateColorsGoldenDusk` 的 ramp 表可直接微调；黄色在亮色模式下对比度有限，改深了会显得脏。
7. **主页预设「PCL CE 公告栏」仍指向 PCL 服务器（待决策，未改）**：`PageLaunchRight.xaml.cs` 的
   `case 14` 会从 `https://s3.pysio.online/pcl2-ce/apiv2/pages/announce.xaml` 拉取主页 XAML，
   只在用户**主动**在下拉框里选了该预设时才联网，与已禁用的「云端公告 / 云控」（`AnnouncementService`）不是同一路径。
   严格按「不连任何 PCL 服务器」的口径，这个预设应当删除或改指向本地资源；本轮（snapshot-8）未改动代码，仅在文档中登记。
8. **EasyTier 内核下载镜像仍走 PCL 静态资源**：`ModLink.cs` 里的 EasyTier 压缩包地址为
   `staticassets.naids.com` / `s3.pysio.online`（与上游一致）。这是**下载内核**用的静态资源，
   不涉及大厅下发或中继；若日后要完全去 PCL 化，需要另找镜像。
9. **HTTPS 报证书链错误时先查系统时间**：证书链校验失败的第一嫌疑是**系统日期/时区**（测试愚人节彩蛋常把日期改成 4 月 1 日），
   其次才是代理/抓包工具的中间人证书。**不要**因此去改网络栈或关闭证书校验（本项目未改动任何证书校验策略）。
10. **启动闪退 / 报 `Failure extracting contents of the application bundle`（解包缓存损坏，实测踩到）**：
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
10. **版本号尚未提升（发版前必做）**：本轮所有改动仍在 `snapshot-8` / `code 531` 下。正式发布应改为
    `snapshot-9` / `code 532`，并同步更新 `DOWNSTREAM-CHANGES.md`（新增对应小节）、本文件 §2 状态表、
    `build.sh` 与 `tools/build.ps1` 注释里的示例版本号。
11. **插件市场页仍是空壳**：`PageToolsPluginMarket` 只有占位文案，静态索引/签名校验/下载安装属 M5（见 §11）。
12. **插件删除是「立即隐藏 + 下次启动彻底删」**：删完目录可能还在磁盘上，这是预期行为，不是 bug（见 §8.13）。
13. **`HMCL-main/` 不在交接包内**：如需参考（例如查 GPL 项目的实现思路）请自行克隆，且**只许阅读，不许拷贝**。

---

## 8. 重要约定（避免踩坑）

1. **不要改程序集名（`AssemblyName`）**：`FormMain.xaml` 等处使用
   `pack://application:,,,/Plain Craft Launcher 2;component/...` 资源 URI，改名会导致图标等资源加载失败。
   产物重命名只在构建后做（`build.sh` / `tools\build.ps1` 已实现）。
2. **MyTextBox 最小高度 28**：控件模板内含 `MinHeight=28` 的行，设成 26 会裁掉下边缘（已修过一次）。
3. **`MyCard.CanSwap` / `IsSwapped`**：`IsSwapped=true` 表示默认收起；需要"可折叠但默认展开"就只加 `CanSwap`。
4. **`MyCheckBox.Change` 的签名是 `(object sender, bool user)`**，不是 `RouteEventArgs`。
5. **`MyTextBox` 的 `ValidatedTextChanged` 是 `RoutedEventHandler`**；校验用 `Validate()` + `ValidateRules`。
6. **`AnimatedBackgroundGrid.BackgroundBrush` 只接受 `SolidColorBrush`**，要渐变请另加叠加层（参考 `RectTitleGolden`）。
7. **HMCL 参考源码**（若本地有 `HMCL-main/`，20 MB，GPLv3）：**仅作阅读参考，严禁拷贝其代码/资源进本仓库**
   （许可不兼容）。发版/打包时不要把它带进产物或提交。
8. **上游素材与法务**：`diamond_sword.png` 由原版 Minecraft 纹理放大而来，仅作装饰用途；
   Minecraft 与 Mojang/Microsoft 无隶属关系（「软件信息 → 上游版权与法律信息」已有声明，保留即可）。
9. **`MyButton.Click` 也是自定义签名 `(object sender, MouseButtonEventArgs e)`**（`MyButton.xaml.cs` 里
   自定义的 `ClickEventHandler`），**不是** `RoutedEventArgs`。写错不会编译报错（XAML 事件是运行时按名绑定），
   而是在**打开页面时**抛 `XamlParseException：无法从文本"Xxx_Click"创建"Click"`（已踩过一次）。
   同理：`MyCheckBox.Change` 是 `(object, bool)`、`MyIconTextButton.Click` 是 `(object, ModBase.RouteEventArgs)`、
   `MyRadioButton.Check` 是 `(MyRadioButton, bool)`、`MyIconButton.Click` 是 `(object, EventArgs)`。
10. **插件宿主的事件在后台线程触发**：`PluginHostManager.Changed` 可能来自任意线程，
    订阅方必须把**所有 UI 访问**（包括 `IsLoaded` 这种 DependencyObject 属性）放进 `ModBase.RunInUi`。
    在后台线程读 `IsLoaded` 会抛 `InvalidOperationException：调用线程无法访问此对象`（已踩过一次）。
11. **新建 C# 工程时 `System.IO` 不在隐式 using 里**（`UseWPF` 工程）：`Path`/`File`/`Directory` 需显式
    `using System.IO;`，否则 CS0103。
12. **不要用 Windows PowerShell 5.1 的 `Get-Content` / `Set-Content` 批量改带中文的源文件**：
    PS 5.1 会把无 BOM 的 UTF-8 按 GBK 读取，回写后中文注释全部乱码、还可能吞掉换行（已踩过一次，整文件重写才修好）。
    批量改文件请用编辑器/专用脚本，或显式 `-Encoding UTF8` 且确认源文件是 BOM 编码。
    同理，**校验语言 XAML 是否可解析时必须按 UTF-8 读取**，否则会误报语法错误。
13. **插件目录不能在插件加载期间直接删除**：`AssemblyLoadContext.Unload()` 只是请求卸载，
    文件句柄要等 GC 真正回收才释放（实测运行期 `Directory.Delete` 必失败）。
    现在的语义是「**立即隐藏 + 下次启动彻底删**」：禁用 → 目录名写进 `PCL/PluginData/pending-delete.json`
    → `PluginLoader.ReloadAll()` 跳过这些目录（列表里立刻不显示）→ 后台再尽力删一次 → 下次启动
    `ProcessPendingDeletes()` 真正删除并清空清单。
    该逻辑已封装在 `PluginInstaller` / `PluginLoader`，**不要绕过它直接删目录**。
14. **`FormMain.PageChange(PageType.Tools, subType)` 不会切换工具箱子页**：
    `PageChangeActual` 的 `case PageType.Tools` 会把 `subType` 强制覆盖成
    `ModMain.frmToolsLeft.pageID`（保持当前子页）。要在「工具」标签页内跳子页，必须走左侧页面，
    例如：`ModMain.frmToolsLeft.ItemPluginMarket.SetChecked(true, false, false);`
    再 `ModMain.frmToolsLeft.PageChange(PageSubType.ToolsPluginMarket);`（与用户点击等价）。
    `Download` / `Setup` 分支有专门的子页遍历逻辑，不受此影响。
15. **插件管理页的列表刷新必须可重入安全**：`RefreshListCore()` 里会重建筛选单选按钮并触发
    `Filter_Check`，用 `_isRendering` 守卫，渲染期间的勾选事件直接 return，否则会死循环或把用户的选择改掉。
16. **筛选计数与自动顺延规则（照抄模组页）**：`全部` 永远显示；`已启用`/`已禁用`/`出错` 计数为 0 时隐藏；
    当当前选中筛选归零时自动顺延到还有内容的那个（优先级 已启用 > 已禁用 > 出错 > 全部）。
17. **零插件时隐藏管理条**：没有发现任何插件目录时，隐藏搜索框/管理条/列表容器，只保留空状态卡片
    （卡片里仅有「从文件安装」「下载新插件」「打开文件夹」「重新扫描」四个按钮）。
18. **单文件产物里的 `metadata.json` 是明文**：可直接 `grep -a` 校验版本字段，不需要反编译。
19. **不要手改 `PCL/PluginData/plugins.state.json`**：启停状态请走 UI / `PluginStateStore`，手改容易被下次写入覆盖。

---

## 9. 交接清单（接手后建议按顺序做）

- [ ] 读本文档（§0 → §1 → §2 → §3 → §4），再按需查 §5 / §11
- [ ] 在 Windows 机器上解压源码，`dotnet --version` 确认 SDK 10
- [ ] 跑通 `tools\build.ps1`（注意 BOM），确认产物名 `PCLCE For SGU 27.1-snapshot-8(2.15.1).exe`
      （Linux/Termux 上改用 `./build.sh x64 Release`）
- [ ] 启动产物，走一遍：首次启动弹窗 → 设置里反馈页/更新页是占位 → 主题只剩蓝色系 → 工具栏看到「插件」分类
- [ ] 确认软件信息页的下游项目声明文案正确
- [ ] 按 `samples/README.md` 安装示例插件，玩一遍：启用 → 禁用 → 删除 → 重启确认目录消失
- [ ] 登录三选项卡（正版/离线/外置，含验证服务器下拉框）逐个走通，启动一次游戏
- [ ] 验证 TerraCotta：两台机器（或本机+朋友）走一遍房主/房客流程（见 §7.1）
- [ ] 确认反馈页/更新页/软件信息页文案与禁用状态符合预期
- [ ] 拿到 CI secrets（可选）：微软登录、CurseForge、Sentry、Naid、联机根服务器；不注入也能正常构建
- [ ] 与剑客群组服服主团队确认发版通道与对外口径（对外只发布 SGU 内部分享版本）

---

## 10. 联系人

- 项目/内部渠道：剑客群组服服主团队
- 问题反馈邮箱：`hmjmfabc@swordsman.top`、`mingyugames@swordsman.top`
- 上游：<https://github.com/PCL-Community/PCL-CE>（**仅作代码参考，不要向其提交本作问题**）
- 原作：<https://github.com/Meloong-Git/PCL>（作者 龙腾猫跃）

---

## 11. 插件系统（当前进度与约定）

完整设计（接口签名、`.pclpkg` 格式、生命周期状态机、权限模型、市场索引、风险与上游同步成本）见
[`docs/PLUGIN-SYSTEM.md`](PLUGIN-SYSTEM.md)。本节只记「现在做到哪、下一步动哪里」。

### 11.1 里程碑

| 里程碑 | 内容 | 状态 |
| --- | --- | --- |
| M0 | 工具侧边栏「插件」分类 + 插件管理 / 插件市场两个页面骨架 + 路由 | ✅ 完成 |
| M1 | SDK 契约 + 加载器（ALC / manifest / 状态 / 配置 / 日志 / 事件）+ 示例插件 + 完整管理界面 | ✅ 完成 |
| M2 | 页面 / 侧边栏扩展点（插件注册自己的页面）、动态页路由、`IPageRegistry` / `ISidebarRegistry` 落地 | ⬜ 未开始 |
| M3 | 事件总线 + Minecraft 只读服务（`IMinecraftService`） | ⬜ 未开始 |
| M4 | 权限模型（`PluginCapability` 落地校验 + 用户授权 UI） | ⬜ 未开始 |
| M5 | 插件市场（静态索引 / 签名校验 / 下载安装） | ⬜ 市场页目前是占位 |

### 11.2 数据与目录约定

- 插件本体：`<启动器目录>/PCL/Plugins/Foo.pclpkg`（**一个插件一个包文件**，zip，根目录含 `plugin.json`）
- 文件名由清单 `id` 决定：`InstallPackage()` 装成 `<id>.pclpkg`，**不沿用源文件名**；同一个 `id` 在目录里只会有一份
  （snapshot-23 修复：此前按源文件名落盘，同一个插件会出现 `Foo.pclpkg` + `PCL.Foo.pclpkg` + `foo-1.0.0.pclpkg` 多份，
  禁用只改一份 → 「禁用后还在、数量不变」）
- 启动自愈：`PluginHostBootstrap.DeduplicatePackages()` 每次启动把同 `id` 的多份包合成一份（**禁用优先**：有一份
  是 `.disabled` 就按禁用保留，文件名规范成 `<id>.pclpkg` / `<id>.pclpkg.disabled`，其余删除）；`PackageAlreadyInstalled()` 也按 `id` 判断
- 禁用形态：`Foo.pclpkg.disabled`（就是改名，**文件名是启用状态的唯一真相**）
- 解包缓存：`PCL/PluginData/cache/<包名>_<长度>_<时间戳>/`，程序集从这里加载，所以包文件始终不被占用
- 插件私有数据：`PCL/PluginData/<id>/`（`config.json` + 插件自建文件；卸载**不删**）
- 卸载队列：`PCL/PluginData/pending-delete.json`（文件被占用时登记，下次启动先删）
- 老格式兼容：`PCL/PluginData/plugins.state.json` 只在迁移历史目录时读一次；老目录 `PCL/Plugins/<id>/` 会在启动时被打包成 `.pclpkg`
- 契约程序集**不能**由插件自带（必须回落默认 ALC），否则 `InvalidCastException`
- 宿主桥：`Modules/Plugins/PluginHostBootstrap.cs`，在 `FormMain` 完成 `HardwareInfo` 初始化后后台启动
- 调试钩子：环境变量 `PCL_FORCE_PLUGIN_PAGE=1` 启动即进入「插件管理」页（用于无人值守验证 UI）

### 11.3 管理页行为约定

- 布局与交互**完整套用「实例设置 → 模组」页**：搜索框、管理条（重新扫描 / 打开文件夹 / 从文件安装 / 下载新插件 / 全选）、
  筛选单选（全部 / 已启用 / 已禁用 / 出错）、排序、列表、底部多选栏（启用 / 禁用 / 删除 / 取消）、空状态卡片
- 列表项 `Controls/MyPluginItem.xaml`：44px 行高、左侧 6px 色条、`lucide/puzzle` 图标、右侧电源/文件夹按钮、多选高亮
- 启用 / 禁用 = 包文件改名，随后 `PluginHostBootstrap.Reload()`；单个失败要弹出具体文件名与原因，**不能吞掉**
- 删除 = 直接从 `PCL/Plugins/` 删包文件（snapshot-22 起不再有「立即隐藏 + 下次启动删」的中间态；只有文件真被占用才走 `pending-delete.json`）
- 「下载新插件」跳转必须走 `ModMain.frmToolsLeft.PageChange(...)`（§8.14）

### 11.4 怎么写一个插件

见 [`docs/PLUGIN-GUIDE.md`](docs/PLUGIN-GUIDE.md)（手把手）与 [`docs/PLUGIN-API.md`](docs/PLUGIN-API.md)（接口签名、能力、功能键、路线）。
最简路径：抄 `samples/HelloPlugin`（id `sgu.sample.hello`）→ `dotnet build ... -c Release -p:Platform=x64`
→ 把 `plugin.json` + `HelloPlugin.dll` 打成 zip 改名 `.pclpkg`（或直接跑 `plugins/package.ps1`）
→ 拖进启动器窗口即可安装。配置会持久化到 `PCL/PluginData/<id>/config.json`。

### 11.5 M2 要动的地方（提示）

- `PCL.PluginSdk`：新增 `IPageRegistry` / `ISidebarRegistry` / 页面描述 DTO（沿用 `docs/PLUGIN-SYSTEM.md` 的接口定义）
- `PCL.PluginHost`：注册表收集 + 冲突检测（保留字路由、重复标题）
- `Plain Craft Launcher 2`：`FormMain.PageSubType` 动态扩展、`frmToolsLeft` 支持注入条目、页面容器按描述创建
- 上游同步成本：与 §4.2 的三方比对配合，新增工程与接线点都是「下游单侧改动」，冲突概率低

---

## 12. 关键文件速查

> 插件相关的架构、功能键、三个插件与分发策略，见 **§13 功能插件化**（snapshot-19/20 起）。

```
Plain Craft Launcher 2/metadata.json              ← 版本号唯一来源
Plain Craft Launcher 2/FormMain.xaml(.cs)         ← 标题栏徽标、遥测弹窗、插件宿主启动
Plain Craft Launcher 2/Modules/Plugins/PluginHostBootstrap.cs   ← 插件宿主接线
Plain Craft Launcher 2/Modules/UI/Material/ModMaterial.cs          ← 高级材质宿主接线（材质 / 不透明度）
Plain Craft Launcher 2/Modules/UI/Material/MaterialGlow.cs         ← 悬停发光
PCL.Core/Utils/OS/Window/MaterialService.cs                        ← 云母 / Aero / 液态玻璃后端与降级
Plain Craft Launcher 2/Modules/UI/Material/ModLiquidGlass.cs       ← 液态玻璃（大玻璃 / 竖线 / 小玻璃 / 磨砂）
PCL.Core/UI/Controls/RefractionBorder.cs                           ← 液态玻璃的边缘折射环带
PCL.Core/Utils/OS/Window/DwmInterop.cs                             ← DwmSetWindowAttribute 封装
plugins/AdvancedMaterials/                                         ← 高级材质插件（只声明 materials.advanced）
Plain Craft Launcher 2/Controls/MyUnavailablePlaceholder.xaml(.cs)  ← 占位页控件
Plain Craft Launcher 2/Controls/MyPluginItem.xaml(.cs)              ← 插件列表项
Plain Craft Launcher 2/Pages/PageTools/PageToolsLeft.xaml(.cs)      ← 工具侧边栏（插件分类）
Plain Craft Launcher 2/Pages/PageTools/PageToolsPluginManage.xaml(.cs)
Plain Craft Launcher 2/Pages/PageTools/PageToolsPluginMarket.xaml(.cs)  ← 占位，M5
Plain Craft Launcher 2/Pages/PageSetup/PageSetupFeedback.xaml(.cs)  ← 反馈占位页
Plain Craft Launcher 2/Pages/PageSetup/PageSetupUpdate.xaml(.cs)    ← 更新占位页
Plain Craft Launcher 2/Modules/Updates/UpdateManager.cs             ← 更新总闸
Plain Craft Launcher 2/Modules/Base/ModBase.cs                      ← 反馈总闸
PCL.Core/App/Essentials/TelemetryService.cs         ← 遥测总闸
PCL.Core/UI/Theme/ThemeService.cs                   ← 主题与发行版门禁
PCL.PluginSdk/                                      ← 插件契约
PCL.PluginHost/                                     ← 插件加载器 / 安装器
samples/HelloPlugin/                                ← 示例插件
tools/build.ps1                                     ← Windows 构建（注意 BOM）
docs/PLUGIN-SYSTEM.md                               ← 插件系统完整设计
```


---

## 13. 功能插件化（snapshot-19 / 20 起，必读）

### 13.1 一句话

被"屏蔽"的功能**不再由构建类型硬编码**，而是由**插件是否提供**决定：宿主保留全部实现，
插件只声明功能键（`provides`）并提供数据（主题名称/颜色、回声洞语录）。

### 13.2 组成

| 层 | 位置 | 职责 |
| --- | --- | --- |
| 契约 | `PCL.PluginSdk/` | `IPlugin`、`PluginCapability`、**`PluginFeatureKeys`**、`IExtraThemesProvider`、`IEchoCaveQuotesProvider`、清单 `PluginManifest.Provides` |
| 宿主 | `PCL.PluginHost/` | 发现/校验/加载/生命周期；`PluginHostManager.ProvidedFeatures`、`GetProviders<T>()` |
| 接线 | `Plain Craft Launcher 2/Modules/Plugins/PluginHostBootstrap.cs` | 启动时安装内置插件 → `Initialize()` → `ApplyProvidedFeatures()` 把功能键写进 `FeatureFlags`、把主题/语录交给核心 |
| 开关 | `PCL.Core/App/FeatureFlags.cs` | 运行期功能表，`Keys` 与 SDK 的键一一对应 |
| 消费 | `ThemeService` / `ModEchoCave` / `ProfileUi` / `PageDownloadClient` | 按 `FeatureFlags.IsEnabled(...)` 决定是否展示被屏蔽功能 |

### 13.3 七个功能键

| 键 | 由谁提供 | 宿主里的作用点 |
| --- | --- | --- |
| `themes.extended` | SGU 插件包 | `ThemeService.IsExtendedThemesEnabled`（非蓝色主题 + 自定义主题色选择器）；主题主色/显示名优先取插件定义 |
| `echo-cave` | SGU 插件包 | `ModEchoCave.SetQuotes()` 接插件语录；百宝箱回声洞卡片按开关显示 |
| `unlock.region` | PCLS 核心插件 | `PageDownloadClient` 的整页屏蔽（红色镐子占位 + 法规提示文案） |
| `unlock.offline-auth` | PCLS 核心插件 | `ProfileUi.CanCreateOtherProfile()`：默认要求先有正版档案，插件解除 |
| `materials.advanced` | 高级材质 | `ModMaterial`（`Modules/UI/Material/`）：云母 / Aero 窗口材质、标题栏与主界面不透明度、悬停发光；个性化页的「高级材质（窗口）」卡片按此键显隐 |
| `welcome.messages` | 欢迎语 | `Modules/Plugins/ModWelcome.cs`：启动时等概率抽一条欢迎语，替换占位符后用右下角小提示显示（插件只提供池） |
| `themes.custom-color` | SGU 插件包 | 自定义主题色输入行（`ThemeService.IsCustomColorEnabled`）；与 `themes.extended` 已拆开，可只给主题不给自定义色 |

### 13.4 五个插件

| 插件 | id | 源码 | 说明 |
| --- | --- | --- | --- |
| SGU 插件包 | `sgu.pack` | `plugins/SguPack/` | 剑客黄（`#E8B33C`，`golden-dusk` 神圣黄昏配色）、初音绿 `#39C5BB`、烤音绿 `#33CCBB` + 回声洞 12 条语录。以后改主题/语录**只动这里** |
| PCLS 核心插件 | `pcls.core` | `plugins/PCLSCore/` | 提供 `unlock.region` / `unlock.offline-auth`；**永远内嵌**，**测试版与正式版都默认启用**（2026-10-29 起；原「正式版默认关闭」已取消） |
| 高级材质 | `sgu.advanced-materials` | `plugins/AdvancedMaterials/` | 提供 `materials.advanced`（云母 / Aero、不透明度、悬停发光）；渲染全在宿主 `ModMaterial` / `MaterialService`，插件只声明功能 |
| 欢迎语 | `sgu.welcome` | `plugins/Welcome/` | 提供 `welcome.messages`；池在 `PCL\PluginData\sgu.welcome\welcome.txt` 可自定义；占位符 `LAUNCHER_BOOT_COUNT` / `TIME`（上午/中午/下午/晚上） / `PLAYERNAME`（最后登录的档案） |
| Hello 示例 | `sgu.sample.hello` | `samples/HelloPlugin/` | 上游 M1 验收用示例，顺带验证插件系统本身 |

打包：`./plugins/package.sh [Release] [x64]`（Linux/Termux，python3 zipfile）或
`powershell -NoProfile -ExecutionPolicy Bypass -File plugins\package.ps1`（Windows，同样编译五个插件并打包）
→ `plugins/dist/*.pclpkg`。`.pclpkg` 内是 `plugin.json` + `<插件名>.dll`。

### 13.5 分发策略（政策落点）

`build.sh` 读取 `metadata.json` 的 `suffix`：

| 构建 | 插件处理 |
| --- | --- |
| **非正式版**（`suffix` 非空：snapshot / pre / rc / experiment / hotfix） | 四个 `.pclpkg` **嵌入 exe**（`-p:IncludeBundledPlugins=true`），同时复制一份到产物 `plugins\`；首次启动自动安装并启用 |
| **正式版**（`suffix` 为空） | exe 内**只嵌入三个无条件内嵌的包：`PCLSCore` / `AdvancedMaterials` / `Welcome`**，首次启动自动安装并**默认启用**；SGU 插件包 / Hello 不嵌入，`.pclpkg` 只放 `dist/` 与 `dist/plugins/`，由用户手动安装；exe 同级 `plugins\` 目录在正式版**不会**被自动安装 |

> 这就是"正式版看不到非蓝色主题"的实现方式：不装插件就没有这些功能。
> 注意：这比 snapshot-18 之前的"构建期硬门禁"弱——正式版用户**手动装 SGU 插件包后依然能用**。
> 这是 2026-10-25 用户明确要求的方向（插件化），已记录在 `docs/PLUGIN-SYSTEM.md` §8.4 修订说明里。

### 13.6 新增一个被屏蔽功能的标准流程

1. 在 `PCL.PluginSdk/PluginFeatureKeys.cs` 与 `PCL.Core/App/FeatureFlags.cs` 的 `Keys` 里**同时**加键（两处必须一致）
2. 宿主里在该管的地方判 `FeatureFlags.IsEnabled(FeatureFlags.Keys.<新键>)`
3. 让对应插件的 `plugin.json` 的 `provides` 里加上这个键
4. 文档：`DOWNSTREAM-CHANGES.md` 记改动，本文件 §13.3 表格加一行

### 13.7 排障清单（插件相关的"没生效"）

| 现象 | 先查什么 |
| --- | --- |
| 插件管理页看不到插件 | ①`PCL\Plugins\Foo.pclpkg`（或 `Foo.pclpkg.disabled`）在不在；②exe 所在目录能否写（放 Program Files 会失败）；③日志里 `[PluginHost] 扫描完成：…` 与 `内置插件安装…` 行 |
| 插件在但功能没生效 | ①文件名是否带 `.disabled`（**启用状态只看文件名**）；②`plugin.json` 的 `provides` 键拼写是否与 `PluginFeatureKeys` 完全一致；③日志 `[PluginHost] 已应用插件功能：…` 是否列出了该功能 |
| 改了主题/语录没变化 | 只改插件时记得 `plugins\package.ps1`（或 `./plugins/package.sh`）重新打包，再把新的 `.pclpkg` 拖进启动器覆盖；宿主里不应再有主题定义（已移到插件数据） |
| 内置插件更新不生效 | exe 里嵌的是打包时的版本；启动时「已存在同名包就跳过」，所以要换新版本：删掉 `PCL\Plugins\` 里的旧包再启动，或让新包换文件名 |
