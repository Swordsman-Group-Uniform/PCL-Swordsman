using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using PCL.Core.Logging;
using PCL.Core.UI.Controls;
using PCL.Core.Utils.OS.Window;

namespace PCL;

/// <summary>
///     液态玻璃：整个启动器是一块圆角大玻璃，侧边栏与主界面共用同一层玻璃（用一条玻璃竖线分割），
///     侧边栏每一项与主界面每一张卡片都是一块小玻璃。
///     <para>
///         分工：<see cref="MaterialService" /> 负责系统后台模糊（玻璃后面得有东西可透），
///         本模块负责宿主侧自绘的玻璃层 —— 圆角、竖线、小玻璃的着色与高光边。
///         四层玻璃的不透明度都能在「设置 → 个性化 → 高级材质」里分别调。
///     </para>
///     <para>
///         实现要点：大玻璃的着色由 `PanForm` 背景 + 一块 `Grid.Row=1` 的着色浮层组成
///         （有壁纸时壁纸不透明，必须用浮层给壁纸蒙一层玻璃；没壁纸时只用 PanForm 背景，避免叠加两次变浑）。
///     </para>
/// </summary>
internal static class ModLiquidGlass
{
    /// <summary>整体玻璃的圆角半径：跟随设置里的「玻璃圆角」（默认 16）。</summary>
    private static double GlassCornerRadius => ModMaterial.CornerRadius;

    /// <summary>小玻璃（卡片）的圆角半径。</summary>
    private const double CardCornerRadius = 11d;


    /// <summary>玻璃竖线的宽度。</summary>
    private const double DividerWidth = 1.5d;

    private const string CardRimName = "RectGlassCardRim";

    /// <summary>着色浮层（有壁纸时给壁纸蒙玻璃用）。</summary>
    private static Border? _tintOverlay;

    /// <summary>光泽浮层（玻璃亮面 / 顶部微光）。</summary>
    private static Border? _sheenOverlay;

    /// <summary>
    ///     边缘折射环带。**必须放在内容之下**（只压住壁纸与着色层）——
    ///     放在内容之上会把整个界面再画一遍，表现为边缘严重重叠。
    /// </summary>
    private static RefractionBorder? _refractionRing;

    /// <summary>整块玻璃的 1px hairline + 顶部 specular（只有描边，不吃内容）。</summary>
    private static Border? _hairline;

    private static bool _isApplied;

    // 原始值缓存（只可能有一份，用静态字段即可）
    private static Brush? _originalDividerFill;
    private static double _originalDividerWidth = double.NaN;
    private static Effect? _originalDividerEffect;
    private static Effect? _originalWallpaperEffect;
    private static Thickness _originalWallpaperMargin;
    private static bool _hasWallpaperOriginals;

    /// <summary>当前是否处于液态玻璃模式（且系统真的应用上了）。</summary>
    public static bool IsActive =>
        ModMaterial.IsAvailable &&
        ModMaterial.Kind == MaterialKind.LiquidGlass &&
        MaterialService.Applied == MaterialKind.LiquidGlass;

    /// <summary>
    ///     重新应用（或退出）液态玻璃。由 <see cref="ModMaterial" /> 在配置 / 主题 / 插件变化时调用。
    /// </summary>
    public static void Refresh()
    {
        try
        {
            if (!IsActive)
            {
                Clear();
                return;
            }

            Apply();
        }
        catch (Exception ex)
        {
            LogWrapper.Warn("Material", "应用液态玻璃失败：" + ex.Message);
        }
    }

    /// <summary>卡片创建时由 <see cref="MyCard" /> 调用。</summary>
    public static void OnCardLoaded(MyCard card)
    {
        if (!IsActive) return;
        StyleCard(card);
    }

    #region 应用

    private static void Apply()
    {
        var frm = ModMain.frmMain;
        if (frm?.PanForm is null) return;

        _isApplied = true;

        ApplyBigGlass(frm);
        ApplyDivider(frm);
        ApplyWallpaperFrost(frm);
        ApplyCards(frm);
        ApplyLoginPills(frm);
    }

