# 首次启动弹窗内容 Dump（27.1-snapshot-8）

> 用途：这些弹窗由配置开关控制，一次性展示后不再出现，导致难以复现。本文档按启动顺序
> 完整列出每个弹窗的**触发条件**、**标题**、**正文原文**、**按钮**、**代码位置** 与
> **重新触发方法**，供下一个 Build 决定如何改写。

---

## 0. 启动顺序总览

启动过程中按以下顺序可能弹出对话框（`Plain Craft Launcher 2/FormMain.xaml.cs`）：

| # | 弹窗 | 触发条件 | 是否首次启动可见 |
| --- | --- | --- | --- |
| 0 | 环境警告 | 检测到运行环境问题（临时目录 / 32 位系统 / 社交软件目录 / 旧版 Windows） | 每次都检查 |
| 1 | 协议授权（EULA） | `SystemEula != true` | ✅ 首次 |
| 2 | 启用遥测数据收集 | `SystemTelemetry` 尚未被用户选择过 | ✅ 首次 |
| 3 | PCL CE 版本说明（社区版提示） | 内部版本代号变化（升级/全新安装）时，每次升级展示一次 | ✅ 每次升级 |
| 4 | 更新日志 | 内部版本代号升高且存在历史版本记录 | ✅ 升级时（首次安装不显示） |
| 5 | 主页预设已重置 | 历史版本代号 < 521 且使用了自定义主页预设 | 旧版本升级时 |
| 6 | PCL CE 联机大厅说明与条款 | 首次进入「百宝箱 → 联机大厅」（`LinkEula != true`） | 首次进入联机大厅 |
| — | 特殊版本提示 | 仅 DEBUG / CI 构建 | — |

代码位置：
- 环境警告：`Plain Craft Launcher 2/Application.xaml.cs` → `_ShowEnvironmentWarning()`
- 协议 / 遥测 / 特殊版本提示：`Plain Craft Launcher 2/FormMain.xaml.cs`（第三阶段加载，`"Start Loader"` 线程内）
- 社区版提示 / 更新日志 / 主页重置：`FormMain.xaml.cs` → `UpgradeSub()`
- 联机大厅条款：`Plain Craft Launcher 2/Pages/PageTools/PageToolsGameLink.xaml`（`PanEula`）

---

## 1. 环境警告

- 资源键：`Application.EnvironmentWarning.*`
- 标题：**环境警告**
- 正文模板：

  ```
  PCL CE 在启动时检测到环境问题：

  {0}

  不解决这些问题可能会导致部分功能无法正常工作……
  ```

  `{0}` 为下列条目的拼接（每行一条，仅在命中时出现）：

  - `- Windows 版本不满足推荐要求，推荐至少 Windows 10 1809，建议考虑升级 Windows 系统`
  - `- 当前系统为 32 位，不受 PCL CE 和新版 Minecraft 支持，非常建议重装为 64 位系统后再进行游戏`
  - `- PCL CE 正在临时目录运行，请将 PCL CE 从压缩包中解压之后再使用，否则可能导致游戏存档或设置丢失`
  - `- PCL CE 正在 QQ、微信、TIM 等社交软件的下载目录运行，请考虑移动到其他位置，否则可能导致游戏存档或设置丢失`
- 按钮：**我知道了**
- 复现：把启动器放到 `%TEMP%` 或微信/QQ 下载目录下运行，或在 32 位系统 / 低于 Win10 1809 的系统上运行。

---

## 2. 协议授权（EULA）

- 资源键：`Main.Eula.*`
- 标题：**协议授权**
- 正文：`在使用 PCL CE 前，请同意 PCL 的用户协议与免责声明。`
- 按钮：**同意** / **拒绝** / **查看用户协议与免责声明**
  - 拒绝 → 直接退出启动器
  - 查看用户协议 → 打开 <https://shimo.im/docs/rGrd8pY8xWkt6ryW>
- 状态键：`SystemEula`（`States.System.LauncherEula`）
- 复现：见文末「重新触发方法」。

> 下游改写建议：文案中的“PCL CE”与上游用户协议链接均指向上游，下游版本可能需要替换为
> PCLCE For SGU 自己的说明。

