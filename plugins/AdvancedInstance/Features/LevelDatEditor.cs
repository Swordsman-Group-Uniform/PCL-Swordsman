using System.IO;
using AdvancedInstance.Nbt;

namespace AdvancedInstance.Features;

/// <summary>从一个存档的 <c>level.dat</c> 里读出来的基本数据。</summary>
internal sealed class LevelDatInfo
{
    public string WorldPath { get; init; } = "";
    public string LevelName { get; set; } = "";
    public int GameType { get; set; }
    public int Difficulty { get; set; }
    public bool Hardcore { get; set; }
    public bool AllowCommands { get; set; }
    public bool Raining { get; set; }
    public bool Thundering { get; set; }
    public long DayTime { get; set; }
    public int SpawnX { get; set; }
    public int SpawnY { get; set; }
    public int SpawnZ { get; set; }

    /// <summary>存档文件里记录的游戏版本名（<c>Data.Version.Name</c>，可能为空）。</summary>
    public string VersionName { get; set; } = "";

    public string GameTypeText => GameType switch
    {
        0 => "生存",
        1 => "创造",
        2 => "冒险",
        3 => "旁观",
        _ => $"未知（{GameType}）"
    };

    public string DifficultyText => Difficulty switch
    {
        0 => "和平",
        1 => "简单",
        2 => "普通",
        3 => "困难",
        _ => $"未知（{Difficulty}）"
    };
}

/// <summary>
///     <c>level.dat</c> 读写：只改「存档基本数据」这几项，其余标签原样保留
///     （NBT 读到内存、改字段、再整份写回，标签顺序与内容都不动）。
/// </summary>
internal static class LevelDatEditor
{
    /// <summary>读一个存档的基本数据；不是有效存档返回 null。</summary>
    public static LevelDatInfo? Load(string worldPath)
    {
        var path = Path.Combine(worldPath, "level.dat");
        if (!File.Exists(path)) return null;

        var root = NbtFile.Read(path, out _);
        var data = root.GetCompound("Data");
        if (data is null) return null;

        var version = data.GetCompound("version") ?? data.GetCompound("Version");
        return new LevelDatInfo
        {
            WorldPath = worldPath,
            LevelName = data.GetString("LevelName") ?? Path.GetFileName(worldPath),
            GameType = data.GetInt("GameType") ?? 0,
            Difficulty = data.GetByte("Difficulty") ?? 2,
            Hardcore = (data.GetByte("hardcore") ?? 0) != 0,
            AllowCommands = (data.GetByte("allowCommands") ?? 0) != 0,
            Raining = (data.GetByte("raining") ?? 0) != 0,
            Thundering = (data.GetByte("thundering") ?? 0) != 0,
            DayTime = data.GetLong("DayTime") ?? 0,
            SpawnX = data.GetInt("SpawnX") ?? 0,
            SpawnY = data.GetInt("SpawnY") ?? 64,
            SpawnZ = data.GetInt("SpawnZ") ?? 0,
            VersionName = version?.GetString("Name") ?? ""
        };
    }

    /// <summary>
    ///     写回基本数据。写之前会把原文件复制成 <c>level.dat.bak-时间</c>，
    ///     万一改出问题可以直接换回来。
    /// </summary>
    public static bool Save(LevelDatInfo info, out string? backupPath, out string? error)
    {
        backupPath = null;
        error = null;

        try
        {
            var path = Path.Combine(info.WorldPath, "level.dat");
            if (!File.Exists(path))
            {
                error = "找不到 level.dat";
                return false;
            }

            var root = NbtFile.Read(path, out var compression);
            var data = root.GetCompound("Data");
            if (data is null)
            {
                error = "level.dat 里没有 Data 标签";
                return false;
            }

            data.SetString("LevelName", info.LevelName);
            data.SetInt("GameType", info.GameType);
            data.SetByte("Difficulty", (byte)Math.Clamp(info.Difficulty, 0, 3));
            data.SetByte("hardcore", (byte)(info.Hardcore ? 1 : 0));
            data.SetByte("allowCommands", (byte)(info.AllowCommands ? 1 : 0));
            data.SetByte("raining", (byte)(info.Raining ? 1 : 0));
            data.SetByte("thundering", (byte)(info.Thundering ? 1 : 0));
            data.SetLong("DayTime", Math.Max(0, info.DayTime));
            data.SetInt("SpawnX", info.SpawnX);
            data.SetInt("SpawnY", info.SpawnY);
            data.SetInt("SpawnZ", info.SpawnZ);

            // 备份原文件
            backupPath = Path.Combine(info.WorldPath, $"level.dat.bak-{DateTime.Now:yyyyMMdd-HHmmss}");
            File.Copy(path, backupPath, true);

            NbtFile.Write(path, root, compression);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>回滚到某个 level.dat 备份。</summary>
    public static bool RestoreBackup(string worldPath, string backupPath, out string? error)
    {
        error = null;
        try
        {
            var target = Path.Combine(worldPath, "level.dat");
            if (!File.Exists(backupPath))
            {
                error = "备份文件不存在";
                return false;
            }

            if (File.Exists(target)) File.Copy(target, target + $".before-restore-{DateTime.Now:HHmmss}", true);
            File.Copy(backupPath, target, true);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>列出某个存档的 level.dat 备份（新的在前）。</summary>
    public static IReadOnlyList<string> ListBackups(string worldPath)
    {
        try
        {
            if (!Directory.Exists(worldPath)) return [];
            return Directory.EnumerateFiles(worldPath, "level.dat.bak-*")
                .OrderByDescending(path => path, StringComparer.Ordinal)
                .ToList();
        }
        catch
        {
            return [];
        }
    }
}
