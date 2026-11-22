using System.Windows;

namespace PCL.PluginSdk;

/// <summary>
///     插件往「实例 → Mod 管理」页面注册自己的面板（需要清单能力 <c>instance.mod-panel</c>）。
///     <para>
///         宿主负责容器与位置，插件只提供内容：面板会渲染在 Mod 管理页的按钮区下方。
///         <see cref="CreatePanel" /> 每次刷新都会重新调用，**每次都要返回新控件**。
///     </para>
/// </summary>
public sealed class PluginModPanelDefinition
{
    /// <summary>面板 id，插件内唯一。</summary>
    public string Id { get; set; } = "";

    /// <summary>面板标题（渲染成一张卡片的小标题）。</summary>
    public string Title { get; set; } = "";

    /// <summary>标题下方的一句话说明，可为空。</summary>
    public string? Description { get; set; }

    /// <summary>排序（小的在前）。</summary>
    public int SortOrder { get; set; } = 100;

    /// <summary>
    ///     只在指定加载器下显示：<c>Fabric</c> / <c>Quilt</c> / <c>Forge</c> / <c>NeoForge</c>；
    ///     留空表示任何实例都显示。
    /// </summary>
    public string? Loader { get; set; }

    /// <summary>创建面板内容。宿主的滚动容器已经就绪，直接返回自己的控件即可。</summary>
    public Func<IPluginContext, FrameworkElement>? CreatePanel { get; set; }
}

/// <summary>
///     插件可以往 Mod 管理页注册面板（需要 <see cref="PluginCapability.InstanceModPanel" />）。
/// </summary>
public interface IPluginInstanceModPanelProvider
{
    IReadOnlyList<PluginModPanelDefinition> GetModPanels();
}

/// <summary>
///     启动前守门：在 Minecraft 真正启动**之前**被调用，可以阻止本次启动
///     （需要清单能力 <c>game.launch-gate</c>）。
///     <para>
///         典型用途：检查 Mod 冲突 / 依赖缺失，弹出警告让用户决定要不要继续。
///         <b>回调在 UI 线程</b>，可以安全地调 <see cref="IPluginNotify.Confirm" />；
///         但**不要做耗时操作**（解析几百个 jar 也请提前缓存好），否则会拖慢启动。
///     </para>
///     <para>
///         多个插件都注册了守门时：任意一个返回 <see cref="PluginLaunchGateResult.Deny" /> 就取消本次启动，
///         且**后面的插件不再被询问**（按插件加载顺序）。
///     </para>
/// </summary>
public interface IPluginLaunchGate
{
    /// <summary>
    ///     返回 <see langword="null" /> 表示「不干预」；返回
    ///     <see cref="PluginLaunchGateResult.Deny" /> 表示阻止启动（宿主会提示
    ///     <see cref="PluginLaunchGateResult.Message" />）。
    /// </summary>
    PluginLaunchGateResult? BeforeLaunch(PluginLaunchGateContext context);
}

/// <summary>启动前守门拿到的信息（只读）。</summary>
public sealed class PluginLaunchGateContext
{
    /// <summary>实例名（实例文件夹名）。</summary>
    public string InstanceName { get; set; } = "";

    /// <summary>实例目录绝对路径；取不到时为空串。</summary>
    public string InstancePath { get; set; } = "";

    /// <summary>原版版本号，如 <c>1.20.1</c>；取不到时为空串。</summary>
    public string VersionName { get; set; } = "";

    /// <summary>加载器名称（Fabric / Forge / NeoForge / Quilt），纯原版时为空串。</summary>
    public string LoaderName { get; set; } = "";

    /// <summary><c>mods</c> 目录里的 jar 数量。</summary>
    public int ModCount { get; set; }
}

/// <summary>启动前守门的结果。</summary>
public sealed class PluginLaunchGateResult
{
    /// <summary>是否允许启动。</summary>
    public bool Allow { get; set; } = true;

    /// <summary>拒绝启动时给用户看的一句话（可空；插件通常已经自己弹过确认框）。</summary>
    public string? Message { get; set; }

    /// <summary>允许启动。</summary>
    public static PluginLaunchGateResult Permit() => new() { Allow = true };

    /// <summary>阻止启动。</summary>
    public static PluginLaunchGateResult Deny(string? message = null) => new() { Allow = false, Message = message };
}
