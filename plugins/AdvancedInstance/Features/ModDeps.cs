using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace AdvancedInstance.Features;

/// <summary>一个已安装 Mod 的元数据（从 jar 里的加载器清单解析出来）。</summary>
internal sealed class ModMetadata
{
    public string FileName { get; set; } = "";
    public string ModId { get; set; } = "";
    public string Version { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Loader { get; set; } = "";

    /// <summary>依赖：modId → 版本范围（空串表示不限）。</summary>
    public Dictionary<string, string> Depends { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>明确声明「不能和它共存」的 mod id → 说明。</summary>
    public Dictionary<string, string> Breaks { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>非强制依赖（Forge <c>mandatory = false</c>；Fabric 里没有这个概念）。</summary>
    public HashSet<string> OptionalDepends { get; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>依赖检查发现的一条问题。</summary>
internal sealed record ModProblem(string Severity, string Text);

/// <summary>一次依赖分析的结果。</summary>
internal sealed class ModAnalysisResult
{
    public List<ModMetadata> Mods { get; } = [];
    public List<ModProblem> Problems { get; } = [];
    public string ModsDirectory { get; set; } = "";
    public int JarCount { get; set; }

    public int ErrorCount => Problems.Count(problem => problem.Severity == "error");
    public int WarnCount => Problems.Count(problem => problem.Severity == "warn");
    public bool HasProblems => Problems.Count > 0;

    /// <summary>一句话摘要（页面与启动前弹窗共用）。</summary>
    public string Summary => Problems.Count == 0
        ? $"已检查 {Mods.Count} 个 Mod，依赖完整，没有发现冲突。"
        : $"已检查 {Mods.Count} 个 Mod，发现 {ErrorCount} 个严重问题、{WarnCount} 个提示。";

    /// <summary>问题的多行文本（启动前弹窗正文用）。</summary>
    public string ProblemText(int maxLines = 12)
    {
        var builder = new StringBuilder();
        foreach (var problem in Problems.Take(maxLines))
            builder.AppendLine((problem.Severity == "error" ? "• " : "· ") + problem.Text);
        if (Problems.Count > maxLines) builder.AppendLine($"…… 还有 {Problems.Count - maxLines} 条");
        return builder.ToString().TrimEnd();
    }
}

/// <summary>
///     Mod 依赖关系分析：读每个 jar 里的 <c>fabric.mod.json</c> / <c>quilt.mod.json</c> /
///     <c>META-INF/mods.toml</c> / <c>META-INF/neoforge.mods.toml</c>，然后检查：
///     <list type="bullet">
///         <item>依赖缺失（强制依赖没装）</item>
///         <item>依赖版本冲突（装了但版本范围不满足）</item>
///         <item>Mod 冲突（<c>breaks</c> / <c>conflicts</c>，或两个 jar 声明了同一个 mod id）</item>
///     </list>
/// </summary>
internal static class ModDependencyAnalyzer
{
    /// <summary>平台自带的依赖，不算「缺失」。</summary>
    private static readonly HashSet<string> PlatformIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "minecraft", "java", "fabricloader", "fabric-loader", "quilt_loader", "quiltloader",
        "forge", "neoforge", "liteloader", "fabric", "fabric-api", "quilted_fabric_api"
    };

    private static readonly Dictionary<string, (string Fingerprint, ModAnalysisResult Result)> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>分析一个 <c>mods</c> 目录（带缓存：目录指纹没变就直接返回上次结果）。</summary>
    public static ModAnalysisResult Analyze(string modsDirectory, bool forceReload = false)
    {
        if (!Directory.Exists(modsDirectory))
            return new ModAnalysisResult { ModsDirectory = modsDirectory };

        string fingerprint;
        try
        {
            fingerprint = BuildFingerprint(modsDirectory);
        }
        catch
        {
            fingerprint = "";
        }

        if (!forceReload && fingerprint.Length > 0 &&
            Cache.TryGetValue(modsDirectory, out var cached) && cached.Fingerprint == fingerprint)
            return cached.Result;

        var result = AnalyzeCore(modsDirectory);
        if (fingerprint.Length > 0) Cache[modsDirectory] = (fingerprint, result);
        return result;
    }

    private static string BuildFingerprint(string modsDirectory)
    {
        var builder = new StringBuilder();
        foreach (var file in Directory.EnumerateFiles(modsDirectory, "*.jar")
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var info = new FileInfo(file);
            builder.Append(info.Name).Append('|').Append(info.Length).Append('|')
                .Append(info.LastWriteTimeUtc.Ticks).Append(';');
        }

        return builder.ToString();
    }

    private static ModAnalysisResult AnalyzeCore(string modsDirectory)
    {
        var result = new ModAnalysisResult { ModsDirectory = modsDirectory };
        var jars = Directory.EnumerateFiles(modsDirectory, "*.jar").ToList();
        result.JarCount = jars.Count;

        foreach (var jar in jars)
        {
            try
            {
                var metadata = ReadMetadata(jar);
                if (metadata is null) continue;
                result.Mods.Add(metadata);
            }
            catch
            {
                // 读不出来的 jar 不影响其它 Mod 的检查
            }
        }

        var byId = new Dictionary<string, List<ModMetadata>>(StringComparer.OrdinalIgnoreCase);
        foreach (var mod in result.Mods)
        {
            if (string.IsNullOrWhiteSpace(mod.ModId)) continue;
            if (!byId.TryGetValue(mod.ModId, out var list))
            {
                list = [];
                byId[mod.ModId] = list;
            }

            list.Add(mod);
        }

        // ① 同一个 mod id 被多个 jar 提供 = 必然冲突
        foreach (var (modId, mods) in byId)
        {
            if (mods.Count <= 1) continue;
            result.Problems.Add(new ModProblem("error",
                $"重复安装：{modId} 同时出现在 {string.Join("、", mods.Select(mod => mod.FileName))}"));
        }

        // ② 依赖缺失 / 版本不满足
        foreach (var mod in result.Mods)
        foreach (var (depId, range) in mod.Depends)
        {
            if (PlatformIds.Contains(depId)) continue;

            if (!byId.TryGetValue(depId, out var installed))
            {
                if (mod.OptionalDepends.Contains(depId)) continue;
                result.Problems.Add(new ModProblem("error",
                    $"{Describe(mod)} 需要 {depId}{(string.IsNullOrWhiteSpace(range) ? "" : $" {range}")}，但没有安装"));
                continue;
            }

            var ok = installed.Any(candidate =>
                string.IsNullOrWhiteSpace(range) || VersionRange.Matches(candidate.Version, range));
            if (ok) continue;

            result.Problems.Add(new ModProblem("error",
                $"{Describe(mod)} 需要 {depId} {range}，已安装 {string.Join("/", installed.Select(mod2 => mod2.Version))}"));
        }

        // ③ 明确声明的不兼容
        foreach (var mod in result.Mods)
        foreach (var (targetId, reason) in mod.Breaks)
        {
            if (!byId.TryGetValue(targetId, out var installed)) continue;
            result.Problems.Add(new ModProblem("error",
                $"{Describe(mod)} 与 {targetId}（{string.Join("/", installed.Select(mod2 => mod2.Version))}）不兼容" +
                (string.IsNullOrWhiteSpace(reason) ? "" : $"：{reason}")));
        }

        // ④ 依赖回路（A 依赖 B、B 依赖 A）—— 只提示，加载器自己能处理
        foreach (var mod in result.Mods)
        foreach (var (depId, _) in mod.Depends)
        {
            if (!byId.TryGetValue(depId, out var others)) continue;
            foreach (var other in others)
                if (other.Depends.Keys.Any(id => string.Equals(id, mod.ModId, StringComparison.OrdinalIgnoreCase)))
                    result.Problems.Add(new ModProblem("warn",
                        $"{Describe(mod)} 与 {Describe(other)} 互相依赖（循环依赖）"));
        }

        return result;
    }

    private static string Describe(ModMetadata mod) =>
        string.IsNullOrWhiteSpace(mod.DisplayName) || mod.DisplayName == mod.ModId
            ? mod.ModId
            : $"{mod.DisplayName}（{mod.ModId}）";

    /// <summary>从一个 jar 里读元数据；没有可识别的清单返回 null。</summary>
    public static ModMetadata? ReadMetadata(string jarPath)
    {
        using var archive = ZipFile.OpenRead(jarPath);
        var metadata = new ModMetadata { FileName = Path.GetFileName(jarPath) };

        if (TryReadEntry(archive, "fabric.mod.json") is { } fabricJson)
        {
            metadata.Loader = "Fabric";
            ParseFabricJson(fabricJson, metadata, quilt: false);
            return metadata;
        }

        if (TryReadEntry(archive, "quilt.mod.json") is { } quiltJson)
        {
            metadata.Loader = "Quilt";
            ParseFabricJson(quiltJson, metadata, quilt: true);
            return metadata;
        }

        if (TryReadEntry(archive, "META-INF/neoforge.mods.toml") is { } neoToml)
        {
            metadata.Loader = "NeoForge";
            ParseModsToml(neoToml, metadata);
            return metadata;
        }

        if (TryReadEntry(archive, "META-INF/mods.toml") is { } forgeToml)
        {
            metadata.Loader = "Forge";
            ParseModsToml(forgeToml, metadata);
            return metadata;
        }

        return null;
    }

    private static string? TryReadEntry(ZipArchive archive, string name)
    {
        var entry = archive.Entries.FirstOrDefault(item =>
            string.Equals(item.FullName, name, StringComparison.OrdinalIgnoreCase));
        if (entry is null) return null;

        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    #region Fabric / Quilt

    private static void ParseFabricJson(string json, ModMetadata metadata, bool quilt)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (quilt)
        {
            // quilt.mod.json：{ "quilt_loader": { "id", "version", "metadata": { "name" }, "depends": [ { "id", "versions" } ] } }
            if (root.TryGetProperty("quilt_loader", out var loader))
            {
                metadata.ModId = GetString(loader, "id");
                metadata.Version = GetString(loader, "version");
                if (loader.TryGetProperty("metadata", out var meta)) metadata.DisplayName = GetString(meta, "name");
                if (loader.TryGetProperty("depends", out var depends) && depends.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in depends.EnumerateArray())
                    {
                        var id = GetString(item, "id");
                        if (string.IsNullOrWhiteSpace(id)) continue;
                        var versions = item.TryGetProperty("versions", out var range) &&
                                       range.ValueKind == JsonValueKind.String
                            ? range.GetString() ?? ""
                            : "";
                        metadata.Depends[id] = versions;
                    }
                }
            }
        }
        else
        {
            metadata.ModId = GetString(root, "id");
            metadata.Version = GetString(root, "version");
            metadata.DisplayName = GetString(root, "name");
            ReadFabricMap(root, "depends", metadata.Depends);
            ReadFabricMap(root, "breaks", metadata.Breaks);
            ReadFabricMap(root, "conflicts", metadata.Breaks);
        }

        if (string.IsNullOrWhiteSpace(metadata.DisplayName)) metadata.DisplayName = metadata.ModId;
    }

    private static void ReadFabricMap(JsonElement root, string propertyName, Dictionary<string, string> target)
    {
        if (!root.TryGetProperty(propertyName, out var map) || map.ValueKind != JsonValueKind.Object) return;
        foreach (var property in map.EnumerateObject())
        {
            var value = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString() ?? "",
                JsonValueKind.Array => string.Join(" || ", property.Value.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString())),
                _ => ""
            };
            target[property.Name] = value;
        }
    }

    private static string GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

    #endregion

    #region Forge / NeoForge（mods.toml）

    /// <summary>
    ///     极简 TOML 读取：只认 <c>[[mods]]</c>、<c>[[dependencies.&lt;modid&gt;]]</c>、<c>modId / version / displayName /
    ///     mandatory / versionRange</c> 这些键。mods.toml 格式很规整，够用且不必引入 TOML 库。
    /// </summary>
    private static void ParseModsToml(string toml, ModMetadata metadata)
    {
        string? section = null; // "mods" 或 "dependencies:<modid>"
        var dependencyOwner = "";
        var dependencySection = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        void FlushDependency()
        {
            if (section != "dependencies") return;
            var depId = dependencySection.GetValueOrDefault("modId", "");
            var range = dependencySection.GetValueOrDefault("versionRange", "");
            var mandatory = !string.Equals(dependencySection.GetValueOrDefault("mandatory", "true"), "false",
                StringComparison.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(depId) &&
                !string.Equals(depId, dependencyOwner, StringComparison.OrdinalIgnoreCase))
            {
                if (mandatory)
                {
                    metadata.Depends[depId] = range;
                    metadata.OptionalDepends.Remove(depId);
                }
                else
                {
                    metadata.Depends[depId] = range;
                    metadata.OptionalDepends.Add(depId);
                }
            }

            dependencySection.Clear();
        }

        foreach (var rawLine in toml.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            if (line.StartsWith("[[", StringComparison.Ordinal))
            {
                FlushDependency();
                var header = line.TrimStart('[').TrimEnd(']').Trim().TrimEnd(']').Trim();
                if (header.StartsWith("dependencies.", StringComparison.OrdinalIgnoreCase))
                {
                    section = "dependencies";
                    dependencyOwner = header["dependencies.".Length..].Trim();
                }
                else if (string.Equals(header, "dependencies", StringComparison.OrdinalIgnoreCase))
                {
                    section = "dependencies";
                    dependencyOwner = "";
                }
                else if (string.Equals(header, "mods", StringComparison.OrdinalIgnoreCase))
                {
                    section = "mods";
                }
                else
                {
                    section = null;
                }

                continue;
            }

            if (line.StartsWith('['))
            {
                FlushDependency();
                section = null;
                continue;
            }

            var index = line.IndexOf('=');
            if (index <= 0) continue;
            var key = line[..index].Trim().Trim('"');
            var value = line[(index + 1)..].Trim();

            // 去掉行尾注释（简单的：不在引号里的 #）
            if (!value.StartsWith('"') && value.IndexOf('#') > 0) value = value[..value.IndexOf('#')].Trim();
            value = value.Trim().Trim('"');

            if (string.Equals(key, "modId", StringComparison.OrdinalIgnoreCase) && section == "mods")
            {
                if (string.IsNullOrWhiteSpace(metadata.ModId)) metadata.ModId = value;
                else if (!string.IsNullOrWhiteSpace(value)) { /* 一个 jar 塞多个 mod，只取第一个 */ }
            }
            else if (string.Equals(key, "version", StringComparison.OrdinalIgnoreCase) && section == "mods")
            {
                if (string.IsNullOrWhiteSpace(metadata.Version)) metadata.Version = value;
            }
            else if (string.Equals(key, "displayName", StringComparison.OrdinalIgnoreCase) && section == "mods")
            {
                if (string.IsNullOrWhiteSpace(metadata.DisplayName)) metadata.DisplayName = value;
            }
            else if (section == "dependencies")
            {
                dependencySection[key] = value;
            }
        }

        FlushDependency();

        // mods.toml 里的 ${file.jarVersion} 之类占位符直接丢掉
        if (metadata.Version.Contains("${")) metadata.Version = "";
        if (string.IsNullOrWhiteSpace(metadata.DisplayName)) metadata.DisplayName = metadata.ModId;
    }

    #endregion
}

/// <summary>
///     版本范围匹配（Fabric / Forge / NeoForge 各自写法略有差异，这里取并集，宁宽松勿漏判）。
///     支持：<c>*</c>、空、精确值、<c>1.2.x</c>、<c>&gt;=1.2</c>、<c>&gt;1.2</c>、<c>&lt;=1.2</c>、
///     <c>&lt;1.2</c>、<c>=1.2</c>、<c>~1.2.3</c>、<c>^1.2.3</c>、<c>[1.0,2.0)</c>、<c>[1.0]</c>、
///     <c>1.0 || 2.0</c>、以及逗号分隔的多个条件（Fabric 的 <c>&gt;=1.0 &lt;2.0</c> 也认）。
/// </summary>
internal static class VersionRange
{
    public static bool Matches(string? version, string? range)
    {
        var text = (range ?? "").Trim();
        if (text.Length == 0 || text == "*") return true;

        // "||" 表示任选其一
        foreach (var alternative in text.Split("||", StringSplitOptions.RemoveEmptyEntries))
            if (MatchesSingle(version ?? "", alternative.Trim()))
                return true;

        return false;
    }

