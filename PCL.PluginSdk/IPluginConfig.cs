namespace PCL.PluginSdk;

/// <summary>
///     插件配置。宿主负责持久化到 <c>&lt;插件数据目录&gt;/config.json</c>。
///     <para>键名建议用点分小写，如 <c>ui.showBadge</c>。</para>
/// </summary>
public interface IPluginConfig
{
    T Get<T>(string key, T defaultValue);

    void Set<T>(string key, T? value);

    bool Remove(string key);

    /// <summary>任意键变更时触发（参数为键名）。</summary>
    event Action<string>? Changed;

    /// <summary>立即落盘（正常情况下宿主会自动保存，无需手动调用）。</summary>
    void Save();
}
