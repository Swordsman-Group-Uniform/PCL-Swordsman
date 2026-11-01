using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using PCL.Core.App;
using PCL.Core.Logging;
using PCL.Core.UI.Theme;
using PCL.Core.Utils.OS;
using PCL.Core.Utils.OS.Window;

namespace PCL;

/// <summary>
///     高级材质（云母 / Aero）的宿主接线。
///     <para>
///         分层：<c>PCL.Core.Utils.OS.Window.MaterialService</c> 负责 Win32 / DWM 调用与降级，
///         本类负责「从配置取参数 → 应用材质 → 按参数调整标题栏 / 主界面的不透明度 → 通知悬停发光」。
///     </para>
///     <para>
///         功能由插件声明（<c>materials.advanced</c>）：未安装插件时一律按「无材质」处理，
///         并把窗口恢复到完全不透明，以免留下半透明残影。
///     </para>
/// </summary>
internal static class ModMaterial
{
    private static nint _hwnd;
    private static bool _isInstalled;
    private static MaterialKind _kind = MaterialKind.None;

    /// <summary>启动时锁定的材质类型（改类型只在重启后生效，见 <c>ReloadOptions</c>）。</summary>
    private static MaterialKind? _lockedKind;

    private static MaterialOptions _options = new();

    /// <summary>标题栏背景的基准画刷（未降低不透明度时的那一份）。</summary>
    private static SolidColorBrush? _titleBaseBrush;

    /// <summary>当前实际写到标题栏上的画刷，用于判断主题是否换掉了它。</summary>
    private static SolidColorBrush? _titleAppliedBrush;

    /// <summary>主界面背景的基准画刷。</summary>
    private static Brush? _mainBaseBrush;

    /// <summary>当前实际写到主界面上的画刷。</summary>
    private static Brush? _mainAppliedBrush;

    /// <summary>
    ///     侧边栏与卡片的基准画刷（即主题给的 <c>ColorBrushTransparentBackground</c>，本身已带半透明 alpha）。
    /// </summary>
    private static SolidColorBrush? _surfaceBaseBrush;

    /// <summary>当前实际写到卡片（资源字典）上的画刷，用于判断主题是否换掉了它。</summary>
    private static SolidColorBrush? _cardAppliedBrush;

    /// <summary>卡片 / 侧边栏背景所用资源键。</summary>
    private const string SurfaceBrushKey = "ColorBrushTransparentBackground";

    /// <summary>窗口是否已被改成「界面铺满 + 圆角外形」。</summary>
    private static bool _isWindowShaped;

    /// <summary>
    ///     我们是否真的给窗口设过 `SetWindowRgn` 区域。
    ///     <para>
    ///         **只有设过才允许清**：`ClearWindowRegion()` 是 `SetWindowRgn(NULL, 重绘)`，
    ///         它会强制窗口重画一次非客户区 —— 在 WPF 的 WindowChrome 下这等于把系统标题栏
    ///         （最小化 / 最大化 / 关闭三键）连同 Basic 主题的窗体一起闪出来。
    ///     </para>
    /// </summary>
    private static bool _shapeRegionApplied;

    /// <summary>界面是否已经铺满窗口（见 <c>SetFullBleed</c>）。</summary>
    private static bool _isFullBleed;

    /// <summary>铺满之前的 <c>PanBack</c> 外边距（上游是 10）。</summary>
    private static Thickness _originalPanBackMargin = new(10d);


    /// <summary>上一次下发的窗口外形描述，避免每帧刷日志。</summary>
    private static string? _lastShapeLog;


    /// <summary>上一次生效的材质描述，避免每帧刷日志。</summary>
    private static string? _lastAppliedLog;

    /// <summary>功能是否可用（由插件声明 <c>materials.advanced</c> 决定）。</summary>
    public static bool IsAvailable => FeatureFlags.IsEnabled(FeatureFlags.Keys.MaterialsAdvanced);

    /// <summary>用户选择的效果（未启用插件时恒为 <see cref="MaterialKind.None" />）。</summary>
    public static MaterialKind Kind => IsAvailable ? _kind : MaterialKind.None;

    /// <summary>实际生效的效果（可能因系统不支持而降级）。</summary>
    public static MaterialKind Applied => MaterialService.Applied;

    /// <summary>降级原因（未降级时为 <see langword="null" />）。</summary>
    public static string? FallbackReason => MaterialService.FallbackReason;

    /// <summary>当前系统支持的最高效果。</summary>
    public static MaterialKind MaxSupported => MaterialService.MaxSupported;

