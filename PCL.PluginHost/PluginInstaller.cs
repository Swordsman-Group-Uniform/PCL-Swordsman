using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using PCL.PluginSdk;

namespace PCL.PluginHost;

/// <summary>
///     插件安装结果。
/// </summary>
/// <param name="Success">是否成功。</param>
/// <param name="PluginId">清单里的插件 id（读不到清单时为 null）。</param>
/// <param name="Error">失败原因。</param>
/// <param name="InstalledPath">安装后的包文件绝对路径。</param>
/// <param name="DisplayName">展示用的插件名（清单里的 name，读不到时为文件名）。</param>
public sealed record PluginInstallResult(
    bool Success,
    string? PluginId,
    string? Error,
    string? InstalledPath = null,
    string? DisplayName = null);

/// <summary>
///     插件包安装、卸载与「下次启动删除」队列。
///     <para>
///         插件以 <c>.pclpkg</c> 包文件的形式直接放在 <c>PCL\Plugins\</c> 里：
///         安装 = 校验后把包文件复制进去（默认启用），删除 = 删掉对应包文件。
///         程序集是从解压缓存加载的，所以包文件不被占用，这些操作都不需要重启。
///     </para>
/// </summary>
public static class PluginInstaller
{
    private const string PendingDeleteFileName = "pending-delete.json";

