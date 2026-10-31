using System.IO;
using System.Threading;
using PCL.Core.App;
using PCL.Core.UI.Theme;
using PCL.PluginHost;

namespace PCL;

/// <summary>
///     插件宿主接线：把 <see cref="PluginHostManager" /> 接到启动器上（宿主版本、目录、日志）。
/// </summary>
internal static class PluginHostBootstrap
{
    private static int _startedFlag;

    /// <summary>
    ///     插件宿主初始化完成（插件已加载且功能开关已应用）时触发。可能在后台线程触发。
    /// </summary>
    public static event Action? Ready;

    public static string PluginsDirectory => ModBase.exePath + @"PCL\Plugins\";

    public static string PluginDataDirectory => ModBase.exePath + @"PCL\PluginData\";

    /// <summary>插件包解压缓存（插件管理页「打开插件文件夹」不显示它，删插件时会自动清理）。</summary>
    public static string PluginCacheDirectory => ModBase.exePath + @"PCL\PluginCache\";

    /// <summary>
    ///     共享级插件目录：<c>%APPDATA%\PCLCE\Plugins</c>。
    ///     实例级（<c>&lt;启动器目录&gt;\PCL\Plugins</c>）优先，同名插件只加载实例级那一份。
    /// </summary>
    public static string SharedPluginsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PCLCE", "Plugins");

    public static PluginHostManager? Manager => PluginHostManager.Current;

    private static int _themeHooked;

    /// <summary>
    ///     发布一个宿主事件给插件（宿主内部用；没有插件订阅时几乎无开销）。
    /// </summary>
    public static void Publish<TEvent>(TEvent payload) where TEvent : class
    {
        try
        {
            Manager?.Publish(payload);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[PluginHost] 发布事件失败", ModBase.LogLevel.Debug);
        }
    }

    /// <summary>订阅主题变化并转发给插件（只会挂一次）。</summary>
    private static void HookThemeEvents()
    {
        if (Interlocked.Exchange(ref _themeHooked, 1) == 1) return;

        // 实例切换 + 下载任务：这两个钩子由启动器侧转发（钩子本身见 ModInstanceList / ModLoader）
        ModInstanceList.InstanceSwitched += instance =>
            Publish(new PCL.PluginSdk.InstanceSwitchedEvent(instance?.Name ?? "", instance?.PathInstance ?? ""));

        ModLoader.DownloadTaskStateChanged += (taskName, success) =>
            Publish(new PCL.PluginSdk.DownloadFinishedEvent(taskName, success));

        ModLoader.DownloadTaskStarted += taskName =>
            Publish(new PCL.PluginSdk.DownloadStartedEvent(taskName));

        ThemeService.ColorThemeChanged += theme =>
            Publish(new PCL.PluginSdk.ThemeChangedEvent(
                ThemeService.IsDarkMode,
                theme.ToString(),
                ThemeDisplayName(theme)));

        ThemeService.ColorModeChanged += (isDark, theme) =>
            Publish(new PCL.PluginSdk.ThemeChangedEvent(
                isDark,
                theme.ToString(),
                ThemeDisplayName(theme)));

        // 语言切换 → 通知插件（插件可以据此重出文案）
        PCL.Core.App.Localization.LocalizationService.LanguageChanged += () =>
            Publish(new PCL.PluginSdk.LanguageChangedEvent(
                PCL.Core.App.Localization.LocalizationService.CurrentLanguage.Code));
    }

    private static string ThemeDisplayName(ColorTheme theme) =>
        ThemeService.GetPluginThemeDisplayName(ThemeService.CurrentPluginThemeId)
        ?? ThemeService.GetExtraThemeDisplayName(theme)
        ?? theme.ToString();