    /// <summary>当前参数（已把配置里的整数百分比换算成 0~1）。</summary>
    public static MaterialOptions Options => _options;

    /// <summary>悬停发光当前是否可用（半径为 0 或透明度为 0 视为关闭）。</summary>
    public static bool IsGlowEnabled => IsAvailable && _options.GlowRadius > 0d && _options.GlowOpacity > 0d;

    /// <summary>液态玻璃：整块玻璃的着色不透明度（0~1）。</summary>
    public static double GlassOpacity => _options.GlassOpacity;

    /// <summary>液态玻璃：边缘折射强度（0~30 像素）。</summary>
    public static double GlassRefraction => _options.GlassRefraction;

    /// <summary>液态玻璃：标题栏底色不透明度（0~1，0 表示标题栏完全透明）。</summary>
    public static double GlassTitleOpacity => _options.GlassTitleOpacity;

    /// <summary>液态玻璃：分割竖线不透明度（0~1）。</summary>
    public static double DividerOpacity => _options.DividerOpacity;

    /// <summary>液态玻璃：登录药丸不透明度（0~1）。</summary>
    public static double PillOpacity => _options.PillOpacity;

    /// <summary>大弹窗（对话框）表面不透明度（0~1）。</summary>
    public static double DialogOpacity => _options.DialogOpacity;

    /// <summary>右下角小弹窗（Toast）表面不透明度（0~1）。</summary>
    public static double ToastOpacity => _options.ToastOpacity;

    /// <summary>是否处于液态玻璃模式（用户选了且功能可用）。</summary>
    public static bool IsLiquidGlass => Kind == MaterialKind.LiquidGlass;

    /// <summary>
    ///     当前生效的窗口圆角（0~24）。
    ///     普通模式取 <c>UiWindowCornerRadius</c>（默认 6，与上游一致）；液态玻璃取 <c>UiMaterialGlassCornerRadius</c>（默认 16）。
    /// </summary>
    public static double CornerRadius
    {
        get
        {
            // 没开材质：圆角就是普通窗口的圆角，随设置走
            if (!IsAvailable || MaterialService.Applied == MaterialKind.None)
                return IsLiquidGlass
                    ? Math.Clamp(Config.System.MaterialGlassCornerRadius, 0, 24)
                    : Math.Clamp(Config.System.WindowCornerRadius, 0, 24);

            // Windows 10：材质生效时**恒为方角**（用户定的规矩，不是 bug）。
            // 系统 AccentBlur 按整个窗口矩形绘制、不吃 `SetWindowRgn`，圆角只会露在四个小三角里，
            // 所以 Win10 上干脆不做圆角 —— 界面、玻璃、磨砂统一成方的，调圆角滑块也不生效。
            if (!DwmInterop.IsBackdropSupported) return 0d;

            // Windows 11：圆角同样写 0，交给 DWM 的窗口圆角去裁。
            // 这样「实际圆角」与「玻璃圆角」永远是同一条曲线：DWM 先圆角，我们的玻璃 / 描边 / 折射环
            // 都是方的、被这条圆角裁出来 —— 不管 DPI 缩放多少（DWM 的半径是物理像素，猜数字一定对不上）。
            return 0d;
        }
    }

    /// <summary>
    ///     圆角设置变化后应用：更新内容裁剪圆角；材质生效时把窗口区域重新裁一遍。
    /// </summary>
    public static void ApplyCornerRadius()
    {
        try
        {
            var frm = ModMain.frmMain;
            if (frm?.RectForm is null) return;

            var radius = CornerRadius;
            if (_isWindowShaped)
            {
                // 材质生效：Win10 方角、Win11 由 DWM 圆角 —— 内容都不再自己裁
                SetContentCornerRadius(frm, 0d);
            }
            else
            {
                SetContentCornerRadius(frm, radius);
            }
        }
        catch (Exception ex)
        {
            LogWrapper.Warn("Material", "应用窗口圆角失败：" + ex.Message);
        }
    }

    /// <summary>悬停发光半径（0~20）。</summary>
    public static double GlowRadius => _options.GlowRadius;

    /// <summary>悬停发光透明度（0~1）。</summary>
    public static double GlowOpacity => _options.GlowOpacity;

