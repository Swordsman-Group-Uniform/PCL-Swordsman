namespace PCL.PluginSdk;

/// <summary>插件在宿主里的状态（管理页展示用）。</summary>
public enum PluginState
{
    /// <summary>发现，尚未处理。</summary>
    Discovered = 0,

    /// <summary>清单合法且已加载程序集。</summary>
    Loaded = 1,

    /// <summary>已启用（Initialize + Enable 成功）。</summary>
    Enabled = 2,

    /// <summary>被用户禁用（不会加载）。</summary>
    Disabled = 3,

    /// <summary>出错（清单非法 / 加载异常 / apiVersion 不匹配）。</summary>
    Faulted = 4,

    /// <summary>与当前宿主版本不兼容。</summary>
    Incompatible = 5
}
