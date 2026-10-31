namespace PCL.PluginSdk;

/// <summary>插件设置项的类型（决定宿主渲染什么控件）。</summary>
public enum PluginSettingType
{
    /// <summary>开关（存 <c>bool</c>）。</summary>
    Toggle = 0,

    /// <summary>单行文本（存 <c>string</c>）。</summary>
    Text = 1,

    /// <summary>整数（存 <c>long</c>，可用 <see cref="PluginSettingDefinition.Min" /> / <see cref="PluginSettingDefinition.Max" /> 限制范围）。</summary>
    Integer = 2,

    /// <summary>小数（存 <c>double</c>，同样支持 Min / Max）。</summary>
    Decimal = 3,

    /// <summary>下拉选择（存选项的 <see cref="PluginSettingOption.Value" />）。</summary>
    Choice = 4
}

/// <summary>下拉选项。</summary>
public sealed class PluginSettingOption
{
    /// <summary>存进配置的值（配置里就是这个字符串）。</summary>
    public string Value { get; set; } = "";

    /// <summary>显示给用户的文字。</summary>
    public string Title { get; set; } = "";
}

/// <summary>
///     一条插件设置的定义。宿主据此在「插件管理 → 插件设置」里生成控件，
///     读写都走插件自己的 <see cref="IPluginConfig" />（键就是 <see cref="Key" />），
///     所以插件不需要自己写设置界面，也不需要自己保存 —— 宿主改完会立刻落盘。
/// </summary>
public sealed class PluginSettingDefinition
{
    /// <summary>配置键，建议点分小写，如 <c>ui.showBadge</c>。</summary>
    public string Key { get; set; } = "";

    /// <summary>显示名（插件自己给文案，宿主不做本地化）。</summary>
    public string Title { get; set; } = "";

    /// <summary>说明文字，可为空。</summary>
    public string? Description { get; set; }

    public PluginSettingType Type { get; set; } = PluginSettingType.Toggle;

    /// <summary>
    ///     默认值的**字符串形式**：开关写 <c>true</c>/<c>false</c>，数字写 <c>1.5</c>，
    ///     下拉写选项的 <c>Value</c>。宿主在首次渲染时用它兜底。
    /// </summary>
    public string? DefaultValue { get; set; }

    /// <summary>文本类控件的占位提示。</summary>
    public string? Placeholder { get; set; }

    /// <summary>数字类控件的最小值（<see langword="null" /> 表示不限制）。</summary>
    public double? Min { get; set; }

    /// <summary>数字类控件的最大值（<see langword="null" /> 表示不限制）。</summary>
    public double? Max { get; set; }

    /// <summary><see cref="PluginSettingType.Choice" /> 的候选。</summary>
    public List<PluginSettingOption> Options { get; set; } = [];
}

/// <summary>
///     插件可以把自己的设置项交给宿主渲染（需要 <c>capabilities</c> 里有 <c>config</c>）。
///     <para>
///         只声明结构即可：读写由宿主通过 <see cref="IPluginConfig" /> 完成，
///         插件自己用 <c>Context.Config.Get&lt;T&gt;(key, 默认值)</c> 读回即可。
///         每次打开设置面板宿主都会重新调用 <see cref="GetSettings" />，所以可以按状态动态给不同项。
///     </para>
/// </summary>
public interface IPluginSettingsProvider
{
    IReadOnlyList<PluginSettingDefinition> GetSettings();
}
