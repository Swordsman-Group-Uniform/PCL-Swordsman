# 插件制作指南（PCLCE For SGU）

> 面向：想给启动器写插件的人 · 契约 API v1 · 适用 `27.1-snapshot-23(2.15.1)` 及以后
> 接口细节看 [`PLUGIN-API.md`](PLUGIN-API.md)；本文件只讲「怎么做出一个能用的包」。

---

## 0. 先搞清楚三件事

1. **插件就是一个 `.pclpkg` 文件**（本质是 zip）。启动器不认文件夹形式的插件，
   也不认 `.jar`、`.dll`、`.exe`——拖进来的非 `.pclpkg` 文件会直接提示无法处理。
2. **启用 / 禁用 = 改文件名**。`Foo.pclpkg` 是启用，`Foo.pclpkg.disabled` 是禁用，
   在管理页点一下就是这个操作，你也可以在资源管理器里手动改，效果一样。
3. **插件跑在启动器进程内**，没有沙箱。装别人的插件等于把电脑交给它，来源不可信就别装。

---

## 1. 十分钟做出第一个插件

以仓库里的 `samples/HelloPlugin` 为模板（可直接抄结构，它已是包格式）。

### 1.1 建工程

```
MyPlugin\
├─ MyPlugin.csproj
├─ MyPlugin.cs
├─ plugin.json
└─ README.md          （可选）
```

`MyPlugin.csproj`：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0-windows</TargetFramework>
    <LangVersion>14</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <EnableDynamicLoading>true</EnableDynamicLoading>
    <Platforms>x64</Platforms>
    <AssemblyName>MyPlugin</AssemblyName>
  </PropertyGroup>

  <ItemGroup>
    <!-- 关键：Private=false + ExcludeAssets=runtime，否则会复制一份 SDK 进输出目录 -->
    <ProjectReference Include="..\..\PCL.PluginSdk\PCL.PluginSdk.csproj"
                      Private="false"
                      ExcludeAssets="runtime" />
  </ItemGroup>
</Project>
```

> 没有仓库源码？把 `PCL.PluginSdk.dll` 拷到插件工程旁边，改成
> `<Reference Include="PCL.PluginSdk"><HintPath>lib\PCL.PluginSdk.dll</HintPath><Private>false</Private></Reference>`。
> 只有这一个文件是契约，**不要**引用 `Plain Craft Launcher 2.dll`。

### 1.2 写入口

```csharp
using PCL.PluginSdk;

namespace MyPlugin;

public sealed class Main : IPlugin
{
    private IPluginContext _ctx = null!;

    public void Initialize(IPluginContext context)
    {
        _ctx = context;
        var count = context.Config.Get("stats.launchCount", 0) + 1;
        context.Config.Set("stats.launchCount", count);

        context.Log.Info($"Hello，这是第 {count} 次初始化");
        context.Log.Info($"宿主版本 {context.HostVersion}，数据目录 {context.DataDirectory}");
    }

