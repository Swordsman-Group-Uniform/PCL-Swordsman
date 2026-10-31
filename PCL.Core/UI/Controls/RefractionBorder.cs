using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace PCL.Core.UI.Controls;

/// <summary>
///     液态玻璃的「边缘折射」层。
///     <para>
///         真液态玻璃与普通毛玻璃（glassmorphism）的本质区别：玻璃的边像一块透镜，
///         会把背后的画面压缩 / 位移（Apple 在 iOS 26 里称 edge refraction）。
///         本控件的做法：把指定的「背景来源」（一般是主窗口的壁纸层）以中心为原点放大一点点，
///         只画在圆角矩形的**边缘环带**里 —— 视觉上背景在边缘被挤压，与透镜一致。
///     </para>
///     <para>
///         ⚠️ 关键约束：本控件**必须放在内容之下**（只压住壁纸 / 着色层）。
///         放在内容之上会把整个界面重新画一遍，表现为「边缘严重重叠」。
///         <see cref="Source" /> 指定采集对象（用壁纸层而不是 BackgroundPresenter 全量采集，
///         这样不会把标题栏、卡片再采一遍，也就不会互相叠加）。
///     </para>
/// </summary>
public class RefractionBorder : Border
{
    /// <summary>折射强度：边缘环带里背景被放大的像素量（0 表示关闭折射）。</summary>
    public static readonly DependencyProperty RefractionStrengthProperty = DependencyProperty.Register(
        nameof(RefractionStrength), typeof(double), typeof(RefractionBorder),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>折射环带的宽度。</summary>
    public static readonly DependencyProperty RingWidthProperty = DependencyProperty.Register(
        nameof(RingWidth), typeof(double), typeof(RefractionBorder),
        new FrameworkPropertyMetadata(10d, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>环带里背景的磨砂半径。</summary>
    public static readonly DependencyProperty BlurRadiusProperty = DependencyProperty.Register(
        nameof(BlurRadius), typeof(double), typeof(RefractionBorder),
        new FrameworkPropertyMetadata(16d, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>环带整体透明度（0~1）。</summary>
    public static readonly DependencyProperty RingOpacityProperty = DependencyProperty.Register(
        nameof(RingOpacity), typeof(double), typeof(RefractionBorder),
        new FrameworkPropertyMetadata(1d, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>采集来源（一般是壁纸层）。为空时退回 <see cref="BackgroundPresenter" /> 全量采集。</summary>
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source), typeof(Visual), typeof(RefractionBorder),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly DependencyProperty MaxDepthProperty =
        BackgroundPresenter.MaxDepthProperty.AddOwner(typeof(RefractionBorder));

    private readonly Stack<UIElement> _parentStack = new();

    public double RefractionStrength
    {
        get => (double)GetValue(RefractionStrengthProperty);
        set => SetValue(RefractionStrengthProperty, value);
    }

    public double RingWidth
    {
        get => (double)GetValue(RingWidthProperty);
        set => SetValue(RingWidthProperty, value);
    }

    public double BlurRadius
    {
        get => (double)GetValue(BlurRadiusProperty);
        set => SetValue(BlurRadiusProperty, value);
    }

    public double RingOpacity
    {
        get => (double)GetValue(RingOpacityProperty);
        set => SetValue(RingOpacityProperty, value);
    }

    public Visual? Source
    {
        get => (Visual?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>采集背面时的最大层数（仅在未指定 <see cref="Source" /> 时使用）。</summary>
    public int MaxDepth
    {
        get => (int)GetValue(MaxDepthProperty);
        set => SetValue(MaxDepthProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc); // 先画自身背景 / 边框（若有）

        var strength = RefractionStrength;
        var ring = RingWidth;
        var opacity = Math.Clamp(RingOpacity, 0d, 1d);
        if (strength <= 0.01d || ring <= 0.5d || opacity <= 0.001d) return;

        var width = RenderSize.Width;
        var height = RenderSize.Height;
        var corner = Math.Max(0d, CornerRadius.TopLeft);
        if (width <= ring * 2d + 2d || height <= ring * 2d + 2d) return;

        try
        {
            // 放大后面的画面：缩小 Viewbox 等于放大内容，且不引入变换，裁剪 / 遮罩都能保持精确
            var zoom = 1d / Math.Max(0.2d, 1d - strength / Math.Max(1d, Math.Min(width, height)));
            var source = BuildSourceBrush(zoom);
            if (source is null) return;

            // 整块裁剪成圆角矩形，四条边各画一条「外亮内透」的渐变带 —— 边缘处的画面被挤压，
            // 中间完全透明，视觉上就是一块透镜边（参考 demo 的 displacement 场，但只用画笔近似）
            var outer = new RectangleGeometry(new Rect(0d, 0d, width, height), corner, corner);
            dc.PushClip(outer);

            DrawEdge(dc, source, width, height, new Point(0d, 0d), new Point(0d, ring), opacity);          // 顶
            DrawEdge(dc, source, width, height, new Point(0d, height), new Point(0d, height - ring),
                opacity);                                                                                  // 底
            DrawEdge(dc, source, width, height, new Point(0d, 0d), new Point(ring, 0d), opacity);          // 左
            DrawEdge(dc, source, width, height, new Point(width, 0d), new Point(width - ring, 0d),
                opacity);                                                                                  // 右

            dc.Pop();
        }
        catch
        {
            // 布局中、元素不可见等情况静默跳过，下一帧再画
        }
    }

    /// <summary>
    ///     画一条边缘折射带：靠外的一侧不透明、向内侧渐变到 0。
    ///     <para>
    ///         遮罩用**绝对坐标**的线性渐变（相对模式是按被画矩形的包围盒算的，和带子对不上）；
    ///         画笔本身用「缩小 Viewbox」的方式放大画面，所以直接整块绘制也不会拉伸变形。
    ///     </para>
    /// </summary>
    private static void DrawEdge(DrawingContext dc, Brush source, double width, double height,
        Point gradientStart, Point gradientEnd, double opacity)
    {
        var mask = new LinearGradientBrush
        {
            StartPoint = gradientStart,
            EndPoint = gradientEnd,
            MappingMode = BrushMappingMode.Absolute
        };
        mask.GradientStops.Add(new GradientStop(Color.FromArgb(255, 255, 255, 255), 0d));
        mask.GradientStops.Add(new GradientStop(Color.FromArgb(170, 255, 255, 255), 0.45d));
        mask.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 1d));
        mask.Freeze();

        dc.PushOpacity(opacity);
        dc.PushOpacityMask(mask);
        dc.DrawRectangle(source, null, new Rect(0d, 0d, width, height));
        dc.Pop();
        dc.Pop();
    }

    /// <summary>以中心为原点缩小的 Viewbox —— 相对坐标下 0.5 ± 0.5/zoom。</summary>
    private static Rect ZoomViewbox(double zoom)
    {
        var size = 1d / Math.Max(0.2d, zoom);
        var origin = 0.5d - size / 2d;
        return new Rect(origin, origin, size, size);
    }

    /// <summary>构造「背景画面」画刷：优先用指定的 <see cref="Source" />，否则回退到全量采集。</summary>
    private Brush? BuildSourceBrush(double zoom = 1d)
    {
        var blur = new BlurEffect
        {
            Radius = Math.Max(0d, BlurRadius),
            RenderingBias = RenderingBias.Performance
        };

        if (Source is { } source)
        {
            var brush = new VisualBrush(source)
            {
                Stretch = Stretch.Fill,
                AlignmentX = AlignmentX.Left,
                AlignmentY = AlignmentY.Top,
                ViewboxUnits = BrushMappingMode.RelativeToBoundingBox,
                Viewbox = ZoomViewbox(zoom),
                ViewportUnits = BrushMappingMode.RelativeToBoundingBox,
                Viewport = new Rect(0d, 0d, 1d, 1d)
            };
            // VisualBrush 自身不能带 Effect，用一层包裹的 DrawingVisual 来做磨砂
            var visual = new DrawingVisual { Effect = blur };
            using (var context = visual.RenderOpen())
                context.DrawRectangle(brush, null, new Rect(0d, 0d, RenderSize.Width, RenderSize.Height));
            return new VisualBrush(visual)
            {
                Stretch = Stretch.Fill,
                ViewboxUnits = BrushMappingMode.RelativeToBoundingBox,
                Viewbox = ZoomViewbox(zoom),
                ViewportUnits = BrushMappingMode.RelativeToBoundingBox,
                Viewport = new Rect(0d, 0d, 1d, 1d)
            };
        }

        // 回退：用宿主既有机制采集背后画面（与 BlurBorder 相同）
        var captured = new DrawingVisual
        {
            Clip = new RectangleGeometry(new Rect(0d, 0d, RenderSize.Width, RenderSize.Height),
                Math.Max(0d, CornerRadius.TopLeft), Math.Max(0d, CornerRadius.TopLeft)),
            Effect = blur
        };
        using (var context = captured.RenderOpen())
            BackgroundPresenter.DrawBackground(context, this, _parentStack, MaxDepth, false);

        if (captured.Drawing is null) return null;

        return new VisualBrush(captured)
        {
            Stretch = Stretch.Fill,
            ViewboxUnits = BrushMappingMode.RelativeToBoundingBox,
            Viewbox = ZoomViewbox(zoom),
            ViewportUnits = BrushMappingMode.RelativeToBoundingBox,
            Viewport = new Rect(0d, 0d, 1d, 1d)
        };
    }
}
