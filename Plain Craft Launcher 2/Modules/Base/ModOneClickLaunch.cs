using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.IO;

namespace PCL;

/// <summary>
///     ⑨ 一键启动：命令行 <c>--launch</c> + 实例快捷方式。
///     <para>
///         `PCL Swordsman.exe --launch` 会在启动器准备好之后**直接启动当前选中的实例**
///         （账号用最后登录的那个），并且在此之前就把启动器窗口藏起来，尽量做到「双击就直接进游戏」；
///         启动失败时会把窗口唤回来，好让用户看到错误提示。
///     </para>
///     <para>
///         快捷方式由百宝箱按钮创建：**名字取实例名、图标取该实例的图标**
///         （PNG 会就地转成 ICO 缓存到 <c>PCL\Logos</c>，因为 Windows 的快捷方式图标只认 .ico / .exe / .dll）。
///     </para>
/// </summary>
internal static class ModOneClickLaunch
{
    /// <summary>命令行是否请求了自动启动。</summary>
    public static bool IsRequested { get; private set; }

    private static bool _hooked;
    private static bool _launchWatched;
    private static bool _windowHiddenByUs;

    /// <summary>由 <see cref="Application" /> 在解析命令行时调用。</summary>
    public static void Request()
    {
        IsRequested = true;
        ModBase.Log("[OneClickLaunch] 检测到 --launch，将在准备完成后直接启动", ModBase.LogLevel.Normal);
    }

    /// <summary>主窗口加载完成后调用一次，挂上「实例列表加载完成」的等待逻辑。</summary>
    public static void Initialize()
    {
        if (!IsRequested || _hooked) return;
        _hooked = true;

        // 先把启动器藏起来，避免启动时窗口闪一下
        HideLauncher();

        ModInstanceList.mcInstanceListLoader.LoadingStateChanged += (_, _) => TryLaunch();
        TryLaunch();
    }

