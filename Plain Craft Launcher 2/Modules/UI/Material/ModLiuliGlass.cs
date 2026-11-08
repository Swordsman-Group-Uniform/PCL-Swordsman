using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using PCL.Core.UI.Theme;

namespace PCL;

/// <summary>
///     「琉璃彩」主题的玻璃层。
///     <para>
///         用户需求（snapshot-86）：
///         <list type="bullet">
///             <item>整个启动器透明化，四层不透明度锁定 30%（见 <see cref="ModMaterial.ApplyWindowOpacity" />）。</item>
///             <item>标题栏 / 侧边栏 / 主界面 = **三块大玻璃**，每块边缘约 8px 用原版玻璃边缘纹理
///                 （<c>Images/Blocks/GlassPaneTop.png</c>），不透明度 60%。</item>
///             <item>所有界面要有玻璃纹理内部的小粒子：粒子较小，不透明度 40%。</item>
///             <item>纹理与标题栏颜色跟随主题色，主题色是**动态彩色渐变**，随时间缓慢变化，
///                 每一帧都是一条渐变（不是单色闪烁）。</item>
///         </list>
///     </para>
///     <para>
///         实现要点（性能）：**不**每帧重建资源字典、**不**走 <c>ThemeService</c> 的全量刷新链路，
///         每帧只改极少数几个画刷的颜色 —— 三块玻璃的着色渐变各 3 个色标 + 粒子的 4 个纯色画刷，
///         合计 13 次颜色写入。定时器 45 fps，色相沿 OKLCH 匀速旋转，转一整圈 96 秒（用户要「缓慢」）。
///     </para>
///     <para>
///         贴图由 <see cref="ModTitleTextures" /> 加载：文件缺失时用同色棋盘兜底，不会让窗口起不来。
///     </para>
/// </summary>
internal static class ModLiuliGlass
{
    #region 参数

    /// <summary>玻璃边缘厚度（用户要求「约 8px」）。</summary>
    private const double EdgeThickness = 8d;

    /// <summary>边缘层不透明度（用户要求 60%）。</summary>
    private const double EdgeOpacity = 0.60d;

    /// <summary>粒子不透明度上限（用户要求 40%）。</summary>
    private const double ParticleOpacity = 0.40d;

    /// <summary>玻璃内部纹理（原版 Glass.png 平铺）的不透明度：只做底纹，不能盖住内容。</summary>
    private const double TextureOpacity = 0.22d;

    /// <summary>跟随主题色的着色渐变层的不透明度。</summary>
    private const double TintOpacity = 0.55d;

    /// <summary>色相转一整圈所需的时间（秒）。用户要「随时间缓慢变化」。</summary>
    private const double HueCycleSeconds = 96d;

    /// <summary>三块玻璃之间的色相错开量（度），让三块玻璃看起来是同一束光的不同折射。</summary>
    private const double PaneHueStep = 26d;

    /// <summary>粒子数（小颗粒，密度低才不会显得脏）。</summary>
    private const int ParticleCount = 46;

    /// <summary>粒子尺寸范围（px）。「粒子较小」。</summary>
    private const double ParticleSizeMin = 2d;
    private const double ParticleSizeMax = 5d;

    /// <summary>粒子漂移速度范围（px/s）与闪烁频率。</summary>
    private const double DriftSpeedMin = 2d;
    private const double DriftSpeedMax = 9d;

    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(22);

    #endregion

    #region 状态

    /// <summary>一块玻璃：整体层 + 内部纹理 + 着色渐变 + 8px 边缘。</summary>
    private sealed class GlassPane
    {
        public required Grid Layer { get; init; }
        public required LinearGradientBrush Tint { get; init; }
    }

    private static readonly List<GlassPane> Panes = [];

    private static Canvas? _particleHost;
    private static readonly List<Particle> Particles = [];
    private static readonly SolidColorBrush[] ParticleBrushes = new SolidColorBrush[4];
    private static Brush? _interiorBrush;
    private static Brush? _edgeBrush;

    private static DispatcherTimer? _timer;
    private static Stopwatch? _clock;
    private static double _lastSeconds;
    private static bool _isAttached;

    /// <summary>
    ///     装配时画布还没测量过（ActualWidth/Height 都是 0），粒子会全挤在左上角；
    ///     第一次拿到有效尺寸时把它们重新撒开。
    /// </summary>
    private static bool _needsSpread;

    /// <summary>当前是否已经装上了玻璃层。</summary>
    public static bool IsEnabled { get; private set; }

