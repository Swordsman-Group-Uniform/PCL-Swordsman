namespace PCL.Core.App;

/// <summary>
/// 联机协议偏好
/// </summary>
public enum LinkProtocolPreference
{
    Tcp,
    Udp
}

/// <summary>
/// 文件下载使用的 HTTP 协议模式。
/// </summary>
public enum DownloadHttpMode
{
    Auto = 0,
    Http11 = 1,
    Http2 = 2
}

/// <summary>
/// 主题模式（亮/暗/系统）
/// </summary>
public enum ColorMode
{
    Light = 0,
    Dark = 1,
    System = 2
}

/// <summary>
/// 配色主题
/// </summary>
public enum ColorTheme
{
    SkyBlue = 0,
    CatBlue = 1,
    DeathBlue = 2,
    HmclBlue = 3,

    /// <summary>
    /// 自定义主题色，颜色由 <c>UiCustomColor</c> 配置项以 #RRGGBB 形式指定。
    /// </summary>
    Custom = 4,

    /// <summary>
    /// 肥鱼蓝（#5A67AC）。
    /// </summary>
    FatFishBlue = 5,

    /// <summary>
    /// 天依蓝（#66CCFF）。
    /// </summary>
    TianyiBlue = 6,

    /// <summary>
    /// 剑客黄（#E8B33C）。使用该主题时标题栏会显示半透明钻石剑装饰。
    /// </summary>
    SwordsmanYellow = 7,

    /// <summary>
    /// 初音绿（#39C5BB）。
    /// </summary>
    MikuGreen = 8,

    /// <summary>
    /// 海人蓝（#3366CC）。
    /// </summary>
    MermanBlue = 9,

    /// <summary>
    /// 烤音绿（#33CCBB）。
    /// </summary>
    KaoYinGreen = 10,

    /// <summary>
    /// 一歌蓝（#33AAEE）。
    /// </summary>
    IchikaBlue = 11,

    /// <summary>
    /// 樱花粉（#FF94D6，取色自樱花披风）。使用该主题时标题栏右侧会显示半透明樱花枝条装饰。
    /// </summary>
    SakuraPink = 12,

    /// <summary>
    /// 下界红（#B3392E）。使用该主题时整条标题栏会覆盖一层半透明的地狱疣块贴图。
    /// </summary>
    NetherRed = 13,

    /// <summary>
    /// 甲基橙（#EE7B1E）。使用该主题时标题栏右上角会显示半透明的甲基橙化学式（像素字，不依赖字体文件）。
    /// </summary>
    MethylOrange = 14,

    /// <summary>
    /// 水晶紫（#9A5CE0）。使用该主题时整条标题栏会覆盖一层半透明的紫水晶块贴图。
    /// </summary>
    AmethystPurple = 15,

    /// <summary>
    /// 插件自带的主题。具体是哪个由配置里的插件主题 id 决定
    /// （<c>UiLightPluginTheme</c> / <c>UiDarkPluginTheme</c>），
    /// 主题下拉框里每个插件主题都映射到这个槽位。
    /// </summary>
    PluginDefined = 16
}

/// <summary>
/// 更新通道
/// </summary>
public enum UpdateChannel
{
    Release = 0,
    Beta = 1,
    Dev = 2
}

/// <summary>
/// 游戏窗口大小模式
/// </summary>
public enum GameWindowSizeMode
{
    Fullscreen = 0,
    Default = 1,
    Launcher = 2,
    Custom = 3,
    Maximized = 4
}

/// <summary>
/// 游戏进程优先级
/// </summary>
public enum GameProcessPriority
{
    AboveNormal = 0,
    Normal = 1,
    BelowNormal = 2,
    High = 3,
    RealTime = 4
}

/// <summary>
/// 游戏启动后启动器可见性
/// </summary>
public enum LauncherVisibility
{
    ExitImmediately = 0,
    ObsoleteCaseDoNotUse = 1,
    HideAndExit = 2,
    HideAndReopen = 3,
    MinimizeAndReopen = 4,
    DoNothing = 5
}

/// <summary>
/// JVM 优先 IP 栈类型
/// </summary>
public enum JvmPreferredIpStack
{
    PreferV4 = 0,
    Default = 1,
    PreferV6 = 2
}

/// <summary>
/// 联机中继行为
/// </summary>
public enum LinkRelayBehavior
{
    Default = 0,
    ForceRelay = 1
}

/// <summary>
/// 启动器更新行为
/// </summary>
public enum LauncherAutoUpdateBehavior
{
    DownloadAndInstall = 0,
    DownloadAndAnnounce = 1,
    AnnounceOnly = 2,
    Disable = 3
}

public enum LauncherTitleType
{
    None = 0,
    Default = 1,
    Text = 2,
    Image = 3
}
