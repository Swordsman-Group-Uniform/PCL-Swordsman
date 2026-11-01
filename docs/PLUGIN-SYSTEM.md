# PCL2 插件化改造技术方案（PCLCE For SGU）

> **设计文档**：含主题机制等实现细节，与 `PLUGIN-API.md` / `PLUGIN-GUIDE.md` / `PLUGIN-DEV.md` 配套阅读。

> 状态：**设计稿（v0.1）**　适用范围：本下游分支
> 目标读者：接手维护者、插件开发者
> 相关：`VERSIONING.md`（API 版本号规则）、`docs/HANDOVER.md`（项目结构）

> ⚠️ **现状已超越本设计稿（snapshot-22 起）**。本文档写于 M0 之前，其中「插件以目录形式放在
> `PCL/Plugins/<id>/`」的布局已被**插件包模型**取代：
>
> - **一个插件 = `PCL/Plugins/` 下的一个 `.pclpkg` 文件**（zip：`plugin.json` + `<id>.dll`）
> - 禁用 = 文件名改成 `Foo.pclpkg.disabled`；删除 = 删掉这个文件
> - 加载时解包到 `PCL/PluginData/cache/...`，包文件不被占用
> - `.pclpkg` 可直接拖进主窗口安装；旧的 `PCL/Plugins/<id>/` 目录会被启动时自动迁移
>
> 以 [`PLUGIN-API.md`](PLUGIN-API.md)（接口与功能键现状）与 [`PLUGIN-GUIDE.md`](PLUGIN-GUIDE.md)
> （制作指南）为准；本文档仅作架构与路线参考（§4 目录布局、§5 加载器实现细节已过时）。

---

## 0. 一句话方案

**把"页面 / 侧边栏 / 设置项 / 事件 / 下载任务"抽象成可注册的扩展点，插件以 `.NET 类库 + plugin.json 清单` 的形式放进 `PCL/Plugins/<id>/`，由宿主的 `PluginHost` 用独立 `AssemblyLoadContext` 加载，契约走单独的 `PCL.PluginSdk` 程序集，能力（权限）在清单里声明、由管理页授权。**

---

## 1. 目标与非目标

### 1.1 目标

| # | 目标 | 验收标准 |
| --- | --- | --- |
| G1 | 插件能**新增左侧页面**并出现在侧边栏（含分组小标题） | 装一个示例插件，重启后「工具 → 插件」下出现其页面 |
| G2 | 插件能**订阅宿主事件**（启动、实例选择、游戏启动前后、下载完成、配置变更、主题变更） | 示例插件在事件里写日志可见 |
| G3 | 插件能有**自己的配置**，宿主负责持久化（YAML/JSON） | 重启后插件配置保留 |
| G4 | 插件能**只读访问 Minecraft 实例信息**（版本、加载器、Mod 列表……） | 示例插件把当前实例版本显示在页面上 |
| G5 | 插件出故障**不拖垮宿主**（异常隔离、超时、连续崩溃自动禁用） | 故意抛异常的插件被禁用且启动正常 |
| G6 | **插件市场**能列出、搜索、下载、安装、更新插件 | 静态索引 + `.pclpkg` 走通全流程 |
| G7 | 插件**不得绕过**本分支底线（更新/反馈/遥测/主题门禁） | 插件 API 不暴露这些能力，且运行期二次校验 |

### 1.2 非目标（明确不做，避免范围爆炸）

- 不做"插件改宿主私有 UI 树"（hook `VisualTree`、注入 Harness）——只提供声明式扩展点。
- 不做跨进程沙箱（虚拟机级隔离）；安全模型是"**信任 + 能力 + 签名**"，不是"任意代码隔离"。
- 不支持 Windows 以外的平台（宿主本身是 WPF）。
- 不为了插件改上游既有文件的结构（目标是**新增文件为主**，见 §12 改动清单）。

---

## 2. 三种插件形态的取舍

| 方案 | 优点 | 缺点 | 结论 |
| --- | --- | --- | --- |
| **A. 进程内 .NET 程序集**（`AssemblyLoadContext` 隔离） | 全功能、直接用 WPF、性能好、调试简单 | 崩溃可拖垮宿主（需 try/catch 兜底）、ALC 卸载不彻底 | ✅ **采用** |
| B. 进程外 IPC（独立进程 + 命名管道） | 真隔离、崩溃不影响宿主 | UI 扩展几乎不可行（跨进程窗口）、序列化成本高、调试复杂 | 预留为"高危插件"档位（M5 之后可选） |
| C. 脚本（Lua / JS / C# Script） | 安全、可热重载 | 表达力弱、WPF 绑定困难、需引入运行时 | ❌ 不做（可作为插件内部的实现细节，宿主不管） |

**结论**：A 为主。用"能力授权 + 签名校验 + 崩溃熔断"来补偿隔离性的不足。

---

## 3. 总体架构

