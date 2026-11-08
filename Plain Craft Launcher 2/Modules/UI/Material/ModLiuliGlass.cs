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
///         用户需求（snapshot-86，按 2026-11-08 反馈修正）：
///         <list type="bullet">
///             <item>整个启动器透明化，四层不透明度锁定 30%（见 <see cref="ModMaterial.ApplyWindowOpacity" />）。</item>
///             <item>**边框贴图只出现在启动器外沿**：8px 的原版玻璃板边缘（<c>GlassPaneTop.png</c>，不透明度 60%），
///                 界面内部**不再有任何框**（以前按「三块玻璃」各画一圈 8px，导致界面中间横竖都是框）。</item>
///             <item>内部是**去掉边框的玻璃内部纹理**：整窗铺一张原版 <c>Glass.png</c>（16×16 原尺寸平铺，
///                 这张图本身就是纯内部粒子、没有边框）。</item>
///             <item>所有界面都有玻璃纹理内部的小粒子（2~5 px，不透明度上限 40%）。</item>
///             <item>纹理与标题栏颜色跟随主题色，主题色是**动态彩色渐变**，随时间缓慢变化，每帧都是一条渐变。</item>
///         </list>
///     </para>
///     <para>
///         结构只有两层，所以内部是**连续的一整块玻璃**：
///         <list type="number">
///             <item><c>_glassLayer</c>（<see cref="Grid" />，跨标题栏 + 内容两行）：内部纹理 + 逐帧着色渐变 + 玻璃粒子，
///                 压在壁纸之上、标题栏背景与页面内容之下；</item>
///             <item><c>_rim</c>（<see cref="Border" />，跨两行、8px 边框）：只沿整窗外沿画一圈原版玻璃边缘。</item>
///         </list>
///     </para>
///     <para>
///         性能：每帧只改 7 个画刷颜色（着色渐变 3 个色标 + 粒子 4 个纯色画刷），
///         不重建资源字典、不走 <c>ThemeService</c> 的全量刷新链路。45 fps，色相沿 OKLCH 96 秒转一圈。
///     </para>
/// </summary>
internal static class ModLiuliGlass
{
    #region 参数

    /// <summary>启动器外沿的玻璃边缘厚度（用户要求「约 8px」）。</summary>
    private const double EdgeThickness = 8d;

    /// <summary>边框层不透明度（用户要求 60%）。</summary>
    private const double EdgeOpacity = 0.60d;

    /// <summary>粒子不透明度上限（用户要求 40%）。</summary>
    private const double ParticleOpacity = 0.40d;

    /// <summary>玻璃内部纹理（原版 Glass.png 平铺）的不透明度：只做底纹，不能盖住内容。</summary>
    private const double TextureOpacity = 0.30d;

    /// <summary>跟随主题色的着色渐变层的不透明度。</summary>
    private const double TintOpacity = 0.55d;

    /// <summary>色相转一整圈所需的时间（秒）。用户要「随时间缓慢变化」。</summary>
    private const double HueCycleSeconds = 96d;

    /// <summary>粒子数（小颗粒，密度低才不会显得脏）。</summary>
    private const int ParticleCount = 46;

    /// <summary>粒子尺寸范围（px）。「粒子较小」。</summary>
    private const double ParticleSizeMin = 2d;
    private const double ParticleSizeMax = 5d;

    /// <summary>粒子漂移速度范围（px/s）。</summary>
    private const double DriftSpeedMin = 2d;
    private const double DriftSpeedMax = 9d;

    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(22);

    #endregion

    #region 状态

    /// <summary>整块玻璃（内部纹理 + 着色渐变 + 粒子），跨标题栏与内容两行。</summary>
    private static Grid? _glassLayer;

    /// <summary>着色渐变（逐帧改色标）。</summary>
    private static LinearGradientBrush? _tint;

    /// <summary>只沿整窗外沿画的 8px 原版玻璃边缘。</summary>
    private static Border? _rim;

    /// <summary>粒子画布（玻璃内部的小颗粒）。</summary>
    private static Canvas? _particleHost;

    private static readonly List<Particle> Particles = [];
    private static readonly SolidColorBrush[] ParticleBrushes = new SolidColorBrush[4];

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
    ///     按主题开关：<paramref name="enable" /> 为真则铺一整块玻璃 + 外沿边框并开始逐帧渐变。
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
            if (frm?.PanForm is null) return;

            if (!IsEnabled || _glassLayer is null)
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
        Particles.Clear();

        // 先摘 Unloaded 事件再拔控件：拔控件会触发 Unloaded，晚摘就会递归回到本方法
        if (_particleHost is not null && _isAttached)
        {
            _particleHost.Unloaded -= HandleHostUnloaded;
            _isAttached = false;
        }

        RemoveFromParent(_rim);
        RemoveFromParent(_glassLayer);

