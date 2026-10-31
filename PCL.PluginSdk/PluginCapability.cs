namespace PCL.PluginSdk;

/// <summary>
///     插件能力（权限）。清单里的 <c>capabilities</c> 用字符串声明，宿主解析成这个 flags。
/// </summary>
[Flags]
public enum PluginCapability
{
    None = 0,

    /// <summary>注册右侧页面。</summary>
    UiPage = 1 << 0,

    /// <summary>注册侧边栏条目 / 分组。</summary>
    UiSidebar = 1 << 1,

    /// <summary>注册设置页分区。</summary>
    UiSettings = 1 << 2,

    /// <summary>弹提示条 / 确认框。</summary>
    UiNotify = 1 << 3,

    /// <summary>拥有自己的配置（默认允许）。</summary>
    Config = 1 << 4,

    /// <summary>订阅宿主事件。</summary>
    Events = 1 << 5,

    /// <summary>读写自己的插件数据目录（默认允许）。</summary>
    FilePluginData = 1 << 6,

    /// <summary>读写游戏目录 / 实例目录（中危）。</summary>
    FileGameDir = 1 << 7,

    /// <summary>由用户选择任意路径后读写（中危）。</summary>
    FileUserPick = 1 << 8,

    /// <summary>出站 HTTP 请求（中危）。</summary>
    NetHttp = 1 << 9,

    /// <summary>修改游戏启动参数（高危）。</summary>
    GameArgv = 1 << 10,

    /// <summary>启动外部进程（高危）。</summary>
    Process = 1 << 11,

    /// <summary>读取当前主题信息（写主题永不开放）。</summary>
    ThemeRead = 1 << 12,

    /// <summary>提供插件自带主题（<see cref="IPluginCustomThemesProvider" />）。</summary>
    ThemesCustom = 1 << 13,

    /// <summary>读取 Minecraft 实例信息（<see cref="IMinecraftService" />，只读）。</summary>
    MinecraftRead = 1 << 14
}