```
┌──────────────────────────── PCL (宿主, net10.0-windows) ────────────────────────────┐
│                                                                                      │
│  FormMain / PageToolsLeft / PageSetupLeft …（既有 UI）                                │
│        ▲ 动态生成条目                        ▲ 注册页面                             │
│  ┌─────┴────────────────────────────────────┴──────────────────────────────────┐    │
│  │                PCL.PluginHost（新项目，宿主内引用）                          │    │
│  │  PluginLoader ─ 发现/校验/依赖解析/ALC 加载/生命周期                          │    │
│  │  PluginRegistry ─ 已加载插件 + 能力表 + 状态机                                │    │
│  │  ExtensionRegistry ─ 页面/侧边栏/设置区/主页卡片/命令                          │    │
│  │  HostEventBus ─ 强类型事件（GameLaunch、InstanceSelected、DownloadDone…）      │    │
│  │  PluginHostServices ─ 日志/配置/文件/HTTP/通知/Minecraft 只读/下载挂接        │    │
│  │  PluginMarketClient ─ 索引拉取/下载/校验/安装（静态托管）                     │    │
│  └───────────────────────────────┬──────────────────────────────────────────────┘    │
│                                  │ 只通过 PCL.PluginSdk 里的接口交互                 │
└──────────────────────────────────┼───────────────────────────────────────────────────┘
                                   │
┌──────────────────────────────────┴───────────────────────────────────────────────────┐
│                     PCL.PluginSdk（独立程序集，仅接口 + DTO）                         │
│   IPlugin · IPluginContext · IPluginLogger · IPluginConfig · IHostEvents              │
│   IPageRegistry · ISidebarRegistry · ISettingsRegistry · INotification                │
│   IFileService · IHttpService · IMinecraftService · IDownloadService · IThemeInfo      │
│   PluginManifest(DTO) · PluginCapability(enum) · PluginApiVersion(常量)               │
└──────────────────────────────────────────────────────────────────────────────────────┘
                                   │ 引用（compile only）
┌──────────────────────────────────┴───────────────────────────────────────────────────┐
│  MyPlugin（第三方）: MyPlugin.dll + plugin.json  →  PCL/Plugins/my-plugin/            │
└──────────────────────────────────────────────────────────────────────────────────────┘
```

### 3.1 为什么契约要单独一个程序集

插件不能引用 `Plain Craft Launcher 2.exe`（单文件发布，没有可引用的托管 dll），而且引用宿主会让 ALC 出现类型标识不一致（同一接口加载两份 → `InvalidCastException`）。

所以：

- `PCL.PluginSdk.dll` **只含接口与 DTO**，不含实现；
- 宿主加载插件时，对 `PCL.PluginSdk` 走 \*\*默认 ALC（回落）\*\*，保证 `IPlugin` 类型唯一；
- 插件其余依赖（第三方库）由插件自带，加载进**插件的 ALC**。

```csharp
// PluginHost 里的关键：SDK 必须回落，避免类型身份分裂
internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private static readonly HashSet<string> SharedAssemblies = new(StringComparer.OrdinalIgnoreCase)
    {
        "PCL.PluginSdk",          // 契约：必须宿主与插件共用
    };

    public PluginLoadContext(string pluginDllPath, string pluginId)
        : base($"plugin:{pluginId}", isCollectible: true)
        => _resolver = new AssemblyDependencyResolver(pluginDllPath);

    protected override Assembly? Load(AssemblyName name)
    {
        if (SharedAssemblies.Contains(name.Name!)) return null;      // 回落到默认 ALC
        var path = _resolver.ResolveAssemblyToPath(name);
        return path is null ? null : LoadFromAssemblyPath(path);
    }
}
```

---

## 4. 插件包与目录布局

### 4.1 磁盘布局

```
<启动器目录>/PCL/
├─ Plugins/                      ← 实例级插件（跟启动器走）
│  ├─ my-plugin/
│  │  ├─ plugin.json             ← 清单（必须有）
│  │  ├─ MyPlugin.dll            ← 入口程序集（清单 entry.assembly 指定）
│  │  ├─ MyPlugin.deps.json      ← 有私有依赖时必须
│  │  ├─ lib/                    ← 私有依赖（可选）
│  │  ├─ assets/                 ← 插件素材（可选）
│  │  └─ icon.png                ← 市场/管理页图标（可选，建议 128×128）
│  └─ ...
└─ PluginData/                   ← 插件运行时数据（配置、缓存），宿主分配
   ├─ my-plugin/
   │  ├─ config.json
   │  └─ cache/
```

- **共享级安装**（可选，M4）：`%APPDATA%\PCLCE\Plugins/`（所有实例共享），优先级低于实例级。
- **禁用**：不改目录名（避免路径失效），用宿主状态文件 `PluginData/plugins.state.json` 记录 `enabled`。
- **删除**：先禁用 → 标记 `pendingDelete` → 下次启动删除（避免删除正在使用的文件）。

### 4.2 `plugin.json`（清单，schema v1）

