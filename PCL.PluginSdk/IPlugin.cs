namespace PCL.PluginSdk;

/// <summary>
///     插件入口。宿主通过清单里的 <c>entry.type</c> 反射创建实例（需有无参构造函数）。
///     <para>
///         生命周期：<see cref="Initialize" /> → <see cref="Enable" /> → …… → <see cref="Disable" /> → <see cref="Dispose" />。
///         任何回调都应自行 try/catch，宿主也会兜底记录异常。
///     </para>
/// </summary>
public interface IPlugin
{
    /// <summary>
    ///     初始化：注册页面 / 侧边栏 / 设置项 / 事件。**不要在这里做耗时操作**。
    /// </summary>
    void Initialize(IPluginContext context);

    /// <summary>启用：可以启动后台任务、定时器。</summary>
    void Enable()
    {
    }

    /// <summary>禁用：停止后台任务。</summary>
    void Disable()
    {
    }

    /// <summary>卸载前清理。事件订阅请在这里取消。</summary>
    void Dispose()
    {
    }
}