    /// <summary>
    ///     注册全局事件（只生效一次）。必须在窗口创建前后尽早调用，之后任何配置 / 主题 / 插件变化都会自动重申请。
    /// </summary>
    public static void EnsureInstalled()
    {
        if (_isInstalled) return;
        _isInstalled = true;

        if (ModMain.frmMain is { } window)
            MaterialGlow.Install(window);

        // 插件启用 / 禁用后功能开关会整体重置，这里重申请一次（无需重启）
        FeatureFlags.Changed += () => ModBase.RunInUi(ApplyAll);
        PluginHostBootstrap.Ready += () => ModBase.RunInUi(ApplyAll);
        // 深色 / 浅色切换与主题色切换：重新下发沉浸式标题栏与材质
        ThemeService.ColorModeChanged += (_, _) => ModBase.RunInUi(ApplyAll);
        ThemeService.ColorThemeChanged += _ => ModBase.RunInUi(ApplyAll);
    }

    /// <summary>
    ///     绑定窗口句柄。首次 <c>Loaded</c> 前句柄可能为 0，此时只记录，等后续调用再应用。
    /// </summary>
    public static void AttachWindow(nint hwnd)
    {
        if (hwnd == nint.Zero) return;
        _hwnd = hwnd;

        // 窗口尺寸 / 状态变化后要重新下发圆角外形（窗口区域是物理像素，不会跟着缩放走）
        if (ModMain.frmMain is { } window)
        {
            window.SizeChanged += (_, _) =>
            {
                if (_isWindowShaped) ApplyWindowOpacity();
            };
            window.StateChanged += (_, _) =>
            {
                if (_isWindowShaped) ApplyWindowOpacity();
            };
        }

        ApplyAll();
    }

    /// <summary>重新读取配置并应用全部效果（材质 + 不透明度 + 悬停发光参数）。</summary>
    public static void ApplyAll()
    {
        try
        {
            ReloadOptions();
            ApplyBackdrop();
            ApplyWindowOpacity();
            MaterialGlow.Refresh();
        }
        catch (Exception ex)
        {
            LogWrapper.Warn("Material", "应用高级材质失败：" + ex.Message);
        }
    }

    /// <summary>
    ///     重新应用四层表面的不透明度：标题栏 / 主界面 / 侧边栏 / 卡片。
    ///     主题刷新换过画刷后必须调用，否则透明度会被主题配色吃掉。
    /// </summary>
    public static void ApplyWindowOpacity()
    {
        try
        {
            var frm = ModMain.frmMain;
            if (frm?.PanForm is null || frm.PanTitle is null) return;

            // 材质未生效时保持原始浓度：否则窗口会直接透出桌面，看起来像坏了
            var materialOn = IsAvailable && MaterialService.Applied != MaterialKind.None;

            if (!materialOn)
                ModWallpaperFrost.Clear();

            double titleFactor, mainFactor, sidebarFactor, cardFactor;
            if (materialOn && IsLiquidGlass)
            {
                // 液态玻璃：整个窗口是一块玻璃 —— 标题栏默认没有独立底色（0 = 与玻璃连成一片），
                // 但允许单独加一点标题栏底色（UiMaterialGlassTitleOpacity，默认 0）
                titleFactor = Math.Clamp(_options.GlassTitleOpacity, 0d, 1d);
                sidebarFactor = 0d;
                mainFactor = Math.Clamp(_options.GlassOpacity, 0.1d, 1d);
                cardFactor = Math.Clamp(_options.CardOpacity, 0.3d, 1d);
            }
            else
            {
                titleFactor = materialOn ? Math.Clamp(_options.TitleOpacity, 0.3d, 1d) : 1d;
                mainFactor = materialOn ? Math.Clamp(_options.MainOpacity, 0.3d, 1d) : 1d;
                sidebarFactor = materialOn ? Math.Clamp(_options.SidebarOpacity, 0.3d, 1d) : 1d;
                cardFactor = materialOn ? Math.Clamp(_options.CardOpacity, 0.3d, 1d) : 1d;
            }

            ApplyTitleOpacity(frm, titleFactor);
            ApplyMainOpacity(frm, mainFactor);
            ApplySurfaceOpacity(frm, sidebarFactor, cardFactor);

            // 玻璃圆角 / 竖线 / 小玻璃（非玻璃模式时自己会还原）
            ModLiquidGlass.Refresh();

            // 大弹窗（对话框）与右下角小弹窗（Toast）也跟随高级材质
            ModOverlayMaterial.Refresh();

            // 壁纸磨砂：
            //  - 液态玻璃：玻璃后面要糊
            //  - 云母 / Aero 在 Windows 10 上：没有 DWM 后台材质，全靠这层自绘磨砂
            //    （Win11 22621+ 有系统 Mica，不需要它，免得糊两遍）
            var needsInAppFrost = materialOn &&
                                 (IsLiquidGlass || !MaterialService.IsWindows11);
            if (needsInAppFrost)
                ModWallpaperFrost.Apply(Math.Clamp(_options.BlurRadius * 1.2d, 8d, 60d));
            else if (!IsLiquidGlass)
                ModWallpaperFrost.Clear();

            // 窗口外形：让系统后台模糊与界面本体完全重合（否则磨砂会比界面大一圈、且是直角）
            ApplyWindowShape(frm, materialOn);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn("Material", "调整窗口不透明度失败：" + ex.Message);
        }
    }