    /// <summary>一颗玻璃微粒。</summary>
    private sealed class Particle
    {
        public required Rectangle Element { get; init; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Phase { get; set; }
        public double DriftX { get; set; }
        public double DriftY { get; set; }
        public double ShimmerSpeed { get; set; }
        public double BaseAlpha { get; set; }
    }

    #endregion

    #region 开关

    /// <summary>
    ///     按主题开关：<paramref name="enable" /> 为真则铺三块玻璃 + 粒子并开始逐帧渐变。
    ///     整个过程包了 try/catch —— 玻璃层出问题也不能连累主题切换与窗口本身。
    /// </summary>
    public static void SetEnabled(bool enable)
    {
        try
        {
            if (!enable)
            {
                Clear();
                return;
            }

            var frm = ModMain.frmMain;
            if (frm?.PanTitle is null || frm.PanMain is null || frm.PanLeft is null || frm.PanForm is null)
                return;

            if (!IsEnabled || Panes.Count == 0)
                Install(frm);

            StartAnimation();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[Liuli] 应用琉璃彩玻璃层失败", ModBase.LogLevel.Normal);
        }
    }

    /// <summary>拆掉玻璃层（切走主题 / 窗口关闭时用，不留定时器、不留控件）。</summary>
    public static void Clear()
    {
        StopAnimation();
        IsEnabled = false;

        foreach (var pane in Panes)
        {
            try
            {
                (pane.Layer.Parent as Panel)?.Children.Remove(pane.Layer);
            }
            catch
            {
                // 已经跟着窗口一起销毁了
            }
        }

        Panes.Clear();
        Particles.Clear();

        if (_particleHost is not null)
        {
            try
            {
                (_particleHost.Parent as Panel)?.Children.Remove(_particleHost);
                _particleHost.Children.Clear();
                if (_isAttached)
                {
                    _particleHost.Unloaded -= HandleHostUnloaded;
                    _isAttached = false;
                }
            }
            catch
            {
                // 忽略
            }

            _particleHost = null;
        }

        Array.Clear(ParticleBrushes);
    }

    #endregion

    #region 装配