    public void Enable() => _ctx.Log.Info("已启用");
    public void Disable() => _ctx.Log.Info("已禁用");
    public void Dispose() { }
}
```

### 1.3 写清单

`plugin.json`：

```json
{
  "id": "sgu.mine.first",
  "name": "我的第一个插件",
  "version": "1.0.0",
  "apiVersion": 1,
  "author": "你的名字",
  "description": "只往日志里打招呼",
  "entry": { "assembly": "MyPlugin.dll", "type": "MyPlugin.Main" },
  "capabilities": ["config"],
  "ui": { "icon": "lucide/puzzle" }
}
```

### 1.4 打包

包里必须包含：`plugin.json` + `MyPlugin.dll`（+ 你的私有依赖 dll，**不含** `PCL.PluginSdk.dll`）。

Windows 上一行搞定（PowerShell，压缩包内部路径用 `/`，根目录就是清单）：

```powershell
$out = "dist\MyPlugin-1.0.0.pclpkg"
$src = "bin\x64\Release\net10.0-windows"
Add-Type -AssemblyName System.IO.Compression.FileSystem
if (Test-Path $out) { Remove-Item $out -Force }
$zip = [System.IO.Compression.ZipFile]::Open($out, 'Create')
foreach ($f in @("plugin.json")) {
  [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $f, "plugin.json") | Out-Null
}
foreach ($f in Get-ChildItem "$src\*.dll" -Exclude PCL.PluginSdk.dll) {
  [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $f.FullName, $f.Name) | Out-Null
}
$zip.Dispose()
```

仓库里也提供了一键脚本：`plugins\package.ps1`（会编译三个示例插件并输出到 `plugins\dist\`）。

### 1.5 装进去看效果

1. 打开启动器 → 左侧「工具」→「插件」→「插件管理」；
2. 把 `.pclpkg` **拖进启动器窗口** → 弹「插件确认安装」→ 确定；
3. 列表里出现「我的第一个插件」，状态**已启用**；
4. 点右边的电源图标可禁用（文件名变成 `.pclpkg.disabled`），再点启用；
5. 「查看日志」里搜 `[Plugin:sgu.mine.first]` 应该能看到你的两行日志。

---

## 2. 打包规则（踩坑都在这）

| 规则 | 说明 |
| --- | --- |
| 清单在根 | `plugin.json` 必须在包根目录；外面套一层目录也行，但别套两层 |
| 路径分隔符 | 一律用 `/`；`\` 在部分解包器里会被当文件名 |
| 不许绝对路径 / `..` | 解包时会拒绝（防目录穿越），会提示「包内有非法路径」 |
| 不含 SDK | `PCL.PluginSdk.dll` 不能进包，否则 `InvalidCastException` |
| 文件名即身份 | 同一个文件名（去掉 `.pclpkg`）视为同一插件，重新拖入=覆盖升级 |
| `id` 才是数据身份 | 配置存在 `PCL\PluginData\<id>\`，改了 `id` 等于换了一个插件 |
| 版本号 | `version` 用 `1.2.3` 这种 SemVer，管理页直接显示 |
| 大小 | 尽量别超过几十 MB；包内容是先解到缓存再加载的 |

**推荐的发布命名**：`<名字>-<版本>.pclpkg`，例如 `SguPack-1.0.0.pclpkg`。

---

## 3. 用户会怎么用它

- **安装**：拖 `.pclpkg` 到窗口（任何安装来源都弹确认框）；或在插件管理页点「从文件安装」。
- **启用/禁用**：管理页列表右侧电源图标；或手动改文件名后缀。
- **删除**：管理页选中后点「删除」，直接删 `PCL\Plugins\` 里对应的 `.pclpkg` 与
  `.pclpkg.disabled`；文件被占用时会提示「下次启动时删除」，重启后自动清掉。
- **更新**：把新包拖进去覆盖即可，配置不会丢。
- **位置**：`<启动器目录>\PCL\Plugins\`。管理页的「打开文件夹」直接定位到这里。

---

## 4. 进阶：用「功能键」放行宿主功能

插件不能直接改启动器，但可以**声明**启用某些特性（`provides` 字段）。
宿主看到功能键就把对应入口放出来——这是本分支「发行版策略」的实现方式。

```json
{ "provides": ["echo-cave"] }
```

| 功能键 | 效果 | 要写代码吗 |
| --- | --- | --- |
| `echo-cave` | 工具→百宝箱出现「回声洞」打字机 | 否，声明即可 |
| `unlock.region` | 下载页解除「中国大陆地区」限制 | 否 |
| `unlock.offline-auth` | 允许不绑正版账号就建离线/外置档案 | 否 |
| `materials.advanced` | 个性化页出现「高级材质（窗口）」：云母 / Aero 材质、标题栏与主界面不透明度、悬停发光 | 否 |

如果要**自己提供数据 / 界面**，实现对应接口（完整签名见 [`PLUGIN-API.md`](PLUGIN-API.md) §5）：

| 想做什么 | 实现这个接口 | 需要的能力 |
| --- | --- | --- |
| 给「回声洞」提供语录 | `IEchoCaveQuotesProvider` | `echo-cave`（`provides`） |
| 启动时弹一条欢迎语 | `IWelcomeMessagesProvider` | `welcome.messages`（`provides`） |
| 给插件加设置项（宿主生成界面） | `IPluginSettingsProvider` | `config` |
| 加一个自己的页面（侧栏出现条目） | `IPluginUiProvider` | `ui.page` |
| 订阅宿主事件（启动完成 / 游戏启动等） | `context.Events.Subscribe<T>()` | `events` |

示例（加一个自己的页面，宿主负责侧栏与容器）：

```csharp
public sealed class Main : IPlugin, IPluginUiProvider
{
    public IReadOnlyList<PluginPageDefinition> GetPages() =>
    [
        new()
        {
            Id = "demo.hello",
            Title = "示例页面",
            Description = "这个页面是插件给的。",
            Icon = "lucide/sparkles",
            CreatePage = context => new System.Windows.Controls.TextBlock
            {
                Text = $"你好，宿主版本 {context.HostVersion}"
            }
        }
    ];
}
```

---

## 5. 调试

1. **日志**：启动器「更多 → 查看日志」，搜 `[Plugin:`。加载失败的原因（清单缺字段、
   `apiVersion` 不匹配、程序集找不到）都会写在里面。
2. **状态**：管理页副信息会显示「出错 / 不兼容」和原因（`插件要求 1，宿主提供 1` 这类）。
3. **点「重新扫描」**：改了文件不用重启启动器（插件会重新 `Initialize`）。
4. **隔离目录试装**：把整个启动器目录复制一份，只在副本里装插件，出问题直接删目录。

常见错误对照见 `PLUGIN-API.md` §9。

---

## 6. 发布检查清单

- [ ] `plugin.json` 在包根，`id` 唯一、`version` 递增、`apiVersion: 1`
- [ ] `entry.assembly` / `entry.type` 与生成的 dll 名、类全名**逐字符一致**
- [ ] 包里没有 `PCL.PluginSdk.dll`
- [ ] 目标框架 `net10.0-windows`，`x64`
- [ ] 在干净启动器目录里拖进去能装、能启用、能禁用、能删
- [ ] 日志里没有你自己的异常
- [ ] 说明里写清楚「需要哪个功能键」「会读哪些数据」

---

## 7. 边界（写之前先看这一节）

- 不要试图恢复「更新 / 自动更新 / 更新检测 / 云控公告」，这些在代码层禁用了。
- 不要引导用户去 PCL Community / PCL2-CE 提 issue，反馈功能在下游是关闭的。
- 不要做依赖 PCL 中继 / 大厅服务器的联机实现，联机固定走 EasyTier 纯 P2P。
- 不要引用 `Plain Craft Launcher 2.dll`，不要自带 `PCL.PluginSdk.dll`。
- 插件是**完全信任**的代码。你发布的包，别人会用它管自己的游戏目录。