    /// <summary>
    ///     窗口外形对齐。
    ///     <para>
    ///         云母 / Aero 的后台模糊是**按整个窗口矩形**绘制的，而界面本体是「内缩 10px + 圆角 6px」裁剪的，
    ///         于是磨砂区会比界面大一圈、并且是直角（用户实测 bug）。
    ///     </para>
    ///     <para>
    ///         **不动界面布局**（边距与圆角保持上游原样），而是把**窗口**裁成界面本体的形状：
    ///         窗口区域 = 客户区内缩 10px（`PanBack.Margin`）+ 圆角 6（上游值）。
    ///         这样磨砂与界面完全重合、圆角与上游一致，界面也不会被拉伸。
    ///     </para>
    /// </summary>
    private static void ApplyWindowShape(FormMain frm, bool materialOn)
    {
        if (frm.PanBack is null) return;

        if (!materialOn)
        {
            RestoreWindowShape(frm);
            return;
        }

        _isWindowShaped = true;

        // 系统磨砂（Win10 的 AccentBlur / Win11 的云母·亚克力）都按**整个窗口矩形**绘制，
        // 而界面默认内缩 10px（PanBack.Margin）：那圈 10px 的磨砂就会露在界面外面 =「边框比启动器大」。
        // 所以材质生效时让界面铺满窗口（Margin=0），磨砂区域与界面**完全重合**。
        SetFullBleed(frm, true);

        // ── Windows 11（22621+）：圆角交给 DWM ──────────────────────────────
        // 关键：DWM 的云母 / 亚克力会**跟着 DWM 的窗口圆角走**，所以让系统自己圆角即可，
        // 千万不要再自己设窗口区域（`SetWindowRgn` 会把 DWM 圆角关掉，而系统材质又不吃区域，
        // 于是磨砂铺满整个矩形 = Win11 上的「模糊溢出」）。
        if (DwmInterop.IsBackdropSupported)
        {
            // 只清「我们自己设过」的区域（新流程从不设，所以这里基本是空操作）。
            // 绝不无条件清区域：那是 `SetWindowRgn(NULL, 重绘)`，会重画非客户区 = 系统三键 + Basic 窗体闪现。
            ClearShapeRegionIfNeeded();

            // Win11 的圆角必须自己开：
            // WPF 的 WindowChrome 窗口在系统眼里是弹窗样式，`DWMWCP_DEFAULT` 判定为**不圆角**，
            // 只有显式设 `Round` 才会被 DWM 圆角裁（这也解释了为什么删掉这句就变成直角）。
            // 只调这一句、不碰非客户区，因此不会有闪烁。
            EnsureSystemRoundedCorners();

            // 圆角完全交给 DWM：内容自己一律不裁（裁两次会在圆角处形成两层边）。
            // 玻璃描边 / 折射环的 CornerRadius 也取 0，靠这条系统圆角裁出来，两者必然同曲率。
            SetContentCornerRadius(frm, 0d);
            frm.ResizeOffsetWpf = 6;
            LogShape("DWM 圆角（Win11：系统材质与玻璃都被同一条 DWM 圆角裁出，不会溢出、也不会对不齐）");
            return;
        }

        // ── Windows 10：一律方角 ─────────────────────────────────────────────
        // 旧版 AccentBlur 由 DWM 按整个窗口矩形绘制、不吃窗口区域（用户日志已证实：
        // 区域设成 (10,10)-(890,540) 也照样铺满整窗）。既然磨砂一定是方的，
        // 就让界面也保持方角，两者完全重合，圆角滑块在 Win10 上不再生效。
        ClearShapeRegionIfNeeded();

        frm.ResizeOffsetWpf = 0;
        SetContentCornerRadius(frm, 0d);
        LogShape("方角（Win10：系统磨砂按整个窗口矩形绘制，界面与之完全重合）");
    }

