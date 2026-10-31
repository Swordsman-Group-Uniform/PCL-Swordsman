using System.Windows;

namespace PCL.PluginSdk;

/// <summary>
///     由插件**自带**的主题（不是宿主内置主题槽位，完全由插件定义）。
///     <para>
///         与 <see cref="IExtraThemesProvider" /> 的区别：那边的 Key 必须落在宿主内置槽位上，
///         只能改名字 / 主色 / 风格；这里的 <see cref="Id" /> 是插件自己的字符串，
///         宿主会给它单独分配一个主题槽位，所以插件可以带任意多个主题，还能配自己的标题栏装饰。
///     </para>
///     <para>
///         需要清单 <c>capabilities</c> 里声明 <c>themes.custom</c>。
///     </para>
/// </summary>
public sealed class PluginCustomThemeDefinition
{
    /// <summary>全局唯一的主题 id（建议 <c>插件id.主题名</c>，如 <c>faded.gray</c>）。</summary>
    public string Id { get; set; } = "";

    /// <summary>主题下拉框里显示的名字。</summary>
    public string DisplayName { get; set; } = "";

    /// <summary>主色，<c>#RRGGBB</c>。</summary>
    public string Hex { get; set; } = "";

    /// <summary>一句话说明，可为空。</summary>
    public string? Description { get; set; }

    /// <summary>
    ///     可选的特殊配色风格：<c>golden-dusk</c> / <c>sakura-bloom</c>；
    ///     留空表示按 <see cref="Hex" /> 做通用的按档加权配色。
    /// </summary>
    public string? Flavor { get; set; }

    /// <summary>主题下拉框里的排序（小的在前）。</summary>
    public int SortOrder { get; set; } = 100;

    /// <summary>
    ///     标题栏右上角的装饰（可选）。宿主会把它放进标题栏右侧的容器里：
    ///     请自行设置 <c>HorizontalAlignment</c> / <c>Width</c> / <c>Height</c> 与透明度，
    ///     宿主只保证「不拦鼠标事件」「跟随主题显隐」「切换主题时重建」。
    /// </summary>
    public Func<FrameworkElement>? CreateTitleDecoration { get; set; }
}

/// <summary>
///     插件可以提供自己定义的主题（需要 <c>themes.custom</c> 能力）。
/// </summary>
public interface IPluginCustomThemesProvider
{
    IReadOnlyList<PluginCustomThemeDefinition> GetCustomThemes();
}