    private static bool MatchesSingle(string version, string expression)
    {
        if (expression.Length == 0 || expression == "*") return true;

        // 方括号区间：[1.0,2.0) / (1.0,2.0] / [1.0] / [1.0,)
        if (expression.StartsWith('[') || expression.StartsWith('('))
            return MatchesBracketRange(version, expression);

        // Fabric 允许 ">=1.0 <2.0" 这种空格分隔的多条件
        var parts = expression.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 1 && parts.All(part => part.Length > 0 && (char.IsDigit(part[0]) || "^~<>=*".Contains(part[0]))))
            return parts.All(part => MatchesSingle(version, part));

        if (expression.StartsWith(">=", StringComparison.Ordinal))
            return Compare(version, expression[2..]) >= 0;
        if (expression.StartsWith("<=", StringComparison.Ordinal))
            return Compare(version, expression[2..]) <= 0;
        if (expression.StartsWith('>'))
            return Compare(version, expression[1..]) > 0;
        if (expression.StartsWith('<'))
            return Compare(version, expression[1..]) < 0;
        if (expression.StartsWith('='))
            return Compare(version, expression[1..]) == 0;

        if (expression.StartsWith('~') || expression.StartsWith('^'))
        {
            var baseVersion = expression[1..].Trim();
            if (Compare(version, baseVersion) < 0) return false;
            var segments = baseVersion.Split('.');
            var bounds = expression[0] == '^'
                ? new VersionParts(baseVersion).Bump(segments.Length >= 2 ? 0 : 0)
                : new VersionParts(baseVersion).Bump(segments.Length >= 2 ? 1 : 0);
            return Compare(version, bounds.ToString()) < 0;
        }

