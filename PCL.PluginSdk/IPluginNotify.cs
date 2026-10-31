namespace PCL.PluginSdk;

/// <summary>插件提示的级别（决定右下角提示的颜色与图标）。</summary>
public enum PluginNotifyLevel
{
    Info = 0,
    Success = 1,
    Warning = 2,
    Error = 3
}

/// <summary>
///     插件弹提示 / 确认框。需要清单 <c>capabilities</c> 里声明 <c>ui.notify</c>。
///     <para>
///         在哪个线程调用都行：宿主会把 UI 操作切回主线程。
///         <see cref="Confirm" /> 会**阻塞**到用户点掉按钮（和启动器内部的确认框一样），
///         因此不要在 UI 线程里连环弹、也不要在需要保持响应的循环里调用它。
///     </para>
/// </summary>
public interface IPluginNotify
{
    /// <summary>右下角小提示（信息）。</summary>
    void Info(string text);

    /// <summary>右下角小提示（成功）。</summary>
    void Success(string text);

    /// <summary>右下角小提示（警告）。</summary>
    void Warning(string text);

    /// <summary>右下角小提示（错误）。</summary>
    void Error(string text);

    /// <summary>
    ///     弹一个「确定 / 取消」确认框，用户选择后回调（在主线程）。
    /// </summary>
    /// <param name="title">标题。</param>
    /// <param name="message">正文。</param>
    /// <param name="onResult">回调：<see langword="true" /> = 确定，<see langword="false" /> = 取消。</param>
    void Confirm(string title, string message, Action<bool> onResult);
}

/// <summary>
///     宿主侧实现：真正把提示 / 确认框显示出来的地方（插件宿主不直接依赖启动器程序集）。
/// </summary>
public interface IPluginNotifyHost
{
    /// <summary>显示一条提示（实现方负责切回 UI 线程）。</summary>
    void Show(string pluginId, string text, PluginNotifyLevel level);

    /// <summary>显示确认框并在用户选择后回调（实现方负责切回 UI 线程）。</summary>
    void Confirm(string pluginId, string title, string message, Action<bool> onResult);
}
