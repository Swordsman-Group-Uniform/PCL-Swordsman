# 插件开发完整文档

> 适用版本：**PCL Swordsman `27.1(2.15.2)` / 扩展 API 1**
> 维护者：剑客群组服服主团队
> 最后更新：2026-11-01
>
> 本文档覆盖插件开发的全部细节：清单、能力、加载流程、UI 与设置、事件与服务、完整性与签名、
> 更新、打包发布、本地化、功能键与主题扩展。主题不再是敏感内容，相关机制见
> [`PLUGIN-THEMES.md`](PLUGIN-THEMES.md)。

---

## 目录

1. [十分钟做出第一个插件](#1-十分钟做出第一个插件)
2. [三层结构与依赖方向](#2-三层结构与依赖方向)
3. [插件包格式与安装位置](#3-插件包格式与安装位置)
4. [清单 `plugin.json` 全字段](#4-清单-pluginjson-全字段)
5. [加载流程与生命周期](#5-加载流程与生命周期)
6. [能力（capabilities）总表](#6-能力capabilities总表)
7. [上下文 API 详解](#7-上下文-api-详解)
8. [UI 扩展点](#8-ui-扩展点)
9. [事件总线](#9-事件总线)
10. [Minecraft 只读服务](#10-minecraft-只读服务)
11. [网络与文件能力](#11-网络与文件能力)
12. [持久化与宿主目录](#12-持久化与宿主目录)
13. [完整性与签名](#13-完整性与签名)
14. [更新检测](#14-更新检测)
15. [打包与发布](#15-打包与发布)
16. [本地化](#16-本地化)
17. [功能键 `provides`](#17-功能键-provides)
18. [主题扩展](#18-主题扩展)
19. [调试与排错](#19-调试与排错)
20. [兼容性铁律](#20-兼容性铁律)
21. [内置插件与示例索引](#21-内置插件与示例索引)
22. [发布前检查清单](#22-发布前检查清单)

---

## 1. 十分钟做出第一个插件

### 1.1 建工程

插件是**独立程序集**，只引用 `PCL.PluginSdk`。**绝对不要引用 `PCL.Core` 或 `Plain Craft Launcher 2`**
（那会把启动器的整个程序集装进插件包，且与宿主的 ALC 共享规则冲突，见 [§20](#20-兼容性铁律)）。

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net10.0-windows</TargetFramework>
        <UseWPF>true</UseWPF>
        <Nullable>enable</Nullable>
        <LangVersion>14.0</LangVersion>
        <Platforms>x64;ARM64</Platforms>
        <!-- 插件不需要自己带 SDK 程序集 -->
        <PrivateAssets>all</PrivateAssets>
    </PropertyGroup>
    <ItemGroup>
        <ProjectReference Include="..\..\PCL.PluginSdk\PCL.PluginSdk.csproj">
            <Private>false</Private>
            <ExcludeAssets>runtime</ExcludeAssets>
        </ProjectReference>
    </ItemGroup>
</Project>
```

### 1.2 写入口类

```csharp
using System.Windows;
using System.Windows.Controls;
using PCL.PluginSdk;

namespace MyPlugin;

public sealed class MyPlugin : IPlugin, IPluginUiProvider, IPluginSettingsProvider
{
    private IPluginContext? _context;

    public void Initialize(IPluginContext context)
    {
        _context = context;
        context.Log.Info($"已初始化：宿主 {context.HostVersion}，语言 {context.LanguageCode}");
        // 配置读取：键名点分小写，宿主负责落盘
        var greeting = context.Config.Get("ui.greeting", "你好");
        context.Log.Debug($"当前问候语：{greeting}");
    }

    public void Enable() { }
    public void Disable() { }
    public void Dispose() { }

    public IReadOnlyList<PluginPageDefinition> GetPages() =>
    [
        new()
        {
            Id = "myplugin.home",
            Title = "我的页面",
            Description = "示例页面",
            Icon = "lucide/puzzle",
            SortOrder = 200,
            // 每次打开都会被调用，必须返回**新实例**
            CreatePage = context => new TextBlock { Text = "Hello", Margin = new Thickness(20) }
        }
    ];

    public IReadOnlyList<PluginSettingDefinition> GetSettings() =>
    [
        new()
        {
            Key = "ui.greeting",
            Title = "问候语",
            Type = PluginSettingType.Text,
            DefaultValue = "你好"
        }
    ];
}
```

### 1.3 写清单

`plugin.json` 放在工程根目录，属性设成 **Copy to Output Directory: PreserveNewest**：

```json
{
  "id": "my.example.plugin",
  "name": "我的插件",
  "version": "1.0.0",
  "apiVersion": 1,
  "minHostVersion": "27.1.0",
  "author": "你的名字",
  "description": "一句话说明",
  "entry": { "assembly": "MyPlugin.dll", "type": "MyPlugin.MyPlugin" },
  "capabilities": ["config", "ui.page", "ui.sidebar"],
  "ui": { "icon": "lucide/puzzle", "sortOrder": 200 }
}
```

### 1.4 打包

```bash
# 在仓库根目录
python3 tools/plugin-hash.py "plugins/dist/MyPlugin.pclpkg"   # 写入 files 哈希（可选但强烈建议）
```

内置插件用 `plugins/package.sh`（见 [§15](#15-打包与发布)）；外部插件自己 zip 即可，
结构必须是 **`plugin.json` 在 zip 根**（或唯一的顶层目录下，宿主会自动剥前缀）：

```
MyPlugin.pclpkg (zip)
├── plugin.json
├── MyPlugin.dll
└── (可选) 资源文件 / 语言表
```

### 1.5 装进去看日志

1. 把 `MyPlugin.pclpkg` 丢进 `<启动器目录>\PCL\Plugins\`；
2. 重启启动器（或插件管理页点「重新扫描」）；
3. 打开 `工具 → 插件 管理`，应该看到它，状态「已启用」；
4. 侧边栏出现「我的页面」；
5. 日志里搜 `[Plugin:my.example.plugin]` 与 `[PluginHost]`，看有没有 `Faulted`。

---

## 2. 三层结构与依赖方向

```
Plain Craft Launcher 2（启动器，WPF 应用）
        │ 引用
        ├──> PCL.Core（核心库：配置 / 主题 / 材质 / 本地化 / 网络）
        │
        └──> PCL.PluginHost（插件宿主：发现、清单、ALC、生命周期、权限、完整性）
                        │ 引用
                        └──> PCL.PluginSdk（纯契约：接口 + DTO + 枚举，无实现）
```

* **SDK 只做契约**：`PCL.PluginSdk` 里没有任何实现逻辑，插件只引用它。
* **宿主不依赖启动器**：`PCL.PluginHost` 通过接口（`IPluginHostEnvironment` / `IPluginNotifyHost` /
  `IPluginPermissionHost`）向启动器要东西，启动器侧的实现放在
  `Plain Craft Launcher 2/Modules/Plugins/`：
  * `PluginHostBootstrap.cs`：版本、目录、日志、信任列表、事件转发；
  * `PluginServices.cs`：权限持久化、`IMinecraftService` / `IPluginFiles` / `IPluginNotifyHost` 的实现；
  * `PluginUpdateService.cs`：更新检测与安装；
  * `PluginSettingsForm.cs`：设置项 schema → 控件的共享渲染器（管理页与「设置 → 插件」都用它）。
* **插件永远看不到 `PCL.Core`**：它能用的能力都由 `IPluginContext` 暴露。

---

## 3. 插件包格式与安装位置

| 项 | 值 |
| --- | --- |
| 扩展名 | `.pclpkg`（本质是 zip） |
| 禁用形态 | 把文件名改成 `.pclpkg.disabled`（**启用状态就是文件名**，不写在配置里） |
| 实例级目录 | `<启动器目录>\PCL\Plugins\`（优先） |
| 共享级目录 | `%APPDATA%\PCLCE\Plugins\`（所有实例共享；同名插件只加载实例级那份） |
| 解压缓存 | `<启动器目录>\PCL\PluginCache\`（按「基名_长度_时间戳」命名，可随时删） |
| 数据目录 | `<启动器目录>\PCL\PluginData\<插件 id>\`（`IPluginContext.DataDirectory`） |

* 也支持**目录形式**的老插件（直接一个文件夹，里面 `plugin.json` + dll），但不推荐：无法签名、无法版本比较。
* 包内允许有唯一顶层目录包裹，宿主会自动剥掉前缀（`PCL.PluginHost.PluginPackage.GetPrefix`）。
* `PCL.PluginHost.PluginPackage.OpenArchiveShared()` 用 `FileShare.ReadWrite | Delete` 打开包，
  所以插件包被别的进程占着也能读（这是个踩过的坑，见 [§19](#19-调试与排错)）。

---

## 4. 清单 `plugin.json` 全字段

模型：`PCL.PluginSdk.PluginManifest`（schema v1）。

| 字段 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `id` | string | ✅ | 全局唯一，建议 `作者.插件` 小写点分；用作数据目录名 |
| `name` | string | ✅ | 展示名 |
| `version` | string | ✅ | 语义化版本，用于安装覆盖判断与更新比较 |
| `apiVersion` | int | ✅ | **必须等于 `PluginApiVersion.Current`（当前 1）**，否则拒绝加载 |
| `minHostVersion` | string | ➖ | 最低启动器版本，如 `27.1.0`；不满足 → `Incompatible`，不加载 |
| `author` / `description` / `homepage` / `license` | string | ➖ | 展示用 |
| `entry.assembly` | string | ✅ | 入口程序集文件名（相对包根） |
| `entry.type` | string | ✅ | `IPlugin` 实现的全名（含命名空间），须有**无参构造函数** |
| `dependencies` | array | ➖ | `[{ "id": "...", "version": ">=1.0.0" }]`，宿主做拓扑排序 + 环检测 |
| `capabilities` | string[] | ➖ | 能力字符串，见 [§6](#6-能力capabilities总表)；未知字符串被忽略（前向兼容） |
| `provides` | string[] | ➖ | 提供的功能键，见 [§17](#17-功能键-provides) |
| `ui.icon` | string | ➖ | lucide 名（`lucide/puzzle`）或包内相对路径 |
| `ui.sortOrder` | int | ➖ | 管理页排序，默认 100 |
| `files` | object | ➖ | `{ "文件名": "<小写 sha256>" }`，加载前校验，见 [§13](#13-完整性与签名) |
| `signature` | object | ➖ | `{ "alg": "ecdsa-p256-sha256", "keyId": "...", "value": "<base64>" }` |
| `updateUrl` | string | ➖ | 返回更新信息的 JSON 地址，见 [§14](#14-更新检测) |

校验失败（缺 `plugin.json`、id/version 为空、`apiVersion` 不等、入口文件不存在、入口类型找不到）→
状态 `Faulted`，`descriptor.Error` 里带具体原因，管理页会显示。

**能力字符串写法**：解析时会去掉 `.`、`-`、`_` 并忽略大小写，因此
`ui.page` / `UiPage` / `ui-page` 等价；`net-http` = `NetHttp`；`themes.custom` = `ThemesCustom`。

---

## 5. 加载流程与生命周期

### 5.1 四个阶段（`PCL.PluginHost.PluginLoader`）

1. **发现**：扫实例级 + 共享级目录里所有 `.pclpkg`（含 `.disabled`，用于展示），
   以及老式目录插件；实例级同名优先。此阶段**只读清单**，不加载程序集。
2. **宿主版本校验**：`minHostVersion` 不满足 → `Incompatible`。
3. **依赖解析**：缺依赖 / 依赖被禁用 / 依赖不可用 / 版本不满足 / 循环依赖 → `Faulted`；
   否则按拓扑序排出加载顺序。
4. **装载**：按顺序建 `PluginLoadContext`（可回收 ALC）→ 加载入口程序集 → `Initialize(context)` → `Enable()`。
   任何一步抛异常 → `Faulted` + 熔断计数 +1。

### 5.2 生命周期回调

```
Initialize(IPluginContext)   ← 注册页面 / 设置项 / 事件；不要做耗时操作
Enable()                     ← 起后台任务、定时器
Disable()                    ← 停后台任务
Dispose()                    ← 退订事件、释放资源
```

* 宿主对每个回调都包了 try/catch 并写日志（前缀 `[Plugin:<id>]`）。
* 禁用插件 = `Disable()` → `Dispose()` → `AssemblyLoadContext.Unload()`；
  **卸载不保证进程内的所有引用都消失**，所以正式行为以「重启生效」为准。
* 插件卸载 / 重扫时宿主会**清空该插件的所有事件订阅**，不必担心泄漏（但你自己最好也 `Dispose()` 退订）。

### 5.3 崩溃熔断

* 规则：**5 分钟内失败 3 次**（`PluginLoader.FaultWindow` / `FaultLimit`）。
* 动作：把包改名成 `.pclpkg.disabled`，下次启动不再加载。
* 记录：`PCL\PluginData\PluginFaults.json`。想复位就删掉这个文件。
* 日志形如：`[PluginHost] <插件> 连续失败 3 次，已自动禁用`。

### 5.4 重新扫描

管理页「重新扫描」/ 安装 / 卸载 / 启用 / 停用之后都会 `Reload()`；
`PluginHostManager.Initialize` 与 `Reload` **会阻塞**（含插件的 Initialize/Enable），启动器是在后台线程调的。
`PluginHostManager.Changed` 事件通知界面刷新（可能在后台线程触发，记得切 UI 线程）。

---

## 6. 能力（capabilities）总表

枚举：`PCL.PluginSdk.PluginCapability`（`[Flags]`）。

| 清单字符串 | 枚举 | 作用 | 强制 | 需要用户授权 |
| --- | --- | --- | --- | --- |
| `config` | `Config` | 拥有自己的配置（`IPluginConfig`） | 是 | 否（默认允许） |
| `file-plugin-data` | `FilePluginData` | 读写自己的数据目录 | 是 | 否（默认允许） |
| `events` | `Events` | 订阅宿主事件；没声明时订阅返回空 `IDisposable` + 一条警告 | 是 | 否 |
| `ui.page` | `UiPage` | 注册右侧页面 | 是 | 否 |
| `ui.sidebar` | `UiSidebar` | 注册侧边栏条目（目前与页面绑定：有页面就有条目） | 是 | 否 |
| `ui.settings` | `UiSettings` | 注册「设置 → 插件」分区 | 是 | 否 |
| `ui.notify` | `UiNotify` | 弹提示 / 确认框 | 是 | 否 |
| `minecraft.read` | `MinecraftRead` | 只读实例信息 | 是 | **是**（首次使用时弹一次，`permissions.json` 记住） |
| `file-game-dir` | `FileGameDir` | 拿到游戏目录路径（中危） | 是 | **是** |
| `file-user-pick` | `FileUserPick` | 弹选择文件 / 文件夹对话框（中危） | 是 | **是** |
| `net-http` | `NetHttp` | 出站 HTTP（中危） | 是 | **是** |
| `themes.custom` | `ThemesCustom` | 提供插件自带主题（见 §18） | 是 | 否 |
| `theme-read` | `ThemeRead` | 读取当前配色信息（只读） | 部分 | 否 |
| `game-argv` | `GameArgv` | 改游戏启动参数（高危） | **未实现** | — |
| `process` | `Process` | 启动外部进程（高危） | **未实现** | — |

> `game-argv` / `process` 是**故意不实现**的（用户明确要求）。清单里写了也不会获得任何 API，
> 只会显示在管理页的「声明能力」里。要加高危能力必须先改这份文档并拿到明确授权。

授权闸的实现：`PCL.PluginHost.PluginContext.EnsureGranted()` →
`IPluginPermissionHost.IsGranted / IsDenied / RequestGrant`，
启动器侧落在 `Plain Craft Launcher 2/Modules/Plugins/PluginServices.cs`（`permissions.json`）。
**没有声明能力时直接拒绝并日志警告一次**，不会静默给一个不可用的对象。

---

## 7. 上下文 API 详解

`IPluginContext`（宿主实现：`PCL.PluginHost.PluginContext`）：

| 成员 | 说明 |
| --- | --- |
| `PluginId` / `PluginVersion` | 来自清单 |
| `HostVersion` | 宿主版本，如 `27.1-snapshot-83(2.15.1)` |
| `DataDirectory` | 插件专属目录（已创建） |
| `Capabilities` / `HasCapability(cap)` | 能力位 |
| `Log` | `IPluginLogger`，宿主统一加 `[Plugin:<id>]` 前缀 |
| `Config` | `IPluginConfig` |
| `Events` | `IPluginEventBus` |
| `Notify` | `IPluginNotify` |
| `Minecraft` | `IMinecraftService` |
| `Net` | `IPluginHttp` |
| `Files` | `IPluginFiles` |
| `LanguageCode` | 当前界面语言码，如 `zh-CN` |

### 7.1 日志

```csharp
context.Log.Trace("..."); context.Log.Debug("..."); context.Log.Info("...");
context.Log.Warn("...");  context.Log.Error("...", ex);
```

级别与启动器一致（`Trace/Debug/Info/Warn/Error`）；正式版默认只落 `Info` 以上，调试时在启动器里开调试模式。

### 7.2 配置

```csharp
var n = context.Config.Get("ui.count", 3);          // 读不到就用默认值，不会抛
context.Config.Set("ui.count", n + 1);              // 宿主自动落盘
context.Config.Remove("ui.count");
context.Config.Changed += key => context.Log.Debug($"配置变了：{key}");
context.Config.Save();                              // 一般不需要手动调
```

落盘位置：`<DataDirectory>\config.json`。
**约定**：键名点分小写；`Get<T>` 的类型必须与写入时一致（底层是 JSON 反序列化）。

---

## 8. UI 扩展点

### 8.1 页面（`IPluginUiProvider`）

```csharp
public sealed class PluginPageDefinition
{
    public string Id { get; set; }            // 插件内唯一，用作侧栏标识
    public string Title { get; set; }         // 侧栏 + 页面标题
    public string? Description { get; set; }  // 页面顶部说明
    public string? Icon { get; set; }         // lucide 名，如 "lucide/puzzle"
    public int SortOrder { get; set; }        // 宿主内置页 0~5，插件默认 100
    public Func<IPluginContext, FrameworkElement>? CreatePage { get; set; }
}
```

* 宿主负责侧栏条目、切换动画、滚动容器、`PagePluginHost` 路由；插件只给 `FrameworkElement`。
* **`CreatePage` 每次打开都会调用，必须返回新实例**（不要复用同一个控件，否则父子关系会炸）。
* 插件页面与内置页共用同一个侧栏单选组：点哪个就跳哪个（早期版本「工具-插件」和「插件管理」
  能同时选中就是这里出过 bug，修法是把条目直接插进 `PanItem`，不要再套一层 `Panel`）。

### 8.2 设置项（`IPluginSettingsProvider`）

宿主把 schema 渲染成控件，读写全走插件自己的 `IPluginConfig`，**插件不用写设置界面，也不用自己保存**。

```csharp
new PluginSettingDefinition
{
    Key = "ui.mode",
    Title = "模式",
    Description = "说明文字",
    Type = PluginSettingType.Choice,          // Toggle / Text / Integer / Decimal / Choice
    DefaultValue = "auto",                    // 一律用字符串形式
    Options = [ new() { Value = "auto", Title = "自动" }, new() { Value = "manual", Title = "手动" } ]
}
```

* `Integer` / `Decimal` 用 `Min` / `Max` 限制范围。
* `GetSettings()` 每次打开面板都会重新调用，可以按状态动态给不同项。
* 渲染器：`Modules/Plugins/PluginSettingsForm.cs`（管理页与「设置 → 插件」共用）。

### 8.3 通知与确认框（`ui.notify`）

```csharp
context.Notify.Info("提示");  context.Notify.Success("成功");
context.Notify.Warning("警告"); context.Notify.Error("错误");
context.Notify.Confirm("标题", "正文", ok => { /* 主线程回调 */ });
```

* 任意线程都能调，宿主切回 UI 线程。
* `Confirm` **会阻塞**（和启动器内部确认框一样），别在 UI 线程里连环弹。

### 8.4 控件与图标

* 插件只能用**标准 WPF 控件**（不能引用 `PCL.Core.UI.Controls` 里的 `MyButton` / `MyCard` 等）。
  想跟启动器风格一致，就自己按当前配色拿颜色——主题 API 见 [§18](#18-主题扩展)。
* 图标用 lucide 名（`lucide/puzzle`），宿主解析；或给包内相对路径（宿主按插件包目录解析）。

---

## 9. 事件总线

`IPluginEventBus.Subscribe<TEvent>(Action<TEvent>)` 返回 `IDisposable`，`Dispose()` 即退订。
**宿主发布、插件订阅**（插件不能自己发事件，避免互相干扰）。

| 事件 | 载荷 | 时机 |
| --- | --- | --- |
| `HostStartupCompletedEvent` | `HostVersion`, `LaunchCount` | 启动完成（主窗口加载、插件就绪） |
| `InstanceSwitchedEvent` | `InstanceName`, `InstancePath` | 选中实例变化（取消选择为空串） |
| `GameLaunchingEvent` | `InstanceName`, `InstancePath` | 参数就绪、进程还没起 |
| `GameLaunchedEvent` | `InstanceName` | 进程已拉起（或按设置隐藏/退出启动器） |
| `DownloadStartedEvent` | `TaskName` | 下载类任务开始 |
| `DownloadFinishedEvent` | `TaskName`, `Success` | 下载类任务结束（中止/失败为 false） |
| `ThemeChangedEvent` | `IsDarkMode`, `ThemeKey`, `ThemeDisplayName` | 切主题或切亮/暗（见 §18） |
| `LanguageChangedEvent` | `LanguageCode` | 界面语言切换 |

* 回调在**发布方所在线程**执行（基本都是 UI 线程）→ 自己 try/catch，耗时操作自己切后台。
* 没声明 `events` 能力时：返回一个空 `IDisposable` + 日志警告，**不抛异常**。
* 插件卸载 / 重扫时宿主清空所有订阅。

---

## 10. Minecraft 只读服务

需要 `minecraft.read`（首次访问会弹一次授权框，拒绝后不再重复弹）。

```csharp
var info = context.Minecraft.CurrentInstance;   // 无实例时为 null
if (info is not null)
    context.Log.Info($"{info.Name} / {info.VersionName} / {info.LoaderName} / mods={info.ModCount}");

IReadOnlyList<PluginInstanceInfo> all = context.Minecraft.Instances;
string gameDir = context.Minecraft.GameDirectory;
```

`PluginInstanceInfo`：`Name`、`Path`、`VersionName`、`LoaderName`、`LoaderVersion`、`ModCount`
（`ModCount` 只是 `mods\*.jar` 计数，不解析依赖）。

* 只读：没有安装 / 删除 / 改配置。
* `Instances` 可能为空——取决于用户是否进过启动页（实例列表是懒加载的）。

---

## 11. 网络与文件能力

### 11.1 HTTP（`net-http`，需授权）

```csharp
var resp = context.Net.Get("https://example.com/api", new Dictionary<string, string> { ["Accept"] = "application/json" });
if (resp.IsSuccess) context.Log.Info(resp.Body);

var post = context.Net.Post("https://example.com/api", "{\"a\":1}");
```

* **同步接口**：宿主内部用 Task 跑完再阻塞返回，方便在任意线程调用；
  但**不要在 UI 线程调**（会卡界面）。
* 默认超时 15 秒；`Body` 按 UTF-8 解码；`Headers` 只读。

### 11.2 文件（`file-game-dir` / `file-user-pick`，需授权）

```csharp
string? gameDir = context.Files.GameDirectory;          // file-game-dir，没授权 → null
string? file = context.Files.PickOpenFile("*.json");     // file-user-pick，弹框让用户选
string? dir  = context.Files.PickFolder();
```

* 没授权时全部返回 `null`（**不抛异常**），插件要自己判空并降级。
* 自己的数据目录不受这些限制（`file-plugin-data` 默认允许）。

---

## 12. 持久化与宿主目录

| 路径 | 内容 |
| --- | --- |
| `PCL\Plugins\` | 插件包（`.pclpkg` / `.pclpkg.disabled`） |
| `PCL\PluginData\<id>\config.json` | 插件配置（宿主代写） |
| `PCL\PluginData\permissions.json` | 插件授权记录（插件 id → 能力 → granted/denied） |
| `PCL\PluginData\trusted-keys.json` | 签名信任公钥列表，见 §13 |
| `PCL\PluginData\PluginFaults.json` | 熔断失败计数，删掉即复位 |
| `PCL\PluginCache\` | 插件包解压缓存（可随时删） |
| `%APPDATA%\PCLCE\Plugins\` | 共享级插件目录 |

> 正式版部署规则：随包插件只有在**版本号比本地新**时才会覆盖；同版本或更旧一律保留用户状态。
> 所以改了内置插件内容必须**提升插件版本号**，否则用户的机器上不会更新。

---

## 13. 完整性与签名

### 13.1 文件哈希（`files`）

```json
"files": { "MyPlugin.dll": "9f2c…（小写 hex）" }
```

* 宿主在**安装时**与**每次加载前**校验；不一致 → `PluginIntegrityState.Mismatch` → **拒绝加载**。
* 老插件不写 `files` 也能加载（`Undeclared`）。
* 生成：`python3 tools/plugin-hash.py <包.pclpkg>`（直接改写包里的 `plugin.json`）。

### 13.2 签名（`signature`）

```json
"signature": { "alg": "ecdsa-p256-sha256", "keyId": "<公钥 SHA-256 指纹，小写 hex>", "value": "<base64>" }
```

**签名载荷**（必须与宿主 `PluginIntegrity.ComputePayload` 完全一致，UTF-8、`\n` 分隔）：

```
<id>\n<version>\n<文件名>:<sha256>…（文件名按 Ordinal 升序，哈希小写）
```

流程：

```bash
# 1) 生成密钥对（私钥自己藏好，公钥给信任列表）
dotnet run --project tools/PluginSigner -- genkey private.pem public.txt

# 2) 计算哈希 + 签名（写回包里的 plugin.json）
python3 tools/plugin-hash.py MyPlugin.pclpkg
dotnet run --project tools/PluginSigner -- sign MyPlugin.pclpkg private.pem

# 3) 本机验证
dotnet run --project tools/PluginSigner -- verify MyPlugin.pclpkg "PCL/PluginData/trusted-keys.json"
```

信任列表 `PCL\PluginData\trusted-keys.json`：

```json
{ "keys": [ "<SubjectPublicKeyInfo 的 Base64>", "…" ] }
```

* 文件不存在 = 不信任任何公钥 → **带签名的插件一律拒绝**（`UntrustedKey`），未签名插件照常可用。
* 验签失败 / 公钥不在列表 → `BadSignature` / `UntrustedKey` → 拒绝加载。
* 状态枚举：`Undeclared` / `Verified` / `Mismatch` / `UntrustedKey` / `BadSignature`，管理页会显示。

---

## 14. 更新检测

清单里写 `updateUrl`，指向一个返回下列 JSON 的地址：

```json
{ "version": "1.1.0", "url": "https://.../Foo.pclpkg", "sha256": "<可选，小写 hex>", "notes": "更新说明" }
```

流程（`Modules/Plugins/PluginUpdateService.cs`）：

1. 取 JSON → 2. 用 `PluginVersion.Compare` 比版本 → 3. 用户确认 → 4. 下载到临时文件 →
5. 声明了 `sha256` 就校验 → 6. `PluginInstaller.InstallPackage`（覆盖同名包、安装后默认启用）→ 7. 重扫。

下载下来的包同样要过完整性校验，所以**更新源被劫持也塞不进改过的包**（前提是包本身带 `files`/`signature`）。

---

## 15. 打包与发布

### 15.1 内置插件（随启动器）

```bash
./plugins/package.sh Release x64      # Termux / Linux
powershell -File tools\build.ps1      # Windows（内部会调用 plugins\package.ps1）
```

* 产物：`plugins/dist/<名字>.pclpkg`。
* **必须在 `dotnet publish` 之前打**：非正式版会把 `.pclpkg` 嵌进 exe（`IncludeBundledPlugins=true`），
  后打会导致 `CS1566`（找不到资源）。
* 当前内置清单（`plugins/package.sh` 里的循环）：`SguPack`、`PCLSCore`、`AdvancedMaterials`、
  `Welcome`、`FadedGray`、`HelloPlugin`（后者在 `samples/`）。
* `FadedGray` 按要求**不内置**：只在 `dist/` 里给一份供手动安装（`build.sh` 里显式跳过）。

### 15.2 外部插件

自己 zip：`plugin.json` + dll（+ 资源），扩展名改成 `.pclpkg`。
建议同时做 `files` 哈希；要放进社区分发就再做签名。

### 15.3 版本号纪律

* 插件 `version` 变化才会触发用户机器的覆盖部署（见 §12）。
* 内容改了但版本号没变 → 用户那边不会有任何变化（这是踩过的坑，最初甚至导致"删掉的插件还在"）。

---

## 16. 本地化

宿主不代管插件文案，只在 `IPluginContext.LanguageCode` 里告诉插件当前语言码。
语言码与宿主一致：`zh-CN`、`zh_sgu`、`zh-TW`、`en-US`、`en-GB`、`ja-JP`、`fr-FR`、`es-ES`。

```csharp
private readonly Dictionary<string, Dictionary<string, string>> _texts = new()
{
    ["page.title"] = new() { ["zh-CN"] = "我的页面", ["en-US"] = "My Page" }
};

private string T(string key) =>
    PluginLocalization.Pick(_texts.GetValueOrDefault(key), _context?.LanguageCode, key);
```

`PluginLocalization.Pick` 回退顺序：**精确语言 → 主语言（`zh-CN` → `zh`）→ `zh-CN` → 表里第一个非空值 → fallback**。
所以插件只写中文也能在其它语言下正常显示。

语言切换时宿主发 `LanguageChangedEvent`，插件收到后重建文案（页面标题由宿主缓存，需要重新注册或刷新）。

---

## 17. 功能键 `provides`

设计原则：**功能实现在宿主，插件只声明"功能存在"并给数据**。正式版不装插件时功能不可达。

`PluginFeatureKeys.All`：

| 键 | 含义 | 谁提供 |
| --- | --- | --- |
| `themes.extended` | 扩展主题（非蓝色主题 + 自定义主题色入口） | `SguPack` |
| `themes.custom-color` | 个性化页的自定义主题色输入 | `SguPack` |
| `echo-cave` | 回声洞（百宝箱里的打字机语录） | `SguPack` |
| `materials.advanced` | 高级材质（云母 / Aero / 液态玻璃） | `AdvancedMaterials` |
| `welcome.messages` | 启动时的欢迎语 | `Welcome` |
| `unlock.region` | 解除中国大陆地区下载限制 | `PCLSCore` |
| `unlock.offline-auth` | 解除「必须先绑正版才能建离线/外置档案」 | `PCLSCore` |

宿主侧用 `FeatureFlags.IsEnabled(key)` 决定是否展示；停用插件后功能立即消失（无需重启）。

---

## 18. 主题扩展

> 主题机制的实现细节另见 [`PLUGIN-THEMES.md`](PLUGIN-THEMES.md)。

两条路：

### 18.1 占用宿主内置槽位（`IExtraThemesProvider`）

`PluginFeatureProviders.cs` 里的 `PluginThemeDefinition`：`Key` 必须落在 `ColorTheme` 的扩展槽位上，
只能改名 / 主色 / 风格。适合"给已有槽位换配色"。

### 18.2 自带任意多个主题（`IPluginCustomThemesProvider`，需要 `themes.custom`）

```csharp
public IReadOnlyList<PluginCustomThemeDefinition> GetCustomThemes() =>
[
    new()
    {
        Id = "faded.gray",                  // 全局唯一，建议 "<插件id>.<主题名>"
        DisplayName = "Faded 灰",
        Hex = "#5C5C5C",
        Flavor = null,                      // 可选："golden-dusk" / "sakura-bloom"
        SortOrder = 300,
        CreateTitleDecoration = () => new Image { Source = ..., Opacity = 0.34, Width = 420 }
    }
];
```

* 宿主为每个自定义主题分配独立槽位枚举值（`ColorTheme.PluginDefined16` 起），
  并把它加进「设置 → 界面」的主题下拉框（`ThemeChoice(ColorTheme, string? PluginThemeId)`）。
* `CreateTitleDecoration` 返回的元素被放进标题栏右上角容器：**自己设** `HorizontalAlignment` /
  `Width` / `Height` / `Opacity`；宿主保证「不拦鼠标」「跟随主题显隐」「切换主题时重建」。
* 宿主内置主题（含素材复用）见 [`PLUGIN-THEMES.md`](PLUGIN-THEMES.md)；
  原版素材抽取工具：`tools/extract-vanilla-textures.py`。
* 主题信息事件 `ThemeChangedEvent` 见 §9。

---

## 19. 调试与排错

### 19.1 日志位置与前缀

启动器日志在 `<启动器目录>\PCL\Log*.log`（最近的几次启动各一份）。
插件相关行前缀：`[PluginHost]`（宿主）、`[Plugin:<id>]`（插件自己的日志）。

必看的几行：

| 日志 | 含义 |
| --- | --- |
| `[PluginHost] 已加载 <id> <ver>（API 1，能力=…）` | 加载成功 |
| `[PluginHost] <文件>：需要启动器 X 或更高（当前 Y）` | `minHostVersion` 不满足 |
| `[PluginHost] <文件>：API 版本不兼容（插件要求 N，宿主提供 1）` | `apiVersion` 写错 |
| `[PluginHost] <插件> 连续失败 3 次，已自动禁用` | 触发熔断 |
| `[PluginHost] 内置插件已存在，保留用户当前状态：<包名>` | 版本号没升，跳过部署 |
| `[Plugin] 读取 permissions.json 失败，按空表处理` | 权限文件损坏（会自动重建） |

### 19.2 常见错误表

| 现象 | 根因 / 处理 |
| --- | --- |
| 编译报 `CS1566`（找不到 `*.pclpkg` 资源） | 先跑 `plugins/package.sh`，再 `dotnet publish` |
| 状态 `Faulted`，`BadImageFormatException: Bad IL format` | 手工改过 zip，dll 被破坏；宿主会检测非 MZ 头并重建缓存，重新打包即可 |
| `The process cannot access the file because it is being used by another process` | 老的 `ZipFile.OpenRead` 占用；现用 `OpenArchiveShared()` 已修，别再自己开包 |
| 页面打不开 / 报 `XamlParseException` | 事件签名不对：`MyButton.Click` 是 `(object, MouseButtonEventArgs)`、`MyIconButton.Click` 是 `(object, EventArgs)`、`MyListItem.Check` 是 `(object, ModBase.RouteEventArgs)`（这一条只对宿主内部控件成立，插件用标准 WPF 控件即可） |
| 设置项渲染出来但改了不生效 | `Key` 与 `Get<T>` 用的键不一致，或 `Get<T>` 类型与写入类型不一致 |
| 两个侧栏条目能同时被选中 | 条目必须直接插进 `PanItem`（同一 `Parent.Children` 才会互斥） |
| 带签名的插件加载不了 | `trusted-keys.json` 不存在或公钥不在列表；`verify` 自检 |
| 更新后插件没变 | 插件 `version` 没升（见 §15.3） |
| 卸载后插件还在 | 目录形式的老插件残留；`PluginPackage.DeleteById` 会同时清掉老目录 |

### 19.3 调试手段

* 管理页齿轮：查看/切换能力授权、查看设置项、看错误文本。
* 「打开插件文件夹」定位 `PCL\Plugins`。
* 删 `PCL\PluginData\PluginFaults.json` 复位熔断。
* 删 `PCL\PluginCache\` 强制重新解压。
* 想知道宿主加载顺序：日志里 `[PluginHost]` 的加载顺序即拓扑序。

---

## 20. 兼容性铁律

1. **API 只做加法**：`PluginApiVersion.Current` 只在**破坏性**变更时 +1。
   新增接口成员 / 新增 DTO 字段**不**加版本（宿主是唯一实现者，插件是消费者）。
2. **插件不得引用** `PCL.Core` / `Plain Craft Launcher 2`；只能用 `PCL.PluginSdk` + BCL + WPF。
3. `PCL.PluginSdk` 在 ALC 里**回落到默认 ALC**，保证 `IPlugin` / `PluginManifest` 等类型身份唯一
   （否则插件实现出来的接口宿主认不出来）。
4. 插件程序集本身用**可回收 ALC** 加载，禁用时可卸载；但不要依赖"卸载后一切归零"。
5. 清单里未知字段 / 未知能力字符串必须被忽略（前向兼容），插件不要指望宿主拒绝它们。
6. 插件抛异常不能拖死宿主：所有回调都要 try/catch；宿主也会兜底。
7. 高危能力（`game-argv` / `process`）**不开放**；需要时必须先改文档 + 明确授权。

---

## 21. 内置插件与示例索引

| 插件 | 位置 | 能力 | provides | 作用 |
| --- | --- | --- | --- | --- |
| `SguPack` | `plugins/SguPack` | `config`, `ui.page`, `ui.sidebar` | `themes.extended`, `themes.custom-color`, `echo-cave` | 扩展主题 + 回声洞 |
| `PCLSCore` | `plugins/PCLSCore` | `config` | `unlock.region`, `unlock.offline-auth` | 解除地区 / 离线档案限制 |
| `AdvancedMaterials` | `plugins/AdvancedMaterials` | `config` | `materials.advanced` | 高级材质（云母/Aero/液态玻璃） |
| `Welcome` | `plugins/Welcome` | `config`, `events`, `ui.settings` | `welcome.messages` | 启动欢迎语 |
| `FadedGray` | `plugins/FadedGray` | `config`, `themes.custom` | — | 自带主题示例（**不内置**，仅供手动安装） |
| `HelloPlugin` | `samples/HelloPlugin` | `config`, `events`, `ui.page`, `ui.sidebar`, `ui.settings`, `ui.notify`, `minecraft.read` | — | **全功能最小示例**（配置/设置/页面/事件/本地化各一份） |

`HelloPlugin` 的详情：`samples/HelloPlugin/HelloPlugin.cs`，清单见同目录 `plugin.json`；
它的 `files` 哈希由 `tools/plugin-hash.py` 生成，随仓库提交。

---

## 22. 发布前检查清单

- [ ] `plugin.json`：`apiVersion` = 1，`minHostVersion` 合理，`entry.type` 是全名且有无参构造
- [ ] **版本号已提升**（否则用户机器不会更新）
- [ ] 每条用到的能力都写进了 `capabilities`；未知/拼错的能力会在管理页显示不出来
- [ ] 页面 `CreatePage` 每次返回新实例；控件不引用启动器程序集
- [ ] 所有回调 try/catch；耗时操作不在 UI 线程
- [ ] 配置键名点分小写，`Get<T>` 类型一致
- [ ] `files` 哈希已生成（`tools/plugin-hash.py`）
- [ ] 需要分发则做签名，并把公钥加进用户 `trusted-keys.json`（或随包提供）
- [ ] 更新源 `updateUrl` 的 JSON 契约正确（`version` / `url` / 可选 `sha256`）
- [ ] 8 种语言至少不崩（`PluginLocalization.Pick` 有兜底）
- [ ] 日志里能看到 `已加载 …`，管理页状态是「已启用」
- [ ] 涉及的文档已同步更新（`docs/PLUGIN-API.md`、`docs/PLUGIN-GUIDE.md`、`docs/PLUGIN-THEMES.md`）
