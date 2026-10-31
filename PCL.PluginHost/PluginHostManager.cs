namespace PCL.PluginHost;

/// <summary>
///     插件宿主的门面：启动器只跟这一层打交道。
/// </summary>
public sealed class PluginHostManager
{
    private readonly object _lock = new();
    private PluginLoader? _loader;
    private IPluginHostEnvironment? _environment;

    /// <summary>当前进程里的实例（启动器启动时赋值）。</summary>
    public static PluginHostManager? Current { get; set; }

    /// <summary>插件列表 / 状态发生变化时触发（可能在后台线程）。</summary>
    public event Action? Changed;

    public bool IsInitialized
    {
        get
        {
            lock (_lock) return _loader is not null;
        }
    }

    public IReadOnlyList<PluginDescriptor> Plugins
    {
        get
        {
            lock (_lock) return _loader?.Plugins ?? [];
        }
    }

    public int EnabledCount => Plugins.Count(p => p.IsUsable);

    /// <summary>
    ///     初始化并加载全部插件。**会阻塞**（含插件 Initialize/Enable），请在后台线程调用。
    /// </summary>
    /// <param name="environment">宿主环境（目录、日志、版本）。</param>
    /// <param name="cacheDirectory">插件包解压缓存目录；留空时用 <c>&lt;插件数据目录&gt;\Cache</c>。</param>
    /// <param name="sharedDirectory">
    ///     共享级插件目录（如 <c>%APPDATA%\PCLCE\Plugins</c>）；实例级目录优先，同名插件只加载实例级那份。
    /// </param>
    public void Initialize(IPluginHostEnvironment environment, string? cacheDirectory = null,
        string? sharedDirectory = null)
    {
        lock (_lock)
        {
            _environment = environment;
            _loader = new PluginLoader(environment, cacheDirectory, sharedDirectory);
        }

        Reload();
    }

    /// <summary>重新扫描并加载（同样会阻塞）。</summary>
    public void Reload()
    {
        PluginLoader? loader;
        IPluginHostEnvironment? environment;
        lock (_lock)
        {
            loader = _loader;
            environment = _environment;
        }

        if (loader is null) return;

        try
        {
            loader.ReloadAll();
        }
        catch (Exception ex)
        {
            // 最后一道防线：插件系统任何异常都不得影响宿主
            environment?.Log("[PluginHost] 重新加载插件时发生未捕获异常", true, ex);
        }

        Changed?.Invoke();
    }

    /// <summary>
    ///     启用 / 禁用（包格式改文件名后缀，下次加载生效）。
    /// </summary>
    public bool SetEnabled(string pluginId, bool enabled)
    {
        PluginLoader? loader;
        lock (_lock) loader = _loader;
        if (loader is null) return false;
        return loader.SetEnabled(pluginId, enabled);
    }

    /// <summary>
    ///     当前**已启用**插件所提供的功能键集合（见 <see cref="PCL.PluginSdk.PluginFeatureKeys" />）。
    ///     宿主在决定「要不要展示被屏蔽的功能」时只看这个集合。
    /// </summary>
    public IReadOnlySet<string> ProvidedFeatures
    {
        get
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var plugin in Plugins)
            {
                if (!plugin.IsUsable) continue;
                var provides = plugin.Manifest?.Provides;
                if (provides is null) continue;
                foreach (var feature in provides)
                {
                    if (string.IsNullOrWhiteSpace(feature)) continue;
                    set.Add(feature.Trim());
                }
            }

            return set;
        }
    }

    /// <summary>
    ///     取出所有已启用插件中实现了 <typeparamref name="T" /> 的实例（例如附加主题 / 回声洞语录提供者）。
    /// </summary>
    public IReadOnlyList<T> GetProviders<T>() where T : class
    {
        var result = new List<T>();
        foreach (var plugin in Plugins)
        {
            if (!plugin.IsUsable) continue;
            if (plugin.Instance is T provider) result.Add(provider);
        }

        return result;
    }

    /// <summary>
    ///     宿主事件总线：插件订阅，宿主发布。
    ///     没有初始化时返回 null。
    /// </summary>
    public PluginEventBus? Events
    {
        get
        {
            lock (_lock) return _loader?.Events;
        }
    }

    /// <summary>发布一个宿主事件给所有已订阅的插件（插件异常被隔离）。</summary>
    public void Publish<TEvent>(TEvent payload) where TEvent : class
    {
        PluginEventBus? events;
        lock (_lock) events = _loader?.Events;
        events?.Publish(payload);
    }

    /// <summary>
    ///     取出所有已启用插件里实现了 <typeparamref name="T" /> 的实例，并带上插件描述符
    ///     （宿主展示「来自哪个插件」或按能力过滤时用）。
    /// </summary>
    public IReadOnlyList<(PluginDescriptor Descriptor, T Provider)> GetProviderEntries<T>() where T : class
    {
        var result = new List<(PluginDescriptor Descriptor, T Provider)>();
        foreach (var plugin in Plugins)
        {
            if (!plugin.IsUsable) continue;
            if (plugin.Instance is T provider) result.Add((plugin, provider));
        }

        return result;
    }

    public bool IsEnabledByUser(string pluginId)
    {
        PluginLoader? loader;
        lock (_lock) loader = _loader;
        return loader?.IsEnabledByUser(pluginId) ?? true;
    }
}
