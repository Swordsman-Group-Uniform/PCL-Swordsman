# 插件示例与开发说明

本目录存放**示例插件**，用于验证插件宿主（`PCL.PluginHost`）与契约（`PCL.PluginSdk`）是否正常工作。
示例工程默认**不参与解决方案构建**（`slnx` 里标了 `Build="false"`），需要手动构建。

完整文档：[`docs/PLUGIN-GUIDE.md`](../docs/PLUGIN-GUIDE.md)（手把手）· [`docs/PLUGIN-API.md`](../docs/PLUGIN-API.md)（接口参考）

## 目录

```
samples/
└─ HelloPlugin/
   ├─ HelloPlugin.csproj
   ├─ HelloPlugin.cs
   └─ plugin.json
```

## 构建并安装示例插件

插件在启动器里的形态是**一个 `.pclpkg` 文件**（zip：`plugin.json` + `<id>.dll`），
放在 `PCL/Plugins/` 下；禁用就是把文件名改成 `.pclpkg.disabled`。

最省事的方式（顺带把三个内置插件一起打包）：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File plugins\package.ps1
# 产物：plugins\dist\HelloPlugin.pclpkg / SguPack.pclpkg / UnlockLimits.pclpkg
```

只构建示例插件：

```powershell
dotnet build samples\HelloPlugin\HelloPlugin.csproj -c Release -p:Platform=x64
# 输出目录：samples\HelloPlugin\bin\x64\Release\net10.0-windows\
```

然后把 `plugin.json` 与 `HelloPlugin.dll` 打进 zip，扩展名命名成 `.pclpkg`
（`plugins\package.ps1` 做的就是这件事）。

## 安装

- **拖进启动器窗口**任意位置 → 弹「插件确认安装」→ 确定即可（自动放进 `PCL/Plugins/` 并启用）；
- 或打开「工具 → 插件 → 插件管理」，点「从文件安装」，选 `.pclpkg`；
- 也可以手动把 `.pclpkg` 复制到 `<启动器目录>/PCL/Plugins/`，下次启动自动发现。

启动后：

1. 「插件管理」页应看到 `Hello 插件（示例） 1.0.0`，状态「已启用」；
2. `PCL/Log/Launch-*.log` 里搜索 `[Plugin:sgu.sample.hello]`，能看到初始化与启用日志；
3. `PCL/PluginData/sgu.sample.hello/config.json` 里会出现 `launchCount`，每启动一次 +1。

## 调试

- 启用 / 禁用：把 `PCL/Plugins/HelloPlugin.pclpkg` 改名成 `HelloPlugin.pclpkg.disabled`（或反过来），
  在管理页点「重新扫描」（或重启启动器）即可看到状态变化。
- 删掉插件：直接删 `.pclpkg` 文件，再从管理页「重新扫描」。
- 改了代码重新打包后，直接覆盖同名 `.pclpkg`（拖入或复制到 `PCL/Plugins/`）即可，旧缓存目录会自动回收。

## 插件开发要点（M1 可用 API）

| 能力 | 用法 |
| --- | --- |
| 日志 | `context.Log.Info/Warn/Error(...)`，宿主统一加 `[Plugin:<id>]` 前缀 |
| 配置 | `context.Config.Get<T>(key, default)` / `Set<T>(key, value)`，自动写入 `config.json` |
| 数据目录 | `context.DataDirectory`（已创建，可放缓存） |
| 能力查询 | `context.HasCapability(PluginCapability.NetHttp)` |
| 宿主版本 | `context.HostVersion` |

后续里程碑（页面 / 侧边栏 / 事件 / Minecraft 只读 / 下载挂接）会以**新增接口成员**的方式扩展，
不破坏已编译的插件。完整设计见 [`docs/PLUGIN-SYSTEM.md`](../docs/PLUGIN-SYSTEM.md)。

## 注意事项

- 插件工程**不要**把 `PCL.PluginSdk.dll` 复制到输出目录（示例工程用 `Private=false` + `ExcludeAssets=runtime`
  已经处理）：契约程序集必须由宿主提供，否则会出现类型身份分裂。
- 插件目标框架必须是 `net10.0-windows`（与宿主一致）。
- 清单 `apiVersion` 必须等于宿主当前 API 版本（目前 `1`）。
- 插件的数据目录在卸载时**不会**被删除（`PCL/PluginData/<id>/` 保留给下次重装使用）。
