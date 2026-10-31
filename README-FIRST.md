# 先读我（交接包导览）

> 这是一个 **PCL CE 的下游分支**：`PCLCE For SGU`。
> 你是接手维护的人。这份文件用 5 分钟告诉你去哪读、怎么跑、哪些地方不能碰。

---

## 0.0 公开仓库里**故意没有**的两样东西

本仓库是公开的（`Swordsman-Group-Uniform/PCL-Swordsman`），因此以下资源**不提交**，克隆后需要本地补齐：

| 缺的东西 | 补齐办法 |
| --- | --- |
| 原版（Minecraft）贴图：`Images/Blocks/NetherWartBlock.png`、`AmethystBlock.png`、`Images/Particles/cherry_*.png` | `python3 tools/extract-vanilla-textures.py <原版 client.jar>`（版权归 Mojang，仅本地使用、不要分发） |
| `plugins/FadedGray/Faded.jpg`（Faded灰 主题的标题栏装饰图，用户提供的素材） | 自己放一张同名图片到 `plugins/FadedGray/`；缺它时插件仍能编译，只是装饰会退化成「没有装饰」 |

其余上游自带资源、构建产物、日志、第三方参考仓库（`HMCL-main` 等）也都不入库；
构建产物请从 Releases 取，或用 `tools/build.ps1` / `./build.sh` 自行构建。

---

## 0. ⛔ 死规矩：遇到任何问题，先读日志，再动代码

- 日志：`<exe 同级>\PCL\Log\Launch-<日期>-<时分秒毫秒>.log`（应用内「设置 → 查看日志 → 导出日志」同款）
- 先看前 30 行的 `版本:` 与 **`系统版本:`**（Win10 / Win11 决定窗口材质能力），再按 `[模块]` 标签 grep
- **没有日志证据不许说「已修好」**；读不到日志就去解决「怎么读到」（`chmod 644` / 让用户贴内容），不要回去猜
- 反面教材：`snapshot-35 / 36 / 37` 三次靠猜修「磨砂框比界面大」，全无效；`snapshot-38` 读了日志 5 分钟定位
- 操作手册：**`docs/READ-THE-LOG.md`**
- 当前交接时点：**preview-73 / code 596**（2026-10-31 交接书：**`HANDOFF-2026-10-31-preview-73.md`**）

---

## 1. 三分钟了解这是什么

- 上游：**PCL CE**（<https://github.com/PCL-Community/PCL-CE>，C# / WPF / net10.0-windows）
- 本源：**PCLCE For SGU** —— 剑客群组服服主团队维护的下游分支，仅供 SGU 及相关服务器成员内部分享，**非官方发布**
- 当前版本：**`27.1-preview-73(2.15.1)`**（内部代号 `596`；预发布阶段，历史名 `pre` 已改为 `preview`），
  产物 `dist/PCLCE For SGU 27.1-preview-73(2.15.1).exe`（25,731,176 B，
  SHA-256 `E6ED8019FB61B5161A1D56DAE2613CD7C9C0A91AD876124460C0F0ABB8B58F5C`）；面向用户的更新日志见 [`CHANGELOG.md`](CHANGELOG.md)
- **交接书（当前唯一入口）：`HANDOFF-2026-10-31-preview-73.md`**（上一份 `HANDOFF-2026-10-31.md` 为 snapshot-71 时点）
- 真实代码基点：上游 `dev` @ `d1149d5`（2026-10-24，即 2.15.1 正式版之后的 dev 快照）；
  `metadata.json.upstream` 的 `2.15.1` **只是标注**，不追同步（见 `HANDOVER.md` §0.19 / §4.1）

**五条底线（改代码前务必知道）**

1. 界面上必须保留「下游项目声明」，不得让人误以为是官方版
2. **反馈功能已禁用**：任何文案都不得引导用户去 PCL Community / PCL2-CE 提 issue
3. 上游版权归 PCL Community 与龙腾猫跃，按仓库 `LICENSE`（Apache-2.0）分发，保留全部上游声明
4. **发行版不得保留非蓝色主题与自定义主题色**（主题色是原作者的主要赞助项目，代码里已有门禁）
5. 更新 / 自动更新 / 更新检测 / 云端公告已**在代码层禁用**，不要重新打开
6. 联机是 **EasyTier 纯 P2P**，不得引入依赖 PCL 中继 / 大厅服务器的实现
7. `HMCL-main/`（本包未包含）是 GPLv3 参考源码，**只许读、不许拷贝**
8. **正式版也内嵌并默认启用** `pcls.core` / `sgu.advanced-materials` / `sgu.welcome` 三个插件（2026-10-29 起）；
   主页那句「请尝试启用『PCLS 核心插件』」的提示**不要删**