    /// <summary>插件自带主题的定义（主题 id → 定义），标题栏装饰与配置校验都用它。</summary>
    private static readonly Dictionary<string, PCL.PluginSdk.PluginCustomThemeDefinition> _customThemes =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>取某个插件自带主题的定义（没有时返回 null）。</summary>
    public static PCL.PluginSdk.PluginCustomThemeDefinition? GetCustomTheme(string? id) =>
        id is not null && _customThemes.TryGetValue(id, out var definition) ? definition : null;

    /// <summary>
    ///     收集插件自带主题并交给主题引擎。插件重扫后调用一次即可。
    ///     能力门禁：没声明 <c>themes.custom</c> 的插件不生效。
    /// </summary>
    public static void RefreshCustomThemes(PluginHostManager manager)
    {
        try
        {
            _customThemes.Clear();
            var themes = new List<(string Id, string DisplayName, string Hex, string? Flavor, int SortOrder)>();

            foreach (var (descriptor, provider) in
                     manager.GetProviderEntries<PCL.PluginSdk.IPluginCustomThemesProvider>())
            {
                if ((descriptor.Capabilities & PCL.PluginSdk.PluginCapability.ThemesCustom) == 0)
                {
                    ModBase.Log($"[Plugin] {descriptor.Id} 提供了自带主题但没有声明 themes.custom 能力，已忽略",
                        ModBase.LogLevel.Debug);
                    continue;
                }

                IReadOnlyList<PCL.PluginSdk.PluginCustomThemeDefinition> definitions;
                try
                {
                    definitions = provider.GetCustomThemes() ?? [];
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, $"[Plugin] {descriptor.Id} 的 GetCustomThemes() 抛出异常", ModBase.LogLevel.Hint);
                    continue;
                }

                foreach (var definition in definitions)
                {
                    if (definition is null || string.IsNullOrWhiteSpace(definition.Id)) continue;
                    if (string.IsNullOrWhiteSpace(definition.Hex)) continue;
                    if (_customThemes.ContainsKey(definition.Id))
                    {
                        ModBase.Log($"[Plugin] 插件主题 id 重复：{definition.Id}（{descriptor.Id}）", ModBase.LogLevel.Hint);
                        continue;
                    }

                    _customThemes[definition.Id] = definition;
                    themes.Add((definition.Id, definition.DisplayName, definition.Hex, definition.Flavor,
                        definition.SortOrder));
                }
            }

            ThemeService.SetPluginThemes(themes);
            if (themes.Count > 0)
                ModBase.Log($"[Plugin] 已注册 {themes.Count} 个插件自带主题：{string.Join(", ", themes.Select(t => t.Id))}",
                    ModBase.LogLevel.Debug);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[Plugin] 收集插件自带主题失败", ModBase.LogLevel.Debug);
        }
    }

    /// <summary>
    ///     在后台线程启动插件宿主（发现 + 加载 + 初始化）。重复调用无副作用。
    /// </summary>
    public static void Start()
    {
        if (Interlocked.Exchange(ref _startedFlag, 1) == 1) return;

        var manager = new PluginHostManager();
        PluginHostManager.Current = manager;

        ModBase.RunInNewThread(() =>
        {
            try
            {
                // 先执行上次残留的「待删除」队列（此时插件还没加载，文件不会被占用）
                var removed = PluginInstaller.ProcessPendingDeletes(PluginsDirectory, PluginDataDirectory);
                if (removed > 0)
                    ModBase.Log($"[PluginHost] 已清理 {removed} 个待删除的插件目录", ModBase.LogLevel.Debug);

                // 历史版本把插件解压成目录；统一打包成 .pclpkg，之后开关与删除都只动文件
                var migrated = PluginInstaller.MigrateDirectoriesToPackages(PluginsDirectory, PluginDataDirectory);
                if (migrated > 0)
                    ModBase.Log($"[PluginHost] 已把 {migrated} 个目录格式插件转换为 .pclpkg 插件包", ModBase.LogLevel.Debug);

                // 老版本按源文件名安装，同一个插件可能留下多份副本；合并成一份，避免「禁用只改到一份」
                var deduplicated = PluginInstaller.DeduplicatePackages(PluginsDirectory);
                if (deduplicated > 0)
                    ModBase.Log($"[PluginHost] 已合并 {deduplicated} 个重复的插件包副本", ModBase.LogLevel.Debug);

                // 插件签名信任列表：必须在扫描插件之前加载，否则已签名插件会被判为「公钥不受信任」
                LoadTrustedKeys();

                // 非正式版：把随包附带的插件（SGU 插件包 / 限制解除）装好并启用
                EnsureBundledPluginsInstalled();

                manager.Initialize(new AppPluginEnvironment(), PluginCacheDirectory, SharedPluginsDirectory);

                // 把「已启用插件提供的功能」写入核心功能开关，再刷新一次主题与界面
                ApplyProvidedFeatures(manager);

                // 主题变化转发给插件（插件宿主里的事件总线只在有订阅者时才真正干活）
                HookThemeEvents();

                // 插件全部就绪：通知插件「启动完成」
                Publish(new PCL.PluginSdk.HostStartupCompletedEvent(
                    ModBase.versionDisplayName,
                    States.System.StartupCount));

                // 通知界面（插件管理页等）可以刷新了
                Ready?.Invoke();
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "[PluginHost] 插件宿主初始化失败", ModBase.LogLevel.Debug);
            }
        }, "PluginHost", ThreadPriority.BelowNormal);
    }

    /// <summary>同步重新扫描（供管理页「重新扫描」按钮使用，建议在后台线程调用）。</summary>
    public static void Reload()
    {
        Manager?.Reload();
        if (Manager is not null) ApplyProvidedFeatures(Manager);
    }

    /// <summary>
    ///     随包附带的插件目录（与 exe 同级的 <c>plugins</c> 文件夹）。
    ///     只有非正式版才会往里放东西，正式版由用户自行安装。
    /// </summary>
    public static string BundledPluginsDirectory => ModBase.exePath + @"plugins\";

    /// <summary>
    ///     把随包附带的插件安装到 <c>PCL\Plugins</c> 并标记为启用。
    ///     <para>
    ///         非正式版：内嵌的插件包全部安装（SGU 插件包 / PCLS 核心插件 / 高级材质 / Hello 示例）。
    ///     </para>
    ///     <para>
    ///         正式版：**只安装无条件内嵌的三个包 —— PCLS 核心插件、高级材质、欢迎语**，
    ///         并且与测试版一样**默认启用**（2026-10-29 起：HMCL 已恢复下载功能，原先「正式版默认关闭下载解锁」
    ///         的理由不再成立；高级材质与欢迎语也按要求内置）。
    ///         exe 同级 <c>plugins\</c> 目录里手动放置的包在正式版**仍然不会**自动安装，避免「解压即装」。
    ///     </para>
    /// </summary>
    private static void EnsureBundledPluginsInstalled()
    {
        try
        {
            // ① 先处理嵌进 exe 的插件包：正式版只有 PCLS 核心插件（csproj 里无条件内嵌），
            //    其余 *.pclpkg 只在 IncludeBundledPlugins=true（非正式版）时才会被嵌进来
            InstallEmbeddedPackages();

            // ② exe 同级 plugins/ 目录：仅非正式版自动安装
            if (ThemeService.IsReleaseDistribution)
            {
                ModBase.Log("[PluginHost] 正式版构建：内置插件（PCLS 核心插件 / 高级材质 / 欢迎语）已就绪，跳过随包插件目录",
                    ModBase.LogLevel.Debug);
                return;
            }

            var bundled = BundledPluginsDirectory;
            if (!Directory.Exists(bundled)) return;

            foreach (var package in Directory.EnumerateFiles(bundled, "*.pclpkg"))
            {
                try
                {
                    if (PackageAlreadyInstalled(package))
                    {
                        ModBase.Log($"[PluginHost] 随包插件已存在，保留用户当前状态：{Path.GetFileName(package)}",
                            ModBase.LogLevel.Debug);
                        continue;
                    }

                    var result = PluginInstaller.InstallPackage(PluginsDirectory, package);
                    ModBase.Log(
                        $"[PluginHost] 随包插件安装{(result.Success ? "成功" : "失败")}：{Path.GetFileName(package)}",
                        ModBase.LogLevel.Debug);
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, $"[PluginHost] 自动安装随包插件失败：{Path.GetFileName(package)}", ModBase.LogLevel.Debug);
                }
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[PluginHost] 处理随包插件失败", ModBase.LogLevel.Debug);
        }
    }

    /// <summary>
    ///     目标插件目录里是否已经装了这个插件（按清单 id 判断，覆盖任意文件名与启用/禁用两种形态）。
    ///     随包插件只在「用户从没碰过」时才安装，这样用户手动禁用后不会被下一次启动强行启用。
    /// </summary>
    /// <summary>某插件某能力是否已被用户授权（管理页展示用）。</summary>
    public static bool IsPermissionGranted(string pluginId, PCL.PluginSdk.PluginCapability capability) =>
        PluginPermissionStore.IsGranted(pluginId, capability);

    /// <summary>某插件某能力是否已被用户拒绝。</summary>
    public static bool IsPermissionDenied(string pluginId, PCL.PluginSdk.PluginCapability capability) =>
        PluginPermissionStore.IsDenied(pluginId, capability);

    /// <summary>主动申请 / 撤回授权（管理页按钮用）。</summary>
    public static void SetPermission(string pluginId, string pluginName, PCL.PluginSdk.PluginCapability capability,
        bool granted)
    {
        if (granted)
            new AppPluginPermissions().RequestGrant(pluginId, pluginName, capability,
                AppPluginPermissions.PluginChildText(capability));
        else
            PluginPermissionStore.Revoke(pluginId, capability);
    }

    /// <summary>该插件声明了、但还需要用户授权的能力列表。</summary>
    public static IReadOnlyList<PCL.PluginSdk.PluginCapability> GatedCapabilities(
        PCL.PluginHost.PluginDescriptor plugin) =>
        PluginPermissionStore.GatedCapabilities
            .Where(capability => (plugin.Capabilities & capability) == capability)
            .ToList();

    /// <summary>
    ///     插件签名的信任公钥列表：<c>PCL\PluginData\trusted-keys.json</c>，格式
    ///     <c>{ "keys": [ "&lt;SubjectPublicKeyInfo 的 Base64&gt;", … ] }</c>。
    ///     <para>
    ///         文件不存在时视为「没有信任任何公钥」——此时**带签名的插件会被拒绝加载**，
    ///         未签名（只带 <c>files</c> 哈希）的插件照常可用。生成密钥与签名用 <c>tools/PluginSigner</c>。
    ///     </para>
    /// </summary>
    private static void LoadTrustedKeys()
    {
        var path = Path.Combine(PluginDataDirectory, "trusted-keys.json");
        try
        {
            PCL.PluginHost.PluginIntegrity.TrustedPublicKeys = [];

            if (!File.Exists(path))
            {
                ModBase.Log($"[PluginHost] 没有插件签名信任列表（{path}），带签名的插件将无法加载",
                    ModBase.LogLevel.Debug);
                return;
            }

            using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("keys", out var keys) ||
                keys.ValueKind != System.Text.Json.JsonValueKind.Array)
            {
                ModBase.Log("[PluginHost] trusted-keys.json 格式不对（需要 {\"keys\":[…] }），忽略", ModBase.LogLevel.Hint);
                return;
            }

            var list = new List<string>();
            foreach (var key in keys.EnumerateArray())
            {
                var text = key.GetString();
                if (!string.IsNullOrWhiteSpace(text)) list.Add(text.Trim());
            }

            PCL.PluginHost.PluginIntegrity.TrustedPublicKeys = list;
            ModBase.Log($"[PluginHost] 已加载 {list.Count} 个插件签名信任公钥", ModBase.LogLevel.Normal);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[PluginHost] 读取 trusted-keys.json 失败", ModBase.LogLevel.Hint);
        }
    }

    /// <summary>
    ///     随包 / 内置插件是否**已经是同版本或更新**。
    ///     <para>
    ///         以前这里只判「装过就跳过」，结果每次升级启动器后，用户机器上仍跑着旧的插件包
    ///         （插件清单里新加的能力、新版本号都不会生效，例如 <c>ui.settings</c>）。
    ///         现在改成比版本：内置包版本更**高**就覆盖安装，插件数据目录（配置）不受影响。
    ///     </para>
    /// </summary>
    private static bool PackageAlreadyInstalled(string packagePath)
    {
        try
        {
            var manifest = PluginPackage.ReadManifest(packagePath).Manifest;
            var id = manifest?.Id;
            if (!string.IsNullOrWhiteSpace(id))
            {
                var installed = PluginPackage.FindPackages(PluginsDirectory, id);
                if (installed.Count > 0)
                {
                    // 取已装包里最高的版本，与内置包比较
                    var best = installed
                        .Select(path => PluginPackage.ReadManifest(path).Manifest?.Version)
                        .Where(version => !string.IsNullOrWhiteSpace(version))
                        .OrderByDescending(version => version!, StringComparer.OrdinalIgnoreCase)
                        .FirstOrDefault();

                    if (PluginVersion.Compare(best, manifest?.Version) >= 0)
                        return true;

                    ModBase.Log($"[PluginHost] 内置插件 {id} 有新版本（已装 {best ?? "?"} → 内置 {manifest?.Version}），将覆盖安装",
                        ModBase.LogLevel.Normal);
                    return false;
                }
            }

            var baseName = PluginPackage.GetBaseName(packagePath);
            return File.Exists(Path.Combine(PluginsDirectory, PluginPackage.GetPackageFileName(baseName, true))) ||
                   File.Exists(Path.Combine(PluginsDirectory, PluginPackage.GetPackageFileName(baseName, false)));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    ///     安装嵌入在程序集资源里的插件包（<c>*.pclpkg</c>）。
    ///     非正式版构建会嵌入 SGU 插件包 / 限制解除 / Hello 示例，首次运行自动装好。
    /// </summary>
    private static void InstallEmbeddedPackages()
    {
        var assembly = typeof(PluginHostBootstrap).Assembly;
        var tempDirectory = Path.Combine(Path.GetTempPath(), "PCLCE-PluginPackages-" + Guid.NewGuid().ToString("N"));
        try
        {
            var names = assembly.GetManifestResourceNames()
                .Where(name => name.EndsWith(".pclpkg", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (names.Count == 0) return;

            Directory.CreateDirectory(tempDirectory);
            foreach (var name in names)
            {
                try
                {
                    var path = Path.Combine(tempDirectory, Path.GetFileName(name));
                    using (var stream = assembly.GetManifestResourceStream(name))
                    {
                        if (stream is null) continue;
                        using var file = File.Create(path);
                        stream.CopyTo(file);
                    }

                    if (PackageAlreadyInstalled(path))
                    {
                        ModBase.Log($"[PluginHost] 内置插件已存在，保留用户当前状态：{Path.GetFileName(name)}",
                            ModBase.LogLevel.Debug);
                        continue;
                    }

                    var result = PluginInstaller.InstallPackage(PluginsDirectory, path);
                    ModBase.Log(
                        $"[PluginHost] 内置插件安装{(result.Success ? "成功" : "失败")}：{Path.GetFileName(name)}",
                        ModBase.LogLevel.Debug);
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, $"[PluginHost] 安装内置插件失败：{name}", ModBase.LogLevel.Debug);
                }
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[PluginHost] 读取内置插件包失败", ModBase.LogLevel.Debug);
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDirectory)) Directory.Delete(tempDirectory, true);
            }
            catch
            {
                // 忽略临时目录清理失败
            }
        }
    }

    /// <summary>
    ///     把插件声明的功能键写进 <see cref="FeatureFlags" />，并把插件提供的
    ///     主题定义 / 主题名称 / 回声洞语录交给宿主。
    /// </summary>
    public static void ApplyProvidedFeatures(PluginHostManager manager)
    {
        try
        {
            var features = manager.ProvidedFeatures;
            FeatureFlags.SetAll(features);

            // 主题定义
            var definitions = new Dictionary<ColorTheme, (string Hex, string? Flavor)>();
            var names = new Dictionary<ColorTheme, string>();
            foreach (var provider in manager.GetProviders<PCL.PluginSdk.IExtraThemesProvider>())
            {
                foreach (var theme in provider.GetThemes())
                {
                    if (string.IsNullOrWhiteSpace(theme.Key)) continue;
                    if (!Enum.TryParse<ColorTheme>(theme.Key.Trim(), true, out var parsed)) continue;
                    definitions[parsed] = (theme.Hex ?? "", theme.Flavor);
                    if (!string.IsNullOrWhiteSpace(theme.DisplayName)) names[parsed] = theme.DisplayName;
                }
            }

            ThemeService.SetExtraThemeDefinitions(definitions.Count > 0 ? definitions : null);
            ThemeService.SetExtraThemeNames(names.Count > 0 ? names : null);

            // 插件自带的主题（独立槽位，可带自己的标题栏装饰）
            RefreshCustomThemes(manager);

            // 诊断：把「设置页 / 通知」这两类界面的候选列出来，
            // 用户反馈「设置页没有入口」时，看这一行就能分清是插件没声明能力、还是界面没接上
            var settingsProviders = manager.GetProviderEntries<PCL.PluginSdk.IPluginSettingsProvider>()
                .Select(entry => entry.Descriptor.Id +
                                 ((entry.Descriptor.Capabilities & PCL.PluginSdk.PluginCapability.UiSettings) != 0
                                     ? "(ui.settings)"
                                     : "(缺 ui.settings)"))
                .ToList();
            // 注意：ui.notify 是宿主提供的服务，插件不会「实现」它，所以这里要看清单能力而不是 provider
            var notifyProviders = manager.Plugins
                .Where(plugin => plugin.IsUsable &&
                                 (plugin.Capabilities & PCL.PluginSdk.PluginCapability.UiNotify) != 0)
                .Select(plugin => plugin.Id)
                .ToList();
            ModBase.Log(
                $"[Plugin] 提供设置的插件：{(settingsProviders.Count == 0 ? "无" : string.Join(", ", settingsProviders))}；" +
                $"声明 ui.notify 的插件：{(notifyProviders.Count == 0 ? "无" : string.Join(", ", notifyProviders))}",
                ModBase.LogLevel.Normal);

            // 回声洞语录
            var quotes = new List<string>();
            foreach (var provider in manager.GetProviders<PCL.PluginSdk.IEchoCaveQuotesProvider>())
                quotes.AddRange(provider.GetQuotes());

            ModEchoCave.SetQuotes(quotes);

            // 启动欢迎语（插件提供池，宿主抽一条并用右下角小提示显示）
            var welcome = new List<string>();
            foreach (var provider in manager.GetProviders<PCL.PluginSdk.IWelcomeMessagesProvider>())
                welcome.AddRange(provider.GetWelcomeMessages());

            ModWelcome.SetMessages(welcome);

            ModBase.Log(
                $"[PluginHost] 已应用插件功能：{(features.Count == 0 ? "无" : string.Join(", ", features))}",
                ModBase.LogLevel.Debug);

            // 欢迎语：等插件把池交上来之后，在 UI 线程弹一次（本次启动只弹一次）
            if (features.Contains(PCL.PluginSdk.PluginFeatureKeys.WelcomeMessages))
                ModBase.RunInUi(ModWelcome.ShowWelcome);

        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[PluginHost] 应用插件功能失败", ModBase.LogLevel.Debug);
        }
    }

    private sealed class AppPluginEnvironment : IPluginHostEnvironment
    {
        public string HostVersion => ModBase.versionDisplayName;

        public string PluginsDirectory => PluginHostBootstrap.PluginsDirectory;

        public string PluginDataDirectory => PluginHostBootstrap.PluginDataDirectory;

        public string LanguageCode => PCL.Core.App.Localization.LocalizationService.CurrentLanguage.Code;

        public PCL.PluginSdk.IPluginNotifyHost Notifier { get; } = new AppPluginNotifier();

        public PCL.PluginSdk.IPluginPermissionHost Permissions { get; } = new AppPluginPermissions();

        public PCL.PluginSdk.IMinecraftService Minecraft { get; } = new AppMinecraftService();

        public string? PickFile(string? filter = null)
        {
            string? result = null;
            ModBase.RunInUi(() =>
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = PCL.Core.App.Localization.Lang.Text("Tools.Plugin.Permission.FileUserPick"),
                    Filter = string.IsNullOrWhiteSpace(filter) ? "所有文件|*.*" : filter,
                    CheckFileExists = true
                };
                if (dialog.ShowDialog() == true) result = dialog.FileName;
            });
            return result;
        }

        public string? PickFolder()
        {
            string? result = null;
            ModBase.RunInUi(() =>
            {
                var dialog = new Microsoft.Win32.OpenFolderDialog
                {
                    Title = PCL.Core.App.Localization.Lang.Text("Tools.Plugin.Permission.FileUserPick")
                };
                if (dialog.ShowDialog() == true) result = dialog.FolderName;
            });
            return result;
        }

        public void Log(string message, bool isError = false, Exception? exception = null)
        {
            if (exception is not null)
                ModBase.Log(exception, message, ModBase.LogLevel.Debug);
            else
                ModBase.Log(message, isError ? ModBase.LogLevel.Debug : ModBase.LogLevel.Debug);
        }
    }

    /// <summary>
    ///     插件提示 / 确认框的宿主实现：右下角小提示走 <see cref="HintService" />，
    ///     确认框走 <see cref="ModMain.MyMsgBox" />。两者都切回 UI 线程执行。
    /// </summary>
    private sealed class AppPluginNotifier : PCL.PluginSdk.IPluginNotifyHost
    {
        public void Show(string pluginId, string text, PCL.PluginSdk.PluginNotifyLevel level)
        {
            var type = level switch
            {
                PCL.PluginSdk.PluginNotifyLevel.Success => HintType.Success,
                PCL.PluginSdk.PluginNotifyLevel.Warning => HintType.Warning,
                PCL.PluginSdk.PluginNotifyLevel.Error => HintType.Error,
                _ => HintType.Info
            };

            ModBase.RunInUi(() => HintService.Hint($"[{pluginId}] {text}", type));
        }

        public void Confirm(string pluginId, string title, string message, Action<bool> onResult)
        {
            ModBase.RunInUi(() =>
            {
                // MyMsgBox 有第二个按钮时会阻塞到用户点掉（与启动器内部确认框一致）
                var result = ModMain.MyMsgBox(message, title,
                    button2: PCL.Core.App.Localization.Lang.Text("Common.Action.Cancel"));
                onResult?.Invoke(result == 1);
                ModBase.Log($"[Plugin] {pluginId} 的确认框已处理：{(result == 1 ? "确定" : "取消")}",
                    ModBase.LogLevel.Debug);
            });
        }
    }
}