```json
{
  "$schema": "https://pclce-sgu.example/plugin-schema-v1.json",
  "id": "com.example.helloworld",
  "name": "Hello World",
  "version": "1.0.0",
  "apiVersion": 1,
  "minHostVersion": "27.1.0",
  "author": "示例作者",
  "description": "演示插件：注册一个页面、订阅启动事件、保存一个配置项。",
  "homepage": "https://example.com/hello",
  "license": "MIT",
  "entry": {
    "assembly": "MyPlugin.dll",
    "type": "MyPlugin.HelloPlugin"
  },
  "dependencies": [],
  "capabilities": [
    "ui.sidebar",
    "ui.page",
    "config",
    "events"
  ],
  "ui": {
    "icon": "assets/icon.png",
    "sortOrder": 100
  },
  "targets": {
    "host": ["PCLCE-SGU"]
  }
}
```

| 字段 | 必填 | 说明 |
| --- | --- | --- |
| `id` | ✅ | 反向域名，全局唯一；目录名建议同名 |
| `version` | ✅ | 插件自身语义化版本 |
| `apiVersion` | ✅ | 插件依赖的**宿主扩展 API 版本**（整数，见 §9） |
| `minHostVersion` | ⬜ | 最低宿主版本（`27.1.0` 起算） |
| `entry.assembly` / `entry.type` | ✅ | 入口程序集与实现 `IPlugin` 的类型全名 |
| `dependencies` | ⬜ | `[{ "id": "...", "version": ">=1.2.0" }]`，宿主做拓扑排序 |
| `capabilities` | ✅ | 申请的能力（§7），安装时展示、可撤销 |
| `ui.icon` / `ui.sortOrder` | ⬜ | 市场与管理页展示 |

### 4.3 `.pclpkg` 打包格式

```
my-plugin-1.0.0.pclpkg  (= zip)
├─ plugin.json
├─ MyPlugin.dll
├─ lib/…
├─ assets/…
└─ META-INF/
   ├─ files.sha256     ← 每行 "<sha256>  <相对路径>"
   └─ signature.ed25519（可选，见 §8.3）
```

安装流程：解压到临时目录 → 校验 `plugin.json` → 校验 `files.sha256` → （有签名则验签）→ 查 id 冲突 → 原子移动到 `Plugins/<id>/`。

---

## 5. 加载器与生命周期

### 5.1 状态机

```
Discovered ──(清单合法)──► Validated ──(依赖满足)──► Resolved
    │                          │                        │
    │ 清单坏 → Faulted          │ 依赖缺失 → PendingDependency
    ▼                          ▼                        ▼
  跳过（记录原因）        等待/禁用                 加载 ALC → Loaded
                                                         │
                                    IPlugin.Initialize(ctx) → Initialized
                                                         │
                                             IPlugin.Enable() → Enabled
                                                         │
                          IPlugin.Disable() / IPlugin.Dispose() → Disabled / Unloaded
```

### 5.2 启动时序（关键：不要拖慢启动）

1. 宿主 `Application` 初始化完成后（`LifecycleState.Running`）→ `PluginHost.BootstrapAsync()` 运行在**后台线程**。
2. 后台线程：发现 → 清单校验 → 依赖排序 → ALC 加载 → 反射创建实例 → 调 `Initialize`（**不给 UI 线程**，只允许注册声明）。
3. 需要 UI 的注册（页面/侧边栏条目）通过 `Dispatcher.InvokeAsync` 回主线程；**必须幂等**。
4. 主窗口显示后再调用各插件 `Enable()`（可在后台线程），保证"插件慢不影响进主页"。
5. 单个插件总超时 **5 s**（初始化）/ 2 s（事件回调），超时 = 该次调用被丢弃并记录。

### 5.3 卸载

`AssemblyLoadContext(isCollectible: true)` + `Unload()`：

- 现实约束：WPF 资源、静态事件、`DispatcherTimer` 会让 ALC 无法回收；
- 因此正式版**卸载 = 标记禁用 + 重启生效**；开发模式提供"热重载"（重建 ALC，允许泄漏，仅调试用）。

### 5.4 崩溃熔断

| 计数 | 行为 |
| --- | --- |
| 单次异常 | 记录日志（插件名 + 堆栈 + 事件名），继续 |
| 5 分钟内 ≥3 次未处理异常 | 自动禁用该插件，Hint 通知用户，写 `plugins.state.json` |
| 初始化异常 | 直接禁用（不加载） |

---

## 6. 宿主扩展 API（契约）

> 全部定义在 `PCL.PluginSdk`，`apiVersion = 1`。以下为**完整接口草案**，实现时按此签名落地。

### 6.1 插件入口

```csharp
namespace PCL.PluginSdk;

public interface IPlugin
{
    /// 初始化：注册页面/侧边栏/设置项/事件，禁止在这里做重活
    void Initialize(IPluginContext context);

    /// 启用：可以启动后台任务、定时器
    void Enable() { }

    /// 禁用：停止任务
    void Disable() { }

    /// 卸载前清理（事件订阅必须在这里取消，或用 IDisposable 自动取消）
    void Dispose() { }
}
```

