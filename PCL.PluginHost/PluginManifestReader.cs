using System.IO;
using System.Text.Json;
using PCL.PluginSdk;

namespace PCL.PluginHost;

/// <summary>清单读取与校验结果。</summary>
public sealed record PluginManifestResult(
    PluginManifest? Manifest,
    PluginCapability Capabilities,
    bool IsCompatible,
    string? Error)
{
    public bool IsValid => Manifest is not null && Error is null;
}

public static class PluginManifestReader
{
    public const string ManifestFileName = "plugin.json";

    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>
    ///     读取并校验一个插件目录（历史格式：解压后的文件夹）的清单。
    ///     新格式请用 <see cref="PluginPackage.ReadManifest" />。
    /// </summary>
    public static PluginManifestResult Read(string pluginDirectory)
    {
        var manifestPath = Path.Combine(pluginDirectory, ManifestFileName);
        if (!File.Exists(manifestPath))
            return new PluginManifestResult(null, PluginCapability.None, false, "缺少 " + ManifestFileName);

        PluginManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<PluginManifest>(File.ReadAllText(manifestPath), _options);
        }
        catch (Exception ex)
        {
            return new PluginManifestResult(null, PluginCapability.None, false, "清单解析失败：" + ex.Message);
        }

        return Validate(manifest, relative => File.Exists(Path.Combine(pluginDirectory, relative)));
    }

    /// <summary>
    ///     校验已经解析出来的清单。<paramref name="assemblyExists" /> 用来确认入口程序集存在：
    ///     目录格式传「目录里有没有这个文件」，包格式传「zip 里有没有这个条目」。
    /// </summary>
    public static PluginManifestResult Validate(PluginManifest? manifest, Func<string, bool> assemblyExists)
    {
        if (manifest is null)
            return new PluginManifestResult(null, PluginCapability.None, false, "清单为空");

        if (string.IsNullOrWhiteSpace(manifest.Id))
            return new PluginManifestResult(manifest, PluginCapability.None, false, "清单缺少 id");
        if (string.IsNullOrWhiteSpace(manifest.Version))
            return new PluginManifestResult(manifest, PluginCapability.None, false, "清单缺少 version");
        if (string.IsNullOrWhiteSpace(manifest.Entry.Assembly))
            return new PluginManifestResult(manifest, PluginCapability.None, false, "清单缺少 entry.assembly");
        if (string.IsNullOrWhiteSpace(manifest.Entry.Type))
            return new PluginManifestResult(manifest, PluginCapability.None, false, "清单缺少 entry.type");

        if (!assemblyExists(manifest.Entry.Assembly))
            return new PluginManifestResult(manifest, PluginCapability.None, false,
                $"入口程序集不存在：{manifest.Entry.Assembly}");

        var capabilities = manifest.ParseCapabilities();

        if (manifest.ApiVersion != PluginApiVersion.Current)
            return new PluginManifestResult(manifest, capabilities, false,
                $"API 版本不兼容（插件要求 {manifest.ApiVersion}，宿主提供 {PluginApiVersion.Current}）");

        if (string.IsNullOrWhiteSpace(manifest.Name))
            manifest.Name = manifest.Id;

        return new PluginManifestResult(manifest, capabilities, true, null);
    }
}
