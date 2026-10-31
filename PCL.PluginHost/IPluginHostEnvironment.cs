using PCL.PluginSdk;

namespace PCL.PluginHost;

/// <summary>
///     宿主环境：由启动器实现，插件宿主不直接依赖宿主程序集。
/// </summary>
public interface IPluginHostEnvironment
{
    /// <summary>宿主版本（展示用），如 <c>27.1-snapshot-8(2.15.1)</c>。</summary>
    string HostVersion { get; }

    /// <summary>插件根目录：<c>&lt;启动器目录&gt;/PCL/Plugins</c>。</summary>
    string PluginsDirectory { get; }

    /// <summary>插件数据根目录：<c>&lt;启动器目录&gt;/PCL/PluginData</c>。</summary>
    string PluginDataDirectory { get; }

    /// <summary>当前界面语言码（zh-CN / en-US …），每次访问都读当前值。</summary>
    string LanguageCode { get; }

    /// <summary>提示 / 确认框的宿主实现（由启动器注入）。</summary>
    IPluginNotifyHost Notifier { get; }

    /// <summary>授权查询 / 申请（由启动器注入）。</summary>
    IPluginPermissionHost Permissions { get; }

    /// <summary>Minecraft 只读服务（由启动器注入）。</summary>
    IMinecraftService Minecraft { get; }

    /// <summary>弹「选择文件」对话框（阻塞到用户选择），取消返回 null。</summary>
    string? PickFile(string? filter = null);

    /// <summary>弹「选择文件夹」对话框（阻塞到用户选择），取消返回 null。</summary>
    string? PickFolder();

    /// <summary>写宿主日志（已带级别）。</summary>
    void Log(string message, bool isError = false, Exception? exception = null);
}