### 6.2 上下文

```csharp
public interface IPluginContext
{
    string PluginId { get; }
    Version PluginVersion { get; }
    string DataDirectory { get; }        // <启动器>/PCL/PluginData/<id>/（已创建）
    bool HasCapability(PluginCapability capability);

    IPluginLogger Log { get; }                       // 日志
    IPluginConfig Config { get; }                    // 配置（自动持久化）
    IHostEvents Events { get; }                      // 事件订阅
    IPageRegistry Pages { get; }                     // 新增页面
    ISidebarRegistry Sidebar { get; }                // 侧边栏条目 / 分组
    ISettingsRegistry Settings { get; }              // 设置页附加分区
    INotificationService Notify { get; }             // 提示条 / 弹窗（需 ui.notify）
    IFileService Files { get; }                      // 受限文件访问
    IHttpService Http { get; }                       // 受限 HTTP
    IMinecraftService Minecraft { get; }             // 只读实例信息
    IDownloadService Download { get; }               // 挂接自定义下载任务
    IThemeInfo Theme { get; }                        // 只读主题信息
    IPluginServices Services { get; }                // 插件间服务注册/获取
    IDisposable OnDispose(Action action);            // 生命周期辅助
}
```

### 6.3 事件

```csharp
public interface IHostEvents
{
    /// 宿主启动完成
    IDisposable OnStartup(Action<HostStartupEventArgs> handler);
    /// 切换 Minecraft 实例
    IDisposable OnInstanceSelected(Action<InstanceSelectedEventArgs> handler);
    /// 游戏启动前（可修改 JVM 参数 / 游戏参数，需 game.argv 能力）
    IDisposable OnGameLaunching(Action<GameLaunchArgs> handler);
    /// 游戏启动成功 / 退出
    IDisposable OnGameStarted(Action<GameProcessInfo> handler);
    IDisposable OnGameExited(Action<GameProcessInfo> handler);
    /// 下载任务完成（宿主内置下载器）
    IDisposable OnDownloadCompleted(Action<DownloadCompletedEventArgs> handler);
    /// 配置变更（只读通知）
    IDisposable OnConfigChanged(Action<ConfigChangedEventArgs> handler);
    /// 主题 / 配色变更
    IDisposable OnThemeChanged(Action<ThemeChangedEventArgs> handler);
    /// 页面切换
    IDisposable OnPageChanged(Action<PageChangedEventArgs> handler);
    /// 自定义事件（插件之间）
    IPluginServices Services { get; }
}
```

> 返回 `IDisposable` 便于 `using` 自动退订；插件 `Dispose` 时宿主**强制清空**其全部订阅。

### 6.4 页面与侧边栏

```csharp
public interface IPageRegistry
{
    /// 注册一个"右侧页面"。返回可用于跳转的句柄。
    IRegisteredPage Register(PluginPageDescriptor descriptor);

    /// 注册一个"带左侧导航的页面组"（工具 / 设置风格的二级页面）
    void RegisterGroup(PluginPageGroupDescriptor descriptor);
}

public sealed record PluginPageDescriptor
{
    public required string Id { get; init; }                 // 插件内唯一
    public required string TitleKey { get; init; }            // 本地化键，宿主回落到插件提供的标题
    public string? Title { get; init; }                       // 直给标题（无本地化时）
    public required string Icon { get; init; }                // lucide 名 或 "file:assets/icon.png"
    public string? Tooltip { get; init; }
    public double LogoScale { get; init; } = 0.9;
    public int SortOrder { get; init; } = 100;
    public required string Section { get; init; }             // 侧边栏分组：tools / settings / custom:<id>
    public required Func<IPluginContext, PluginPage> PageFactory { get; init; }
}

/// 插件页面：宿主提供 WPF 宿主容器，插件只返回内容控件（推荐做法）
public abstract class PluginPage
{
    /// 返回页面内容（会被放进宿主的滚动区域 + 卡片容器）
    public abstract FrameworkElement CreateContent();

    /// 页面进入 / 离开（可选）
    public virtual void OnEnter() { }
    public virtual void OnLeave() { }
}
```

> **安全设计**：插件**不直接继承 `MyPageRight`**，而是返回 `FrameworkElement`，由宿主包一层 `PluginPageHost : MyPageRight`。这样插件无法接触宿主页面的内部字段，也方便日后换渲染方式。

```csharp
public interface ISidebarRegistry
{
    /// 在已有分组里追加条目（如 tools 下）
    IRegisteredPage AddItem(string section, PluginPageDescriptor descriptor);
    /// 新增一个分组（灰色小标题 + 条目）
    void AddSection(SidebarSectionDescriptor descriptor);
}

public sealed record SidebarSectionDescriptor
{
    public required string Id { get; init; }        // "plugins"
    public required string TitleKey { get; init; }  // 灰色小标题
    public int SortOrder { get; init; } = 100;
}
```

### 6.5 配置