---

## 3. 启用遥测数据收集

- 资源键：`Main.Telemetry.*`
- 标题：**启用遥测数据收集**
- 正文（原文，换行以 `\n` 表示）：

  ```
  启用遥测数据收集后，启动器将会收集并上报错误与设备环境信息，这可以帮助开发者修复潜在的问题、更好的进行规划和开发。
  若启用此功能，我们将会收集以下信息：

  - 启动器内出现的错误
  - 启动器版本信息与识别码
  - Windows 系统版本与架构
  - 已安装的物理内存大小
  - NAT 与 IPv6 支持情况
  - 是否使用过原版 PCL、HMCL 或 BakaXL

  这些数据均不与你关联，我们也绝不会向第三方出售数据。
  如果不希望启用遥测，可以选择拒绝。这不会影响其他功能的正常使用，但可能会影响开发者修复潜在 Bug。
  你可以随时在启动器设置中调整这项设置。
  ```
- 按钮：**同意** / **拒绝**
- 状态键：`SystemTelemetry`（`Config.System.Telemetry`）
- 复现：见文末「重新触发方法」。

> 注意：本下游分支未注入 Sentry DSN（`PCL_SENTRY_DSN`），因此即使同意也不会真正上报数据。

---

## 4. PCL CE For SGU 版本说明（社区版提示）

- 资源键：`Update.CommunityNotice.*`
- 触发：`UpdateManager.ShowCEAnnounce()`，在内部版本代号变化的升级流程中展示
- 标题：**PCL CE For SGU版本说明**
- 正文（已按下游要求改写，原文如下）：

  ```
  你正在使用来自 剑客群组服服主团队 的 PCL CE For SGU，此版本的PCL CE仅供SGU及其相关服务器（VFH术友汇、HCE圣苦力怕帝国）成员内部分享使用，不要外传，遇到问题请不要向社区版仓库反馈！
  剑客群组服服主团队 及其成员与PCL-Community及龙腾猫跃无从属关系，且均不会为您的使用做担保。

  如果你不是 SGU 成员或不需要 SGU 相关功能，建议下载原版 PCL / PCL CE 使用。

  如果你在非剑客群组服（The Swordsman Group Uniform）官方提供的渠道下载到了 PCL CE For SGU，请向hmjmfabc@swordsman.top或mingyugames@swordsman.top反馈。

  该版本与原版/社区版的特性区别：

  · 砍掉了所有需要连接 PCL (CE) 相关服务器的联网功能
  · 联机功能：采用去中心化的纯P2P实现，砍掉了大厅
  · 禁用了反馈和更新功能

  此提示会在启动器更新后展示一次。
  ```
- 按钮：**我知道了**
- 代码：`Plain Craft Launcher 2/Modules/Updates/UpdateManager.cs` → `ShowCEAnnounce()`
- 其他语言：`zh-TW` 为繁体版本，`en-US` / `en-GB` 为英文版本，内容与上述一致。

---

## 5. 更新日志

- 资源键：`Main.UpdateLog.*`
- 触发：`UpgradeSub()` 中内部版本代号升高时（首次安装即 `LastVersion == 0` 时不显示）
- 标题模板：**PCL CE 已更新至 {分支名} {完整版本号}**
  （例：`PCL CE 已更新至 Publish 27.1-snapshot-8(2.15.1)`）
- 正文：读取临时目录下的 `CEUpdateLog.md`；由于更新功能已禁用，该文件不存在，因此正文固定为
  **欢迎使用呀~**
- 按钮：**确定** / **完整更新日志**（→ <https://github.com/PCL-Community/PCL2-CE/releases>）
- 代码：`FormMain.xaml.cs` → `ShowUpdateLog()`

---

## 6. 主页预设已重置

- 资源键：`Main.HomepageReset.*`
- 标题：**主页预设已重置**
- 正文：`由于启动器更新，已重置主页预设。请重新前往 设置 - 个性化 选择预设`
- 按钮：**确定**
- 触发：`lastVersionCode < 521 && Config.Preference.Homepage.SelectedPreset >= 3`
- 说明：仅从很旧的版本升级时出现，新安装与常规升级均不会触发。

