namespace PCL.PluginSdk;

/// <summary>插件相关异常的基类。</summary>
public class PluginException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>插件调用了自己未申请 / 已被撤销的能力。</summary>
public sealed class PluginCapabilityException(PluginCapability capability, string? pluginId = null)
    : PluginException($"插件{(pluginId is null ? "" : " " + pluginId)} 未获授权的能力：{capability}");
