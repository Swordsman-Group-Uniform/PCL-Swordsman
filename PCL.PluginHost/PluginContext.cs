using System.IO;
using PCL.PluginSdk;

namespace PCL.PluginHost;

/// <summary>
///     <see cref="IPluginContext" /> 的宿主实现：日志 / 配置 / 数据目录 / 能力 / 事件订阅。
/// </summary>
internal sealed class PluginContext : IPluginContext, IPluginContextSnapshot
{
    private readonly JsonPluginConfig _config;
    private readonly Func<string> _languageCode;
    private IPluginHostEnvironment _environment = null!;
    private readonly bool _hasMinecraftCapability;
    private bool? _minecraftGranted;
    private bool _minecraftCapabilityWarned;
    private readonly bool _canUseGameDir;
    private readonly bool _canUseUserPick;

    public PluginContext(PluginDescriptor descriptor, IPluginHostEnvironment environment, string dataDirectory,
        PluginEventBus? eventBus = null)
    {
        Directory.CreateDirectory(dataDirectory);
        PluginId = descriptor.Id;
        PluginName = descriptor.Name;
        PluginVersion = descriptor.Version;
        HostVersion = environment.HostVersion;
        DataDirectory = dataDirectory;
        Capabilities = descriptor.Capabilities;
        Log = new PluginLogger(descriptor.Id, environment);
        _config = new JsonPluginConfig(dataDirectory);
        _languageCode = () => environment.LanguageCode;
        _environment = environment;
        _hasMinecraftCapability = (descriptor.Capabilities & PluginCapability.MinecraftRead) == PluginCapability.MinecraftRead;
        _canUseGameDir = (descriptor.Capabilities & PluginCapability.FileGameDir) == PluginCapability.FileGameDir;
        _canUseUserPick = (descriptor.Capabilities & PluginCapability.FileUserPick) == PluginCapability.FileUserPick;
        Net = new PluginHttp(descriptor.Id, descriptor.Name, (descriptor.Capabilities & PluginCapability.NetHttp) == PluginCapability.NetHttp,
            environment.Permissions, Log);
        Files = new PluginFiles(descriptor.Id, descriptor.Name, environment, _canUseGameDir, _canUseUserPick, Log);
        Notify = new PluginNotify(descriptor.Id,
            (descriptor.Capabilities & PluginCapability.UiNotify) == PluginCapability.UiNotify,
            environment.Notifier, Log);
        Events = eventBus?.CreateSubscriber(
                     descriptor.Id,
                     (descriptor.Capabilities & PluginCapability.Events) == PluginCapability.Events,
                     Log)
                 ?? new NoopEventBus(Log);
    }

    public string PluginId { get; }

    /// <summary>清单里的展示名（授权弹窗用）。</summary>
    public string PluginName { get; }

    public string PluginVersion { get; }

    public string HostVersion { get; }

    public string DataDirectory { get; }

    public PluginCapability Capabilities { get; }

    public IPluginLogger Log { get; }

    public IPluginConfig Config => _config;

    public IPluginEventBus Events { get; }

    public string LanguageCode => _languageCode();

    public IPluginNotify Notify { get; }

    public IMinecraftService Minecraft
    {
        get
        {
            if (!_hasMinecraftCapability)
            {
                // 只警告一次，避免插件在页面里反复读导致刷屏
                if (!_minecraftCapabilityWarned)
                {
                    _minecraftCapabilityWarned = true;
                    Log.Warn($"插件 {PluginId} 没有声明 minecraft.read 能力，读取实例信息被拒绝");
                }

                return EmptyMinecraftService.Instance;
            }

            // 能力只是「允许申请」，真正读取还要用户授权（与 net-http / 文件能力同一套规则）
            return EnsureMinecraftGranted() ? _environment.Minecraft : EmptyMinecraftService.Instance;
        }
    }

    /// <summary>minecraft.read 的授权闸：只问一次，结果复用（持久化在 permissions.json）。</summary>
    private bool EnsureMinecraftGranted()
    {
        if (_minecraftGranted is not null) return _minecraftGranted.Value;

        try
        {
            var permissions = _environment.Permissions;
            if (permissions.IsGranted(PluginId, PluginCapability.MinecraftRead))
            {
                _minecraftGranted = true;
                return true;
            }

            if (permissions.IsDenied(PluginId, PluginCapability.MinecraftRead))
            {
                _minecraftGranted = false;
                return false;
            }

            _minecraftGranted = permissions.RequestGrant(PluginId, PluginName, PluginCapability.MinecraftRead,
                "读取 Minecraft 实例信息");
            Log.Info($"minecraft.read 授权：{(_minecraftGranted.Value ? "已允许" : "已拒绝")}");
            return _minecraftGranted.Value;
        }
        catch (Exception ex)
        {
            Log.Error($"插件 {PluginId} 的 minecraft.read 授权流程出错，按拒绝处理", ex);
            _minecraftGranted = false;
            return false;
        }
    }