```csharp
public interface IPluginConfig
{
    T Get<T>(string key, T defaultValue);
    void Set<T>(string key, T value);
    /// 变更即落盘（去抖 300ms），写入 <DataDirectory>/config.json
    event Action<string, object?> Changed;
    void Save();
}
```

> 插件也可以用强类型：SDK 提供 `PluginConfigSection<T>` 基类（`System.Text.Json` 源生成友好），但**不强制**。

### 6.6 通知

```csharp
public interface INotificationService
{
    void Info(string message, string? key = null);
    void Success(string message, string? key = null);
    void Warn(string message, string? key = null);
    void Error(string message, string? key = null);
    Task<bool> ConfirmAsync(string message, string title);
    Task<string?> PromptAsync(string message, string title, string? defaultValue = null);
}
```

### 6.7 文件 / 网络（受能力约束）

```csharp
public interface IFileService
{
    /// 只能访问宿主允许的根（插件 DataDirectory、实例 .minecraft、用户选择的路径）
    string GetDataPath(params string[] parts);
    bool Exists(string path);
    Task<string> ReadAllTextAsync(string path);
    Task WriteAllTextAsync(string path, string content);
    /// 弹选择框（需 ui.dialog）
    Task<string?> PickFileAsync(string filter);
    Task<string?> PickFolderAsync();
}

public interface IHttpService
{
    /// 只允许 GET/POST，UA 统一为宿主 UA，受宿主代理配置
    Task<string> GetStringAsync(string url);
    Task<byte[]> GetBytesAsync(string url);
    Task<bool> DownloadFileAsync(string url, string savePath, IProgress<double>? progress = null);
}
```

### 6.8 Minecraft 只读 + 下载挂接

```csharp
public interface IMinecraftService
{
    /// 当前选中的实例（可能为 null）
    IMinecraftInstance? Selected { get; }
    IReadOnlyList<IMinecraftInstance> Instances { get; }
    IDisposable OnInstancesReloaded(Action handler);
}

public interface IMinecraftInstance
{
    string Name { get; }
    string VersionName { get; }
    string LoaderName { get; }        // Vanilla / Forge / Fabric / NeoForge …
    string Path { get; }              // 实例目录
    IReadOnlyList<IMinecraftModInfo> Mods { get; }
}

public interface IDownloadService
{
    /// 把一个自定义下载任务塞进宿主下载中心（复用宿主并发/重试/进度 UI）
    IDownloadTaskHandle Enqueue(PluginDownloadTask task);
}
```

---

## 7. 能力（权限）模型

```csharp
[Flags]
public enum PluginCapability
{
    None = 0,
    UiPage = 1 << 0,          // 注册页面
    UiSidebar = 1 << 1,       // 注册侧边栏条目/分组
    UiSettings = 1 << 2,      // 注册设置分区
    UiNotify = 1 << 3,        // 提示条/弹窗
    Config = 1 << 4,          // 自有配置（默认允许）
    Events = 1 << 5,          // 订阅宿主事件
    FilePluginData = 1 << 6,  // 访问自己的 DataDirectory（默认允许）
    FileGameDir = 1 << 7,     // 访问 .minecraft / 实例目录（读写）
    FileUserPick = 1 << 8,    // 用户选择的任意路径
    NetHttp = 1 << 9,         // 出站 HTTP
    GameArgv = 1 << 10,       // 修改启动参数（高危）
    Process = 1 << 11,        // 启动外部进程（高危）
    ThemeRead = 1 << 12       // 读取主题（写主题仍不开放；扩展主题改为「提供数据 + provides 声明」）
}
```

| 规则 | 说明 |
| --- | --- |
| 默认拒绝 | 清单未声明 = 不授予 |
| 首次安装提示 | 管理页展示"该插件申请：页面、侧边栏、网络访问…" |
| 可撤销 | 管理页可单独关掉某项能力，插件调用时抛 `PluginCapabilityException` |
| 高危双确认 | `GameArgv` / `Process` 需要二次确认 |
| 不可申请 | 无任何能力能开启更新/反馈/遥测/关闭主题门禁 |

---

## 8. 安全

### 8.1 信任层级

| 来源 | 默认状态 |
| --- | --- |
| 市场（索引里有签名） | 已签名 → 默认允许安装；无签名 → 需确认 |
| 本地 `.pclpkg` / 文件夹 | 用户主动安装 → 需确认 |
| 解压目录里凭空出现的插件 | **不自动加载**，仅在管理页显示"未受管插件" |

### 8.2 哈希校验

`META-INF/files.sha256` 覆盖除 `META-INF/` 外全部文件；安装与**每次启动**都校验（可配置跳过以提速，默认启动只校验 dll + json）。

### 8.3 签名

- 算法：**Ed25519**（.NET 8+ 内置 `System.Security.Cryptography`）。
- 签名内容：`files.sha256` 的 SHA-256。
- 公钥来源：市场索引里按 `authorId` 给出公钥指纹；本地安装可用"信任此发布者"。