    /// <summary>设置内容自身的圆角裁剪（窗口区域裁好时传 0，避免和区域叠成两层）。</summary>
    private static void SetContentCornerRadius(FormMain frm, double radius)
    {
        try
        {
            if (frm.RectForm is null) return;
            if (Math.Abs(frm.RectForm.RadiusX - radius) < 0.01d &&
                Math.Abs(frm.RectForm.RadiusY - radius) < 0.01d) return;
            frm.RectForm.RadiusX = radius;
            frm.RectForm.RadiusY = radius;
        }
        catch
        {
            // 几何体可能已冻结，忽略
        }
    }

    /// <summary>恢复窗口矩形外形（界面布局本来就没动过）。</summary>
    private static void RestoreWindowShape(FormMain frm)
    {
        if (!_isWindowShaped) return;
        _isWindowShaped = false;
        SetFullBleed(frm, false);

        if (frm is not null)
        {
            try
            {
                var radius = Math.Clamp(Config.System.WindowCornerRadius, 0, 24);
                frm.RectForm.RadiusX = radius;
                frm.RectForm.RadiusY = radius;
            }
            catch
            {
                // 忽略
            }

            frm.ResizeOffsetWpf = 6; // 上游的缩放热区偏移
        }

        if (_hwnd == nint.Zero) return;
        try
        {
            ClearShapeRegionIfNeeded();
            LogShape("矩形窗口（未启用材质）");
        }
        catch (Exception ex)
        {
            LogWrapper.Warn("Material", "恢复窗口外形失败：" + ex.Message);
        }
    }

    /// <summary>Win11 是否已经打开过 DWM 圆角（只设一次，避免每次重申请都调一遍 DWM）。</summary>
    private static bool _systemRoundEnabled;

    /// <summary>
    ///     打开 DWM 窗口圆角（Win11 22621+）。
    ///     <para>
    ///         WPF 的 WindowChrome 窗口用 `DWMWCP_DEFAULT` 会被判定成弹窗、**不圆角**，
    ///         必须显式设 `Round`。这个调用只改 DWM 属性，不触发 `WM_NCCALCSIZE` / 非客户区重画，
    ///         所以不会闪系统标题栏；而且只在第一次调。
    ///     </para>
    /// </summary>
    private static void EnsureSystemRoundedCorners()
    {
        if (_systemRoundEnabled || _hwnd == nint.Zero) return;
        if (DwmInterop.TrySetWindowCornerPreference(_hwnd, DwmInterop.WindowCornerPreference.Round))
        {
            _systemRoundEnabled = true;
            LogWrapper.Info("Material", "已启用 DWM 窗口圆角（Win11：材质与玻璃都由这条系统圆角裁出）");
        }
    }

    /// <summary>
    ///     只在我们确实设过窗口区域时才清掉它（见 <see cref="_shapeRegionApplied" /> 的说明）。
    ///     没设过就什么都不做 —— 空清一次会强制重画非客户区，把系统三键和 Basic 主题窗体闪出来。
    /// </summary>
    private static void ClearShapeRegionIfNeeded()
    {
        if (!_shapeRegionApplied || _hwnd == nint.Zero) return;
        _shapeRegionApplied = false;
        WindowInterop.ClearWindowRegion(_hwnd);
        LogWrapper.Info("Material", "已清除窗口区域（恢复系统默认外形）");
    }

    /// <summary>
    ///     界面是否铺满整个窗口（去掉 <c>PanBack</c> 的 10px 外边距）。
    ///     只有「系统磨砂生效」时才需要：磨砂按窗口矩形画，界面铺满才能和它完全重合。
    /// </summary>
    private static void SetFullBleed(FormMain frm, bool enabled)
    {
        if (frm?.PanBack is null) return;

        // 窗口四周那圈假阴影（`PanShadow`，10px 黑色渐变 + 四个 16px 圆角方块）本来是画在
        // 10px 透明边距里的；界面铺满窗口后它正好压在玻璃/磨砂上，看上去就是「边框糊了一层透明膜」。
        // 铺满时整组隐藏，还原边距时再放出来。
        if (frm.PanShadow is not null)
        {
            var shadowVisibility = enabled ? Visibility.Collapsed : Visibility.Visible;
            if (frm.PanShadow.Visibility != shadowVisibility) frm.PanShadow.Visibility = shadowVisibility;
        }

        if (enabled)
        {
            if (!_isFullBleed)
            {
                _originalPanBackMargin = frm.PanBack.Margin;
                _isFullBleed = true;
                // 界面铺满后，窗口本身就等于界面；为了让「有材质 / 没材质」两种状态**看起来一样大**，
                // 窗口要相应缩小两圈边距（左右各 10、上下各 10），保持中心不动。
                ResizeWindow(frm, -_originalPanBackMargin.Left * 2d, -_originalPanBackMargin.Top * 2d);
            }

            if (frm.PanBack.Margin != new Thickness(0))
                frm.PanBack.Margin = new Thickness(0);
        }
        else if (_isFullBleed)
        {
            _isFullBleed = false;
            frm.PanBack.Margin = _originalPanBackMargin;
            ResizeWindow(frm, _originalPanBackMargin.Left * 2d, _originalPanBackMargin.Top * 2d);
        }
    }

