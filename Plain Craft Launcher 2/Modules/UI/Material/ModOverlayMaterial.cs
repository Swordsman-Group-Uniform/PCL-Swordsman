using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCL.Core.Utils.OS.Window;

namespace PCL;

/// <summary>
///     让「大弹窗（对话框）」与「右下角小弹窗（Toast）」也使用当前的高级材质表面。
///     <para>
///         这两类控件都渲染在主窗口内部（`PanMsg` / `PanHint`），背后就是主窗口的材质背景，
///         所以只要把它们的底色改成半透明、并按材质风格描一圈高光边，就能和主界面的卡片 / 玻璃保持一致。
///     </para>
///     <list type="bullet">
///         <item>材质未生效：恢复成主题原色（完全不透明）。</item>
///         <item>云母 / Aero：底色 alpha = 主界面不透明度（默认 92%），透出一点磨砂背景。</item>
///         <item>液态玻璃：底色 alpha = 大玻璃不透明度 + 25%（保证可读），并加顶亮底暗的玻璃描边。</item>
///     </list>
/// </summary>
internal static class ModOverlayMaterial
{
    /// <summary>弹窗卡片在 XAML 里的名字（`MyMsg*` 都是 `PanBorder`）。</summary>
    private const string DialogCardName = "PanBorder";

    /// <summary>「实例独立设置」药丸的玻璃覆盖层名字。</summary>
    private const string SwitchPillOverlayName = "RectMaterialSwitchPill";

    /// <summary>材质是否真的生效（与 <see cref="ModMaterial" /> 的判断保持一致）。</summary>
    private static bool MaterialOn =>
        ModMaterial.IsAvailable && MaterialService.Applied != MaterialKind.None;

    /// <summary>对话框表面透明度：由「对话框不透明度」配置决定（云母 / Aero / 液态玻璃通用）。</summary>
    private static double DialogAlpha => !MaterialOn ? 1d : Math.Clamp(ModMaterial.DialogOpacity, 0.3d, 1d);

    /// <summary>Toast 表面透明度：由「提示弹窗不透明度」配置决定（云母 / Aero / 液态玻璃通用）。</summary>
    private static double ToastAlpha => !MaterialOn ? 1d : Math.Clamp(ModMaterial.ToastOpacity, 0.3d, 1d);

    /// <summary>
    ///     重新应用（或还原）所有弹窗与 Toast 的表面样式。
    ///     由 <see cref="ModMaterial" /> 在配置 / 主题 / 插件变化时调用。
    /// </summary>
    public static void Refresh()
    {
        try
        {
            var frm = ModMain.frmMain;
            if (frm?.PanMsg is null || frm.PanHint is null) return;

            foreach (var child in frm.PanMsg.Children)
                if (child is FrameworkElement dialog)
                    ApplyDialog(dialog);

            foreach (var toast in frm.PanHint.Children.OfType<MyToast>())
            {
                if (MaterialOn)
                {
                    ApplyToastSurface(toast);
                }
                else
                {
                    // 关闭材质：收起模糊层，去掉描边，底色交还给主题配色
                    ClearToastSurface(toast);
                    toast.RestoreThemeColors();
                }
            }

            // 设置 → 游戏 → 启动 页面最底部的「实例独立设置」药丸
            ApplySwitchPill();
        }
        catch (Exception ex)
        {
            PCL.Core.Logging.LogWrapper.Warn("Material", "应用弹窗材质失败：" + ex.Message);
        }
    }

    /// <summary>新弹窗加入 `PanMsg` 后调用。</summary>
    public static void OnDialogShown(FrameworkElement dialog)
    {
        try
        {
            ApplyDialog(dialog);
        }
        catch (Exception ex)
        {
            PCL.Core.Logging.LogWrapper.Warn("Material", "应用弹窗材质失败：" + ex.Message);
        }
    }

    /// <summary>新 Toast 加入 `PanHint` 后调用。</summary>
    public static void OnToastShown(MyToast toast) => ApplyToastSurface(toast);

