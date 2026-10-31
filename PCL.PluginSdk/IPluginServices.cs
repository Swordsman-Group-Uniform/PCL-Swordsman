namespace PCL.PluginSdk;

/// <summary>一个 Minecraft 实例的只读信息。</summary>
public sealed class PluginInstanceInfo
{
    /// <summary>实例名（实例文件夹名）。</summary>
    public string Name { get; set; } = "";

    /// <summary>实例目录绝对路径。</summary>
    public string Path { get; set; } = "";

    /// <summary>原版版本号，如 <c>1.20.1</c>；读不到时为空串。</summary>
    public string VersionName { get; set; } = "";

    /// <summary>加载器名称（Forge / Fabric / NeoForge …），纯原版时为空串。</summary>
    public string LoaderName { get; set; } = "";

    /// <summary>加载器版本号，可为空。</summary>
    public string LoaderVersion { get; set; } = "";

    /// <summary><c>mods</c> 目录里的 <c>.jar</c> 数量（只是快速计数，不解析依赖）。</summary>
    public int ModCount { get; set; }
}

/// <summary>
///     Minecraft 只读服务（需要清单里声明 <c>minecraft.read</c> 能力）。
///     <para>只读：不提供安装、删除、改配置等写操作；写操作走之后的高危 API。</para>
/// </summary>
public interface IMinecraftService
{
    /// <summary>当前选中的实例（没有实例时为 <see langword="null" />）。</summary>
    PluginInstanceInfo? CurrentInstance { get; }

    /// <summary>已加载的实例列表（可能为空，取决于用户是否进过启动页）。</summary>
    IReadOnlyList<PluginInstanceInfo> Instances { get; }

    /// <summary>游戏根目录（<c>.minecraft</c>）。</summary>
    string GameDirectory { get; }
}

/// <summary>一次 HTTP 响应的只读视图。</summary>
public sealed class PluginHttpResponse
{
    /// <summary>HTTP 状态码。</summary>
    public int StatusCode { get; set; }

    /// <summary>响应正文（按 UTF-8 解码）。</summary>
    public string Body { get; set; } = "";

    /// <summary>响应头。</summary>
    public IReadOnlyDictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();

    /// <summary>是否 2xx。</summary>
    public bool IsSuccess => StatusCode is >= 200 and < 300;
}

/// <summary>
///     出站 HTTP（需要 <c>net-http</c> 能力，且用户授权后才会真的发请求）。
///     <para>同步接口：宿主内部用 <see cref="System.Threading.Tasks.Task" /> 跑完后阻塞返回，方便插件在任意线程调用。</para>
/// </summary>
public interface IPluginHttp
{
    /// <summary>发一个 GET 请求。</summary>
    PluginHttpResponse Get(string url, IReadOnlyDictionary<string, string>? headers = null, int timeoutSeconds = 15);

    /// <summary>发一个 POST 请求。</summary>
    PluginHttpResponse Post(string url, string body, string contentType = "application/json",
        IReadOnlyDictionary<string, string>? headers = null, int timeoutSeconds = 15);
}

/// <summary>
///     插件能碰的文件能力（都要用户授权）。
///     <para>
///         <see cref="GameDirectory" /> 需要 <c>file-game-dir</c>；
///         <see cref="PickOpenFile" /> / <see cref="PickFolder" /> 需要 <c>file-user-pick</c>（由用户自己选路径）。
///     </para>
/// </summary>
public interface IPluginFiles
{
    /// <summary>游戏根目录；没授权或读不到时返回 <see langword="null" />。</summary>
    string? GameDirectory { get; }

    /// <summary>弹「选择文件」对话框，返回所选路径；取消或没授权返回 <see langword="null" />。</summary>
    string? PickOpenFile(string? filter = null);

    /// <summary>弹「选择文件夹」对话框，返回所选路径；取消或没授权返回 <see langword="null" />。</summary>
    string? PickFolder();
}

/// <summary>
///     宿主侧的授权查询 / 申请（插件宿主不直接弹界面，交给启动器实现）。
/// </summary>
public interface IPluginPermissionHost
{
    /// <summary>已经授权过（为 true 时宿主不再询问）。</summary>
    bool IsGranted(string pluginId, PluginCapability capability);

    /// <summary>曾经拒绝过（为 true 时不再重复弹窗，直接按拒绝处理）。</summary>
    bool IsDenied(string pluginId, PluginCapability capability);

    /// <summary>弹确认框申请授权（阻塞到用户选择），并把结果持久化。</summary>
    bool RequestGrant(string pluginId, string pluginName, PluginCapability capability, string description);
}
