using PCL.PluginSdk;

namespace PCLSCore;

/// <summary>
///     PCLS 核心插件：本插件不含任何行为代码，只在清单里声明
///     <c>unlock.region</c> 与 <c>unlock.offline-auth</c> 两个功能键。
///     宿主看到这两个键之后才会放行对应的限制。
/// </summary>
public sealed class PCLSCorePlugin : IPlugin
{
    public void Initialize(IPluginContext context)
    {
        context.Log.Info("PCLS 核心插件已加载：下载页的中国大陆地区屏蔽已解除，离线 / 外置档案不再要求先绑定正版账号");
    }
}