    public IPluginHttp Net { get; }

    public IPluginFiles Files { get; }

    public void Save() => _config.Save();
}

/// <summary>没有事件总线时的占位实现：订阅一律丢弃，只在日志里留痕。</summary>
internal sealed class NoopEventBus(IPluginLogger log) : IPluginEventBus
{
    public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class
    {
        log.Warn($"事件总线不可用，{typeof(TEvent).Name} 的订阅被忽略");
        return new Noop();
    }

    private sealed class Noop : IDisposable
    {
        public void Dispose()
        {
        }
    }
}

/// <summary>
///     <see cref="IPluginNotify" /> 的宿主实现：只做能力判断与转发，真正的界面在启动器那边。
/// </summary>
internal sealed class PluginNotify(
    string pluginId,
    bool canNotify,
    IPluginNotifyHost host,
    IPluginLogger log) : IPluginNotify
{
    public void Info(string text) => Show(text, PluginNotifyLevel.Info);

    public void Success(string text) => Show(text, PluginNotifyLevel.Success);

    public void Warning(string text) => Show(text, PluginNotifyLevel.Warning);

    public void Error(string text) => Show(text, PluginNotifyLevel.Error);

    public void Confirm(string title, string message, Action<bool> onResult)
    {
        if (!canNotify)
        {
            log.Warn($"插件 {pluginId} 没有声明 ui.notify 能力，确认框「{title}」被忽略");
            onResult?.Invoke(false);
            return;
        }

        try
        {
            host.Confirm(pluginId, title, message, result =>
            {
                try
                {
                    onResult?.Invoke(result);
                }
                catch (Exception ex)
                {
                    log.Error($"插件 {pluginId} 的确认框回调抛出异常", ex);
                }
            });
        }
        catch (Exception ex)
        {
            log.Error($"插件 {pluginId} 弹确认框失败", ex);
            onResult?.Invoke(false);
        }
    }

    private void Show(string text, PluginNotifyLevel level)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        if (!canNotify)
        {
            log.Warn($"插件 {pluginId} 没有声明 ui.notify 能力，提示「{text}」被忽略");
            return;
        }

        try
        {
            host.Show(pluginId, text, level);
        }
        catch (Exception ex)
        {
            log.Error($"插件 {pluginId} 弹提示失败", ex);
        }
    }
}

/// <summary>没有 <c>minecraft.read</c> 能力时的空服务：字段全空，避免插件拿到 null 直接崩。</summary>
internal sealed class EmptyMinecraftService : IMinecraftService
{
    public static readonly EmptyMinecraftService Instance = new();

    public PluginInstanceInfo? CurrentInstance => null;

    public IReadOnlyList<PluginInstanceInfo> Instances => [];

    public string GameDirectory => "";
}

/// <summary>
///     <see cref="IPluginFiles" /> 的宿主实现：能力 + 授权双闸，真正的对话框交给启动器。
/// </summary>
internal sealed class PluginFiles(
    string pluginId,
    string pluginName,
    IPluginHostEnvironment environment,
    bool canUseGameDir,
    bool canUseUserPick,
    IPluginLogger log) : IPluginFiles
{
    public string? GameDirectory
    {
        get
        {
            if (!canUseGameDir)
            {
                log.Warn($"插件 {pluginId} 没有声明 file-game-dir 能力，读取游戏目录被拒绝");
                return null;
            }

            if (!EnsureGranted(PluginCapability.FileGameDir, "读取游戏目录"))
                return null;

            var path = environment.Minecraft.GameDirectory;
            return string.IsNullOrWhiteSpace(path) ? null : path;
        }
    }

    public string? PickOpenFile(string? filter = null)
    {
        if (!canUseUserPick)
        {
            log.Warn($"插件 {pluginId} 没有声明 file-user-pick 能力，选择文件被拒绝");
            return null;
        }

        if (!EnsureGranted(PluginCapability.FileUserPick, "让你选择一个文件"))
            return null;

        return environment.PickFile(filter);
    }

    public string? PickFolder()
    {
        if (!canUseUserPick)
        {
            log.Warn($"插件 {pluginId} 没有声明 file-user-pick 能力，选择文件夹被拒绝");
            return null;
        }

        if (!EnsureGranted(PluginCapability.FileUserPick, "让你选择一个文件夹"))
            return null;

        return environment.PickFolder();
    }

    private bool EnsureGranted(PluginCapability capability, string description)
    {
        try
        {
            var permissions = environment.Permissions;
            if (permissions.IsGranted(pluginId, capability)) return true;
            if (permissions.IsDenied(pluginId, capability)) return false;

            var granted = permissions.RequestGrant(pluginId, pluginName, capability, description);
            log.Info($"{description}：{(granted ? "已允许" : "已拒绝")}");
            return granted;
        }
        catch (Exception ex)
        {
            log.Error($"插件 {pluginId} 的授权流程出错，按拒绝处理", ex);
            return false;
        }
    }
}
