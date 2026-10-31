using System.Windows;

namespace PCL.PluginSdk;

/// <summary>
///     一个由插件提供的页面。宿主负责侧栏条目、切换动画与容器，插件只提供标题 / 图标 / 内容。
///     <para>
///         需要清单 <c>capabilities</c> 里声明 <c>ui.page</c>（侧栏条目另需 <c>ui.sidebar</c>，
///         宿主目前把两者视为一体：注册了页面就会出现侧栏条目）。
///     </para>
///     <para>
///         两个硬性约定：
///         <list type="bullet">
///             <item><see cref="CreatePage" /> 每次打开都会被调用，**每次都返回新实例**，不要返回同一个控件；</item>
///             <item>页面里的异步动作必须自己 <c>try/catch</c>；宿主只在创建时兜底一次，之后你的按钮事件由你负责。</item>
///         </list>
///     </para>
/// </summary>
public sealed class PluginPageDefinition
{
    /// <summary>页面 id，插件内唯一（用作侧栏条目的标识）。</summary>
    public string Id { get; set; } = "";

    /// <summary>侧栏与页面标题。</summary>
    public string Title { get; set; } = "";

    /// <summary>页面顶部的一句话说明，可为空。</summary>
    public string? Description { get; set; }

    /// <summary>图标：lucide 名（如 <c>lucide/puzzle</c>），留空用默认图标。</summary>
    public string? Icon { get; set; }

    /// <summary>侧栏排序（小的在前，宿主内置页面为 0~5）。</summary>
    public int SortOrder { get; set; } = 100;

    /// <summary>
    ///     创建页面内容。宿主会把它放进可滚动的页面容器里，并把插件上下文交给你
    ///     （日志 / 配置 / 数据目录 / 事件订阅都在里面）。
    /// </summary>
    public Func<IPluginContext, FrameworkElement>? CreatePage { get; set; }
}

/// <summary>
///     插件可以提供自己的页面（需要 <c>ui.page</c> 能力）。
/// </summary>
public interface IPluginUiProvider
{
    IReadOnlyList<PluginPageDefinition> GetPages();
}
