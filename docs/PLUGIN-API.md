# 插件接口参考（PCLCE For SGU）

> 适用版本：`27.1-snapshot-59(2.15.1)` 及以后 · 契约 API 版本 **1**
> 相关文档：[`PLUGIN-GUIDE.md`](PLUGIN-GUIDE.md)（怎么做插件）、[`PLUGIN-SYSTEM.md`](PLUGIN-SYSTEM.md)（内部设计笔记，**不随社区文档发行**）

本文写清楚三件事：

1. **插件现在能调用 / 能提供什么**（已实现的接口，逐条列出签名与限制）；
2. **清单字段与能力（权限）怎么声明**；
3. **排错速查**。

---

## 0. 三个程序集

| 程序集 | 谁引用 | 作用 |
| --- | --- | --- |
| `PCL.PluginSdk.dll` | 插件 + 宿主 | **契约**：`IPlugin`、`IPluginContext`、清单模型、能力枚举、功能键、各类 Provider 接口。版本稳定，只增不改 |
| `PCL.PluginHost.dll` | 宿主 | **加载器**：包扫描、解包缓存、`AssemblyLoadContext` 加载、启停、安装/卸载、依赖与版本校验。插件**不要**引用 |
| `Plain Craft Launcher 2.dll` | —— | 宿主本体。插件**不要**引用（否则上游一改就崩） |

契约程序集由宿主提供：插件工程的 `ProjectReference` 必须带 `Private=false` + `ExcludeAssets=runtime`，
输出目录里**不能**出现 `PCL.PluginSdk.dll`，否则同一个类型会在两个 `AssemblyLoadContext` 里各有一份，
表现为 `InvalidCastException`（见 `samples/HelloPlugin/HelloPlugin.csproj`）。

目标框架固定 `net10.0-windows`（`UseWPF=true`），`apiVersion` 必须等于 `PluginApiVersion.Current`（当前 `1`）。

---

## 1. 生命周期与热开关

```
发现 .pclpkg → 读清单 → 校验清单 / 宿主版本 / 依赖
   → 按依赖拓扑序解包到缓存 → ALC 加载程序集 → 反射创建 entry.type 实例
   → Initialize(context) → Enable()
禁用（改名为 .pclpkg.disabled）→ Disable() → Dispose() → 卸载 ALC
```

- **启用 / 禁用 = 文件改名**：`Foo.pclpkg` ⇄ `Foo.pclpkg.disabled`，不写数据库、不改清单。
- 切换后宿主会重新扫描并重载插件（`PluginHostBootstrap.Reload()`），`Initialize` 会再跑一次。
  需要区分「首次」与「重启」时，用 `context.Config` 落一个标记。
- `PluginState`：`Discovered` / `Loaded` / `Enabled` / `Disabled` / `Faulted` / `Incompatible`。
  管理页把 `Faulted`、`Incompatible` 归到「出错」筛选，并把原因直接显示在条目副信息里。
- 插件的**实例只应有一个**，且必须有无参构造函数。
- **依赖与宿主版本是真的会拦的**：
  `minHostVersion` 高于宿主 → `Incompatible`；缺依赖 / 依赖被禁用 / 依赖版本不满足 / 循环依赖 → `Faulted`。
  依赖先加载，依赖加载失败会连带停用依赖它的插件。

---

## 2. `plugin.json` 全部字段

```jsonc
{
  "id": "sgu.sample.hello",        // 必填，全局唯一，建议 反向域名.名字；同时是数据目录名
  "name": "Hello 插件（示例）",      // 必填，管理页显示名
  "version": "1.0.0",              // 必填，SemVer
  "apiVersion": 1,                 // 必填，必须等于宿主 PluginApiVersion.Current
  "minHostVersion": "27.1.0",      // 选填，宿主版本低于它 → 不兼容，不予加载
  "author": "PCLCE For SGU",       // 选填
  "description": "……",             // 选填
  "homepage": "https://…",         // 选填
  "license": "Apache-2.0",         // 选填
  "entry": {                       // 必填
    "assembly": "HelloPlugin.dll", // 包内程序集文件名
    "type": "MyPlugin.HelloPlugin" // 实现 IPlugin 的类型全名
  },
  "dependencies": [                // 选填，缺了就报错（见下）
    { "id": "sgu.other.plugin", "version": ">=1.0.0" }
  ],
  "capabilities": ["config", "events", "ui.page"],  // 能力声明，见 §4
  "provides": ["echo-cave"],       // 功能键声明，见 §5.1
  "ui": { "icon": "lucide/puzzle", "sortOrder": 100 }, // 选填，管理页图标与默认排序（见下）
  "files": {                       // 选填，包内文件的 SHA-256（见 §9）
    "HelloPlugin.dll": "<sha256 小写十六进制>"
  },
  "signature": {                   // 选填，ECDSA P-256 签名（见 §9）
    "alg": "ecdsa-p256-sha256",
    "keyId": "<公钥 SHA-256 指纹>",
    "value": "<Base64 签名>"
  },
  "updateUrl": "https://…/update.json"  // 选填，更新源（见 §10）
}
```

