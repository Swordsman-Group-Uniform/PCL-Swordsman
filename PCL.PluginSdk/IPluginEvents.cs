namespace PCL.PluginSdk;

/// <summary>
///     插件的事件总线。**宿主发布，插件订阅**（插件不能自己发事件，避免互相干扰）。
///     <para>
///         需要在 <c>plugin.json</c> 的 <c>capabilities</c> 里声明 <c>events</c>；
///         没有声明时订阅会返回一个空的 <see cref="IDisposable" /> 并在日志里留一条警告，不会抛异常。
///     </para>
///     <para>
///         回调在**发布方所在的线程**上执行（宿主的事件基本都在 UI 线程），
///         所以插件里应该在回调里自行 <c>try/catch</c>，耗时操作请自己切后台，不要卡 UI。
///         插件卸载 / 重扫时宿主会自动清空所有订阅，<see cref="IDisposable.Dispose" /> 也可以手动提前退订。
///     </para>
/// </summary>
public interface IPluginEventBus
{
    /// <summary>订阅一种事件，返回的对象 <c>Dispose()</c> 后即退订。</summary>
    IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class;
}

/// <summary>启动器启动完成（主窗口已加载、插件已全部就绪）。</summary>
/// <param name="HostVersion">宿主版本，如 <c>27.1-snapshot-57(2.15.1)</c>。</param>
/// <param name="LaunchCount">累计启动次数（第一次启动为 1）。</param>
public sealed record HostStartupCompletedEvent(string HostVersion, int LaunchCount);

/// <summary>配色主题发生变化（切主题或切换亮 / 暗模式都会发）。</summary>
/// <param name="IsDarkMode">当前是否深色模式。</param>
/// <param name="ThemeKey">主题的枚举名，如 <c>SakuraPink</c>。</param>
/// <param name="ThemeDisplayName">主题的展示名，如「樱花粉」。</param>
public sealed record ThemeChangedEvent(bool IsDarkMode, string ThemeKey, string ThemeDisplayName);

/// <summary>即将启动一个 Minecraft 实例（参数已就绪、进程还没起）。</summary>
/// <param name="InstanceName">实例名。</param>
/// <param name="InstancePath">实例目录绝对路径。</param>
public sealed record GameLaunchingEvent(string InstanceName, string InstancePath);

/// <summary>启动流程走完（游戏进程已拉起，或按设置隐藏 / 退出启动器）。</summary>
/// <param name="InstanceName">实例名。</param>
public sealed record GameLaunchedEvent(string InstanceName);

/// <summary>当前选中的 Minecraft 实例发生变化。</summary>
/// <param name="InstanceName">新实例名（取消选择时为空串）。</param>
/// <param name="InstancePath">新实例目录（取消选择时为空串）。</param>
public sealed record InstanceSwitchedEvent(string InstanceName, string InstancePath);

/// <summary>一个新的下载类任务开始（进任务栏的粗粒度任务）。</summary>
/// <param name="TaskName">任务名，如「下载 Mod」。 </param>
public sealed record DownloadStartedEvent(string TaskName);

/// <summary>下载类任务结束。</summary>
/// <param name="TaskName">任务名。</param>
/// <param name="Success">是否成功（中止 / 失败为 false）。</param>
public sealed record DownloadFinishedEvent(string TaskName, bool Success);

/// <summary>语言被切换。</summary>
/// <param name="LanguageCode">语言代码，如 <c>zh-CN</c>。</param>
public sealed record LanguageChangedEvent(string LanguageCode);