---

## 2. 按顺序读这几份

| 顺序 | 文件 | 内容 | 什么时候读 |
| --- | --- | --- | --- |
| **0** | **`docs/READ-THE-LOG.md`** | **死规矩手册**：日志在哪、行格式、`[模块]` grep 配方、权限坑、自检清单 | **出任何问题前先读** |
| 1 | **`HANDOFF-2026-10-31-preview-73.md`** | **最新交接书**（preview-73 时点）：窗口材质重做（铺满窗口 / 磨砂等大 / 假阴影 / 尺寸对齐 / 重启生效）、新徽标与启动图、预览版协议、构建与验证清单、踩坑大全 | 现在就读 |
| 2 | `HANDOFF-2026-10-25.md` | **给下一位维护者的交接书**：现状、验证到哪、下一步、未完成清单、踩坑、待拍板事项 | 现在就读 |
| 3 | `HANDOVER.md` | 最全的常驻文档：项目全景、构建发版、上游同步、下游改动索引、插件系统、踩坑清单（按快照记录历史） | 查细节时读 |
| 4 | `DOWNSTREAM-CHANGES.md` | 逐版改动记录 | 想知道「为什么这么写」时查 |
| 5 | `VERSIONING.md` | 版本号规范（括号外 = 下游，括号内 = 上游） | 发版前必读 |
| 6 | `docs/DOWNLOAD-LIMIT.md` | 下载页关闭机制 + 「下载限制解除」插件原理 | 改下载相关代码前读 |
| 7 | `docs/PLUGIN-API.md` / `docs/PLUGIN-GUIDE.md` / `docs/PLUGIN-SYSTEM.md` | 插件接口 / 制作教程 / 系统设计（M0–M1 已实现，M2–M5 待做） | 写或改插件时读 |
| 8 | `samples/README.md` | 如何编译 / 打包 / 安装一个插件 | 验证插件系统时读 |
| 9 | `docs/FIRST-RUN-DIALOGS.md` | 首次启动各弹窗的文案与复现方法 | 改文案时读 |

## 3. 马上跑起来（Windows）

唯一推荐入口是仓库自带的 PowerShell 脚本（`build.sh` 需要 bash + python3，Windows 上通常没有）：

```powershell
# 需要 .NET SDK 10（开发时用的是 10.0.402）
powershell -ExecutionPolicy Bypass -File tools\build.ps1
# 产物：artifact-x64\PCLCE For SGU 27.1-snapshot-27(2.15.1).exe（并同步到 dist\）
```

> ⚠️ `tools/build.ps1` 与 `plugins/package.ps1` **必须保持 UTF-8 with BOM** 保存。Windows PowerShell 5.1
> 对无 BOM 的 UTF-8 会按 GBK 解码，中文注释可能吞掉换行导致 `param` 块解析失败（已实测踩过）。

构建脚本会先校验本地化 XAML 无重复键、再打插件包、最后 publish。
运行前需要 **.NET 10 桌面运行时**；首次启动会弹一系列对话框，`docs/FIRST-RUN-DIALOGS.md` 里有原文与重触发方法。

---

## 4. 哪些东西不要碰

