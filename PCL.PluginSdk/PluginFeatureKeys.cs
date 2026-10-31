namespace PCL.PluginSdk;

/// <summary>
///     插件可以「提供」的功能键。宿主用这些键决定要不要展示被屏蔽的功能。
///     <para>
///         设计要点：功能**实现**留在宿主（主题引擎、回声洞卡片、登录/下载逻辑），
///         插件只负责**声明功能存在**并提供**数据**（主题名称与颜色、回声洞语录等）。
///         这样正式版不装插件时功能不可达，装了插件就能用，而宿主仍然掌握渲染与门禁。
///     </para>
///     插件在 <c>plugin.json</c> 的 <c>provides</c> 数组里声明这些键。
/// </summary>
public static class PluginFeatureKeys
{
    /// <summary>扩展主题（所有非蓝色主题 + 自定义主题色选择器）。</summary>
    public const string ExtendedThemes = "themes.extended";

    /// <summary>自定义主题色（个性化页的主题色输入框）。</summary>
    public const string CustomThemeColor = "themes.custom-color";

    /// <summary>回声洞（百宝箱里的打字机语录）。</summary>
    public const string EchoCave = "echo-cave";

    /// <summary>高级材质（云母 / Aero）。</summary>
    public const string MaterialsAdvanced = "materials.advanced";

    /// <summary>启动时的欢迎语（右下角小提示）。</summary>
    public const string WelcomeMessages = "welcome.messages";

    /// <summary>解除「中国大陆地区」相关的下载限制。</summary>
    public const string UnlockRegion = "unlock.region";

    /// <summary>解除「必须先绑定正版账号才能创建离线 / 外置档案」的限制。</summary>
    public const string UnlockOfflineAuth = "unlock.offline-auth";

    /// <summary>全部已知功能键（用于管理页展示与拼写校验）。</summary>
    public static readonly string[] All =
    [
        ExtendedThemes, CustomThemeColor, EchoCave, UnlockRegion, UnlockOfflineAuth, MaterialsAdvanced,
        WelcomeMessages
    ];
}
