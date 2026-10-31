using PCL.PluginSdk;

namespace AdvancedMaterials;

/// <summary>
///     高级材质插件：只声明功能与默认参数，渲染由宿主的 MaterialService 负责。
/// </summary>
public sealed class AdvancedMaterialsPlugin : IPlugin
{
    public void Initialize(IPluginContext context)
        => context.Log.Info("高级材质已加载：云母 / Aero 效果可用（默认云母）");
}
