using System.IO;
using System.IO.Compression;
using System.Text.Json;
using PCL.PluginSdk;

namespace PCL.PluginHost;

/// <summary>
///     插件包文件（<c>.pclpkg</c>）的命名规则与磁盘操作。
///     <para>
///         插件在磁盘上就是 <c>PCL\Plugins\</c> 目录里的一个包文件：
///         <c>Foo.pclpkg</c> = 已启用，<c>Foo.pclpkg.disabled</c> = 已禁用。
///         加载时先把包解压到缓存目录（<c>PCL\PluginCache\</c>），所以包文件本身
///         永远不会被加载上下文占用：改后缀即可热开关，删除也只删文件。
///     </para>
/// </summary>
public static class PluginPackage
{
    /// <summary>插件包扩展名。</summary>
    public const string Extension = ".pclpkg";

    /// <summary>禁用插件的文件名后缀，追加在 <see cref="Extension" /> 之后。</summary>
    public const string DisabledSuffix = ".disabled";

    private const string PackageDisabledExtension = Extension + DisabledSuffix;

    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    #region 文件名

    /// <summary>是否为插件包文件（含已禁用的 <c>.pclpkg.disabled</c>）。</summary>
    public static bool IsPackageFile(string path)
    {
        return path.EndsWith(Extension, StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(PackageDisabledExtension, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>文件名是否表示「已启用」（<c>.pclpkg</c> 而不是 <c>.pclpkg.disabled</c>）。</summary>
    public static bool IsEnabledFileName(string fileName)
    {
        if (fileName.EndsWith(PackageDisabledExtension, StringComparison.OrdinalIgnoreCase)) return false;
        return fileName.EndsWith(Extension, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>去掉 <c>.pclpkg</c> / <c>.pclpkg.disabled</c> 后的基名（用作缓存目录名与删除键）。</summary>
    public static string GetBaseName(string fileNameOrPath)
    {
        var name = Path.GetFileName(fileNameOrPath);
        if (name.EndsWith(PackageDisabledExtension, StringComparison.OrdinalIgnoreCase))
            return name[..^PackageDisabledExtension.Length];
        if (name.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
            return name[..^Extension.Length];
        return name;
    }

    /// <summary>由基名拼出包文件名（<paramref name="enabled" /> 决定是否带 <c>.disabled</c>）。</summary>
    public static string GetPackageFileName(string baseName, bool enabled)
    {
        var sanitized = Sanitize(baseName);
        if (string.IsNullOrWhiteSpace(sanitized)) sanitized = "plugin";
        return sanitized + (enabled ? Extension : PackageDisabledExtension);
    }

    #endregion

    #region 启用 / 禁用 / 删除

    /// <summary>
    ///     通过改文件名启用或禁用插件。
    ///     包文件没有被加载上下文占用（程序集是从缓存目录加载的），所以可以热切换。
    /// </summary>
    public static bool TrySetEnabled(string packagePath, bool enabled, out string newPath, out string? error)
    {
        newPath = packagePath;
        error = null;

        try
        {
            if (!File.Exists(packagePath))
            {
                error = "插件包已不存在";
                return false;
            }

            var currentlyEnabled = IsEnabledFileName(packagePath);
            if (currentlyEnabled == enabled) return true;

            var directory = Path.GetDirectoryName(packagePath)!;
            var baseName = GetBaseName(packagePath);
            newPath = Path.Combine(directory, GetPackageFileName(baseName, enabled));

            if (File.Exists(newPath))
            {
                error = $"同名插件包已存在：{Path.GetFileName(newPath)}";
                newPath = packagePath;
                return false;
            }

            MoveWithRetry(packagePath, newPath);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            newPath = packagePath;
            return false;
        }
    }

    /// <summary>
    ///     从插件目录删除某个插件的所有表示形式：
    ///     <c>Foo.pclpkg</c>、<c>Foo.pclpkg.disabled</c>，以及历史遗留的 <c>Foo\</c> 目录。
    ///     返回实际删除的条目数。
    /// </summary>
    public static int Delete(string pluginsDirectory, string baseName)
    {
        var removed = 0;
        foreach (var fileName in new[]
                 {
                     GetPackageFileName(baseName, true), GetPackageFileName(baseName, false)
                 })
        {
            var path = Path.Combine(pluginsDirectory, fileName);
            if (!File.Exists(path)) continue;
            if (TryDeleteFile(path)) removed++;
        }

        // 历史版本把插件解压成目录，一并清掉（目录里可能还有残留程序集）
        var legacyDirectory = Path.Combine(pluginsDirectory, Sanitize(baseName));
        if (Directory.Exists(legacyDirectory) && TryDeleteDirectory(legacyDirectory)) removed++;

        return removed;
    }

    /// <summary>
    ///     插件安装后应使用的包文件名：以清单 id 为准（而不是源文件名），
    ///     保证同一个插件无论从哪里安装都只有一份，不会出现多份副本。
    /// </summary>
    public static string GetInstallFileName(string pluginId, bool enabled)
    {
        var id = Sanitize(pluginId);
        return GetPackageFileName(string.IsNullOrWhiteSpace(id) ? "plugin" : id, enabled);
    }

    /// <summary>
    ///     找出插件目录里属于某个插件的**所有**包文件（启用与禁用形态、任意文件名）。
    ///     判据是包内清单的 id；清单读不出来时退化为比较文件名基名。
    /// </summary>
    public static IReadOnlyList<string> FindPackages(string pluginsDirectory, string pluginId)
    {
        var found = new List<string>();
        if (string.IsNullOrWhiteSpace(pluginId) || !Directory.Exists(pluginsDirectory)) return found;

        var id = pluginId.Trim();
        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(pluginsDirectory).ToList();
        }
        catch
        {
            return found;
        }

        foreach (var file in files)
        {
            if (!IsPackageFile(file)) continue;
            var manifest = ReadManifest(file).Manifest;
            var matches = manifest is not null
                ? string.Equals(manifest.Id, id, StringComparison.OrdinalIgnoreCase)
                : string.Equals(GetBaseName(file), id, StringComparison.OrdinalIgnoreCase);
            if (matches) found.Add(file);
        }

        return found;
    }

    /// <summary>
    ///     删除某个插件的所有包文件（任意文件名、启用或禁用形态）与历史同名目录。
    ///     返回实际删除的条目数。
    /// </summary>
    public static int DeleteById(string pluginsDirectory, string pluginId)
    {
        var removed = 0;
        foreach (var path in FindPackages(pluginsDirectory, pluginId))
            if (TryDeleteFile(path))
                removed++;

        removed += Delete(pluginsDirectory, pluginId);
        return removed;
    }

    #endregion

    /// <summary>
    ///     以**宽松共享**方式打开包文件：允许别人同时读、也允许改名 / 删除。
    ///     <para>
    ///         <see cref="ZipFile.OpenRead" /> 用的是 <see cref="FileShare.Read" />，不共享 Delete ——
    ///         后台扫描还在读某个包时，用户在管理页点「启用 / 禁用」要改名就会撞上
    ///         「The process cannot access the file because it is being used by another process」。
    ///         日志里 `sgu.sample.test1` 那次失败就是这个原因，改成共享 Delete 后可以边读边改名。
    ///     </para>
    /// </summary>
    internal static FileStream OpenReadShared(string packagePath) =>
        new(packagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

    /// <summary>同 <see cref="OpenReadShared" />，返回 zip 读取器。</summary>
    internal static ZipArchive OpenArchiveShared(string packagePath) =>
        new(OpenReadShared(packagePath), ZipArchiveMode.Read);

    #region 清单

    /// <summary>
    ///     从插件包里读取并校验清单。包内 <c>plugin.json</c> 在根目录或其上一层唯一目录里。
    /// </summary>
    public static PluginManifestResult ReadManifest(string packagePath)
    {
        try
        {
            using var archive = OpenArchiveShared(packagePath);

            var manifestEntry = FindManifestEntry(archive);
            if (manifestEntry is null)
                return new PluginManifestResult(null, PluginCapability.None, false,
                    "包内缺少 " + PluginManifestReader.ManifestFileName);

            string json;
            using (var reader = new StreamReader(manifestEntry.Open()))
                json = reader.ReadToEnd();

            var manifest = JsonSerializer.Deserialize<PluginManifest>(json, _options);
            if (manifest is null)
                return new PluginManifestResult(null, PluginCapability.None, false, "清单为空");

            var prefix = GetPrefix(manifestEntry);
            var entries = new HashSet<string>(
                archive.Entries.Where(e => !string.IsNullOrEmpty(e.Name)).Select(e => e.FullName),
                StringComparer.OrdinalIgnoreCase);

            return PluginManifestReader.Validate(
                manifest,
                relative =>
                {
                    var full = prefix + relative.Replace('\\', '/');
                    return entries.Contains(full);
                });
        }
        catch (Exception ex)
        {
            return new PluginManifestResult(null, PluginCapability.None, false, "无法读取插件包：" + ex.Message);
        }
    }

    private static ZipArchiveEntry? FindManifestEntry(ZipArchive archive)
    {
        var manifestName = PluginManifestReader.ManifestFileName;
        return archive.Entries.FirstOrDefault(e =>
                   string.Equals(e.FullName, manifestName, StringComparison.OrdinalIgnoreCase))
               ?? archive.Entries.FirstOrDefault(e =>
                   e.FullName.EndsWith("/" + manifestName, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetPrefix(ZipArchiveEntry manifestEntry)
    {
        var manifestName = PluginManifestReader.ManifestFileName;
        return manifestEntry.FullName.Length > manifestName.Length
            ? manifestEntry.FullName[..^manifestName.Length]
            : "";
    }

    #endregion

    #region 解压缓存

    /// <summary>
    ///     把插件包解压到 <paramref name="cacheRoot" /> 下以「基名_长度_时间戳」命名的目录，
    ///     已存在同指纹目录时直接复用。返回可以加载的目录。
    /// </summary>
    public static string ExtractToCache(string packagePath, string cacheRoot)
    {
        var info = new FileInfo(packagePath);
        var key = $"{Sanitize(GetBaseName(packagePath))}_{info.Length}_{info.LastWriteTimeUtc.Ticks:x}";
        var target = Path.Combine(cacheRoot, key);

        if (IsUsableCache(target)) return target;

        // 上次卸载/清理时程序集还没被释放，会留下「目录还在、plugin.json 已被删掉」的残件。
        // 这种目录必须先删干净再解压；删不掉（文件仍被占用）就换一个唯一目录名，
        // 绝不能把不完整的目录返回给加载器——那正是「启用后报缺少 plugin.json」的根因。
        if (Directory.Exists(target) && !TryDeleteDirectory(target))
            target = Path.Combine(cacheRoot, $"{key}-{Guid.NewGuid().ToString("N")[..6]}");

        var temp = target + ".tmp-" + Guid.NewGuid().ToString("N")[..6];
        if (Directory.Exists(temp)) TryDeleteDirectory(temp);
        Directory.CreateDirectory(temp);

        using (var archive = OpenArchiveShared(packagePath))
        {
            var manifestEntry = FindManifestEntry(archive);
            var prefix = manifestEntry is null ? "" : GetPrefix(manifestEntry);
            var tempRoot = Path.GetFullPath(temp);

            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name)) continue; // 目录项
                if (!entry.FullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;

                var relative = entry.FullName[prefix.Length..].Replace('/', Path.DirectorySeparatorChar);
                if (relative.Length == 0) continue;

                var destination = Path.GetFullPath(Path.Combine(tempRoot, relative));
                // 防止包内路径穿越：必须比较「带分隔符的前缀」，
                // 否则同级兄弟目录（<key>.tmp 与 <key>.tmp-evil）会被判为在根内
                var rootWithSeparator = tempRoot.EndsWith(Path.DirectorySeparatorChar)
                    ? tempRoot
                    : tempRoot + Path.DirectorySeparatorChar;
                if (!destination.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase)) continue;

                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                entry.ExtractToFile(destination, true);
            }
        }

        try
        {
            if (Directory.Exists(target)) TryDeleteDirectory(target);
            Directory.Move(temp, target);
        }
        catch
        {
            if (IsUsableCache(target))
            {
                // 并发解压时另一个线程已经放好了，直接用它的
                TryDeleteDirectory(temp);
            }
            else
            {
                // 目标目录仍然不可用（多半是被残留文件占着）：换唯一名字落盘，
                // 实在不行就地把临时目录当结果——它本身就是完整解压出来的。
                var fallback = Path.Combine(cacheRoot, $"{key}-{Guid.NewGuid().ToString("N")[..6]}");
                try
                {
                    Directory.Move(temp, fallback);
                    target = fallback;
                }
                catch
                {
                    target = temp;
                }
            }
        }

        if (!IsUsableCache(target))
            throw new IOException($"插件包解压失败：缓存目录缺少 {PluginManifestReader.ManifestFileName}（{Path.GetFileName(packagePath)}）");

        return target;
    }

    /// <summary>缓存目录是否可用来加载插件（目录存在且清单文件在位）。</summary>
    private static bool IsUsableCache(string directory)
    {
        return Directory.Exists(directory) &&
               File.Exists(Path.Combine(directory, PluginManifestReader.ManifestFileName));
    }

    /// <summary>
    ///     清理不再被任何插件包引用的缓存目录（尽力而为）。
    /// </summary>
    public static void CleanupCache(string cacheRoot, ICollection<string> keepDirectoryNames)
    {
        try
        {
            if (!Directory.Exists(cacheRoot)) return;
            foreach (var directory in Directory.GetDirectories(cacheRoot))
            {
                if (keepDirectoryNames.Contains(Path.GetFileName(directory))) continue;
                TryDeleteDirectory(directory);
            }
        }
        catch
        {
            // 忽略
        }
    }

    #endregion

    #region 工具

    /// <summary>把插件 id / 基名里的非法文件名字符替换成下划线。</summary>
    public static string Sanitize(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();

        // 只允许安全的相对目录名：`.` / `..` 会让数据目录、卸载路径逃出 PCL\Plugins 与 PCL\PluginData
        if (cleaned is "." or "..") return "_";
        while (cleaned.Contains("..")) cleaned = cleaned.Replace("..", "_");
        cleaned = cleaned.Trim('.', ' '); // 结尾的点 / 空格在 Windows 上会被忽略，同样不安全
        return cleaned.Length == 0 ? "_" : cleaned;
    }

    /// <summary>
    ///     去掉文件/目录的只读属性：用户从只读位置（压缩包、光盘、他人共享目录）复制过来的
    ///     插件包会带只读标记，直接删除会抛 <see cref="UnauthorizedAccessException" />。
    /// </summary>
    public static void MakeWritable(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReadOnly) != 0)
                    File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
            }
            else if (Directory.Exists(path))
            {
                var attributes = File.GetAttributes(path);
                if ((attributes & FileAttributes.ReadOnly) != 0)
                    File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
                foreach (var child in Directory.EnumerateFileSystemEntries(path))
                    MakeWritable(child);
            }
        }
        catch (Exception)
        {
            // 拿不到权限就交给后面的删除流程报错，这里不吞掉真实原因
        }
    }

    private static void MoveWithRetry(string source, string destination, int retries = 3)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                MakeWritable(source);
                File.Move(source, destination);
                return;
            }
            catch (IOException) when (attempt < retries - 1)
            {
                Thread.Sleep(80);
            }
            catch (UnauthorizedAccessException) when (attempt < retries - 1)
            {
                Thread.Sleep(80);
            }
        }
    }

    private static bool TryDeleteFile(string path)
    {
        try
        {
            if (!File.Exists(path)) return true;
            MakeWritable(path);
            File.Delete(path);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>尽力删除一个目录（缓存重建用，公开版本）。</summary>
    public static bool TryDeleteCacheDirectory(string directory) => TryDeleteDirectory(directory);

    private static bool TryDeleteDirectory(string directory, int retries = 3)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                if (!Directory.Exists(directory)) return true;
                MakeWritable(directory);
                Directory.Delete(directory, true);
                return true;
            }
            catch (Exception)
            {
                if (attempt >= retries - 1) return false;
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                Thread.Sleep(120);
            }
        }
    }

    #endregion
}