---

## 7. PCL CE 联机大厅说明与条款

- 资源键：`Tools.GameLink.Agreement.*`
- 触发：首次进入「百宝箱 → 联机大厅」，`States.Link.LinkEula != true`
- 标题：**PCL CE 联机大厅说明与条款**
- 正文：

  说明：`此处列出了 PCL CE 大厅使用的相关服务文档及介绍`
  - 条目：**PCL CE 大厅相关隐私政策** —— `了解 PCL CE 如何处理您的个人信息`
    （→ <https://www.pclc.cc/privacy/personal-info-brief.html>）
  - 条目：**Natayark OpenID 服务条款** —— `查看 Natayark OpenID 服务条款`
    （→ <https://account.naids.com/policy>）

  条款正文：

  ```
  使用大厅功能即代表你同意下列条款：

  我承诺严格遵守中国大陆相关法律法规，不会将大厅功能用于违法违规用途。
  我承诺使用大厅功能带来的一切风险自行承担。
  我已知晓并同意 PCL CE 收集经处理的本机识别码、Natayark ID 与其他信息并在必要时提供给执法部门。
  为保护未成年人个人信息，使用联机大厅前，我确认我已满十四周岁。

  另外，你还需要同意 PCL CE 大厅相关隐私政策及《Natayark OpenID 服务条款》。
  ```
- 按钮：**我已阅读并同意**
- 撤销授权（进入大厅后）：
  - 标题：**撤销授权确认**
  - 正文：`你确定要撤销联机协议授权吗？`
  - 撤销后提示：`联机功能已停用！`
- 状态键：`LinkEula`（`States.Link.LinkEula`）

> 本 Build 已恢复联机大厅功能（内置公共联机根服务器），因此该条款页会真实出现，需要重点确认文案。

---

## 8. 特殊版本提示（仅 DEBUG / CI 构建）

- 资源键：`Main.SpecialVersion.*`
- 标题：**特殊版本提示**
- 正文（CI）：`当前运行的 PCL CE 为 CI 自动构建版本。……`；正文（Debug）：`当前运行的 PCL CE 为 Debug 版本。……`
- 补充：`可以添加 PCL_DISABLE_DEBUG_HINT 环境变量 (任意值) 来隐藏这个提示。`
- 按钮：**我清楚我在做什么** / **打开最新版下载页并退出**
- 说明：Release 构建不会出现。

---

## 重新触发方法

启动器的状态保存在两个位置：

| 范围 | 路径 |
| --- | --- |
| 共享配置（跨实例，含 EULA / 遥测 / 版本代号 / 账号档案） | `%APPDATA%\PCLCE\config.v1.json` |
| 实例配置（启动器同级目录） | `<启动器目录>\PCL\config.v1.yml` |

脚本会一次性展示的开关都在共享配置里：

| 弹窗 | 配置键 | 存储形式 |
| --- | --- | --- |
| 协议授权 | `SystemEula` | 明文 `true/false` |
| 遥测 | `SystemTelemetry` | 明文 `true/false` |
| 版本代号（用于判断升级） | `SystemLastVersionReg`、`SystemHighestAlphaVersionReg`、`SystemHighestBetaVersionReg` | 加密字符串 |
| 联机大厅条款 | `LinkEula` | 明文 `true/false` |

**方法 A（推荐，可精确复现）**：关闭启动器 → 备份 `%APPDATA%\PCLCE\config.v1.json` → 用文本编辑
器打开该 JSON，删除其中 `SystemEula`、`SystemTelemetry`、`SystemLastVersionReg`、
`SystemHighestAlphaVersionReg`、`SystemHighestBetaVersionReg` 这几个键 → 重新启动。

**方法 B（干净复现全部首次弹窗）**：关闭启动器 → 把 `%APPDATA%\PCLCE` 整个目录重命名备份
→ 重新启动。⚠️ 该操作会一并丢失已保存的账号档案（`Profile` 键）与全部共享设置。

**方法 C（只看升级弹窗）**：把 `metadata.json` 里的 `version.code` 改大（如 526）后重新构建，
启动时会走升级流程，从而再次展示「PCL CE 版本说明」与「更新日志」。
