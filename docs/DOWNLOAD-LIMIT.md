# 下载功能关闭、「Minecraft 下载限制」与「下载限制解除」插件

> 自 27.1-snapshot-26 起生效（snapshot-25 起默认禁止下载 Minecraft）。
> 默认构建把整个「下载」页关掉；解除限制的能力放在插件里，启动器没有任何「检测到插件就放行」的逻辑。

## 1. 默认构建的行为

| 位置 | 行为 |
| --- | --- |
| 下载 → 任意子页（原版游戏、各类加载器、Mod、整合包、资源包、光影、世界……） | 右侧统一替换为占位页 `PageDownloadBlocked`；左侧列表仍可见但点任何一项都只显示这个占位页 |
| 占位页长什么样 | 与「设置 → 反馈」被禁用时完全一致：`MyCard` 里放一个 `MyUnavailablePlaceholder`，文案取自 `Download.Disabled.Message` |
| 所有下载入口（`ModDownloadLib`） | `DownloadPolicy.CheckMinecraftDownloadAllowed()` 作为首个守卫，被关闭时直接返回并提示 |
| 整合包下载（`PageDownloadCompDetail`） | 不安装，改为保存为 `.mrpack` / `.zip` 到默认下载文件夹（`设置 → 下载 → 下载文件夹`，未设置时为 `PCL\MyDownload`），完成后提示「整合包已保存到：…」 |

实现只有三处：

- `Plain Craft Launcher 2\Modules\Base\DownloadPolicy.cs`：进程内静态开关 `AllowDownloads`（默认 `false`）、`AllowMinecraftDownload`（`AllowDownloads` 的别名）、`InstallModpackAfterDownload`（默认 `false`）、`Changed` 事件、`GetDownloadFolder()`、`GetSavePath()`、`CheckMinecraftDownloadAllowed()`。
- `Plain Craft Launcher 2\Pages\PageDownload\PageDownloadLeft.xaml.cs`：`PageGet()` 里 `if (!DownloadPolicy.AllowDownloads)` 直接返回占位页；页面订阅 `DownloadPolicy.Changed`，状态一变就 `RefreshPage()` 重新装载当前分页。
- `Plain Craft Launcher 2\Pages\PageDownload\PageDownloadBlocked.xaml(.cs)`：占位页本身。

启动器不读取插件清单、不判断插件是否启用，因此「放行」只可能来自进程内有人改写了这些属性。

## 2. 插件：下载限制解除

- 源码：`plugins\DownloadUnlock\`（`DownloadUnlock.csproj`、`plugin.json`、`DownloadUnlockPlugin.cs`）
- 包：`dist\DownloadUnlock.pclpkg`，插件 id `sgu.download-unlock`，启动器内显示名「下载限制解除」
- 打包：`plugins\package.ps1`（该插件的 `Out = 'dist'`，**只放在 `dist\` 根目录**，不进 `plugins\dist\`）

为什么不能进 `plugins\dist\`：非正式版构建会把 `plugins\dist\*.pclpkg` 内嵌进 exe、并在首次启动时自动安装到 exe 同级 `plugins\`。若把本插件放进去，默认构建就会自动解除限制，等于没有限制。

### 插件怎么起作用

`Initialize` 只保存上下文；`Enable()` / `Disable()` 通过反射找到启动器进程内的 `PCL.DownloadPolicy`，改写 `AllowDownloads`（以及别名 `AllowMinecraftDownload`）与 `InstallModpackAfterDownload`，再调用 `NotifyChanged()` 刷新界面。插件不引用启动器程序集，全部通过 `GetProperty` / `GetMethod` 完成。

因此：

- **安装并启用** → 与限制前完全一致：下载页正常、原版与加载器可下载、整合包下载后自动安装。
- **禁用或卸载** → 立刻恢复限制（页面订阅了 `Changed`，正在打开的下载页会立即换成占位页；重启后同样默认受限）。

### 安装方式

任选其一：

1. 插件管理页「从文件安装」选择 `dist\DownloadUnlock.pclpkg`；
2. 直接把 `.pclpkg` 拖进启动器窗口；
3. 复制到启动器的 `PCL\Plugins\` 目录后重启。

安装后打开插件管理页启用即可；禁用只需关闭开关（文件改名为 `.pclpkg.disabled`）。

## 3. 与旧「限制解除」（`sgu.unlock-limits`）的关系

旧插件声明的是 `unlock.region` 这个功能键，而启动器已不再读取该键（下载限制改由 `DownloadPolicy` 控制）。两个插件可以共存，但要解除下载限制必须启用新的「下载限制解除」。
