using System.Collections.Generic;

namespace PCL.Core.App;

/// <summary>
///     运行期功能开关。<br/>
///     被「屏蔽」的功能（非蓝色主题、自定义主题色、回声洞、各类限制解除）在正式版里默认不可用，
///     启动时会根据**已启用插件提供的功能键**（<c>plugin.json</c> 的 <c>provides</c>）填充这里。
///     <para>
///         实现仍然留在宿主里，插件只声明「这些功能可用」并提供数据，
///         因此渲染、门禁与安全检查依旧由宿主掌握。
///     </para>
/// </summary>
public static class FeatureFlags
{
    /// <summary>
    ///     已知功能键（与 <c>PCL.PluginSdk.PluginFeatureKeys</c> 保持一致）。
    /// </summary>
    public static class Keys
    {
        /// <summary>扩展主题（所有非蓝色主题 + 自定义主题色选择器）。</summary>
        public const string ExtendedThemes = "themes.extended";

        /// <summary>自定义主题色（与扩展主题分开的功能键）。</summary>
        public const string CustomThemeColor = "themes.custom-color";

        /// <summary>回声洞。</summary>
        public const string EchoCave = "echo-cave";

        /// <summary>解除中国大陆地区相关下载限制。</summary>
        public const string UnlockRegion = "unlock.region";

        /// <summary>解除「必须先绑定正版账号才能创建离线 / 外置档案」的限制。</summary>
        public const string UnlockOfflineAuth = "unlock.offline-auth";

        /// <summary>高级材质（云母 / Aero 窗口材质、悬停发光）。</summary>
        public const string MaterialsAdvanced = "materials.advanced";

        /// <summary>启动时的欢迎语。</summary>
        public const string WelcomeMessages = "welcome.messages";
    }

    private static readonly HashSet<string> _enabled = new(System.StringComparer.OrdinalIgnoreCase);
    private static readonly object _lock = new();

    /// <summary>功能开关发生变化时触发（在设置开关的那个线程上）。</summary>
    public static event System.Action? Changed;

    /// <summary>用插件的功能键集合整体替换当前开关（启动时调用一次）。</summary>
    public static void SetAll(IEnumerable<string>? features)
    {
        lock (_lock)
        {
            _enabled.Clear();
            if (features is not null)
                foreach (var feature in features)
                {
                    if (string.IsNullOrWhiteSpace(feature)) continue;
                    _enabled.Add(feature.Trim());
                }
        }

        Changed?.Invoke();
    }

    /// <summary>某个功能当前是否可用。</summary>
    public static bool IsEnabled(string feature)
    {
        if (string.IsNullOrWhiteSpace(feature)) return false;
        lock (_lock) return _enabled.Contains(feature.Trim());
    }

    /// <summary>当前可用功能键快照。</summary>
    public static IReadOnlyCollection<string> EnabledFeatures
    {
        get
        {
            lock (_lock) return [.._enabled];
        }
    }
}