        _particleHost?.Children.Clear();
        _particleHost = null;
        _glassLayer = null;
        _rim = null;
        _tint = null;
        Array.Clear(ParticleBrushes);
    }

    private static void RemoveFromParent(UIElement? element)
    {
        if (element is null) return;
        try
        {
            if (element is FrameworkElement { Parent: Panel panel }) panel.Children.Remove(element);
        }
        catch
        {
            // 已经跟着窗口一起销毁了
        }
    }

    #endregion

    #region 装配

    private static void Install(FormMain frm)
    {
        Clear();

        // 内部纹理：原版 Glass.png（纯内部粒子、没有边框）；边缘：原版玻璃板顶边
        var interiorBrush = ModTitleTextures.GetTiled("Glass.png");
        var edgeBrush = ModTitleTextures.GetTiledOrFallback("GlassPaneTop.png");
        if (interiorBrush is null)
            ModBase.Log("[Liuli] 原版玻璃贴图缺失（Images/Blocks/Glass.png），玻璃只剩着色层 —— " +
                        "用 tools/extract-vanilla-textures.py <client.jar> 解出来即可", ModBase.LogLevel.Normal);

        for (var i = 0; i < ParticleBrushes.Length; i++) ParticleBrushes[i] = new SolidColorBrush(Colors.White);

        // ① 着色渐变（逐帧改色标）：一条从左上到右下的三色渐变，任何时候看过去都是彩色渐变
        _tint = new LinearGradientBrush
        {
            StartPoint = new Point(0d, 0d),
            EndPoint = new Point(1d, 1d)
        };
        _tint.GradientStops.Add(new GradientStop(Colors.Transparent, 0d));
        _tint.GradientStops.Add(new GradientStop(Colors.Transparent, 0.55d));
        _tint.GradientStops.Add(new GradientStop(Colors.Transparent, 1d));

        // ② 整块玻璃：内部纹理 + 着色渐变 + 玻璃内部粒子（跨两行，压在页面内容之下）
        var layer = new Grid
        {
            IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ClipToBounds = true
        };
        layer.Children.Add(new Rectangle
        {
            IsHitTestVisible = false,
            Fill = interiorBrush,
            Opacity = interiorBrush is null ? 0d : TextureOpacity
        });
        layer.Children.Add(new Rectangle
        {
            IsHitTestVisible = false,
            Fill = _tint,
            Opacity = TintOpacity
        });

        _particleHost = new Canvas
        {
            IsHitTestVisible = false,
            ClipToBounds = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        SpawnParticles(_particleHost);
        layer.Children.Add(_particleHost);

        Grid.SetRow(layer, 0);
        Grid.SetRowSpan(layer, 2);

        // 插到壁纸上、标题栏背景之下：PanForm 的 0 号是壁纸，1 号是标题栏，
        // 所以插到 1 号位置 —— 标题栏的底色与页面内容都在它上面，玻璃只是「下面那层整块的」
        frm.PanForm.Children.Insert(Math.Min(1, frm.PanForm.Children.Count), layer);
        if (!_isAttached)
        {
            _particleHost.Unloaded += HandleHostUnloaded;
            _isAttached = true;
        }

        _glassLayer = layer;

        // ③ 外沿边框：只有这一圈，界面内部不再有任何框
        _rim = new Border
        {
            IsHitTestVisible = false,
            BorderThickness = new Thickness(EdgeThickness),
            BorderBrush = edgeBrush,
            Opacity = EdgeOpacity
        };
        Grid.SetRow(_rim, 0);
        Grid.SetRowSpan(_rim, 2);
        Panel.SetZIndex(_rim, 1); // 压在内容之上、弹窗层（ZIndex 2）之下
        frm.PanForm.Children.Add(_rim);

        IsEnabled = true;
        ModBase.Log(
            $"[Liuli] 琉璃彩玻璃层已装配：整窗一块玻璃（内部纹理 Glass.png {TextureOpacity:P0} 不透明度）+ " +
            $"外沿 {EdgeThickness:F0}px 玻璃边缘（{EdgeOpacity:P0}）+ {Particles.Count} 颗粒子（{ParticleOpacity:P0}）、" +
            $"渐变周期 {HueCycleSeconds:F0}s",
            ModBase.LogLevel.Normal);
    }

    #endregion

    #region 逐帧渐变

    private static void StartAnimation()
    {
        if (_timer is not null) return;

        _clock ??= Stopwatch.StartNew();
        _lastSeconds = _clock.Elapsed.TotalSeconds;

        var dispatcher = _glassLayer?.Dispatcher ??
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
            if (clock is null || _tint is null) return;

            // 窗口最小化时不要白烧 CPU
            if (_particleHost is not null && Window.GetWindow(_particleHost) is { } window &&
                window.WindowState == WindowState.Minimized)
                return;

            var seconds = clock.Elapsed.TotalSeconds;
            var delta = Math.Clamp(seconds - _lastSeconds, 0d, 0.25d);
            _lastSeconds = seconds;

            // 色相随时间匀速旋转：一帧一个位置，所以「每一帧都是不同的渐变」
            UpdateTint(seconds * (360d / HueCycleSeconds));
            for (var i = 0; i < ParticleBrushes.Length; i++)
                UpdateParticleBrush(ParticleBrushes[i], seconds * (360d / HueCycleSeconds) + i * 41d);

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
    ///     更新着色渐变：三个色标取同一色相环上错开的位置，
    ///     所以任何时候看过去都是一条彩色渐变，而不是单色。
    ///     <para>
    ///         明度跟着当前的深浅色模式走（深色模式下压暗），否则浅底的彩色玻璃上
    ///         深色模式的浅色文字会糊成一片。
    ///     </para>
    /// </summary>
    private static void UpdateTint(double hue)
    {
        var stops = _tint?.GradientStops;
        if (stops is null || stops.Count < 3) return;

        var lightScale = ThemeService.IsDarkMode ? 0.42d : 1d;
        stops[0].Color = LabColor.FromLch(0.88d * lightScale, 0.10d, WrapHue(hue));
        stops[1].Color = LabColor.FromLch(0.76d * lightScale, 0.20d, WrapHue(hue + 42d));
        stops[2].Color = LabColor.FromLch(0.62d * lightScale, 0.16d, WrapHue(hue + 88d));
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
