using System.IO;
using System.IO.Compression;
using PCL.PluginSdk;

namespace AdvancedInstance.Features;

/// <summary>
///     存档自动备份。
///     <para>
///         结构：<c>&lt;插件数据目录&gt;/Saves/&lt;实例名&gt;/&lt;存档名&gt;/&lt;yyyyMMdd-HHmmss&gt;.zip</c>，
///         每个 zip 是一份完整的存档文件夹副本。
///         默认每 30 分钟备份一次、每个存档保留最近 10 份。
///     </para>
///     <para>
///         存档被删除后（磁盘上这个存档已经不存在）：**只保留最后一份副本**，
///         其余较早的副本立刻清掉；如果这份副本也已经超过 30 天，则连它一起删掉。
///     </para>
/// </summary>
internal sealed class SaveBackupService
{
    private readonly IPluginContext _context;
    private readonly string _root;

    private System.Threading.Timer? _timer;
    private string? _lastError;
    private DateTime _lastRunAt = DateTime.MinValue;

    public SaveBackupService(IPluginContext context)
    {
        _context = context;
        _root = Path.Combine(context.DataDirectory, "Saves");
    }

    #region 配置

    public bool Enabled
    {
        get => _context.Config.Get("saveBackup.enabled", true);
        set => _context.Config.Set("saveBackup.enabled", value);
    }

    public int IntervalMinutes
    {
        get => Math.Clamp(_context.Config.Get("saveBackup.intervalMinutes", 30), 1, 24 * 60);
        set => _context.Config.Set("saveBackup.intervalMinutes", Math.Clamp(value, 1, 24 * 60));
    }

    public int KeepPerWorld
    {
        get => Math.Clamp(_context.Config.Get("saveBackup.keepPerWorld", 10), 1, 200);
        set => _context.Config.Set("saveBackup.keepPerWorld", Math.Clamp(value, 1, 200));
    }

    public int OrphanKeepDays
    {
        get => Math.Clamp(_context.Config.Get("saveBackup.orphanKeepDays", 30), 1, 3650);
        set => _context.Config.Set("saveBackup.orphanKeepDays", Math.Clamp(value, 1, 3650));
    }

    #endregion

    #region 定时器

    /// <summary>启动后台定时器（每分钟检查一次，到点了才真的备份）。</summary>
    public void Start(Func<(string Name, string Path)?> currentInstance)
    {
        Stop();
        _timer = new System.Threading.Timer(_ =>
        {
            try
            {
                Tick(currentInstance);
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                _context.Log.Warn($"自动备份出错：{ex.Message}");
            }
        }, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60));
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    private void Tick(Func<(string Name, string Path)?> currentInstance)
    {
        if (!Enabled) return;

        var instance = currentInstance();
        if (instance is null || string.IsNullOrWhiteSpace(instance.Value.Path)) return;
        if (!Directory.Exists(instance.Value.Path)) return;

        var now = DateTime.Now;
        if ((now - _lastRunAt).TotalMinutes < IntervalMinutes) return;

        var saves = Path.Combine(instance.Value.Path, "saves");
        if (!Directory.Exists(saves)) return;

        var worlds = Directory.EnumerateDirectories(saves).ToList();
        if (worlds.Count == 0) return;

        var backedUp = 0;
        foreach (var world in worlds)
        {
            // 世界没动过就不重复备份（level.dat 的写入时间当作「动过」的标志）
            if (!NeedsBackup(instance.Value.Name, world)) continue;
            if (BackupWorld(instance.Value.Name, world, out var error))
            {
                backedUp++;
            }
            else if (!string.IsNullOrEmpty(error))
            {
                _lastError = error;
                _context.Log.Warn($"备份存档 {Path.GetFileName(world)} 失败：{error}");
            }
        }

        Prune(instance.Value.Name, saves);
        _lastRunAt = now;
        if (backedUp > 0)
            _context.Log.Info($"已自动备份 {backedUp} 个存档（{instance.Value.Name}）");
    }

    /// <summary>这个存档需不需要再备份（按 level.dat 的修改时间判断）。</summary>
    private bool NeedsBackup(string instanceName, string worldPath)
    {
        try
        {
            var levelDat = Path.Combine(worldPath, "level.dat");
            if (!File.Exists(levelDat)) return false;

            var lastWrite = File.GetLastWriteTime(levelDat);
            var newest = NewestBackupTime(instanceName, Path.GetFileName(worldPath));
            return newest is null || lastWrite > newest.Value;
        }
        catch
        {
            return true;
        }
    }

    #endregion

    #region 备份 / 清理 / 恢复