    /// <summary>按中心缩放窗口（正数放大、负数缩小）。</summary>
    private static void ResizeWindow(FormMain frm, double deltaWidth, double deltaHeight)
    {
        if (Math.Abs(deltaWidth) < 0.01d && Math.Abs(deltaHeight) < 0.01d) return;

        try
        {
            if (frm.WindowState != WindowState.Normal) return;

            var width = Math.Max(frm.MinWidth, frm.Width + deltaWidth);
            var height = Math.Max(frm.MinHeight, frm.Height + deltaHeight);
            var actualDeltaWidth = width - frm.Width;
            var actualDeltaHeight = height - frm.Height;

            frm.Left -= actualDeltaWidth / 2d;
            frm.Top -= actualDeltaHeight / 2d;
            frm.Width = width;
            frm.Height = height;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn("Material", "调整窗口尺寸失败：" + ex.Message);
        }
    }

    /// <summary>
    ///     存盘时给窗口尺寸补回「铺满吃掉的那圈边距」：
    ///     `States.UI.WindowWidth/Height` 存的始终是**没有材质时的窗口尺寸**，
    ///     否则每次启动都会再缩 20px，越缩越小。
    /// </summary>
    public static double WindowSizeCompensation =>
        _isFullBleed ? Math.Max(_originalPanBackMargin.Left, _originalPanBackMargin.Top) * 2d : 0d;

    /// <summary>
    ///     按当前尺寸下发「界面本体形状」的窗口区域（最大化时不做裁剪，避免顶到屏幕边）。
    /// </summary>
    private static bool UpdateWindowRegion(FormMain frm, double radius)
    {
        if (_hwnd == nint.Zero) return false;

        try
        {
            if (frm.WindowState == WindowState.Maximized)
            {
                WindowInterop.ClearWindowRegion(_hwnd);
                LogShape("最大化：不裁剪");
                return true;
            }

            var dpi = VisualTreeHelper.GetDpi(frm);

            // 缩进量 = 界面本体的外边距（上游 10），圆角 = 上游的 6
            var insetDip = frm.PanBack.Margin.Left;
            var radiusPx = Math.Max(1, (int)Math.Round(radius * dpi.DpiScaleX));
            var insetPx = (int)Math.Round(insetDip * dpi.DpiScaleX);

            // 先关掉系统圆角，避免和我们自己的区域叠成两层边
            DwmInterop.TrySetWindowCornerPreference(_hwnd, DwmInterop.WindowCornerPreference.DoNotRound);

            if (WindowInterop.SetClientAreaRoundedRegion(_hwnd, radiusPx, insetPx))
            {
                _shapeRegionApplied = true;
                // 缩放热区必须落在区域内部：区域从 inset 处开始
                frm.ResizeOffsetWpf = (int)Math.Round(insetDip);

                // 读回区域的包围盒，确认系统真的接受了这个形状（实机排查靠这行）
                var boxText = WindowInterop.TryGetWindowRegionBox(_hwnd, out var rLeft, out var rTop,
                    out var rRight, out var rBottom)
                    ? $"区域包围盒 ({rLeft},{rTop})-({rRight},{rBottom})"
                    : "区域读回失败";
                LogShape($"窗口区域（内缩 {insetPx}px / 圆角 {radiusPx}px；{boxText}）");
                return true;
            }

            // 兜底：区域设置失败时至少让系统把窗口圆一下（Win11）
            frm.ResizeOffsetWpf = 6;
            if (DwmInterop.TrySetWindowCornerPreference(_hwnd, DwmInterop.WindowCornerPreference.Round))
                LogShape("DWM 圆角（窗口区域设置失败）");
            else
                LogWrapper.Warn("Material", "设置窗口圆角区域失败，窗口保持矩形（磨砂可能露出直角边）");
            return false;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn("Material", "调整窗口外形失败：" + ex.Message);
            return false;
        }
    }

