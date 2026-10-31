namespace PCL.PluginSdk;

public enum PluginLogLevel
{
    Trace = 0,
    Debug = 1,
    Info = 2,
    Warn = 3,
    Error = 4
}

/// <summary>
///     插件日志。宿主会统一加上 <c>[Plugin:&lt;id&gt;]</c> 前缀。
/// </summary>
public interface IPluginLogger
{
    void Log(PluginLogLevel level, string message);
    void Log(PluginLogLevel level, string message, Exception ex);

    void Trace(string message) => Log(PluginLogLevel.Trace, message);
    void Debug(string message) => Log(PluginLogLevel.Debug, message);
    void Info(string message) => Log(PluginLogLevel.Info, message);
    void Warn(string message) => Log(PluginLogLevel.Warn, message);
    void Error(string message) => Log(PluginLogLevel.Error, message);
    void Error(string message, Exception ex) => Log(PluginLogLevel.Error, message, ex);
}
