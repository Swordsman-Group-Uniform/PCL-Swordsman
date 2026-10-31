using PCL.PluginSdk;

namespace PCL.PluginHost;

/// <summary>
///     一个已发现 / 已加载的插件。
/// </summary>
public sealed class PluginDescriptor
{
    /// <summary>
    ///     插件「目录」绝对路径。
    ///     包格式（<c>.pclpkg</c>）指向解压出来的缓存目录；历史目录格式指向插件目录本身。
    /// </summary>
    public required string DirectoryPath { get; set; }

    /// <summary>基名（包文件名去掉 <c>.pclpkg</c> / <c>.pclpkg.disabled</c>，或目录名）。</summary>
    public required string FolderName { get; init; }

    /// <summary>插件包文件的绝对路径；历史目录格式为 <see langword="null" />。</summary>
    public string? PackagePath { get; init; }

    /// <summary>插件包文件名（含 <c>.pclpkg</c> 后缀，禁用时为 <c>.pclpkg.disabled</c>）。</summary>
    public string? PackageFileName { get; init; }

    /// <summary>是否为 <c>.pclpkg</c> 包格式（新格式）。</summary>
    public bool IsPackage => PackagePath is not null;

    public PluginManifest? Manifest { get; internal set; }

    public PluginCapability Capabilities { get; internal set; }

    public PluginState State { get; internal set; } = PluginState.Discovered;

    /// <summary>出错时的可读原因。</summary>
    public string? Error { get; internal set; }

    /// <summary>插件包的完整性 / 签名状态（见 <see cref="PluginIntegrityState" />）。</summary>
    public PluginIntegrityState Integrity { get; internal set; } = PluginIntegrityState.Undeclared;

    /// <summary>插件实例（未加载时为 null）。</summary>
    public IPlugin? Instance { get; internal set; }

    internal PluginLoadContext? LoadContext { get; set; }

    internal IPluginContextSnapshot? ContextSnapshot { get; set; }

    /// <summary>宿主实现的插件上下文（未加载时为 null）。</summary>
    internal PluginContext? HostContext { get; set; }

    /// <summary>
    ///     插件的配置读写入口（宿主「插件设置」界面用的就是它；未加载时为 null）。
    ///     插件自己通过 <see cref="IPluginContext.Config" /> 拿到的是同一个对象。
    /// </summary>
    public IPluginConfig? Config => HostContext?.Config;

    /// <summary>插件的上下文（宿主转交给插件页面工厂用；未加载时为 null）。</summary>
    public IPluginContext? Context => HostContext;

    /// <summary>插件实现了 <see cref="IPluginSettingsProvider" /> 时返回它，否则 null。</summary>
    public IPluginSettingsProvider? SettingsProvider => Instance as IPluginSettingsProvider;

    public string Id => Manifest?.Id ?? FolderName;

    public string Name => Manifest?.Name ?? FolderName;

    public string Version => Manifest?.Version ?? "-";

    public bool IsUsable => State is PluginState.Loaded or PluginState.Enabled;

    /// <summary>展示在「文件」位置的信息：包文件名或目录名。</summary>
    public string DisplayFileName => PackageFileName ?? FolderName;
}

/// <summary>
///     上下文里需要留存的生命周期引用（配置/日志）。
/// </summary>
internal interface IPluginContextSnapshot
{
    void Save();
}