    private static void Install(FormMain frm)
    {
        Clear();

        _interiorBrush = ModTitleTextures.GetTiled("Glass.png");
        // 8px 边缘是「玻璃一定要有」的一层，贴图缺失时用同色棋盘兜底
        _edgeBrush = ModTitleTextures.GetTiledOrFallback("GlassPaneTop.png");
        if (_interiorBrush is null)
            ModBase.Log("[Liuli] 原版玻璃贴图缺失（Images/Blocks/Glass.png），玻璃只剩着色层 —— " +
                        "用 tools/extract-vanilla-textures.py <client.jar> 解出来即可", ModBase.LogLevel.Normal);

        for (var i = 0; i < ParticleBrushes.Length; i++) ParticleBrushes[i] = new SolidColorBrush(Colors.White);

        // ① 标题栏：插在最底层（背景之上、Logo / 标签 / 三键之下）
        var titlePane = CreatePane();
        frm.PanTitle.Children.Insert(0, titlePane.Layer);
        Panes.Add(titlePane);

        // ② 侧边栏：PanLeft 的 0 号列正好是左边栏那条底色（宽度跟随 RectLeftBackground），
        //    插在底色之后、右侧分隔阴影之前，这样 8px 玻璃边缘是清晰的
        var sidebarPane = CreatePane();
        Grid.SetColumn(sidebarPane.Layer, 0);
        frm.PanLeft.Children.Insert(Math.Min(1, frm.PanLeft.Children.Count), sidebarPane.Layer);
        Panes.Add(sidebarPane);

        // ③ 主界面：PanMain 的 1 号列就是右侧主内容区，插在最底层（页面内容之下）
        var mainPane = CreatePane();
        Grid.SetColumn(mainPane.Layer, 1);
        frm.PanMain.Children.Insert(0, mainPane.Layer);
        Panes.Add(mainPane);

        // ④ 玻璃内部的小粒子：铺满整个窗口，压在界面内容下面（四层不透明度 30%，粒子能透出来）
        _particleHost = new Canvas
        {
            IsHitTestVisible = false,
            ClipToBounds = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        Grid.SetRow(_particleHost, 0);
        Grid.SetRowSpan(_particleHost, 2);
        Panel.SetZIndex(_particleHost, 0);

        SpawnParticles(_particleHost);

        // 压在壁纸与标题栏背景之上、侧边栏 / 主界面之下
        frm.PanForm.Children.Insert(Math.Min(2, frm.PanForm.Children.Count), _particleHost);
        if (!_isAttached)
        {
            _particleHost.Unloaded += HandleHostUnloaded;
            _isAttached = true;
        }

        IsEnabled = true;
        ModBase.Log(
            $"[Liuli] 琉璃彩玻璃层已装配：{Panes.Count} 块玻璃（边缘 {EdgeThickness:F0}px / 不透明度 {EdgeOpacity:P0}）、" +
            $"{Particles.Count} 颗粒子（不透明度 {ParticleOpacity:P0}）、渐变周期 {HueCycleSeconds:F0}s",
            ModBase.LogLevel.Normal);
    }

    /// <summary>
    ///     造一块玻璃：内部纹理（原版 Glass.png 平铺）+ 跟随主题色的着色渐变 + 8px 原版玻璃边缘。
    /// </summary>
    private static GlassPane CreatePane()
    {
        var layer = new Grid
        {
            IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ClipToBounds = true
        };

        // ① 玻璃内部纹理
        var texture = new Rectangle
        {
            IsHitTestVisible = false,
            Fill = _interiorBrush,
            Opacity = _interiorBrush is null ? 0d : TextureOpacity
        };
        layer.Children.Add(texture);

        // ② 着色渐变（逐帧改色标）
        var tint = new LinearGradientBrush
        {
            StartPoint = new Point(0d, 0d),
            EndPoint = new Point(1d, 1d)
        };
        tint.GradientStops.Add(new GradientStop(Colors.Transparent, 0d));
        tint.GradientStops.Add(new GradientStop(Colors.Transparent, 0.55d));
        tint.GradientStops.Add(new GradientStop(Colors.Transparent, 1d));

        layer.Children.Add(new Rectangle
        {
            IsHitTestVisible = false,
            Fill = tint,
            Opacity = TintOpacity
        });

        // ③ 8px 原版玻璃边缘（BorderBrush 平铺 = 玻璃板顶边纹理）
        layer.Children.Add(new Border
        {
            IsHitTestVisible = false,
            BorderThickness = new Thickness(EdgeThickness),
            BorderBrush = _edgeBrush,
            Opacity = EdgeOpacity
        });

        return new GlassPane { Layer = layer, Tint = tint };
    }

    #endregion

    #region 逐帧渐变

    private static void StartAnimation()
    {
        if (_timer is not null) return;

        _clock ??= Stopwatch.StartNew();
        _lastSeconds = _clock.Elapsed.TotalSeconds;

        var dispatcher = _particleHost?.Dispatcher ??
                         (ModMain.frmMain is { } frm ? frm.Dispatcher : Dispatcher.CurrentDispatcher);
        _timer = new DispatcherTimer(FrameInterval, DispatcherPriority.Background, OnTick, dispatcher);

        // 先把第一帧的颜色算出来，避免切主题瞬间看到一帧纯透明
        OnTick(null, EventArgs.Empty);
        _timer.Start();
    }

    private static void StopAnimation()
    {
        if (_timer is null) return;
        _timer.Stop();
        _timer = null;
        _clock = null;
        _lastSeconds = 0d;
    }

    private static void HandleHostUnloaded(object sender, RoutedEventArgs e) => Clear();

    private static void OnTick(object? sender, EventArgs e)
    {
        try
        {
            var clock = _clock;
            if (clock is null || Panes.Count == 0) return;

            // 窗口最小化时不要白烧 CPU
            if (_particleHost is not null && Window.GetWindow(_particleHost) is { } window &&
                window.WindowState == WindowState.Minimized)
                return;

            var seconds = clock.Elapsed.TotalSeconds;
            var delta = Math.Clamp(seconds - _lastSeconds, 0d, 0.25d);
            _lastSeconds = seconds;

            // 色相随时间匀速旋转：一帧一个位置，所以「每一帧都是不同的渐变」
            var hue = seconds * (360d / HueCycleSeconds);

            for (var i = 0; i < Panes.Count; i++) UpdatePaneGradient(Panes[i], hue + i * PaneHueStep);
            for (var i = 0; i < ParticleBrushes.Length; i++)
                UpdateParticleBrush(ParticleBrushes[i], hue + i * 41d);

            UpdateParticles(delta, seconds);
        }
        catch (Exception ex)
        {
            // 逐帧回调里出错就停掉动画，绝不弹窗、绝不刷屏
            ModBase.Log(ex, "[Liuli] 琉璃彩渐变动画出错，已停止", ModBase.LogLevel.Normal);
            StopAnimation();
        }
    }

    /// <summary>
    ///     更新一块玻璃的着色渐变：三个色标用同一色相环上错开的位置，
    ///     所以任何时候看过去都是一条彩色渐变，而不是单色。
    /// </summary>
    private static void UpdatePaneGradient(GlassPane pane, double hue)
    {
        var stops = pane.Tint.GradientStops;
        if (stops.Count < 3) return;

        stops[0].Color = LabColor.FromLch(0.88d, 0.10d, WrapHue(hue));
        stops[1].Color = LabColor.FromLch(0.76d, 0.20d, WrapHue(hue + 42d));
        stops[2].Color = LabColor.FromLch(0.62d, 0.16d, WrapHue(hue + 88d));
    }

    private static void UpdateParticleBrush(SolidColorBrush brush, double hue)
    {
        if (brush.IsFrozen) return;
        brush.Color = LabColor.FromLch(0.93d, 0.12d, WrapHue(hue));
    }

    /// <summary>色相归一化到 [0, 360)，别把 400+ 这种值丢给色彩库。</summary>
    private static double WrapHue(double hue)
    {
        var wrapped = hue % 360d;
        return wrapped < 0d ? wrapped + 360d : wrapped;
    }

    #endregion

    #region 粒子

    private static void SpawnParticles(Canvas host)
    {
        for (var i = 0; i < ParticleCount; i++)
        {
            var size = ParticleSizeMin + Random.Shared.NextDouble() * (ParticleSizeMax - ParticleSizeMin);
            var element = new Rectangle
            {
                Width = size,
                Height = size,
                IsHitTestVisible = false,
                SnapsToDevicePixels = true,
                UseLayoutRounding = false,
                Fill = ParticleBrushes[Random.Shared.Next(ParticleBrushes.Length)],
                Opacity = ParticleOpacity
            };

            var particle = new Particle
            {
                Element = element,
                X = Random.Shared.NextDouble() * Math.Max(1d, host.ActualWidth),
                Y = Random.Shared.NextDouble() * Math.Max(1d, host.ActualHeight),
                Phase = Random.Shared.NextDouble() * Math.PI * 2d,
                DriftX = (Random.Shared.NextDouble() - 0.5d) * 2d * DriftSpeedMax,
                DriftY = -(DriftSpeedMin + Random.Shared.NextDouble() * (DriftSpeedMax - DriftSpeedMin)),
                ShimmerSpeed = 0.6d + Random.Shared.NextDouble() * 1.6d,
                BaseAlpha = 0.62d + Random.Shared.NextDouble() * 0.38d
            };

            Canvas.SetLeft(element, particle.X);
            Canvas.SetTop(element, particle.Y);
            host.Children.Add(element);
            Particles.Add(particle);
        }

        _needsSpread = true;
    }

    private static void UpdateParticles(double delta, double seconds)
    {
        var host = _particleHost;
        if (host is null || Particles.Count == 0) return;

        var width = host.ActualWidth;
        var height = host.ActualHeight;
        if (width <= 0d || height <= 0d) return;

        if (_needsSpread)
        {
            // 画布刚有尺寸：把挤在左上角的粒子按真实大小撒开
            _needsSpread = false;
            foreach (var particle in Particles)
            {
                particle.X = Random.Shared.NextDouble() * width;
                particle.Y = Random.Shared.NextDouble() * height;
            }

            ModBase.Log($"[Liuli] 玻璃粒子画布就绪（{width:F0}×{height:F0}），已铺开", ModBase.LogLevel.Debug);
        }

        foreach (var particle in Particles)
        {
            particle.X += particle.DriftX * delta;
            particle.Y += particle.DriftY * delta;

            // 飘出边界就从另一侧回来（粒子是玻璃内部的气泡，永远填满玻璃）
            if (particle.Y < -8d) particle.Y = height + 8d;
            else if (particle.Y > height + 8d) particle.Y = -8d;
            if (particle.X < -8d) particle.X = width + 8d;
            else if (particle.X > width + 8d) particle.X = -8d;

            Canvas.SetLeft(particle.Element, particle.X);
            Canvas.SetTop(particle.Element, particle.Y);

            // 闪烁：上限就是用户要的 40%
            var shimmer = 0.55d + 0.45d * Math.Sin(seconds * particle.ShimmerSpeed + particle.Phase);
            particle.Element.Opacity = ParticleOpacity * particle.BaseAlpha * Math.Clamp(shimmer, 0d, 1d);
        }
    }

    #endregion
}