    /// <summary>备份一个存档；返回是否成功。</summary>
    public bool BackupWorld(string instanceName, string worldPath, out string? error)
    {
        error = null;
        try
        {
            var worldName = Path.GetFileName(worldPath);
            var targetDir = Path.Combine(_root, SafeName(instanceName), SafeName(worldName));
            Directory.CreateDirectory(targetDir);

            var zipPath = Path.Combine(targetDir, $"{DateTime.Now:yyyyMMdd-HHmmss}.zip");
            var levelDat = Path.Combine(worldPath, "level.dat");
            if (!File.Exists(levelDat))
            {
                error = "缺少 level.dat（不是有效的存档文件夹）";
                return false;
            }

            // 逐个文件打包：游戏正在写某些 region 文件时可能被占用，
            // 这种文件跳过就好，不能因为一个文件锁住就整份备份失败
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                foreach (var file in Directory.EnumerateFiles(worldPath, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        var relative = Path.GetRelativePath(worldPath, file);
                        zip.CreateEntryFromFile(file, relative, CompressionLevel.Fastest);
                    }
                    catch (IOException)
                    {
                        // 占用中，跳过
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // 无权限，跳过
                    }
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>备份当前实例的所有存档（用户点「立即备份一次」）。</summary>
    public int BackupAll(string instanceName, string instancePath, out string? error)
    {
        error = null;
        var saves = Path.Combine(instancePath, "saves");
        if (!Directory.Exists(saves))
        {
            error = "这个实例没有 saves 文件夹";
            return 0;
        }

        var count = 0;
        foreach (var world in Directory.EnumerateDirectories(saves))
            if (BackupWorld(instanceName, world, out error))
                count++;

        Prune(instanceName, saves);
        _lastRunAt = DateTime.Now;
        return count;
    }

    /// <summary>
    ///     清理：存档还在 → 只留最近 <see cref="KeepPerWorld" /> 份；
    ///     存档已被删除 → 只留最后一份，超过 <see cref="OrphanKeepDays" /> 天则连最后一份也删。
    /// </summary>
    public void Prune(string instanceName, string savesPath)
    {
        try
        {
            var instanceDir = Path.Combine(_root, SafeName(instanceName));
            if (!Directory.Exists(instanceDir)) return;

            var existingWorlds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(savesPath) && Directory.Exists(savesPath))
                foreach (var world in Directory.EnumerateDirectories(savesPath))
                    existingWorlds.Add(Path.GetFileName(world));

            foreach (var worldDir in Directory.EnumerateDirectories(instanceDir))
            {
                var worldName = Path.GetFileName(worldDir);
                var backups = Directory.EnumerateFiles(worldDir, "*.zip")
                    .OrderByDescending(path => Path.GetFileName(path), StringComparer.Ordinal)
                    .ToList();
                if (backups.Count == 0)
                {
                    TryDeleteDirectory(worldDir);
                    continue;
                }

                var worldExists = existingWorlds.Contains(worldName);
                if (worldExists)
                {
                    foreach (var old in backups.Skip(KeepPerWorld)) TryDeleteFile(old);
                }
                else
                {
                    // 存档被删了：只留最后一份，且最多留 OrphanKeepDays 天
                    foreach (var old in backups.Skip(1)) TryDeleteFile(old);

                    var last = backups[0];
                    var age = DateTime.Now - File.GetLastWriteTime(last);
                    if (age.TotalDays > OrphanKeepDays)
                    {
                        TryDeleteFile(last);
                        _context.Log.Info($"存档「{worldName}」已被删除且最后一份备份超过 {OrphanKeepDays} 天，已清理");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _context.Log.Warn($"清理旧备份失败：{ex.Message}");
        }
    }

    /// <summary>把某个备份还原回 <c>saves/&lt;存档名&gt;</c>；同名存档会先改名成 <c>&lt;存档名&gt;.old-时间</c>。</summary>
    public bool Restore(string instancePath, string instanceName, string worldName, string zipPath, out string? error)
    {
        error = null;
        try
        {
            var saves = Path.Combine(instancePath, "saves");
            Directory.CreateDirectory(saves);
            var target = Path.Combine(saves, worldName);

            if (Directory.Exists(target))
            {
                var oldName = $"{worldName}.old-{DateTime.Now:yyyyMMdd-HHmmss}";
                Directory.Move(target, Path.Combine(saves, oldName));
                _context.Log.Info($"还原前把现有存档改名为 {oldName}");
            }

            Directory.CreateDirectory(target);
            ZipFile.ExtractToDirectory(zipPath, target, true);
            Prune(instanceName, saves);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>列出某个备份实例下每个存档的备份情况。</summary>
    public IReadOnlyList<(string World, int Count, DateTime? Newest, string NewestPath)> ListBackups(string instanceName)
    {
        var result = new List<(string, int, DateTime?, string)>();
        try
        {
            var instanceDir = Path.Combine(_root, SafeName(instanceName));
            if (!Directory.Exists(instanceDir)) return result;

            foreach (var worldDir in Directory.EnumerateDirectories(instanceDir))
            {
                var backups = Directory.EnumerateFiles(worldDir, "*.zip")
                    .OrderByDescending(path => Path.GetFileName(path), StringComparer.Ordinal)
                    .ToList();
                if (backups.Count == 0) continue;
                result.Add((Path.GetFileName(worldDir), backups.Count, File.GetLastWriteTime(backups[0]), backups[0]));
            }
        }
        catch (Exception ex)
        {
            _context.Log.Warn($"读取备份列表失败：{ex.Message}");
        }

        return result;
    }

    public string BackupRoot => _root;

    public string? LastError => _lastError;

    public DateTime LastRunAt => _lastRunAt;

    private DateTime? NewestBackupTime(string instanceName, string worldName)
    {
        try
        {
            var dir = Path.Combine(_root, SafeName(instanceName), SafeName(worldName));
            if (!Directory.Exists(dir)) return null;
            DateTime? newest = null;
            foreach (var file in Directory.EnumerateFiles(dir, "*.zip"))
            {
                var time = File.GetLastWriteTime(file);
                if (newest is null || time > newest) newest = time;
            }

            return newest;
        }
        catch
        {
            return null;
        }
    }

    private static string SafeName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim();
        return cleaned.Length == 0 ? "unknown" : cleaned;
    }

    private void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex)
        {
            _context.Log.Warn($"删除旧备份 {Path.GetFileName(path)} 失败：{ex.Message}");
        }
    }

    private void TryDeleteDirectory(string path)
    {
        try
        {
            Directory.Delete(path, true);
        }
        catch
        {
            // 忽略
        }
    }

    #endregion
}