字段解析实现见 `PCL.PluginSdk/PluginManifest.cs`，版本与依赖判定见 `PCL.PluginHost/PluginVersion.cs`。要点：

- `capabilities` 用 `"ui.page"`、`"ui-page"`、`"UiPage"` 三种写法都能解析（忽略大小写与分隔符）；
  未知字符串被忽略，不会导致加载失败（前向兼容）。
- `provides` 里的功能键**区分语义**：写错等于没写，管理页不会提示——对照 §5.1 的表抄。
- `dependencies[].version` 支持：留空 / `*`（任意）、`1.2.0`（≥）、`>=`、`>`、`<=`、`<`、`=`、`^1.2`、`~1.2`。
- `minHostVersion` 只比数字段（`27.1-snapshot-59(2.15.1)` 取 `27.1`），后缀不参与比较。
- 清单必须放在包**根目录**，或包内**唯一一层**目录里；`entry.assembly` 按包内相对路径解析。
- `ui.icon` 必须指向**内置图标包里真实存在**的名字（`lucide/` 后接图标名，如 `lucide/sparkles`）；
  写错/写空会退回默认的 `lucide/puzzle`（宿主会校验资源是否存在，不会白屏）。
- `ui.sortOrder` 是管理页「按名称」排序时的**主键**（小的在前，缺省 100），同档再按名称。

---

## 3. 代码里能拿到的东西

### 3.1 `IPlugin`

```csharp
public interface IPlugin
{
    void Initialize(IPluginContext context);  // 注册、读配置；不要做耗时操作
    void Enable() { }                          // 启动后台任务 / 定时器
    void Disable() { }                         // 停后台任务
    void Dispose() { }                         // 取消事件订阅、释放资源
}
```

四个方法都有默认空实现，最小插件只需 `Initialize`。
宿主对每个回调都兜底 try/catch，异常会记 `[Plugin:<id>]` 并让插件进入 `Faulted`。

### 3.2 `IPluginContext`