    private static void TryLaunch()
    {
        if (!IsRequested) return;
        if (ModInstanceList.mcInstanceListLoader.State != ModBase.LoadState.Finished) return;

        var page = ModMain.frmLaunchLeft;
        if (page is null) return;

        // 启动按钮还不可用（实例 / 账号尚未就绪）时，等它变可用
        if (!page.BtnLaunch.IsEnabled)
        {
            page.BtnLaunch.IsEnabledChanged -= OnLaunchButtonEnabled;
            page.BtnLaunch.IsEnabledChanged += OnLaunchButtonEnabled;
            return;
        }

        IsRequested = false;
        ModBase.Log("[OneClickLaunch] 准备就绪，开始启动（启动器保持隐藏）", ModBase.LogLevel.Normal);
        HideLauncher();
        WatchLaunchResult();

        ModBase.RunInUi(() =>
        {
            try
            {
                page.LaunchButtonClick();
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "[OneClickLaunch] 自动启动失败", ModBase.LogLevel.Hint);
                ShowLauncher();
            }
        });
    }

    private static void OnLaunchButtonEnabled(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not true) return;
        if (sender is not MyButton button) return;
        button.IsEnabledChanged -= OnLaunchButtonEnabled;
        TryLaunch();
    }

    #region 窗口隐藏 / 唤回

    /// <summary>把启动器彻底藏起来（移出屏幕 + 不占任务栏）。</summary>
    private static void HideLauncher()
    {
        var frm = ModMain.frmMain;
        if (frm is null || frm.Hidden) return;

        _windowHiddenByUs = true;
        ModBase.RunInUi(() => frm.Hidden = true);
    }

    /// <summary>把启动器唤回来（启动失败、需要用户处理时使用）。</summary>
    private static void ShowLauncher()
    {
        if (!_windowHiddenByUs) return;
        _windowHiddenByUs = false;

        var frm = ModMain.frmMain;
        if (frm is null) return;
        ModBase.RunInUi(() =>
        {
            frm.Hidden = false;
            frm.ShowWindowToTop();
        });
    }

    /// <summary>盯住启动加载器：失败就把窗口唤回来，成功则保持隐藏。</summary>
    private static void WatchLaunchResult()
    {
        if (_launchWatched) return;
        _launchWatched = true;

        ModLaunch.mcLaunchLoader.LoadingStateChanged += (_, _) =>
        {
            if (ModLaunch.mcLaunchLoader.State != ModBase.LoadState.Failed) return;
            ModBase.Log("[OneClickLaunch] 启动失败，唤回启动器", ModBase.LogLevel.Normal);
            ShowLauncher();
        };
    }

    #endregion

    #region 创建快捷方式

    /// <summary>
    ///     为**当前选中的实例**创建「一键启动」快捷方式：名字 = 实例名，图标 = 实例图标。
    /// </summary>
    /// <returns>创建成功的说明；用户取消 / 没选实例时返回 <see langword="null" />。</returns>
    public static string? CreateShortcut()
    {
        var instance = ModInstanceList.McMcInstanceSelected;
        if (instance is null)
        {
            HintService.Hint(Lang.Text("Tools.Test.Shortcut.OneClickNoInstance"), HintType.Error);
            return null;
        }

        var instanceName = string.IsNullOrWhiteSpace(instance.Name) ? "Minecraft" : instance.Name.Trim();
        var fileName = SanitizeFileName(instanceName) + ".lnk";
        var desktop = Paths.GetSpecialPath(Environment.SpecialFolder.Desktop, fileName);
        var startMenu = Paths.GetSpecialPath(Environment.SpecialFolder.StartMenu, @"Programs\" + fileName);

        var desktopName = Lang.Text("Tools.Test.Shortcut.Desktop");
        var startName = Lang.Text("Tools.Test.Shortcut.StartMenu");
        var choice = ModMain.MyMsgBox(
            Lang.Text("Tools.Test.Shortcut.OneClickConfirmMessage", instanceName, desktop, startMenu),
            Lang.Text("Tools.Test.Shortcut.OneClickSelectLocation"),
            Lang.Text("Common.Action.Cancel"), desktopName, startName);
        if (choice != 2 && choice != 3) return null;

        var path = choice == 2 ? desktop : startMenu;
        try
        {
            // 图标：实例图标（PNG → ICO），失败就退回本程序图标
            var iconPath = BuildInstanceIconFile(instance);
            Files.CreateShortcut(path, Basics.ExecutablePath, arguments: "--launch",
                description: Lang.Text("Tools.Test.Shortcut.OneClickDescription", instanceName),
                icon: iconPath is null ? Basics.ExecutablePath + ",0" : iconPath + ",0");

            return Lang.Text("Tools.Test.Shortcut.OneClickCreated", instanceName,
                choice == 2 ? desktopName : startName);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[OneClickLaunch] 创建快捷方式失败", ModBase.LogLevel.Msgbox,
                userSummary: Lang.Text("Tools.Test.Shortcut.CreateFailed"));
            return null;
        }
    }

    /// <summary>把实例图标转成 .ico 存到 <c>PCL\Logos</c>，返回文件路径；失败返回 null。</summary>
    private static string? BuildInstanceIconFile(McInstance instance)
    {
        try
        {
            var source = ResolveLogoUri(instance);
            if (source is null) return null;

            var png = RenderPng(source, 256);
            if (png is null) return null;

            var dir = Path.Combine(ModBase.exePath, "PCL", "Logos");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, SanitizeFileName(instance.Name) + ".ico");
            File.WriteAllBytes(path, WrapIco(png));
            ModBase.Log($"[OneClickLaunch] 已生成实例图标：{path}", ModBase.LogLevel.Debug);
            return path;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[OneClickLaunch] 生成实例图标失败，改用本程序图标", ModBase.LogLevel.Debug);
            return null;
        }
    }

    /// <summary>解析实例图标的来源 URI（自定义 PNG / 本地图片 / pack 资源）。</summary>
    private static Uri? ResolveLogoUri(McInstance instance)
    {
        var logo = instance.Logo;
        if (string.IsNullOrWhiteSpace(logo)) return null;

        // 自定义图标：实例目录下的 PCL\Logo.png
        if (logo.Replace('/', '\\').EndsWith(@"PCL\Logo.png", StringComparison.OrdinalIgnoreCase))
        {
            var file = Path.Combine(instance.PathInstance, "PCL", "Logo.png");
            return File.Exists(file) ? new Uri(Path.GetFullPath(file)) : null;
        }

        // 绝对路径的本地图片
        if (File.Exists(logo)) return new Uri(Path.GetFullPath(logo));

        // 内置方块图标等：pack://application:,,,/Plain Craft Launcher 2;component/Images/…
        if (logo.StartsWith("pack://", StringComparison.OrdinalIgnoreCase))
            return new Uri(logo, UriKind.Absolute);

        return null;
    }

    /// <summary>解码图片并等比缩放到指定尺寸，输出 PNG 字节。</summary>
    private static byte[]? RenderPng(Uri uri, int size)
    {
        var frame = BitmapFrame.Create(uri, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
        if (frame.PixelWidth <= 0 || frame.PixelHeight <= 0) return null;

        var scale = size / (double)Math.Max(frame.PixelWidth, frame.PixelHeight);
        BitmapSource source = Math.Abs(scale - 1d) < 0.01d
            ? frame
            : new TransformedBitmap(frame, new ScaleTransform(scale, scale));

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(source));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    /// <summary>把 PNG 字节包成单图标的 ICO（Vista 起支持 PNG 负载）。</summary>
    private static byte[] WrapIco(byte[] png)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write((ushort)0);           // reserved
        writer.Write((ushort)1);           // type = icon
        writer.Write((ushort)1);           // count
        writer.Write((byte)0);             // width  0 = 256
        writer.Write((byte)0);             // height 0 = 256
        writer.Write((byte)0);             // color count
        writer.Write((byte)0);             // reserved
        writer.Write((ushort)1);           // planes
        writer.Write((ushort)32);          // bpp
        writer.Write(png.Length);          // bytes in resource
        writer.Write(6 + 16);              // offset（ICONDIR 6 + 一条 ICONDIRENTRY 16）
        writer.Write(png);
        writer.Flush();
        return stream.ToArray();
    }

    /// <summary>文件名里去掉 Windows 不允许的字符。</summary>
    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Trim().Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        var result = new string(chars).Trim();
        return string.IsNullOrWhiteSpace(result) ? "Minecraft" : result;
    }

    #endregion
}
