using System.IO;
using PCL.Core.App;
using PCL.Core.App.Localization;

namespace PCL;

/// <summary>
///     Minecraft 下载限制策略。
///     <para>
///         默认按相关法律法规要求，禁止通过启动器下载 Minecraft（原版、各类加载器，以及会顺带下载 MC 的整合包安装），
///         整合包下载只保存为 .mrpack 文件而不安装。
///     </para>
///     <para>
///         这里只有进程内的静态状态与一个变更事件，启动器不感知、不检测任何插件；
///         需要在运行时解除限制时，由外部程序集（例如插件）直接改写这些属性并调用 <see cref="NotifyChanged" />。
///     </para>
/// </summary>
public static class DownloadPolicy
{
    /// <summary>
    ///     是否允许使用「下载」页面的任何下载功能。默认关闭。
    ///     <para>为 <c>false</c> 时，下载页的所有子页都会被替换为同一个「功能已关闭」占位页。</para>
    /// </summary>
    public static bool AllowDownloads { get; set; } = true;

    /// <summary>
    ///     是否允许下载 / 安装 Minecraft。默认禁止。
    ///     <para>这是 <see cref="AllowDownloads" /> 的别名：下载页整体关闭时，Minecraft 下载自然也关闭。</para>
    /// </summary>
    public static bool AllowMinecraftDownload
    {
        // 显式设置优先；未设置时由「PCLS 核心插件」的 unlock.region 功能键决定
        get => _allowMinecraftDownload ?? (AllowDownloads && FeatureFlags.IsEnabled(FeatureFlags.Keys.UnlockRegion));
        set => _allowMinecraftDownload = value;
    }

    private static bool? _allowMinecraftDownload;

    /// <summary>
    ///     整合包下载完成后是否直接安装。默认否，只把文件保存到下载文件夹。
    /// </summary>
    public static bool InstallModpackAfterDownload { get; set; }

    /// <summary>
    ///     策略发生变化时触发，供已经打开的界面刷新限制状态。
    /// </summary>
    public static event Action? Changed;

    /// <summary>
    ///     通知界面重新读取策略（可在任意线程调用）。
    /// </summary>
    public static void NotifyChanged() => ModBase.RunInUi(() => Changed?.Invoke());

    /// <summary>
    ///     获取默认下载文件夹（优先使用设置里的下载位置）。
    /// </summary>
    public static string GetDownloadFolder()
    {
        var folder = States.Tool.DownloadFolder;
        if (string.IsNullOrWhiteSpace(folder)) folder = Path.Combine(ModBase.exePath, "PCL", "MyDownload");
        Directory.CreateDirectory(folder);
        return folder;
    }

    /// <summary>
    ///     在下载文件夹中生成一个不重名的保存路径。
    /// </summary>
    public static string GetSavePath(string folder, string fileName, string extension)
    {
        var baseName = SanitizeFileName(string.IsNullOrWhiteSpace(fileName) ? "modpack" : fileName);
        var path = Path.Combine(folder, $"{baseName}.{extension}");
        for (var index = 1; File.Exists(path); index++)
            path = Path.Combine(folder, $"{baseName} ({index}).{extension}");
        return path;
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars()) name = name.Replace(invalid, '_');
        return name.Trim().TrimEnd('.');
    }

    /// <summary>
    ///     检查是否允许下载 Minecraft；被禁止时弹出提示并返回 <c>false</c>。
    /// </summary>
    public static bool CheckMinecraftDownloadAllowed()
    {
        if (AllowMinecraftDownload) return true;
        HintService.Hint(Lang.Text("Download.Disabled.Message"), HintType.Error);
        return false;
    }
}