    /// <summary>整块玻璃：圆角 + 着色浮层 + 光泽浮层 + 边框高光（顶部 specular）。</summary>
    private static void ApplyBigGlass(FormMain frm)
    {
        // ① 直角 → 圆角大玻璃
        try
        {
            frm.RectForm.RadiusX = GlassCornerRadius;
            frm.RectForm.RadiusY = GlassCornerRadius;
        }
        catch
        {
            // 忽略几何体已冻结等异常
        }

        var glassAlpha = Math.Clamp(ModMaterial.GlassOpacity, 0.05d, 1d);
        var themeColor = ThemeBackgroundColor();

        // ② 内容区着色浮层：壁纸不透明，必须在上方再蒙一层玻璃；没壁纸时 PanForm 背景本身就是玻璃
        var hasWallpaper = frm.ImgBack is not null && frm.ImgBack.Visibility == Visibility.Visible;
        if (hasWallpaper)
        {
            if (_tintOverlay is null)
            {
                _tintOverlay = new Border
                {
                    IsHitTestVisible = false,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };
                Grid.SetRow(_tintOverlay, 1); // 只蒙内容区，标题栏那一行靠 PanForm 背景当玻璃
            }

            _tintOverlay.Background = new SolidColorBrush(WithAlpha(themeColor, glassAlpha));
            if (!frm.PanForm.Children.Contains(_tintOverlay))
                frm.PanForm.Children.Insert(Math.Min(2, frm.PanForm.Children.Count), _tintOverlay);
        }
        else if (_tintOverlay is not null && frm.PanForm.Children.Contains(_tintOverlay))
        {
            frm.PanForm.Children.Remove(_tintOverlay);
        }

        // ③ 光泽浮层（玻璃的「亮面」）：左上一抹白，模拟光照在玻璃上；盖在着色层之上、内容之下
        if (_sheenOverlay is null)
        {
            _sheenOverlay = new Border
            {
                IsHitTestVisible = false,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            Grid.SetRow(_sheenOverlay, 1);
        }

        _sheenOverlay.Background = MakeSheenBrush(glassAlpha);
        if (!frm.PanForm.Children.Contains(_sheenOverlay))
        {
            var tintIndex = _tintOverlay is not null && frm.PanForm.Children.Contains(_tintOverlay)
                ? frm.PanForm.Children.IndexOf(_tintOverlay) + 1
                : Math.Min(2, frm.PanForm.Children.Count);
            frm.PanForm.Children.Insert(tintIndex, _sheenOverlay);
        }

        // ④ 边缘折射（真液态玻璃与普通毛玻璃的本质区别）：只取壁纸层做放大重采样，
        //    并且放在内容之下 —— 否则会把标题栏 / 卡片再画一遍，看起来就是「边缘严重重叠」
        var refraction = Math.Clamp(ModMaterial.GlassRefraction, 0d, 30d);
        if (refraction <= 0.01d)
        {
            // 不折射时连元素都不留，避免任何残留覆盖
            if (_refractionRing is not null && frm.PanForm.Children.Contains(_refractionRing))
                frm.PanForm.Children.Remove(_refractionRing);
        }
        else
        {
            if (_refractionRing is null)
            {
                _refractionRing = new RefractionBorder
                {
                    IsHitTestVisible = false,
                    Background = null,
                    BorderThickness = new Thickness(0d)
                };
                Grid.SetRow(_refractionRing, 1); // 只覆盖内容区：标题栏那一行没有壁纸可折射
            }

            _refractionRing.Source = frm.ImgBack;
            _refractionRing.CornerRadius = new CornerRadius(GlassCornerRadius);
            _refractionRing.RefractionStrength = refraction;
            _refractionRing.RingWidth = 8d + refraction * 0.6d;
            _refractionRing.BlurRadius = Math.Clamp(ModMaterial.Options.BlurRadius, 4d, 40d);
            _refractionRing.RingOpacity = 1d;
            if (!frm.PanForm.Children.Contains(_refractionRing))
                frm.PanForm.Children.Insert(Math.Min(1, frm.PanForm.Children.Count), _refractionRing);
        }

        // ⑤ 1px 白色 hairline + 顶部 specular（Apple 配方的 inset shadow）：只有描边，盖在最上层也不吃内容
        _hairline ??= new Border { IsHitTestVisible = false, Background = null };
        _hairline.CornerRadius = new CornerRadius(GlassCornerRadius);
        _hairline.BorderThickness = new Thickness(1.2d);
        _hairline.BorderBrush = MakeHairlineBrush(glassAlpha);
        Grid.SetRowSpan(_hairline, 99);
        Panel.SetZIndex(_hairline, 9);
        if (!frm.PanForm.Children.Contains(_hairline))
            frm.PanForm.Children.Add(_hairline);

    }

    /// <summary>侧边栏与主界面之间的玻璃竖线（复用窗口原本的那条 4px 阴影）。</summary>
    private static void ApplyDivider(FormMain frm)
    {
        var divider = frm.RectLeftShadow;
        if (divider is null) return;

        _originalDividerFill ??= divider.Fill;
        if (double.IsNaN(_originalDividerWidth)) _originalDividerWidth = divider.Width;
        _originalDividerEffect ??= divider.Effect;

        var alpha = Math.Clamp(ModMaterial.DividerOpacity, 0d, 1d);
        divider.Width = DividerWidth;
        divider.Fill = MakeDividerBrush(alpha);
        divider.Effect = alpha <= 0.01d
            ? null
            : new DropShadowEffect
            {
                Color = Colors.White,
                ShadowDepth = 0d,
                Direction = 0d,
                BlurRadius = 7d,
                Opacity = alpha * 0.55d,
                RenderingBias = RenderingBias.Performance
            };
    }

    /// <summary>
    ///     壁纸磨砂：玻璃后面必须是被模糊过的画面，否则看着像「贴了一张半透明色纸」。
    ///     具体实现抽在 <see cref="ModWallpaperFrost" />（云母 / Aero 在 Windows 10 上也要用同一套）。
    /// </summary>
    private static void ApplyWallpaperFrost(FormMain frm)
    {
        _ = frm;
        ModWallpaperFrost.Apply(Math.Clamp(ModMaterial.Options.BlurRadius * 1.4d, 12d, 60d));
    }

    /// <summary>
    ///     登录方式的三个药丸（正版 / 离线 / 外置）也做成小玻璃。
    ///     <para>
    ///         <see cref="MyRadioButton" /> 本身就是 <see cref="Border" />（圆角 13.5 的药丸），
    ///         所以直接换底色为玻璃着色 + 1px 高光边；原值记在弱表里，退出玻璃模式时还原。
    ///         注意：这些控件的 <c>Tag</c> 被用作选项卡索引，**不能**拿来存原始样式。
    ///     </para>
    /// </summary>
    private static void ApplyLoginPills(FormMain frm)
    {
        var tabs = ModMain.frmLaunchLeft?.PanLoginTabs;
        if (tabs is null) return;

        var alpha = Math.Clamp(ModMaterial.PillOpacity, 0.05d, 1d);
        foreach (var child in tabs.Children)
            if (child is MyRadioButton pill)
                StylePill(pill, alpha);
    }

    /// <summary>药丸玻璃样式：着色 + 高光边。</summary>
    private static void StylePill(MyRadioButton pill, double alpha)
    {
        _pillOriginals.AddOrUpdate(pill, new PillOriginal(pill.Background, pill.BorderBrush,
            pill.BorderThickness));

        pill.Background = new SolidColorBrush(WithAlpha(ThemeBackgroundColor(), alpha));
        pill.BorderThickness = new Thickness(1d);
        pill.BorderBrush = MakeRimBrush(0.55d * alpha);
    }

    /// <summary>还原药丸样式（退出玻璃模式）。</summary>
    private static void ClearPills(FormMain frm)
    {
        var tabs = ModMain.frmLaunchLeft?.PanLoginTabs;
        if (tabs is null) return;

        foreach (var child in tabs.Children)
        {
            if (child is not MyRadioButton pill) continue;
            if (!_pillOriginals.TryGetValue(pill, out var original)) continue;

            pill.Background = original.Background;
            pill.BorderBrush = original.BorderBrush;
            pill.BorderThickness = original.BorderThickness;
            _pillOriginals.Remove(pill);
        }
    }

    /// <summary>药丸的原始外观（弱引用保存，避免持有控件）。</summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<MyRadioButton, PillOriginal>
        _pillOriginals = new();

    private sealed record PillOriginal(Brush? Background, Brush? BorderBrush, Thickness BorderThickness);

    /// <summary>主界面每一张卡片都变成一块小玻璃（圆角更大 + 一圈高光边）。</summary>
    private static void ApplyCards(FormMain frm)
    {
        foreach (var card in FindVisualChildren<MyCard>(frm))
            StyleCard(card);
    }

    private static void StyleCard(MyCard card)
    {
        card.CornerRadius = new CornerRadius(CardCornerRadius);
        card.BackgroundBlurRadius = Math.Clamp(ModMaterial.Options.BlurRadius * 1.2d, 12d, 40d);

        // 卡片自身的 BlurBorder 在开了模糊时只画背景、不画边框，所以高光边单独放一个浮层
        var rim = FindOverlay(card, CardRimName);
        if (rim is null)
        {
            rim = new Border
            {
                Name = CardRimName,
                IsHitTestVisible = false,
                Background = null,
                BorderThickness = new Thickness(1d),
                CornerRadius = new CornerRadius(CardCornerRadius)
            };
            card.Children.Add(rim);
        }

        rim.BorderBrush = MakeRimBrush(0.42d * Math.Clamp(ModMaterial.GlassOpacity, 0.05d, 1d));
    }

    /// <summary>找出之前插进去的玻璃浮层（按名字，避免重复插入）。</summary>
    private static Border? FindOverlay(Panel panel, string name) =>
        panel.Children.OfType<Border>().FirstOrDefault(child => child.Name == name);

    private static void ClearCard(MyCard card)
    {
        card.CornerRadius = new CornerRadius(5d); // MyCard 的默认圆角
        card.BackgroundBlurRadius = 16d;          // BlurBorder 的默认磨砂半径
        var rim = FindOverlay(card, CardRimName);
        if (rim is not null) card.Children.Remove(rim);
    }

    #endregion

    #region 还原

    private static void Clear()
    {
        if (!_isApplied) return;
        _isApplied = false;

        try
        {
            var frm = ModMain.frmMain;
            if (frm is null) return;

            // 圆角恢复
            try
            {
                var radius = ModMaterial.CornerRadius;
                frm.RectForm.RadiusX = radius;
                frm.RectForm.RadiusY = radius;
            }
            catch
            {
                // 忽略
            }

            // 着色 / 光泽 / 折射 / hairline 移除
            if (_tintOverlay is not null) frm.PanForm.Children.Remove(_tintOverlay);
            if (_sheenOverlay is not null) frm.PanForm.Children.Remove(_sheenOverlay);
            if (_refractionRing is not null) frm.PanForm.Children.Remove(_refractionRing);
            if (_hairline is not null) frm.PanForm.Children.Remove(_hairline);


            // 竖线恢复
            var divider = frm.RectLeftShadow;
            if (divider is not null)
            {
                divider.Fill = _originalDividerFill;
                if (!double.IsNaN(_originalDividerWidth)) divider.Width = _originalDividerWidth;
                divider.Effect = _originalDividerEffect;
            }

            // 壁纸磨砂恢复（共享工具，只还原一次；云母 / Aero 在 Win10 上也可能正在用它）
            ModWallpaperFrost.Clear();

            // 小玻璃恢复
            foreach (var card in FindVisualChildren<MyCard>(frm))
                ClearCard(card);
            ClearPills(frm);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn("Material", "退出液态玻璃时还原失败：" + ex.Message);
        }
    }

    #endregion

    #region 工具

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var descendant in FindVisualChildren<T>(child)) yield return descendant;
        }
    }

    /// <summary>
    ///     玻璃边框：顶边 specular 高光 + 底部暗边。
    ///     <para>
    ///         参考 `liquid-glass-main` 示例（D3D11 像素着色器版）的两个关键光效：
    ///         `TOP_LIGHT_STRENGTH`（顶部亮边）与 `LOWER_SHADOW_STRENGTH`（底部压暗），
    ///         这里用一条竖向渐变的描边烘出来，顶白、底黑。
    ///     </para>
    /// </summary>
    private static Brush MakeHairlineBrush(double glassAlpha)
    {
        var white = Math.Clamp(0.55d * glassAlpha, 0d, 1d);
        var dark = Math.Clamp(0.5d * glassAlpha, 0d, 1d);
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0d, 0d),
            EndPoint = new Point(0d, 1d)
        };
        brush.GradientStops.Add(Stop(white, 0d, false));          // 顶边 specular
        brush.GradientStops.Add(Stop(white * 0.45d, 0.06d, false));
        brush.GradientStops.Add(Stop(white * 0.22d, 0.22d, false));
        brush.GradientStops.Add(Stop(white * 0.16d, 0.72d, false));
        brush.GradientStops.Add(Stop(dark * 0.55d, 0.9d, true));   // 底部暗边起
        brush.GradientStops.Add(Stop(dark, 1d, true));             // 底边压暗到底
        brush.Freeze();
        return brush;

        static GradientStop Stop(double a, double offset, bool isDark) =>
            new(isDark
                    ? Color.FromArgb((byte)Math.Round(Math.Clamp(a, 0d, 1d) * 255d), 0, 0, 0)
                    : Color.FromArgb((byte)Math.Round(Math.Clamp(a, 0d, 1d) * 255d), 255, 255, 255),
                offset);
    }

    /// <summary>小玻璃（卡片 / 侧边栏项）的高光边：配方同上，但更淡。</summary>
    private static Brush MakeRimBrush(double alpha)
    {
        alpha = Math.Clamp(alpha, 0d, 1d);
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0d, 0d),
            EndPoint = new Point(0d, 1d)
        };
        brush.GradientStops.Add(
            new GradientStop(Color.FromArgb((byte)Math.Round(alpha * 255d), 255, 255, 255), 0d));
        brush.GradientStops.Add(
            new GradientStop(Color.FromArgb((byte)Math.Round(alpha * 0.3d * 255d), 255, 255, 255), 0.12d));
        brush.GradientStops.Add(
            new GradientStop(Color.FromArgb((byte)Math.Round(alpha * 0.22d * 255d), 255, 255, 255), 0.75d));
        brush.GradientStops.Add(
            new GradientStop(Color.FromArgb((byte)Math.Round(alpha * 0.45d * 255d), 255, 255, 255), 1d));
        brush.Freeze();
        return brush;
    }

    /// <summary>玻璃亮面：左上角一抹白，向右下淡出（相当于 Web 里的 brightness + 高光渐变）。</summary>
    private static Brush MakeSheenBrush(double glassAlpha)
    {
        var alpha = Math.Clamp(0.16d * glassAlpha, 0d, 1d);
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0d, 0d),
            EndPoint = new Point(0.75d, 1d)
        };
        brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)Math.Round(alpha * 255d), 255, 255, 255), 0d));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)Math.Round(alpha * 0.35d * 255d), 255, 255, 255), 0.35d));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 1d));
        brush.Freeze();
        return brush;
    }

    /// <summary>玻璃竖线：两端淡、中断亮，像玻璃的折射高光。</summary>
    private static Brush MakeDividerBrush(double alpha)
    {
        alpha = Math.Clamp(alpha, 0d, 1d);
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0d, 0d),
            EndPoint = new Point(0d, 1d)
        };
        brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)Math.Round(alpha * 0.15d * 255d), 255, 255, 255), 0d));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)Math.Round(alpha * 255d), 255, 255, 255), 0.5d));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)Math.Round(alpha * 0.15d * 255d), 255, 255, 255), 1d));
        brush.Freeze();
        return brush;
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

    private static Color WithAlpha(Color color, double alpha)
    {
        var a = (byte)Math.Round(Math.Clamp(alpha, 0d, 1d) * 255d);
        return Color.FromArgb(a, color.R, color.G, color.B);
    }

    #endregion
}