    /// <summary>
    ///     给 Toast 贴材质表面。由 <see cref="MyToast" /> 在每次刷新配色后调用，
    ///     否则 <c>UpdateColors()</c> 会把底色重置回主题色（这就是「Toast 没应用液态玻璃」的原因）。
    ///     <para>
    ///         真正的「糊」靠 XAML 里的 <c>RectToastBlur</c>（<see cref="PCL.Core.UI.Controls.BlurBorder" />）：
    ///         它把弹窗**背后**的内容（壁纸 / 主界面卡片）画进来再模糊。因此这里必须：
    ///         <list type="number">
    ///             <item>把模糊层打开，并只给它一条**半透明**底色 —— 底色一不透明就把它自己糊没了；</item>
    ///             <item>把 Toast 自身底色清成透明，否则主题底色（不透明）会盖在模糊层上面。</item>
    ///         </list>
    ///         只设一条半透明底色是**糊不出东西**的，这就是之前云母 / Aero / 液态玻璃下都没有模糊的原因。
    ///     </para>
    /// </summary>
    internal static void ApplyToastSurface(MyToast toast)
    {
        try
        {
            if (!MaterialOn)
            {
                ClearToastSurface(toast);
                return;
            }

            var blur = toast.BackdropBlur;
            if (blur is null) return;

            blur.Visibility = Visibility.Visible;
            blur.CornerRadius = toast.CornerRadius;
            blur.BlurRadius = ToastBlurRadius;
            blur.BlurSamplingRate = 0.7d;
            blur.BorderThickness = new Thickness(1d);
            blur.BorderBrush = HairlineBrush(ToastAlpha);
            blur.Background = ToastSurfaceBrush(ToastAlpha);

            // 底色让给模糊层，否则不透明的主题底色会把模糊整个盖住
            toast.Background = Brushes.Transparent;
            toast.BorderBrush = null;
            toast.BorderThickness = new Thickness(0d);

            // 留给日志的证据：出问题时按住这行去对「有没有真的开模糊」
            ModBase.Log(
                $"[Material] 小弹窗毛玻璃：半径 {ToastBlurRadius:F1} px，底色 alpha {ToastAlpha:F2}" +
                $"（{(ModMaterial.IsLiquidGlass ? "液态玻璃" : "云母/Aero")}）",
                ModBase.LogLevel.Debug);
        }
        catch (Exception ex)
        {
            PCL.Core.Logging.LogWrapper.Warn("Material", "应用 Toast 材质失败：" + ex.Message);
        }
    }

    /// <summary>材质关闭时把 Toast 的模糊层收起来，底色交还给主题配色。</summary>
    private static void ClearToastSurface(MyToast toast)
    {
        var blur = toast.BackdropBlur;
        if (blur is not null)
        {
            blur.Visibility = Visibility.Collapsed;
            blur.Background = null;
            blur.BorderBrush = null;
            blur.BorderThickness = new Thickness(0d);
        }

        toast.BorderThickness = new Thickness(0d);
        toast.BorderBrush = null;
    }

    /// <summary>
    ///     Toast 的模糊半径：液态玻璃比云母 / Aero 再糊一点；即使用户把材质模糊调到 0
    ///     （云母 / Aero 下那是「不要窗口模糊」的意思），小弹窗也保留一层基础毛玻璃。
    /// </summary>
    private static double ToastBlurRadius
    {
        get
        {
            var radius = ModMaterial.Options.BlurRadius;
            if (radius <= 0d) radius = 14d;
            return Math.Clamp(ModMaterial.IsLiquidGlass ? radius * 1.2d : radius, 10d, 40d);
        }
    }

    /// <summary>
    ///     Toast 表面底色：上淡下浓的竖向渐变。
    ///     用一块均匀的半透明色会把模糊层整体压平（看得出「换了颜色」但看不出「糊」），
    ///     渐变则能让上沿一直露出背后的模糊，读起来才是玻璃 / 毛玻璃。
    /// </summary>
    private static Brush ToastSurfaceBrush(double alpha)
    {
        var color = ThemeBackgroundColor();
        var glass = ModMaterial.IsLiquidGlass;
        var top = Math.Clamp(alpha * (glass ? 0.55d : 0.70d), 0d, 1d);
        var bottom = Math.Clamp(alpha, 0d, 1d);
        var brush = new LinearGradientBrush { StartPoint = new Point(0d, 0d), EndPoint = new Point(0d, 1d) };
        brush.GradientStops.Add(new GradientStop(Alpha(top, color), 0d));
        brush.GradientStops.Add(new GradientStop(Alpha(bottom, color), 1d));
        brush.Freeze();
        return brush;

        static Color Alpha(double a, Color c) =>
            Color.FromArgb((byte)Math.Round(Math.Clamp(a, 0d, 1d) * 255d), c.R, c.G, c.B);
    }

    #region 具体应用