    /// <summary>
    ///     把插件包安装到 <paramref name="pluginsDirectory" />。
    ///     已存在同名插件包（启用或禁用形态）时覆盖，安装后默认启用。
    /// </summary>
    public static PluginInstallResult InstallPackage(string pluginsDirectory, string packagePath, bool enabled = true)
    {
        try
        {
            if (!File.Exists(packagePath))
                return new PluginInstallResult(false, null, "插件包不存在");

            if (!PluginPackage.IsPackageFile(packagePath))
                return new PluginInstallResult(false, null, "只支持 .pclpkg 插件包");

            var manifest = PluginPackage.ReadManifest(packagePath);
            if (manifest.Manifest is null)
                return new PluginInstallResult(false, null, manifest.Error ?? "插件包无法读取");

            // 装之前先校验完整性与签名：声明了就必须过，否则不许进 Plugins
            var integrity = PluginIntegrity.Verify(packagePath, manifest.Manifest);
            if (!integrity.CanLoad)
                return new PluginInstallResult(false, manifest.Manifest.Id,
                    "完整性校验未通过：" + integrity.Error);

            Directory.CreateDirectory(pluginsDirectory);

            var pluginId = manifest.Manifest.Id;
            var sourceBaseName = PluginPackage.GetBaseName(packagePath);
            // 安装后的文件名以清单 id 为准：同一个插件无论从哪个文件装进来，
            // 在 Plugins 里都只有一份（否则禁用时只改到一份，看起来像没生效）。
            var installBaseName = string.IsNullOrWhiteSpace(pluginId)
                ? sourceBaseName
                : PluginPackage.Sanitize(pluginId);
            var target = Path.Combine(pluginsDirectory, PluginPackage.GetPackageFileName(installBaseName, enabled));

            // 已经在目标位置（用户直接把包拖进 Plugins 目录）就不用复制
            if (!string.Equals(Path.GetFullPath(packagePath), Path.GetFullPath(target),
                    StringComparison.OrdinalIgnoreCase))
            {
                if (File.Exists(target)) PluginPackage.MakeWritable(target);
                File.Copy(packagePath, target, true);
                // 复制会继承源文件的只读属性，装进来的包必须可写，否则禁用/删除会失败
                PluginPackage.MakeWritable(target);
            }

            // 清掉同一插件的其它副本（不同文件名 / 已禁用形态），只保留刚装好这一份
            foreach (var existing in PluginPackage.FindPackages(pluginsDirectory, pluginId))
            {
                if (string.Equals(Path.GetFullPath(existing), Path.GetFullPath(target),
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                PluginPackage.MakeWritable(existing);
                File.Delete(existing);
            }

            // 历史目录格式的同名插件会让列表出现两份，顺手清掉
            foreach (var legacyName in new[] { installBaseName, sourceBaseName }
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var legacyDirectory = Path.Combine(pluginsDirectory, PluginPackage.Sanitize(legacyName));
                if (Directory.Exists(legacyDirectory)) TryDeleteDirectory(legacyDirectory);
            }

            var displayName = string.IsNullOrWhiteSpace(manifest.Manifest.Name)
                ? installBaseName
                : manifest.Manifest.Name;

            return new PluginInstallResult(true, pluginId, null, target, displayName);
        }
        catch (Exception ex)
        {
            return new PluginInstallResult(false, null, ex.Message);
        }
    }

    /// <summary>
    ///     卸载插件：删除 <c>PCL\Plugins</c> 下该插件的**所有**包文件
    ///     （任意文件名、启用与禁用形态，以及历史遗留的同名目录）。
    /// </summary>
    /// <param name="pluginsDirectory">插件根目录。</param>
    /// <param name="pluginId">插件 id（也接受包文件基名）。</param>
    /// <param name="pluginDataDirectory">插件数据目录（用于登记「下次启动删除」）。</param>
    /// <returns>是否已删除干净（返回 false 时已登记下次启动再删）。</returns>
    public static bool Uninstall(string pluginsDirectory, string pluginId, string? pluginDataDirectory = null)
    {
        var removed = PluginPackage.DeleteById(pluginsDirectory, pluginId);
        if (removed > 0) return true;

        // 一个都没删掉：可能是文件仍被占用（例如用户手动打开着），登记下次启动再删
        if (pluginDataDirectory is not null)
            MarkPendingDelete(pluginDataDirectory, pluginId);
        return false;
    }

    /// <summary>
    ///     合并同一个插件的多份副本（老版本按源文件名安装造成的历史问题）。
    ///     规则：<br />
    ///     · 同 id 的包只保留一份，文件名统一成 <c>&lt;id&gt;.pclpkg</c>（或 <c>.pclpkg.disabled</c>）；<br />
    ///     · 若其中任意一份处于禁用状态，就按「禁用」保留——用户最后一次操作很可能是禁用，
    ///     宁可少加载也不要擅自把用户关掉的东西又打开；<br />
    ///     · 保留时间最新的那份的内容。
    /// </summary>
    /// <returns>删除的多余副本数量。</returns>
    public static int DeduplicatePackages(string pluginsDirectory)
    {
        try
        {
            if (!Directory.Exists(pluginsDirectory)) return 0;

            var groups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in Directory.EnumerateFiles(pluginsDirectory).Where(PluginPackage.IsPackageFile))
            {
                var manifest = PluginPackage.ReadManifest(file).Manifest;
                var key = manifest?.Id;
                if (string.IsNullOrWhiteSpace(key)) key = PluginPackage.GetBaseName(file);
                if (!groups.TryGetValue(key, out var list)) groups[key] = list = [];
                list.Add(file);
            }

            var removed = 0;
            foreach (var (pluginId, files) in groups)
            {
                var hasDisabled = files.Any(f => !PluginPackage.IsEnabledFileName(f));
                var candidates = hasDisabled
                    ? files.Where(f => !PluginPackage.IsEnabledFileName(f)).ToList()
                    : files;
                var keep = candidates
                    .OrderByDescending(f => File.GetLastWriteTimeUtc(f))
                    .First();

                var canonical = Path.Combine(pluginsDirectory, PluginPackage.GetInstallFileName(pluginId, !hasDisabled));
                if (!string.Equals(Path.GetFullPath(keep), Path.GetFullPath(canonical),
                        StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        PluginPackage.MakeWritable(keep);
                        if (File.Exists(canonical))
                        {
                            PluginPackage.MakeWritable(canonical);
                            File.Delete(canonical);
                        }

                        File.Move(keep, canonical);
                        keep = canonical;
                    }
                    catch
                    {
                        // 改名失败就保持原文件名，仍然继续删多余副本
                    }
                }

                foreach (var file in files)
                {
                    if (string.Equals(Path.GetFullPath(file), Path.GetFullPath(keep),
                            StringComparison.OrdinalIgnoreCase))
                        continue;

                    try
                    {
                        PluginPackage.MakeWritable(file);
                        File.Delete(file);
                        removed++;
                    }
                    catch
                    {
                        // 单个副本删不掉不影响其它插件
                    }
                }
            }

            return removed;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>读取「下次启动删除」队列（历史插件目录名）。</summary>
    public static IReadOnlyList<string> GetPendingDeletes(string pluginDataDirectory) =>
        ReadPendingDeletes(pluginDataDirectory);

    /// <summary>登记「下次启动删除」。</summary>
    public static void MarkPendingDelete(string pluginDataDirectory, string folderName)
    {
        try
        {
            var path = Path.Combine(pluginDataDirectory, PendingDeleteFileName);
            var folders = ReadPendingDeletes(pluginDataDirectory);
            if (!folders.Contains(folderName, StringComparer.OrdinalIgnoreCase))
                folders.Add(folderName);
            var root = new JsonObject
            {
                ["folders"] = new JsonArray(folders.Select(f => (JsonNode?)JsonValue.Create(f)).ToArray())
            };
            Directory.CreateDirectory(pluginDataDirectory);
            File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // 忽略
        }
    }

    /// <summary>
    ///     启动时执行待删除队列（此时插件尚未加载，文件不会被占用）。返回成功删除的数量。
    /// </summary>
    public static int ProcessPendingDeletes(string pluginsDirectory, string pluginDataDirectory)
    {
        try
        {
            var folders = ReadPendingDeletes(pluginDataDirectory);
            if (folders.Count == 0) return 0;

            var remaining = new List<string>();
            var removed = 0;
            foreach (var folder in folders)
            {
                var ok = PluginPackage.Delete(pluginsDirectory, folder) > 0 ||
                         !Directory.Exists(Path.Combine(pluginsDirectory, folder));
                if (ok) removed++;
                else remaining.Add(folder);
            }

            var path = Path.Combine(pluginDataDirectory, PendingDeleteFileName);
            if (remaining.Count == 0)
            {
                if (File.Exists(path)) File.Delete(path);
            }
            else
            {
                var root = new JsonObject
                {
                    ["folders"] = new JsonArray(remaining.Select(f => (JsonNode?)JsonValue.Create(f)).ToArray())
                };
                File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            }

            return removed;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    ///     把历史「解压成目录」的插件打包成 <c>.pclpkg</c>，然后删掉目录。
    ///     这样所有插件都回到「一个插件一个包文件」的统一模型，未打勾与删除都只动文件。
    ///     返回迁移成功的数量。
    /// </summary>
    public static int MigrateDirectoriesToPackages(string pluginsDirectory, string pluginDataDirectory)
    {
        try
        {
            if (!Directory.Exists(pluginsDirectory)) return 0;

            var stateStore = new PluginStateStore(pluginDataDirectory);
            var migrated = 0;

            foreach (var directory in Directory.GetDirectories(pluginsDirectory))
            {
                if (!File.Exists(Path.Combine(directory, PluginManifestReader.ManifestFileName))) continue;

                var name = Path.GetFileName(directory);
                try
                {
                    var result = PluginManifestReader.Read(directory);
                    var baseName = PluginPackage.Sanitize(result.Manifest?.Id ?? name);
                    var enabledPath = Path.Combine(pluginsDirectory, PluginPackage.GetPackageFileName(baseName, true));
                    var disabledPath = Path.Combine(pluginsDirectory, PluginPackage.GetPackageFileName(baseName, false));

                    if (File.Exists(enabledPath) || File.Exists(disabledPath) ||
                        (result.Manifest is not null &&
                         PluginPackage.FindPackages(pluginsDirectory, result.Manifest.Id).Count > 0))
                    {
                        // 已经有同一个插件的包：目录是多余副本，直接删掉即可
                        if (TryDeleteDirectory(directory)) migrated++;
                        continue;
                    }

                    var disabled = name.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase) ||
                                   (result.Manifest is not null && stateStore.IsDisabled(result.Manifest.Id));
                    var target = Path.Combine(pluginsDirectory, PluginPackage.GetPackageFileName(baseName, !disabled));

                    ZipFile.CreateFromDirectory(directory, target, CompressionLevel.Optimal, false);
                    if (!TryDeleteDirectory(directory))
                    {
                        // 目录删不掉就先不留包，避免出现两份同名插件
                        try
                        {
                            File.Delete(target);
                        }
                        catch
                        {
                            // 忽略
                        }

                        continue;
                    }

                    if (result.Manifest is not null) stateStore.SetDisabled(result.Manifest.Id, false);
                    migrated++;
                }
                catch
                {
                    // 单个插件迁移失败不影响其它插件
                }
            }

            return migrated;
        }
        catch
        {
            return 0;
        }
    }

    private static List<string> ReadPendingDeletes(string pluginDataDirectory)
    {
        try
        {
            var path = Path.Combine(pluginDataDirectory, PendingDeleteFileName);
            if (!File.Exists(path)) return [];
            var node = JsonNode.Parse(File.ReadAllText(path)) as JsonObject;
            if (node?["folders"] is not JsonArray array) return [];
            return array.Select(item => item?.GetValue<string>())
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .Select(f => f!)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    ///     删除目录，失败时强制 GC 后重试（用于等待 <c>AssemblyLoadContext</c> 真正释放文件）。
    /// </summary>
    private static bool TryDeleteDirectory(string directory, int retries = 5)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                if (!Directory.Exists(directory)) return true;
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
}