| 不要动 | 原因 |
| --- | --- |
| `Plain Craft Launcher 2.csproj` 的 `AssemblyName` | `pack://application:,,,/Plain Craft Launcher 2;component/...` 资源 URI 依赖它，改名会让图标等资源加载失败 |
| `UpdateManager.IsUpdateSupported` / `ModBase.IsFeedbackSupported` / `TelemetryService.IsTelemetrySupported` | 这三个 `false` 是本分支的产品决定 |
| `ThemeService` 的发行版主题门禁 | 同上，涉及原作者赞助 |
| `DownloadPolicy` 的默认值（`AllowDownloads = false`） | 下载页关闭是本分支的产品决定；解除只能靠插件反射改写，不要加「检测到插件就放行」的分支 |
| 插件文件名的命名规则 | 必须保持 `<id>.pclpkg` / `<id>.pclpkg.disabled`（`PluginPackage` 统一生成）；手改成别的名字会出现同一个 id 多份副本，禁用/删除只作用到一份 |
| 语言文件里出现重复 `x:Key` | `ResourceDictionary` 加载失败后整个界面文字变空（构建已加门禁） |
| `HMCL-main/`（本包未包含） | GPLv3 参考源码，**只许读、不许拷贝** |
| `PCL-2.99.0.0/`（本包未包含） | 上游参考仓库副本，与构建无关；是否保留待拍板（见 `HANDOFF` §8.1） |

---

## 5. 现在的「未完成」列表（接手后先看这个）

详见 `HANDOFF-2026-10-25.md` §3 / §4，这里只列最要紧的：

1. **GUI 全部没实测**：所有界面改动只验证到「编译通过 + 探针通过」，必须人工点一遍（单实例互斥挡住了自动化）
2. **陶瓦联机（TerraCotta）从未在真实 Windows 上双机联调**
3. **插件系统只做到 M1**：M2 页面扩展点 / M3 事件总线 + Minecraft 只读 / M4 权限 / M5 插件市场未做
4. **插件市场页是空壳**
5. **`sgu.unlock-limits` 半废弃**：`unlock.region` 已无人读取，`unlock.offline-auth` 仍被 `ProfileUi.cs` 使用
6. **本地化仍有缺口**：`zh-TW` / `en-GB` 各比 `zh-CN` 少若干键（运行时回退 zh-CN，不崩）

---

## 6. 目录速览

```
├─ Plain Craft Launcher 2/     WPF 启动器主程序（上游 + 下游改动）
├─ PCL.Core/                   核心库（上游 + 下游改动）
├─ PCL.PluginSdk/              ★ 新增：插件契约（接口 / DTO / 能力枚举）
├─ PCL.PluginHost/             ★ 新增：插件加载器 / 包模型 / 安装卸载
├─ plugins/                    ★ 新增：四个插件源码 + package.ps1|sh + dist/*.pclpkg
├─ samples/HelloPlugin/        ★ 新增：示例插件（含可安装的 .pclpkg）
├─ tools/build.ps1             ★ 新增：Windows 构建脚本
├─ dist/                       ★ 本包附带：已构建的 exe 与插件包
├─ docs/                       设计文档（archive/ 是旧交接文档与日志）
└─ HMCL-main/                  （本包未包含）GPLv3 参考源码
```

★ = 本下游分支新增（上游没有），同步上游时**直接保留**即可。

---

## 7. 遇到问题先查这里

- 界面**所有文字都空白**
  → 语言文件里有重复的 `x:Key`（`ResourceDictionary` 加载失败），跑一次构建会直接报出来
- 启动器双击没反应 / 报 `Failure extracting contents of the application bundle`
  → `%TEMP%\.net\<exe 名字>\` 有 0 字节残件，删掉该目录再启动（详见 `HANDOVER.md` §7.10）
- 禁用插件后它还在、启用数量不变
  → 老版本遗留的同 id 多份包；启动时会自动去重并规范成 `<id>.pclpkg`，仍异常看日志里的 `[PluginHost]` 行
- 禁用后再启用报「缺少 plugin.json」
  → 解包缓存残件，删 `PCL/PluginData/cache/`（纯缓存）
- 插件装了不出现
  → 管理页看状态（出错会写原因）；或看日志 `[PluginHost]` 开头的几行
- XAML 报 `无法从文本"Xxx_Click"创建"Click"`
  → 事件签名不对：`MyButton.Click` 是 `(object, MouseButtonEventArgs)`（详见 `HANDOVER.md` §8.9）
- 工具页之间跳转没反应
  → `FormMain.PageChange(PageType.Tools, …)` 不会切子页，要走 `frmToolsLeft`（详见 §8.14）

---

祝顺利。有任何和上游行为不一致的地方，先在 `DOWNSTREAM-CHANGES.md` 里搜一眼——大概率是有意为之。
