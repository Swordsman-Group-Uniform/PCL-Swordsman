namespace PCL.PluginSdk;

/// <summary>
///     一个由插件提供的附加主题定义。<br/>
///     宿主负责把 <see cref="Key" /> 映射到内置主题槽位并完成配色计算，
///     插件只提供「叫什么、是什么颜色、要不要特殊配色」这些数据。
/// </summary>
public sealed class PluginThemeDefinition
{
    /// <summary>
    ///     主题键，必须与宿主内置主题槽位同名（区分大小写），可选值：
    ///     <c>SwordsmanYellow</c>、<c>SakuraPink</c>、<c>NetherRed</c>、<c>MethylOrange</c>、
    ///     <c>AmethystPurple</c>、<c>MikuGreen</c>、<c>KaoYinGreen</c>、<c>TianyiBlue</c>、
    ///     <c>MermanBlue</c>、<c>IchikaBlue</c>。
    /// </summary>
    public string Key { get; set; } = "";

    /// <summary>展示名称（中文，直接显示在主题下拉框里）。</summary>
    public string DisplayName { get; set; } = "";

    /// <summary>主色，<c>#RRGGBB</c>。</summary>
    public string Hex { get; set; } = "";

    /// <summary>一句话说明，可为空。</summary>
    public string? Description { get; set; }

    /// <summary>
    ///     特殊配色风格；留空表示普通十六进制配色。目前宿主支持
    ///     <c>golden-dusk</c>（神圣黄昏 + 渐变，剑客黄专用）、
    ///     <c>sakura-bloom</c>（樱粉 → 桃红 → 梅紫的花瓣渐变，樱花粉专用）。
    /// </summary>
    public string? Flavor { get; set; }
}

/// <summary>
///     插件可以提供附加主题（需要 <see cref="PluginFeatureKeys.ExtendedThemes" /> 功能声明）。
/// </summary>
public interface IExtraThemesProvider
{
    IReadOnlyList<PluginThemeDefinition> GetThemes();
}

/// <summary>
///     插件可以提供回声洞语录（需要 <see cref="PluginFeatureKeys.EchoCave" /> 功能声明）。
/// </summary>
public interface IEchoCaveQuotesProvider
{
    IReadOnlyList<string> GetQuotes();
}

/// <summary>
///     插件可以提供启动欢迎语（需要 <see cref="PluginFeatureKeys.WelcomeMessages" /> 功能声明）。
///     <para>
///         宿主每次启动时**等概率**抽一条，替换占位符后用右下角小提示（爱称「Toast」）显示：
///         <c>LAUNCHER_BOOT_COUNT</c> = 启动次数、<c>TIME</c> = 时段（早上 / 上午 / 中午 / 下午 / 晚上 / 深夜）、
///         <c>PLAYERNAME</c> = 当前档案的玩家名。
///     </para>
/// </summary>
public interface IWelcomeMessagesProvider
{
    IReadOnlyList<string> GetWelcomeMessages();
}