| 成员 | 类型 | 说明 |
| --- | --- | --- |
| `PluginId` | `string` | 清单里的 `id` |
| `PluginVersion` | `string` | 清单里的 `version` |
| `HostVersion` | `string` | 宿主展示版本，如 `27.1-snapshot-59(2.15.1)` |
| `DataDirectory` | `string` | `<启动器>\PCL\PluginData\<id>\`，已创建，随便写 |
| `Capabilities` | `PluginCapability` | 合并后的能力 flags |
| `HasCapability(PluginCapability)` | `bool` | 默认接口实现，建议用它判断而不是 `&` |
| `Log` | `IPluginLogger` | 见下 |
| `Config` | `IPluginConfig` | 见下 |
| `Events` | `IPluginEventBus` | 宿主事件订阅，见 §5.3 |
| `LanguageCode` | `string` | 当前界面语言码（`zh-CN` / `zh-TW` / `en-US` …），文案按语言出时用它 |
| `Notify` | `IPluginNotify` | 弹提示 / 确认框（需要 `ui.notify`），见 §5.6 |
| `Minecraft` | `IMinecraftService` | Minecraft 只读信息（需要 `minecraft.read` + 授权），见 §5.8 |
| `Net` | `IPluginHttp` | 出站 HTTP（需要 `net-http` + 授权），见 §5.9 |
| `Files` | `IPluginFiles` | 游戏目录 / 选择路径（需要 `file-game-dir` / `file-user-pick` + 授权） |

**注意**：`IPluginContext` 是「只增不改」的契约，新增成员都带默认实现，
现在编译好的插件在后续版本里仍然能加载。

### 3.3 `IPluginLogger`

```csharp
context.Log.Trace / Debug / Info / Warn / Error(string message)
context.Log.Error(string message, Exception ex)
context.Log.Log(PluginLogLevel level, string message)
```

宿主统一加 `[Plugin:<id>]` 前缀，写进 `PCL\Log\Launch-*.log`。

### 3.4 `IPluginConfig`

```csharp
T Get<T>(string key, T defaultValue);
void Set<T>(string key, T? value);   // 内部已落盘
bool Remove(string key);
event Action<string>? Changed;        // 参数是键名
void Save();                          // 一般不用手调
```

- 落盘位置：`<插件数据目录>\config.json`，UTF-8 JSON。
- 键名建议点分小写（`ui.showBadge`）。值走 `System.Text.Json` 序列化，**别塞不可序列化的对象**。
- 卸载插件时**不会**自动删数据目录——这是刻意的，方便重装后恢复设置。
- **想让用户在界面上改配置，不要自己写设置页**：实现 `IPluginSettingsProvider`（§5.2），
  宿主「插件管理 → 齿轮」会自动生成控件并写进这份配置。

---

## 4. 能力（`capabilities`）

`PluginCapability` 是 `[Flags]`（`PCL.PluginSdk/PluginCapability.cs`）：

| 枚举 | 字符串写法 | 含义 | 状态 |
| --- | --- | --- | --- |
| `Config` | `config` | 拥有自己的配置 | ✅ 生效 |
| `FilePluginData` | `file-plugin-data` | 读写自己的数据目录 | ✅ 生效（默认就有） |
| `Events` | `events` | 订阅宿主事件 | ✅ **生效**：没声明时订阅会被忽略并记警告 |
| `UiPage` | `ui.page` | 注册右侧页面（侧栏条目随页面一起出现） | ✅ **生效**：没声明时页面被忽略 |
| `UiSidebar` | `ui.sidebar` | 注册侧边栏条目 | ✅ 与 `ui.page` 等价，**两者声明其一即可** |
| `UiSettings` | `ui.settings` | 设置页「插件」分区里出现该插件的设置卡片 | ✅ **生效**：没声明就只在「插件管理 → 齿轮」里出现 |
| `UiNotify` | `ui.notify` | 弹提示条 / 确认框 | ✅ **生效**：没声明时调用只记警告、不弹 |
| `FileGameDir` | `file-game-dir` | 读写实例目录 | ⏳ 未实现 |
| `FileUserPick` | `file-user-pick` | 用户选择路径后读写 | ⏳ 未实现 |
| `NetHttp` | `net-http` | 出站 HTTP | ⏳ 未实现（宿主不代发请求） |
| `GameArgv` | `game-argv` | 改启动参数（高危） | ⏳ 未实现 |
| `Process` | `process` | 起外部进程（高危） | ❌ 不开放（高危，暂无计划） |
| `MinecraftRead` | `minecraft.read` | 读实例信息（只读） | ✅ **生效**：没声明返回空服务；首次使用要用户授权 |
| 参考：`file-game-dir` / `file-user-pick` / `net-http` | 同上 | 读游戏目录 / 让用户选路径 / 联网 | ✅ **生效**：可声明可用，但**必须用户授权**后才真正生效 |

**现状要诚实**：`events` / `ui.page` / `ui.settings` / `ui.notify` 已经在宿主侧**真实门禁**（不声明就不给），
其余标注 ⏳ 的能力目前只是「声明 + 记录」，宿主还没有对应的 API 可以让它生效。
插件跑在宿主进程内，**没有沙箱**：能干的坏事和你手动跑一个 exe 一样多，来源必须可信。

---

## 5. 插件能提供的东西（Provider 接口）

宿主在插件加载完成、以及每次重扫之后，会调用下面这些接口取数据。

| 接口 | 何时被调用 | 返回 |
| --- | --- | --- |
| `IEchoCaveQuotesProvider.GetQuotes()` | 启动时、插件重载后 | `IReadOnlyList<string>` |
| `IWelcomeMessagesProvider.GetWelcomeMessages()` | 同上 | `IReadOnlyList<string>` |
| `IPluginSettingsProvider.GetSettings()` | 用户点开「插件管理 → 齿轮」时 | `IReadOnlyList<PluginSettingDefinition>` |
| `IPluginUiProvider.GetPages()` | 每次进入侧栏时 | `IReadOnlyList<PluginPageDefinition>` |
| `IPluginEventBus.Subscribe<T>()` | 插件在 `Initialize` 里自己订阅 | `IDisposable` |
| `IPluginNotify`（`context.Notify`） | 插件随时调用 | 无（提示即时显示） |

### 5.1 功能键（`provides`）：插件能「解锁」什么

功能键不提供代码能力，而是**告诉宿主「这些功能可以用」**。实现留在宿主，插件只给数据。
宿主启动时把已启用插件的功能键汇总成 `FeatureFlags`（`PCL.Core/App/FeatureFlags.cs`）。

| 功能键 | 解锁内容 |
| --- | --- |
| `echo-cave` | 百宝箱里的「回声洞」打字机语录（语录由插件提供） |
| `unlock.region` | 解除下载页「中国大陆地区」屏蔽 |
| `unlock.offline-auth` | 解除「先绑定正版账号才能建离线/外置档案」 |
| `materials.advanced` | 云母 / Aero 窗口材质、标题栏与主界面不透明度、悬停发光；个性化页出现「高级材质（窗口）」卡片 |
| `welcome.messages` | 启动欢迎语：插件提供池（`IWelcomeMessagesProvider`），宿主等概率抽一条用右下角小提示显示 |

### 5.2 插件设置（`IPluginSettingsProvider`）

插件只**声明结构**，读写由宿主通过你的 `IPluginConfig` 完成 —— 你不用写界面、也不用管保存：

```csharp
public sealed class PluginSettingDefinition
{
    public string Key { get; set; }                            // 配置键
    public string Title { get; set; }                          // 显示名
    public string? Description { get; set; }                   // 说明
    public PluginSettingType Type { get; set; }                // Toggle / Text / Integer / Decimal / Choice
    public string? DefaultValue { get; set; }                  // 字符串形式的默认值
    public string? Placeholder { get; set; }
    public double? Min { get; set; }
    public double? Max { get; set; }
    public List<PluginSettingOption> Options { get; set; } = []; // Choice 用
}
```

- 宿主渲染成 `MyCheckBox` / `MyTextBox` / `MyComboBox` / `MySlider`，改完立刻落盘；
- 插件侧读回来就是 `context.Config.Get<T>(key, 默认值)`；
- 需要 `config` 能力（默认允许）。

### 5.3 宿主事件（`context.Events`）

```csharp
IDisposable subscription = context.Events.Subscribe<GameLaunchedEvent>(e => { /* ... */ });
```

| 事件 | 何时发布 | 字段 |
| --- | --- | --- |
| `HostStartupCompletedEvent` | 插件全部就绪、主窗口已加载 | `HostVersion`、`LaunchCount` |
| `GameLaunchingEvent` | 即将启动实例（进程还没起） | `InstanceName`、`InstancePath` |
| `GameLaunchedEvent` | 启动流程走完 | `InstanceName` |
| `LanguageChangedEvent` | 语言切换（宿主已发布） | `LanguageCode` |
| `InstanceSwitchedEvent` | 当前选中的实例变化 | `InstanceName`、`InstancePath` |
| `DownloadStartedEvent` | 一个下载类任务开始（任务栏级别的粗粒度任务） | `TaskName` |
| `DownloadFinishedEvent` | 下载类任务结束 | `TaskName`、`Success` |

- 需要 `events` 能力；没声明时 `Subscribe` 返回空订阅并记一条警告，**不会抛异常**。
- 回调在**发布方所在线程**执行（宿主的事件基本都在 UI 线程）——耗时操作请自己切后台，不要卡 UI。
- 回调抛异常只会记 `[PluginHost] 插件 <id> 的事件回调抛出异常`，不影响其它插件。
- 插件卸载 / 重扫时宿主自动清空全部订阅；也可以自己 `subscription.Dispose()` 提前退订。

### 5.4 插件页面（`IPluginUiProvider`）

```csharp
public sealed class PluginPageDefinition
{
    public string Id { get; set; }                                        // 插件内唯一
    public string Title { get; set; }                                     // 侧栏 + 页面标题
    public string? Description { get; set; }                              // 页面顶部说明
    public string? Icon { get; set; }                                     // lucide/xxx，留空用默认图标
    public int SortOrder { get; set; } = 100;
    public Func<IPluginContext, FrameworkElement>? CreatePage { get; set; } // 每次打开都新建
}
```

- 需要 `ui.page` 能力；不声明时页面被忽略并记日志。
- 宿主负责：侧栏条目（「工具 → 插件 → 插件页面」）、切换动画、可滚动容器、标题栏与「来源插件」标注、
  创建失败时的兜底卡片。
- 你负责：**每次返回新控件**（不要复用同一个实例）、控件里的事件自己 try/catch、页面工厂里别做 IO。
- `Icon` 同样会被校验（不存在的名字退回清单 `ui.icon`，再退回 `lucide/puzzle`）。
- 插件被禁用 / 删除后，条目与已打开的页面会立刻收掉并退回插件管理页。

### 5.5 弹提示 / 确认框（`context.Notify`）

```csharp
context.Notify.Info("已完成");            // 右下角小提示
context.Notify.Success("装好了");
context.Notify.Warning("有点问题");
context.Notify.Error("失败了");

