namespace PCL.PluginSdk;

/// <summary>
///     插件扩展 API 版本。
///     <para>
///         只在**破坏性**变更时递增；新增接口成员/新增 DTO 字段不递增（插件是消费者，宿主是唯一实现者）。
///         插件清单里的 <c>apiVersion</c> 必须与 <see cref="Current" /> 相等，否则宿主拒绝加载。
///     </para>
/// </summary>
public static class PluginApiVersion
{
    /// <summary>当前宿主实现的扩展 API 版本。</summary>
    public const int Current = 1;
}