### 8.4 与下游底线的硬约束

插件即使拿到 `Process` 能力也**不能**：

1. 调用 `UpdateManager.IsUpdateSupported` 相关入口 —— 该类型**不出现在 SDK** 里，且宿主对更新入口做了 `IsUpdateSupported == false` 短路（插件无法绕过）；
2. 打开反馈/引导上游 issue —— 同上，`ModBase.IsFeedbackSupported` 在宿主内恒 false；
3. 恢复遥测 —— `TelemetryService.IsTelemetrySupported` 恒 false；
4. ~~让非蓝色主题在发行版可见 —— 主题只读，且 `ThemeService.IsThemeSelectable()` 的门禁在宿主侧。~~
   **【2026-10-25 修订（snapshot-19）】政策变更**：屏蔽改由「插件是否提供功能」控制，
   主题 / 回声洞 / 限制解除由 `plugin.json` 的 `provides` 声明（见 `PluginFeatureKeys`），
   宿主在 `FeatureFlags` 里记录后放行。因此「装了插件就能用」是**预期行为**，
   安全性改由**分发策略**保证：正式版本体不随包附带任何插件，插件包只放在 `dist/plugins/` 供用户手动安装；
   快照 / 预发布等非正式版才会随包自动安装。
   实现（主题引擎、回声洞卡片、登录与下载判定）**仍然全部在宿主里**，插件只提供数据与声明，
   宿主始终掌握渲染与门禁。

> 兜底：`PluginHost` 对插件创建的任何**窗口**插入一次"下游项目声明"检查（M4 再做，MVP 只做文档约束）。

---

## 9. 版本与兼容

| 版本 | 含义 | 当前 |
| --- | --- | --- |
| `apiVersion` | 宿主扩展 API 大版本（不兼容即 +1） | `1` |
| `plugin.version` | 插件自身语义化版本 | — |
| `minHostVersion` | 需要的最小宿主版本 | 如 `27.1.0` |
| 宿主版本 | `Basics.Metadata.Version.BaseName`（如 `27.1-snapshot-9`） | — |

规则：

1. `apiVersion` 只在**破坏性**变更时 +1；新增接口/字段不 +1（订阅者模式）。
2. 插件 `apiVersion != 宿主 apiVersion` → 拒绝加载，管理页显示"需要更新"。
3. `minHostVersion` 高于当前 → 拒绝加载。

---

## 10. 插件市场（Market）

### 10.1 托管形态（不依赖任何 PCL 服务器）

静态文件托管（GitHub Pages / 对象存储 / CDN 均可），三个文件：

```
https://<your-host>/index.json          ← 索引（带 ETag / 可分页）
https://<your-host>/packages/<file>.pclpkg
https://<your-host>/schema/index-v1.json
```

### 10.2 `index.json`

```json
{
  "schemaVersion": 1,
  "generatedAt": "2026-10-25T00:00:00Z",
  "plugins": [
    {
      "id": "com.example.helloworld",
      "name": "Hello World",
      "summary": "演示插件",
      "description": "Markdown 长描述",
      "author": { "name": "示例作者", "id": "example", "publicKey": "base64-ed25519-pub" },
      "categories": ["utility", "ui"],
      "tags": ["demo"],
      "apiVersion": 1,
      "minHostVersion": "27.1.0",
      "icon": "https://<host>/icons/hello.png",
      "homepage": "https://example.com/hello",
      "versions": [
        {
          "version": "1.0.0",
          "publishedAt": "2026-10-20T00:00:00Z",
          "file": "packages/helloworld-1.0.0.pclpkg",
          "sha256": "…",
          "size": 123456,
          "changelog": "首个版本",
          "signature": "base64-ed25519-sig"
        }
      ]
    }
  ]
}
```

### 10.3 市场页交互

搜索（本地过滤 id/name/tags/author）→ 详情（描述 Markdown + 版本列表 + 权限清单）→ 安装/更新（下载 → 校验 → 解压 → 提示重启生效）→ 已安装标记。

离线策略：索引缓存到 `PluginData/.market/index.json`，无网时展示缓存 + "无法检查更新"。

---

## 11. UI 落点（本仓库现状）

| 位置 | 现状 | 插件化后 |
| --- | --- | --- |
| 侧边栏 | `PageToolsLeft.xaml` 手写条目 | 保留内置条目，**下方追加插件分组**（动态生成） |
| 页面工厂 | `PageGet` 里 `switch` 硬编码 | `switch` 命中内置；未命中则查 `ExtensionRegistry` 的插件页 |
| 页面枚举 | `FormMain.PageSubType`（每个 Tab 内部从 0/1 起编号） | 插件页用**高位段** `0x4000 + n`，避免与内置冲突 |
| 设置页 | `PageSetupLeft` 硬编码 | 插件分组插在「关于」之后 |
| 首页 | `PageLaunchRight` | M4：主页卡片扩展点 |

---

## 12. 落地改动清单（尽量少碰上游文件）