        // 通配：1.2.x / 1.2.* / 1.*
        if (expression.Contains('x') || expression.Contains('X') || expression.Contains('*'))
        {
            var prefix = expression.Split('x', 'X', '*')[0].TrimEnd('.');
            return version.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
                   version.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase);
        }

        // 精确或前缀（1.2 匹配 1.2.3）
        return Compare(version, expression) == 0 ||
               version.StartsWith(expression + ".", StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesBracketRange(string version, string expression)
    {
        var lowerInclusive = expression.StartsWith('[');
        var upperInclusive = expression.EndsWith(']');
        var body = expression.Trim('[', ']', '(', ')');
        var pieces = body.Split(',');

        if (pieces.Length == 1)
            return Compare(version, pieces[0].Trim()) == 0;

        var lower = pieces[0].Trim();
        var upper = pieces.Length > 1 ? pieces[1].Trim() : "";

        if (lower.Length > 0)
        {
            var compare = Compare(version, lower);
            if (lowerInclusive ? compare < 0 : compare <= 0) return false;
        }

        if (upper.Length > 0)
        {
            var compare = Compare(version, upper);
            if (upperInclusive ? compare > 0 : compare >= 0) return false;
        }

        return true;
    }

    /// <summary>比较两个版本号（按数字段比较，段数不同时短的补 0；非数字后缀忽略）。</summary>
    private static int Compare(string left, string right)
    {
        var partsLeft = new VersionParts(left);
        var partsRight = new VersionParts(right);
        var length = Math.Max(partsLeft.Count, partsRight.Count);
        for (var i = 0; i < length; i++)
        {
            var a = partsLeft[i];
            var b = partsRight[i];
            if (a != b) return a.CompareTo(b);
        }

        return 0;
    }

    private sealed class VersionParts
    {
        private readonly List<int> _values = [];

        public VersionParts(string version)
        {
            foreach (var raw in version.Trim().Split('.', '-', '+', '_'))
            {
                var digits = new string(raw.TakeWhile(char.IsDigit).ToArray());
                _values.Add(digits.Length > 0 ? int.Parse(digits) : 0);
            }

            if (_values.Count == 0) _values.Add(0);
        }

        public int Count => _values.Count;

        public int this[int index] => index < _values.Count ? _values[index] : 0;

        /// <summary>把第 index 段加一、后面清零（用于 ^ / ~ 的上界）。</summary>
        public VersionParts Bump(int index)
        {
            var values = new List<int>(_values);
            while (values.Count <= index) values.Add(0);
            values[index]++;
            for (var i = index + 1; i < values.Count; i++) values[i] = 0;
            return new VersionParts(string.Join('.', values));
        }

        public override string ToString() => string.Join('.', _values);
    }
}