    /// <summary>窗口外形路径变化时打一条日志（实机排查用）。</summary>
    private static void LogShape(string description)
    {
        if (_lastShapeLog == description) return;
        _lastShapeLog = description;
        ModBase.Log($"[Material] 窗口外形：{description}", ModBase.LogLevel.Debug);
    }

    /// <summary>从配置读取参数（配置里的百分比是整数，55 表示 0.55）。</summary>
    private static void ReloadOptions()
    {
        // 材质**类型**在进程内锁定：改类型要重启才生效。
        // 原因：Win10 的旧版 AccentBlur 一旦挂到窗口上，运行时撤销在部分环境里不生效
        // （这台机器装了 DWMBlurGlass 这类注入 DWM 的工具时尤其明显），表现就是「材质关了、磨砂还在」。
        // 模糊半径 / 不透明度这些参数仍然即时生效。
        _lockedKind ??= (MaterialKind)Math.Clamp(Config.System.MaterialKind, 0, 3);
        _kind = _lockedKind.Value;
        _options = new MaterialOptions
        {
            BlurRadius = Math.Clamp(Config.System.MaterialBlurRadius, 0, 40),
            TintStrength = Math.Clamp(Config.System.MaterialTintStrength, 0, 100) / 100d,
            TitleOpacity = Math.Clamp(Config.System.MaterialTitleOpacity, 30, 100) / 100d,
            MainOpacity = Math.Clamp(Config.System.MaterialMainOpacity, 30, 100) / 100d,
            SidebarOpacity = Math.Clamp(Config.System.MaterialSidebarOpacity, 30, 100) / 100d,
            CardOpacity = Math.Clamp(Config.System.MaterialCardOpacity, 30, 100) / 100d,
            GlassOpacity = Math.Clamp(Config.System.MaterialGlassOpacity, 10, 100) / 100d,
            GlassTitleOpacity = Math.Clamp(Config.System.MaterialGlassTitleOpacity, 0, 100) / 100d,
            GlassRefraction = Math.Clamp(Config.System.MaterialGlassRefraction, 0, 30),
            // Win10 只有一个可用实现：系统级 AccentBlur（DWM 绘制，四角会溢出，见 ApplyWindowShape 的说明）
            AllowLegacyAccentBlur = true,
            DividerOpacity = Math.Clamp(Config.System.MaterialDividerOpacity, 0, 100) / 100d,
            PillOpacity = Math.Clamp(Config.System.MaterialPillOpacity, 30, 100) / 100d,
            DialogOpacity = Math.Clamp(Config.System.MaterialDialogOpacity, 30, 100) / 100d,
            ToastOpacity = Math.Clamp(Config.System.MaterialToastOpacity, 30, 100) / 100d,
            GlowRadius = Math.Clamp(Config.System.MaterialGlowRadius, 0, 20),
            GlowOpacity = Math.Clamp(Config.System.MaterialGlowOpacity, 0, 100) / 100d
        };
    }

    /// <summary>把材质交给 DWM / 组合器，并按当前配色模式设置沉浸式深色标题栏。</summary>
    private static void ApplyBackdrop()
    {
        if (_hwnd == nint.Zero) return;

        var kind = Kind;
        MaterialService.Apply(_hwnd, kind, _options);
        DwmInterop.TrySetImmersiveDarkMode(_hwnd, ThemeService.IsDarkMode);
        // Windows 11：材质生效时关掉 DWM 画在窗口最外圈的那条 1px 描边。
        // 它沿**窗口矩形**画，和我们的圆角区域对不上，看起来就是「界面外面多一圈边」。
        DwmInterop.TrySetWindowBorder(_hwnd, MaterialService.Applied == MaterialKind.None);

        var appliedLog = $"{kind} → {MaterialService.Applied}（{MaterialService.AppliedVia ?? "纯色"}）" +
                         $"｜系统 build {Environment.OSVersion.Version.Build}" +
                         (MaterialService.ForcesSquareCorners ? "｜旧版模糊生效，圆角已强制为 0" : "") +
                         (MaterialService.FallbackReason is null ? "" : $"，降级：{MaterialService.FallbackReason}");
        if (_lastAppliedLog != appliedLog)
        {
            _lastAppliedLog = appliedLog;
            ModBase.Log($"[Material] 效果：{appliedLog}", ModBase.LogLevel.Debug);
        }
    }