### 12.1 新增（互不干扰，未来同步上游时直接保留）

```
PCL.PluginSdk/                       ← 新项目：接口 + DTO + PluginApiVersion
PCL.PluginHost/                      ← 新项目：加载器/注册表/事件总线/服务实现/市场客户端
Plain Craft Launcher 2/
├─ Controls/PluginPageHost.xaml(.cs) ← 把插件返回的 FrameworkElement 包成 MyPageRight
├─ Pages/PageTools/PageToolsPluginManage.xaml(.cs)   ← 插件管理页
├─ Pages/PageTools/PageToolsPluginMarket.xaml(.cs)   ← 插件市场页
└─ Modules/Plugins/PluginHostBootstrap.cs            ← 启动接线（单文件）
docs/PLUGIN-SYSTEM.md                ← 本文档
docs/PLUGIN-DEV-GUIDE.md             ← 插件开发指南（M2 完成后补）
```

### 12.2 修改（共 4 个文件，改动都集中在可定位的短段落）

| 文件 | 改动 |
| --- | --- |
| `Plain Craft Launcher 2.slnx` | 加入两个新项目引用 |
| `Plain Craft Launcher 2/Plain Craft Launcher 2.csproj` | `ProjectReference` 到 `PCL.PluginHost` |
| `Pages/PageTools/PageToolsLeft.xaml(.cs)` | 追加「插件」分组；`PageGet/PageCheck` 支持动态页 |
| `Modules/ModMain.cs` | 两个页面字段（或改为字典） |
| `FormMain.xaml.cs` | 启动时 `PluginHostBootstrap.Start()`；`PageSubType` 追加插件区段常量 |

> 目标：**M1–M3 期间不修改任何上游业务代码**（`ModLaunch`、`ModDownload` 等一律不动）。

---

## 13. 里程碑

| 阶段 | 内容 | 交付物 | 现状（2026-10-31，snapshot-68） |
| --- | --- | --- | --- |
| **M0** | 侧边栏「插件」分组 + 插件管理/插件市场两个页面骨架 + 本文档 | 可点击的空壳页面 | ✅ 完成 |
| **M1** | `PCL.PluginSdk` + `PCL.PluginHost`：发现/清单/ALC 加载/生命周期/日志/配置 | 示例插件已被成功加载并持久化配置 | ✅ 完成（并已超出：拓扑依赖 + `minHostVersion` 强制，原计划挂在 M2） |
| **M2** | 页面/侧边栏扩展点 + `PluginPageHost` + 动态页路由 | 插件能新增页面并出现在侧边栏 | ✅ 完成（`IPluginUiProvider` + `PagePluginHost` + `PageSubType.ToolsPluginPage`，snapshot-59） |
| **M3** | 事件总线（启动/实例/游戏/下载/主题/配置）+ Minecraft 只读服务 | 示例插件显示当前实例信息 | ✅ **完成**（snapshot-69）：事件 8 个（启动 / 实例切换 / 游戏启动 / 游戏完成 / 下载开始 / 下载完成 / 主题 / 语言）+ `IMinecraftService`；示例插件页面已显示当前实例的版本 / 加载器 / mods 数 |
| **M4** | 文件/网络能力 + 权限 UI + 管理页完整功能（启用/禁用/卸载/更新/打开目录） | 管理页可用 | ✅ **完成**（snapshot-69）：管理页全套 + `net-http` / `file-game-dir` / `file-user-pick` API + **首次使用授权弹窗与 `permissions.json`** + 管理页权限区 + **崩溃熔断** + **共享级安装目录**；高危 `game-argv` / `process` **按用户要求不做** |
| **M5** | 插件市场（索引/搜索/下载/校验/签名）+ 发布工具 `pcl-plugin pack` | 市场页可用 | 🟡 **校验/签名/发布工具已提前完成**（`files` 哈希 + ECDSA 签名 + 信任列表 + `tools/plugin-hash.py` + `tools/PluginSigner`，snapshot-68；发布工具相当于计划里的 `pcl-plugin pack`）；**市场本身未开始**（页面仍是空壳：无索引格式、无搜索、无下载安装） |

**一句话结论（snapshot-69）：M0–M4 全部完成（高危 `game-argv` / `process` 按用户要求不做），M5 只剩「插件市场」本体没做——索引/搜索/下载安装；校验、签名、发布工具已完成。**
按里程碑口径可以说是**「M4 进行中」**，但离 M3 的验收句（插件显示当前实例信息）还差一个只读服务。

### 13.1 M1 实现现状（与本文档的差异）