context.Notify.Confirm("标题", "正文？", result => {
    context.Log.Info(result ? "用户点了确定" : "用户点了取消");
});
```

- 需要 `ui.notify` 能力；没声明时调用**不弹**，只在 `[Plugin:<id>]` 日志里留一条警告；
- 在哪个线程调用都行，宿主会把界面操作切回主线程；
- `Confirm` 的回调**在用户点掉按钮后**于主线程执行；它和启动器内部确认框一样会阻塞界面，
  所以别在 UI 线程里连环弹，也别在必须保持响应的循环里用它；
- 提示前缀会自动带上 `[插件id]`，方便用户看出是哪个插件在说话。

### 5.6 设置页分区（`ui.settings`）

- 实现 `IPluginSettingsProvider`（§5.2）并声明 `ui.settings` 后，插件设置会**同时**出现在：
  「设置 → 插件」页（每个插件一张卡片）与「工具 → 插件 → 插件管理」的齿轮面板；
- 两处用同一份渲染实现（`PluginSettingsForm`），改一处两边同时生效；
- 没声明 `ui.settings` 的插件只在管理页齿轮里可见，不会污染设置页。

### 5.7 文案本地化约定

宿主不代管插件的文案表（插件字符串是插件自己给的），只提供**当前语言**与一个挑选工具：

```csharp
IReadOnlyDictionary<string, string> title = new Dictionary<string, string>
{
    ["zh-CN"] = "Hello 示例",
    ["zh-TW"] = "Hello 範例",
    ["en-US"] = "Hello Sample"
};

