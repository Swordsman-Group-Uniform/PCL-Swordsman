namespace PCL.PluginSdk;

/// <summary>
///     宿主传给插件的上下文。M1 只提供日志 / 配置 / 数据目录 / 能力查询；
///     页面、事件、Minecraft 等服务会在后续里程碑以**新增成员**的方式扩展（不破坏已有插件）。
/// </summary>
public interface IPluginContext
{
    string PluginId { get; }

    string PluginVersion { get; }

    /// <summary>宿主版本，如 <c>27.1-snapshot-8(2.15.1)</c>。</summary>
    string HostVersion { get; }

    /// <summary>插件专属数据目录（已创建），配置与缓存放这里。</summary>
    string DataDirectory { get; }

    PluginCapability Capabilities { get; }

    bool HasCapability(PluginCapability capability) => (Capabilities & capability) == capability;

    IPluginLogger Log { get; }

    IPluginConfig Config { get; }

    /// <summary>
    ///     宿主事件订阅（需要清单里声明 <c>events</c> 能力）。插件卸载 / 重扫时宿主会清空订阅。
    /// </summary>
    IPluginEventBus Events { get; }

    /// <summary>
    ///     弹提示 / 确认框（需要清单里声明 <c>ui.notify</c> 能力）。
    /// </summary>
    IPluginNotify Notify { get; }

    /// <summary>Minecraft 只读信息（需要 <c>minecraft.read</c> 能力）。</summary>
    IMinecraftService Minecraft { get; }

    /// <summary>出站 HTTP（需要 <c>net-http</c> 能力，且用户授权后才会真的发请求）。</summary>
    IPluginHttp Net { get; }

    /// <summary>文件能力（需要 <c>file-game-dir</c> / <c>file-user-pick</c>，同样要用户授权）。</summary>
    IPluginFiles Files { get; }

    /// <summary>
    ///     当前界面语言码（<c>zh-CN</c> / <c>zh-TW</c> / <c>en-US</c> …）。
    ///     插件要按语言出文案时用它 + <see cref="PluginLocalization.Pick" />，
    ///     语言切换会发 <see cref="LanguageChangedEvent" />。
    /// </summary>
    string LanguageCode { get; }
}