| 计划项 | 实际实现 |
| --- | --- |
| `IPluginContext` | 已含 `PluginId` / `PluginVersion` / `HostVersion` / `DataDirectory` / `Capabilities` / `HasCapability` / `Log` / `Config`；页面/事件/服务等在 M2/M3 以**新增成员**方式补 |
| 清单校验 | id / version / entry.assembly / entry.type / 入口文件存在性 / `apiVersion == 1`；`minHostVersion` 仅记录不强制 |
| 依赖解析 | M1 只解析字段，**不做拓扑排序与环检测**（M2 随页面注册一起补） |
| 崩溃熔断 | M1 只有 try/catch + 状态记录，**没有**「5 分钟 3 次自动禁用」（M4 补） |
| 卸载 | `Disable` → `Dispose` → `AssemblyLoadContext.Unload()`；正式版仍以「重启生效」为准 |
| 管理页 | 已完整套用「实例设置 → 模组」的管理方式：搜索 / 打开文件夹 / 从文件安装 / 下载新插件（跳市场）/ 全选 / 筛选（全部·已启用·已禁用·出错）/ 排序 / 底部多选栏（启用·禁用·删除·取消）；`PluginInstaller` 负责 `.pclpkg` 安装与卸载 |
| 调试钩子 | 环境变量 `PCL_FORCE_PLUGIN_PAGE=1` 强制打开插件管理页（无人值守验证 UI 用） |
| 示例插件 | `samples/HelloPlugin`（清单 + 程序集 + 说明见 `samples/README.md`） |

---

## 14. 测试计划

| 类型 | 用例 |
| --- | --- |
| 单元测试 | 清单校验（缺字段/坏 JSON/坏版本）、依赖拓扑排序与环检测、能力校验、哈希校验、签名验签、版本比较 |
| 集成测试 | 加载正常插件；加载抛异常插件（宿主不崩 + 熔断）；加载 apiVersion 不匹配插件；插件页注册/路由 |
| 手工测试 | 安装/禁用/卸载/重启生效；插件页切换动画；主题切换下插件页配色；无网时市场页降级 |
| 回归 | 全部内置页面仍可正常进入（`PageSubType` 未被污染） |

---

## 15. 风险与对策

| 风险 | 影响 | 对策 |
| --- | --- | --- |
| ALC 卸载不彻底 | 禁用后内存仍在 | 明确"重启生效"；开发模式才热重载 |
| 插件 UI 破坏外观一致性 | 观感割裂 | 页面由宿主包 `MyCard` + 主题画刷，插件只给内容 |
| 插件崩溃拖垮宿主 | 启动失败 | 全链路 try/catch + 超时 + 熔断 + 初始化放后台线程 |
| 上游同步冲突 | 维护成本 | 新代码集中在 2 个新项目 + 5 处短改动（§12.2） |
| 插件变成"绕过底线"的口子 | 合规风险 | SDK 不暴露更新/反馈/遥测/主题写；宿主侧二次校验（§8.4） |
| 市场被投毒 | 供应链 | 哈希 + Ed25519 签名 + 作者公钥指纹 + 手动确认 |
| 插件页与内置页编号冲突 | 路由错乱 | 插件页使用 `0x4000+` 高位段 |

---

## 16. 示例插件（M2 验收用）

```csharp
using PCL.PluginSdk;

namespace MyPlugin;

public sealed class HelloPlugin : IPlugin
{
    private IPluginContext _ctx = null!;
    private IDisposable? _sub;

    public void Initialize(IPluginContext context)
    {
        _ctx = context;
        context.Log.Info("Hello 插件已初始化");

        context.Pages.Register(new PluginPageDescriptor
        {
            Id = "hello",
            Title = "Hello",
            TitleKey = "myplugin.page.hello",
            Icon = "lucide/puzzle",
            Section = "tools.plugins",
            SortOrder = 10,
            PageFactory = _ => new HelloPage(context)
        });

        _sub = context.Events.OnStartup(e => context.Log.Info($"宿主启动：{e.HostVersion}"));
    }

    public void Enable()
    {
        var count = _ctx.Config.Get("launchCount", 0) + 1;
        _ctx.Config.Set("launchCount", count);
        _ctx.Log.Info($"这是第 {count} 次启用");
    }

    public void Dispose() => _sub?.Dispose();
}

internal sealed class HelloPage(IPluginContext ctx) : PluginPage
{
    public override FrameworkElement CreateContent()
    {
        var instance = ctx.Minecraft.Selected;
        return new System.Windows.Controls.TextBlock
        {
            Margin = new System.Windows.Thickness(20, 20, 20, 20),
            TextWrapping = System.Windows.TextWrapping.Wrap,
            Text = $"你好！当前实例：{instance?.Name ?? "（未选择）"}"
        };
    }
}
```

---

## 17. 命名与代码风格约定

- 命名空间：`PCL.PluginSdk`（契约）、`PCL.PluginHost`（实现）、插件自定。
- 所有对外类型 `public`，所有宿主内部实现 `internal`。
- 可空性开启（与宿主一致）。
- 禁止在 SDK 里引用 WPF 以外的宿主类型；WPF 类型只允许 `FrameworkElement` 等基础类型。
- 插件日志统一加前缀 `[Plugin:<id>]`，便于过滤。

---

*本文档随实现推进持续更新；M1 完成后补 `docs/PLUGIN-DEV-GUIDE.md`（面向插件作者的操作手册）。*
