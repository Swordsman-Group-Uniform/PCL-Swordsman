# PCL Swordsman

**PCL Swordsman** 是 [PCL CE](https://github.com/PCL-Community/PCL-CE)（Plain Craft Launcher Community Edition）的下游分支，
由 **剑客群组服服主团队**（Swordsman Group Uniform）维护，供 SGU 及相关服务器成员内部分享使用。
技术栈 **C# / WPF / .NET 10**，Windows 10 / 11 桌面应用。当前版本 **`27.1-preview-73`**（预发布）。

> A downstream fork of **PCL CE** maintained by the **Swordsman Group Uniform** server group, for internal use by
> SGU members. Built with **C# / WPF / .NET 10** for Windows 10 / 11. Current version: **`27.1-preview-73`** (pre-release).

---

## 中文

### 这是什么 / 不是什么

- ✅ 一个**能实际用起来的启动器**：重做了窗口材质，追加多套主题，并把插件系统从骨架补成可用体系
- ✅ 一个**可复用的插件平台**：契约 SDK、四阶段加载、事件总线、插件设置与页面、权限授权、完整性校验与签名
- ❌ **不是 PCL CE 官方仓库**，不代表 PCL Community 或原作者龙腾猫跃的任何立场
- ❌ **请勿**向上游仓库（PCL Community / PCL2-CE）提交 issue 或 PR
- ❌ 更新 / 自动更新 / 云端公告**已在代码层禁用**；联机固定 **EasyTier 纯 P2P**

### 特色

| 功能 | 说明 |
| --- | --- |
| **窗口材质** | Win11 22621+ 走 DWM 云母 / 亚克力；Win10 走系统 `AccentBlur`，并把界面铺满窗口，使**磨砂范围与界面完全等大** |
| **液态玻璃** | 整块启动器是一块大圆角玻璃 + 一条玻璃竖分隔线；主界面每张卡片是小玻璃；药丸、对话框、Toast 同为玻璃，不透明度全部可调 |
| **主题** | 多套内置主题与专属标题栏装饰（钻石剑 / **原版樱花粒子** / 原版方块贴图 / 像素化学式），并支持**插件自带主题** |
| **插件系统（M0–M4）** | 契约 SDK、四阶段加载（发现 / 版本 / 依赖拓扑 / 加载）、事件总线、插件设置与插件页面、权限授权、`files` 哈希 + ECDSA 签名、更新检测、崩溃熔断、共享插件目录 |
| **本地化** | 8 个语言文件完全键对齐：zh-CN / zh_sgu / zh-TW / en-US / en-GB / ja-JP / fr-FR / es-ES |
| **一键启动** | 为实例生成快捷方式：名称 = 实例名、图标 = 该实例图标，启动后自动隐藏启动器 |

插件市场（M5 剩余部分）**未实现**；`game-argv` / `process` 等高危插件 API **明确不开放**。

### 下载

构建产物不进 Git 仓库，请从本仓库的 **Releases** 获取 `PCLCE For SGU <版本>.exe` 与插件包（`.pclpkg`）。

### 从源码构建

```powershell
# Windows（推荐）：需要 .NET SDK 10
powershell -ExecutionPolicy Bypass -File tools\build.ps1
```

```bash
# Termux / Linux 交叉构建
DOTNET_gcServer=0 ./build.sh x64 Release
```

先打插件包（`plugins/package.ps1|sh`）再 `dotnet publish`，否则内嵌插件会报 `CS1566`。
完整流程见 [`README-FIRST.md`](README-FIRST.md) 与 [`HANDOFF-2026-10-31-preview-73.md`](HANDOFF-2026-10-31-preview-73.md)。

> ⚠️ 公开仓库**不包含**从原版 Minecraft 客户端提取的贴图等不宜公开分发的资源，
> 需要时请用 `tools/extract-vanilla-textures.py <client.jar>` 本地生成。

### 目录

```
Plain Craft Launcher 2/   启动器主程序（WPF：页面、控件、材质与玻璃）
PCL.Core/                 核心库：配置、主题配色、本地化、窗口互操作、模糊控件
PCL.PluginSdk/            插件契约（接口与 DTO，只增不改）
PCL.PluginHost/           插件加载器（发现 / 版本 / 依赖 / 完整性 / 签名 / 熔断）
plugins/ samples/         随包插件与示例插件
tools/                    构建与素材工具（build.ps1、贴图提取、插件签名器）
docs/                     插件接口文档、日志排查手册、内部设计笔记
```

### 反馈

邮箱 `hmjmfabc@swordsman.top`、`mingyugames@swordsman.top`；请在本仓库提 issue，**不要**去上游。

### 致谢与许可

- 上游：[PCL CE](https://github.com/PCL-Community/PCL-CE)（Apache-2.0）与原作者 [龙腾猫跃](https://github.com/Meloong-Git)
  的 [PCL](https://github.com/Meloong-Git/PCL)；上游代码版权归 **PCL Community** 与 **龙腾猫跃**
- 本分支按仓库 [`LICENSE`](LICENSE)（Apache-2.0）分发，保留全部上游声明
- 原版 Minecraft 贴图版权归 **Mojang Studios**，仅本地使用、不再分发
- 第三方组件许可清单见 [`Plain Craft Launcher 2/metadata.json`](Plain%20Craft%20Launcher%202/metadata.json) 的 `licenses`

---

## English

### What it is / is not

- ✅ A **working launcher**: window materials rebuilt, several themes added, plugin system taken from skeleton to a
  usable platform
- ✅ A **reusable plugin platform**: contract SDK, four-stage loading, event bus, plugin settings & pages, permission
  grants, integrity hashes + ECDSA signing
- ❌ **Not** an official PCL CE repository; not affiliated with PCL Community or the original author
- ❌ Do **not** file issues or PRs upstream (PCL Community / PCL2-CE)
- ❌ Update checking / auto-update / cloud announcements are **disabled in code**; multiplayer is **EasyTier pure P2P**

### Highlights

| Feature | Notes |
| --- | --- |
| **Window materials** | Win11 22621+ uses DWM Mica/Acrylic; Win10 uses system `AccentBlur` with the UI made full-bleed, so the **frost rectangle matches the UI rectangle exactly** |
| **Liquid glass** | The launcher is one large rounded glass pane plus a glass divider; main-UI cards are small glass tiles; pills, dialogs and toasts are glass too — all opacities configurable |
| **Themes** | Several built-in themes with dedicated title-bar decorations (diamond sword, **vanilla cherry-blossom particles**, vanilla block textures, pixel-art formula) plus **plugin-provided themes** |
| **Plugin system (M0–M4)** | Contract SDK, four-stage loading (discover / host-version / dependency graph / load), event bus, plugin settings & pages, permission grants, `files` hashes + ECDSA signing, update checking, crash circuit breaker, shared plugin directory |
| **Localization** | 8 key-aligned language files: zh-CN, zh_sgu, zh-TW, en-US, en-GB, ja-JP, fr-FR, es-ES |
| **One-click launch** | Creates a shortcut named after the instance, uses that instance's icon, then hides the launcher |

The plugin marketplace (remaining part of M5) is **not implemented**; high-risk plugin APIs (`game-argv`, `process`)
are **intentionally not exposed**.

### Download

Build outputs are not committed — grab `PCLCE For SGU <version>.exe` and the `.pclpkg` bundles from **Releases**.

### Build from source

```powershell
# Windows (recommended): requires .NET SDK 10
powershell -ExecutionPolicy Bypass -File tools\build.ps1
```

```bash
# Termux / Linux cross-build
DOTNET_gcServer=0 ./build.sh x64 Release
```

Package the plugins first (`plugins/package.ps1|sh`), then `dotnet publish`, otherwise embedded plugin resources fail
with `CS1566`. See [`README-FIRST.md`](README-FIRST.md) for the full workflow.

> ⚠️ This public repository **does not include** assets extracted from the vanilla Minecraft client; regenerate them
> locally with `tools/extract-vanilla-textures.py <client.jar>`.

### Feedback

E-mail: `hmjmfabc@swordsman.top`, `mingyugames@swordsman.top` — please open issues in **this** repository, not upstream.

### Credits & License

- Upstream: [PCL CE](https://github.com/PCL-Community/PCL-CE) (Apache-2.0) and the original
  [PCL](https://github.com/Meloong-Git/PCL) by [龙腾猫跃](https://github.com/Meloong-Git); upstream code is
  © PCL Community and 龙腾猫跃
- This fork is distributed under the repository [`LICENSE`](LICENSE) (Apache-2.0), all upstream notices preserved
- Vanilla Minecraft textures are © Mojang Studios; used locally only, not redistributed
- Third-party licences: see the `licenses` field of
  [`Plain Craft Launcher 2/metadata.json`](Plain%20Craft%20Launcher%202/metadata.json)
