using System.Text.Json.Serialization;

namespace PCL.PluginSdk;

/// <summary>
///     插件清单（<c>plugin.json</c>）模型。schema v1。
/// </summary>
public sealed class PluginManifest
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";

    [JsonPropertyName("name")] public string Name { get; set; } = "";

    [JsonPropertyName("version")] public string Version { get; set; } = "";

    /// <summary>依赖的宿主扩展 API 版本，必须等于 <see cref="PluginApiVersion.Current" />。</summary>
    [JsonPropertyName("apiVersion")]
    public int ApiVersion { get; set; }

    /// <summary>最低宿主版本，如 <c>27.1.0</c>（M1 只记录，不强制）。</summary>
    [JsonPropertyName("minHostVersion")]
    public string? MinHostVersion { get; set; }

    [JsonPropertyName("author")] public string? Author { get; set; }

    [JsonPropertyName("description")] public string? Description { get; set; }

    [JsonPropertyName("homepage")] public string? Homepage { get; set; }

    [JsonPropertyName("license")] public string? License { get; set; }

    [JsonPropertyName("entry")] public PluginEntryPoint Entry { get; set; } = new();

    [JsonPropertyName("dependencies")]
    public List<PluginDependency> Dependencies { get; set; } = [];

    /// <summary>能力字符串列表，见 <see cref="PluginCapability" />。</summary>
    [JsonPropertyName("capabilities")]
    public List<string> Capabilities { get; set; } = [];

    /// <summary>
    ///     本插件提供的功能键列表，见 <see cref="PluginFeatureKeys" />。
    ///     宿主据此决定是否展示被屏蔽的功能（主题、回声洞、限制解除等）。
    /// </summary>
    [JsonPropertyName("provides")]
    public List<string> Provides { get; set; } = [];

    [JsonPropertyName("ui")] public PluginUiOptions? Ui { get; set; }

    /// <summary>
    ///     可选：插件包内文件的 SHA-256（文件名 → 小写十六进制）。宿主在**安装时**与**每次加载前**校验，
    ///     对不上直接拒绝（包被改过或打包出错）。老插件不写这一项也能正常加载。
    /// </summary>
    [JsonPropertyName("files")] public Dictionary<string, string> Files { get; set; } = [];

    /// <summary>
    ///     可选：插件包签名。签名内容见 <c>PluginIntegrity.ComputePayload</c>；
    ///     所用公钥必须在宿主的信任列表里，否则拒绝加载。
    /// </summary>
    [JsonPropertyName("signature")] public PluginSignature? Signature { get; set; }

    /// <summary>
    ///     可选：更新源 URL（返回 <c>{version, url, sha256, notes}</c> 的 JSON）。
    ///     配上之后管理页可以「检查更新」；留空表示由用户手动替换插件包。
    /// </summary>
    [JsonPropertyName("updateUrl")] public string? UpdateUrl { get; set; }

    /// <summary>把字符串能力解析成 flags；未知字符串被忽略（前向兼容）。</summary>
    public PluginCapability ParseCapabilities()
    {
        var result = PluginCapability.None;
        foreach (var raw in Capabilities)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var name = raw.Trim();
            // 允许 "ui.page" 这类小写点分写法，也允许枚举名
            name = name.Replace(".", "").Replace("-", "").Replace("_", "");
            // 纯数字会被 Enum.TryParse 当成位掩码（"2047" 等于拿到全部能力），只接受字母开头的名字
            if (name.Length == 0 || !char.IsLetter(name[0])) continue;
            if (Enum.TryParse<PluginCapability>(name, true, out var parsed))
                result |= parsed;
        }

        return result;
    }
}

public sealed class PluginEntryPoint
{
    /// <summary>入口程序集文件名（相对插件目录），如 <c>MyPlugin.dll</c>。</summary>
    [JsonPropertyName("assembly")]
    public string Assembly { get; set; } = "";

    /// <summary>实现 <see cref="IPlugin" /> 的类型全名（含命名空间）。</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";
}

public sealed class PluginDependency
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";

    /// <summary>要求的版本范围（M1 只记录，不强制）。</summary>
    [JsonPropertyName("version")]
    public string? Version { get; set; }
}

public sealed class PluginUiOptions
{
    /// <summary>图标：lucide 名（如 <c>lucide/puzzle</c>）或插件内相对路径。</summary>
    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    [JsonPropertyName("sortOrder")] public int SortOrder { get; set; } = 100;
}