var text = PluginLocalization.Pick(title, context.LanguageCode, "Hello 示例");
```

- `PluginLocalization.Pick` 的回落顺序：**精确语言 → 主语言（`zh-CN` → `zh`）→ `zh-CN` → 表里第一个值 → 形参兜底**，
  所以插件只写中文也不会在其它语言下变成空白；
- 页面标题 / 设置项文字 / 其它由宿主渲染的字符串，都在**宿主来取的时候**生成
  （页面每次打开都会重新调 `GetPages()`，语言切换后重新打开即生效）；
- 语言切换时宿主会发 `LanguageChangedEvent`，插件可以据此刷新自己的缓存文案。

### 5.8 Minecraft 只读服务（`minecraft.read`）

```csharp
var instance = context.Minecraft.CurrentInstance;   // 可能为 null（未选实例 / 未授权）
if (instance is not null)
    context.Log.Info($"{instance.Name} / {instance.VersionName} / mods {instance.ModCount}");

foreach (var item in context.Minecraft.Instances) { /* 已加载的实例快照 */ }
var mcDir = context.Minecraft.GameDirectory;        // 游戏根目录
```

- `PluginInstanceInfo`：`Name` / `Path` / `VersionName` / `LoaderName` / `LoaderVersion` / `ModCount`（`mods` 里 `.jar` 计数）；
- 数据是**快照**：用户在启动页看到什么，插件就看到什么；启动页还没加载过时列表可能为空；
- 没声明 `minecraft.read` 时拿到的是空服务（不会抛异常，但字段全空）。

### 5.9 网络与文件（`net-http` / `file-game-dir` / `file-user-pick`）

```csharp
var response = context.Net.Get("https://example.com/api");
if (response.IsSuccess) context.Log.Info(response.Body);

