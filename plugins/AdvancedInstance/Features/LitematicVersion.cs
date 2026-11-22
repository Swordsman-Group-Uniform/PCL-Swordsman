using System.IO;
using AdvancedInstance.Nbt;

namespace AdvancedInstance.Features;

/// <summary>一个投影文件的关键信息。</summary>
internal sealed record LitematicInfo(string Path, int? Version, int? DataVersion, string Name, long Size);

/// <summary>批量修改的结果。</summary>
internal sealed class LitematicApplyResult
{
    public int Total { get; set; }
    public int Changed { get; set; }
    public int AlreadyOk { get; set; }
    public int Failed { get; set; }
    public int BackedUp { get; set; }
    public List<string> Errors { get; } = [];

    public string Summary =>
        $"共 {Total} 个文件：已修改 {Changed}，本来就是目标版本 {AlreadyOk}，失败 {Failed}" +
        (BackedUp > 0 ? $"，已创建备份 {BackedUp} 份" : "");
}

/// <summary>
///     Litematica 投影文件的「适配版本」修改。
///     <para>
///         适配版本就是 <c>.litematic</c>（gzip 压缩的 NBT）根标签里的 <c>Version</c> 字段
///         —— 平时要用 NBTExplorer 打开改的那个。这里支持**一次改多个文件**。
///     </para>
///     <para>
///         版本号与 Minecraft 版本的对应关系来自 Litematica 自身写文件时的常量
///         （旧分支 5 / 现行分支 6）：<b>1.12.2 及更早 = 5，1.13 及以后 = 6</b>。
///         如果遇到 Litematica 之外的实现，可以在「自定义」里直接填数字。
///         另外，真正的方块 ID 兼容性由根标签里的 <c>MinecraftDataVersion</c> 决定，
///         需要一起改时勾上「同时修改 MinecraftDataVersion」即可。
///     </para>
/// </summary>
internal static class LitematicVersion
{
    /// <summary>目标版本预设（Version 字段的取值）。</summary>
    public static readonly (string Label, int Version, string Note)[] Presets =
    [
        ("1.12.2 及更早（旧版 Litematica）", 5, "旧分支写出的投影版本号；现在只在老整合包里见到"),
        ("1.13 ～ 1.21.x（现行 Litematica）", 6, "现行 Litematica 的 SCHEMATIC_VERSION，绝大多数投影是 6")
    ];

    /// <summary>按扩展名挑出投影文件（<c>.litematic</c>，也顺带认 <c>.schematic</c> 之外的旧名）。</summary>
    public static List<string> EnumerateFiles(string root, bool recursive)
    {
        if (!Directory.Exists(root)) return [];
        var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        return Directory.EnumerateFiles(root, "*.litematic", option)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>读一个投影文件的关键信息；不是有效投影返回 null。</summary>
    public static LitematicInfo? Read(string path)
    {
        try
        {
            var root = NbtFile.Read(path, out _);
            var name = root.GetCompound("Metadata")?.GetString("Name") ?? Path.GetFileNameWithoutExtension(path);
            return new LitematicInfo(path, root.GetInt("Version"), root.GetInt("MinecraftDataVersion"), name,
                new FileInfo(path).Length);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>批量修改适配版本（可选同时改 MinecraftDataVersion、可选改前备份）。</summary>
    public static LitematicApplyResult Apply(IEnumerable<string> files, int targetVersion,
        int? targetDataVersion, bool backupBeforeChange, Action<string>? warn = null)
    {
        var result = new LitematicApplyResult();

        foreach (var file in files)
        {
            result.Total++;
            try
            {
                var root = NbtFile.Read(file, out var compression);
                var current = root.GetInt("Version");
                var currentData = root.GetInt("MinecraftDataVersion");

                var versionOk = current == targetVersion;
                var dataOk = targetDataVersion is null || currentData == targetDataVersion;
                if (versionOk && dataOk)
                {
                    result.AlreadyOk++;
                    continue;
                }

                if (backupBeforeChange)
                {
                    var backupPath = $"{file}.bak-{DateTime.Now:yyyyMMdd-HHmmss}";
                    File.Copy(file, backupPath, true);
                    result.BackedUp++;
                }

                root.SetInt("Version", targetVersion);
                if (targetDataVersion is not null) root.SetInt("MinecraftDataVersion", targetDataVersion.Value);

                NbtFile.Write(file, root, compression);
                result.Changed++;
            }
            catch (Exception ex)
            {
                result.Failed++;
                result.Errors.Add($"{Path.GetFileName(file)}：{ex.Message}");
                warn?.Invoke($"修改投影 {Path.GetFileName(file)} 失败：{ex.Message}");
            }
        }

        return result;
    }
}