    private static void ApplyDialog(FrameworkElement dialog)
    {
        if (dialog.FindName(DialogCardName) is not Border card) return;

        if (!MaterialOn)
        {
            // 还原成 XAML 里的主题资源引用
            card.SetResourceReference(Border.BackgroundProperty, "ColorBrushBackground");
            card.BorderThickness = new Thickness(0d);
            card.BorderBrush = null;
            return;
        }

        card.Background = TintedBackground(DialogAlpha);
        card.BorderThickness = new Thickness(1d);
        card.BorderBrush = HairlineBrush(DialogAlpha);
    }

    /// <summary>
    ///     「实例独立设置」药丸（`MyExtraTextButton`）也跟随材质：
    ///     在强调色药丸**之上、文字之下**插一层玻璃覆盖层，这样控件自身的悬停配色动画照常工作，
    ///     而药丸整体始终是玻璃 / 毛玻璃的观感。
    /// </summary>
    private static void ApplySwitchPill()
    {
        var button = ModMain.frmSetupLaunch?.BtnSwitch;
        if (button?.PanScale is null || button.PanColor is null) return;

        var host = button.PanScale;
        var overlay = host.Children.OfType<Border>()
            .FirstOrDefault(child => child.Name == SwitchPillOverlayName);

        if (!MaterialOn)
        {
            if (overlay is not null) host.Children.Remove(overlay);
            return;
        }

        if (overlay is null)
        {
            overlay = new Border
            {
                Name = SwitchPillOverlayName,
                IsHitTestVisible = false,
                BorderThickness = new Thickness(1d)
            };
            // 圆角跟强调色药丸保持一致（它本身是按高度算的 Binding）
            overlay.SetBinding(Border.CornerRadiusProperty,
                new System.Windows.Data.Binding(nameof(Border.CornerRadius)) { Source = button.PanColor });
            // 插在强调色药丸之后、图标文字之前
            host.Children.Insert(Math.Min(1, host.Children.Count), overlay);
        }

        overlay.Background = TintedBackground(PillSurfaceAlpha);
        overlay.BorderBrush = HairlineBrush(PillSurfaceAlpha);
    }

    /// <summary>药丸的表面透明度：比弹窗更透一点，才看得出「毛玻璃 / 玻璃」。</summary>
    private static double PillSurfaceAlpha =>
        !MaterialOn
            ? 1d
            : ModMaterial.IsLiquidGlass
                ? Math.Clamp(ModMaterial.GlassOpacity + 0.15d, 0.3d, 0.9d)
                : 0.78d;

    #endregion

    #region 画笔工具

    /// <summary>主题背景色 + 指定 alpha。</summary>
    private static Brush TintedBackground(double alpha)
    {
        var color = ThemeBackgroundColor();
        var brush = new SolidColorBrush(Color.FromArgb(
            (byte)Math.Round(Math.Clamp(alpha, 0d, 1d) * 255d), color.R, color.G, color.B));
        brush.Freeze();
        return brush;
    }

    /// <summary>
    ///     表面描边：顶部一圈亮边（玻璃的 specular），底部压暗；普通材质下用统一淡白边。
    /// </summary>
    private static Brush HairlineBrush(double alpha)
    {
        var glass = ModMaterial.IsLiquidGlass;
        var light = Math.Clamp(0.5d * alpha, 0d, 1d);
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0d, 0d),
            EndPoint = new Point(0d, 1d)
        };
        brush.GradientStops.Add(Stop(light, 0d, false));
        brush.GradientStops.Add(Stop(light * 0.35d, glass ? 0.12d : 0.35d, false));
        brush.GradientStops.Add(Stop(light * 0.3d, glass ? 0.7d : 0.7d, false));
        if (glass)
        {
            brush.GradientStops.Add(Stop(light * 0.55d, 0.9d, true));
            brush.GradientStops.Add(Stop(light * 1.0d, 1d, true));
        }

        brush.Freeze();
        return brush;

        static GradientStop Stop(double a, double offset, bool isDark) =>
            new(isDark
                    ? Color.FromArgb((byte)Math.Round(Math.Clamp(a, 0d, 1d) * 255d), 0, 0, 0)
                    : Color.FromArgb((byte)Math.Round(Math.Clamp(a, 0d, 1d) * 255d), 255, 255, 255),
                offset);
    }

    private static Color ThemeBackgroundColor()
    {
        try
        {
            if (System.Windows.Application.Current?.TryFindResource("ColorBrushBackground") is SolidColorBrush brush)
                return brush.Color;
        }
        catch
        {
            // 忽略
        }

        return Color.FromRgb(0x20, 0x20, 0x20);
    }

    #endregion
}