var dir = context.Files.GameDirectory;      // 需要 file-game-dir + 授权
var picked = context.Files.PickOpenFile("JSON|*.json");  // 需要 file-user-pick + 授权
```

- 这三项**光声明能力还不够**：宿主会在**第一次真正用到**时弹一次确认框，结果记进
  `PCL\PluginData\permissions.json`（拒绝过的不会再问，想改去「插件管理 → 齿轮 → 权限」）；
- 没授权时 `Net.Get` 返回 `StatusCode = 0` 与说明文字，`Files` 返回 `null`——插件据此降级即可；
- 受支持的方法：`Get` / `Post`（同步返回，宿主内部异步等待，超时默认 15 秒、可传 `timeoutSeconds`）。

---

## 6. 文件与目录约定

```
<启动器目录>\
├─ PCLCE For SGU 27.1-snapshot-59(2.15.1).exe
├─ PCL\
│  ├─ Plugins\                    ← 用户/宿主放插件包的唯一位置
│  │  ├─ Foo.pclpkg
│  │  └─ Bar.pclpkg.disabled      ← 已禁用（就是个改名）
│  ├─ PluginData\
│  │  ├─ <id>\config.json         ← 插件配置（卸载不删）
│  │  ├─ cache\<包名>-<hash>\      ← 解包缓存，可随时清
│  │  └─ pending-delete.json      ← 「文件被占用，下次启动再删」的登记表
│  └─ Log\Launch-*.log            ← 插件日志在这里搜 [Plugin:<id>]
├─ plugins\                       ← 非正式版构建时随包分发的种子插件包（首次启动自动安装）
└─ dist\plugins\                  ← 发布包同样带一份
```

- 「安装」= 把包**复制**进 `PCL\Plugins\`，默认启用。
- **文件名由插件 `id` 决定**（`<id>.pclpkg` / `<id>.pclpkg.disabled`），不采用源文件名：
  `Foo-1.2.0.pclpkg`、`Foo(3).pclpkg` 装进去都叫 `Foo.pclpkg`。同一个 `id` 只会保留一份。
- 启动时会自动去重：历史遗留的同 `id` 多份文件会被合并成一份，**禁用优先**。
- 「删除」= 删掉该 `id` 的**全部**文件。文件被占用时登记 `pending-delete.json`，下次启动先删。
- 只读文件（从压缩包/共享目录复制来的）会被自动清掉只读标记再操作。
- `PluginData\cache\` 是纯派生数据：删掉只会让下次启动慢一点；残缺缓存会被识别并重新解压。

---

## 7. 插件不能做什么（硬边界）

1. 不能改「更新 / 自动更新 / 更新检测 / 云控公告」——这些在代码层已禁用。
2. 不能恢复「反馈」入口或引导用户去 PCL Community / PCL2-CE 提 issue。
3. 不能引入依赖 PCL 中继 / 大厅服务器的联机实现（联机固定 EasyTier 纯 P2P）。
4. 不能引用宿主程序集 `Plain Craft Launcher 2.dll`——上游一升级插件就崩，且会破坏 ALC 隔离。
5. 不能自带 `PCL.PluginSdk.dll`（类型身份分裂，见 §0）。
6. 插件跑在宿主进程内，**没有沙箱**。别装来路不明的包。

### 7.1 反例与正例：插件怎么「改启动器行为」

官方插件 `plugins\PCLSCore`（限制解除）演示了唯一被推荐的做法：**反射改写宿主里明确公开的静态状态**。

- 启动器侧（`Modules\Base\DownloadPolicy.cs`）只有公开静态属性 `AllowDownloads`、
  `AllowMinecraftDownload`、`InstallModpackAfterDownload` 与 `Changed` 事件，**完全不读插件清单**；
- 插件侧 `Enable()` 用 `Assembly.GetType("PCL.DownloadPolicy")` → `GetProperty(...).SetValue(null, true)`
  → 调 `NotifyChanged()`，`Disable()` 反向改回；
- 好处：插件禁用/卸载即恢复原状，启动器没有任何「检测到某个插件就开闸」的分支（那种设计等于后门）。

反面做法（不要学）：在启动器里加 `if (PluginHostManager.IsEnabledByUser("xxx")) unlock()`。

---

## 8. 排错速查

| 现象 | 原因 | 处理 |
| --- | --- | --- |
| 管理页「出错」，副信息「缺少 plugin.json」 | 包根目录没有清单，或包了一层多余目录 | 重新打包，`plugin.json` 放根 |
| 「API 版本不兼容」 | `apiVersion` ≠ 1 | 改成 `1`，或等宿主升级 |
| 「需要启动器 x.y.z 或更高」 | `minHostVersion` 高于当前宿主 | 降低清单要求，或升级启动器 |
| 「缺少依赖插件 / 依赖插件已被禁用 / 版本不满足 / 循环依赖」 | 清单 `dependencies` 与实际不符 | 装齐依赖、启用依赖，或修正版本范围 |
| 「无法加载程序集」 | 目标框架不是 `net10.0-windows` / 程序集名与 `entry.assembly` 不符 | 对齐 csproj 与清单 |
| `InvalidCastException`（`IPlugin` 转不过去） | 插件自带了一份 `PCL.PluginSdk.dll` | `Private=false` + `ExcludeAssets=runtime` 重编 |
| 页面不出现 | 没声明 `ui.page` 能力，或 `CreatePage` 为空 | 补能力、补工厂；日志里有「已忽略」记录 |
| 订阅收不到事件 | 没声明 `events` 能力 | 补能力；日志里有警告 |
| 插件页面只有错误卡片 | `CreatePage` 抛异常或返回 null | 看日志 `[Plugin] 打开 <id>/<page> 的页面失败` |
| 启用点了没反应 | 文件被占用 / 没有写权限 | 关掉占用程序；宿主会提示具体错误 |
| 删了但还在列表 | 文件被占用，已登记下次启动删 | 重启启动器 |

---

## 9. 完整性与签名（`files` / `signature`）

宿主在**安装时**与**每次加载前**都会校验，声明的项过不了就**拒绝加载**（管理页显示「校验失败」）。
没声明这两个字段的老插件照常可用（管理页显示「未校验」）。

### 9.1 文件哈希（`files`）

- 键是包内相对路径（如 `HelloPlugin.dll`），值是**解压后内容**的 SHA-256（小写十六进制）；
- 只声明其它文件，**不要**把 `plugin.json` 自己算进去（它装着哈希）；
- 生成方式：`python3 tools/plugin-hash.py <包.pclpkg>`（仓库里 `plugins/package.sh` 打包后会自动跑）。

### 9.2 签名（`signature`）

- 算法目前只支持 `ecdsa-p256-sha256`；
- 签名载荷（UTF-8，`\n` 分隔，顺序固定）：
  ```
  <id>\n<version>\n<文件名>:<sha256>…
  ```
  文件名按 Ordinal 升序、哈希小写 —— 宿主与工具必须完全一致；
- `keyId` = 公钥（SubjectPublicKeyInfo）字节的 SHA-256，小写十六进制；
- 宿主只认**信任列表里的公钥**：`PCL\PluginData\trusted-keys.json`
  ```json
  { "keys": [ "<SubjectPublicKeyInfo 的 Base64>" ] }
  ```
  文件不存在 = 没有信任任何公钥，此时带签名的插件**会被拒绝加载**（未签名的不受影响）；
- 生成密钥、签名、验证都用仓库里的工具（.NET，无需 openssl）：

  ```bash
  # 1) 生成密钥对（私钥留着，公钥贴进 trusted-keys.json）
  dotnet run --project tools/PluginSigner -- genkey my-plugin.pem my-plugin.pub.txt

  # 2) 给包写入 files 哈希并签名
  dotnet run --project tools/PluginSigner -- sign dist/MyPlugin.pclpkg my-plugin.pem

  # 3) 本地自检（可带信任列表）
  dotnet run --project tools/PluginSigner -- verify dist/MyPlugin.pclpkg PCL/PluginData/trusted-keys.json
  ```

- 宿主验签同时接受 IEEE P1363 与 DER（RFC 3279）两种签名格式，用 openssl 之类的工具签也能验。

---

## 10. 更新检测（`updateUrl`）

清单里写 `updateUrl` 后，「工具 → 插件 → 插件管理」里该插件多一个 **↻ 检查更新** 按钮：

1. 宿主 GET 该地址，期望返回：
   ```json
   { "version": "1.2.0", "url": "https://…/Foo.pclpkg", "sha256": "<可选，整包哈希>", "notes": "<可选，更新说明>" }
   ```
2. 版本比当前清单**高**才提示（`PluginVersion.Compare`，只比数字段）；
3. 用户确认 → 下载到临时文件 → 声明了 `sha256` 就校验整包哈希 →
   `PluginInstaller` 安装（同样要过 §9 的完整性与签名）→ 自动重扫。
4. 没有 `updateUrl` 的插件，按钮不显示，更新靠用户手动替换包文件。

> 更新源是你自己托管的一个静态 JSON，不需要插件市场。
