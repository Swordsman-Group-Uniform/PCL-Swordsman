using PCL.PluginSdk;

namespace PCL.PluginHost;

/// <summary>
///     插件日志：统一加 <c>[Plugin:&lt;id&gt;]</c> 前缀后转交宿主。
/// </summary>
internal sealed class PluginLogger(string pluginId, IPluginHostEnvironment environment) : IPluginLogger
{
    public void Log(PluginLogLevel level, string message)
        => environment.Log($"[Plugin:{pluginId}] [{level}] {message}", level >= PluginLogLevel.Error);

    public void Log(PluginLogLevel level, string message, Exception ex)
        => environment.Log($"[Plugin:{pluginId}] [{level}] {message}", true, ex);
}