    /// <summary>标题栏：降低背景画刷的不透明度，让材质透出来（文字与按钮不受影响）。</summary>
    private static void ApplyTitleOpacity(FormMain frm, double factor)
    {
        var panTitle = frm.PanTitle;
        var current = panTitle.BackgroundBrush;

        // 主题换了画刷（DynamicResource 重新求值）时以新的为基准，否则沿用自己的基准，避免不透明度叠加衰减
        var baseBrush = System.Windows.Application.Current?.TryFindResource("ColorBrush3") as SolidColorBrush
                        ?? (ReferenceEquals(current, _titleAppliedBrush) ? _titleBaseBrush : current);
        if (baseBrush is null) return;

        _titleBaseBrush = baseBrush;
        var brush = WithAlpha(baseBrush, factor);
        _titleAppliedBrush = brush;
        if (!ReferenceEquals(current, brush))
            panTitle.BackgroundBrush = brush;
    }

    /// <summary>主界面：降低 <c>PanForm</c> 背景画刷的不透明度（可能是纯色，也可能是渐变）。</summary>
    private static void ApplyMainOpacity(FormMain frm, double factor)
    {
        var panForm = frm.PanForm;
        var current = panForm.Background;

        if (current is null) return;
        // 主题刷新会重新写一次背景画刷，此时把它当作新的基准
        if (!ReferenceEquals(current, _mainAppliedBrush))
            _mainBaseBrush = current;

        var baseBrush = _mainBaseBrush ?? current;
        var brush = WithAlpha(baseBrush, factor);
        _mainAppliedBrush = brush;
        _mainBaseBrush = baseBrush;
        if (!ReferenceEquals(current, brush))
            panForm.Background = brush;
    }

    /// <summary>
    ///     侧边栏与卡片：两者都用主题的 <c>ColorBrushTransparentBackground</c>（本身带半透明 alpha），
    ///     因此这里的系数是**在主题原始浓度上再乘**，100% 等于保持现状。
    ///     <para>
    ///         卡片通过替换资源字典项生效（<see cref="MyCard" /> 用 <c>SetResourceReference</c> 跟着走，
    ///         WPF 的资源引用会自动失效重取），侧边栏则直接写 <c>RectLeftBackground.BackgroundBrush</c>，
    ///         这样两者可以各自独立调节。
    ///     </para>
    /// </summary>
    private static void ApplySurfaceOpacity(FormMain frm, double sidebarFactor, double cardFactor)
    {
        var resources = System.Windows.Application.Current?.Resources;
        if (resources is null) return;

        if (resources[SurfaceBrushKey] is SolidColorBrush current &&
            !ReferenceEquals(current, _cardAppliedBrush))
            _surfaceBaseBrush = current; // 主题刚换过画刷，把新的当作基准

        var baseBrush = _surfaceBaseBrush;
        if (baseBrush is null) return;

        // ① 卡片：写回资源字典，所有 MyCard 通过资源引用自动跟随
        var cardBrush = WithAlpha(baseBrush, cardFactor);
        _cardAppliedBrush = cardBrush;
        if (!ReferenceEquals(resources[SurfaceBrushKey], cardBrush))
            resources[SurfaceBrushKey] = cardBrush;

        // ② 侧边栏：直接写元素的画刷，与卡片互不影响
        if (frm.RectLeftBackground is null) return;
        var sidebarBrush = WithAlpha(baseBrush, sidebarFactor);
        if (!ReferenceEquals(frm.RectLeftBackground.BackgroundBrush, sidebarBrush))
            frm.RectLeftBackground.BackgroundBrush = sidebarBrush;
    }

    /// <summary>按系数缩放画刷的 alpha 通道（1 表示原样返回，避免无意义的克隆）。</summary>
    private static Brush WithAlpha(Brush brush, double factor)
    {
        if (factor >= 0.999d) return brush;

        switch (brush)
        {
            case SolidColorBrush solid:
            {
                var result = WithAlpha(solid, factor);
                return result;
            }
            case GradientBrush gradient:
                try
                {
                    var clone = gradient.Clone();
                    foreach (var stop in clone.GradientStops)
                        stop.Color = ScaleAlpha(stop.Color, factor);
                    clone.Freeze();
                    return clone;
                }
                catch
                {
                    return brush;
                }
            default:
                return brush;
        }
    }

    private static SolidColorBrush WithAlpha(SolidColorBrush brush, double factor)
    {
        if (factor >= 0.999d) return brush;
        var result = new SolidColorBrush(ScaleAlpha(brush.Color, factor));
        result.Freeze();
        return result;
    }

    private static Color ScaleAlpha(Color color, double factor)
    {
        var alpha = (byte)Math.Round(Math.Clamp(color.A * factor, 0d, 255d));
        return Color.FromArgb(alpha, color.R, color.G, color.B);
    }
}
