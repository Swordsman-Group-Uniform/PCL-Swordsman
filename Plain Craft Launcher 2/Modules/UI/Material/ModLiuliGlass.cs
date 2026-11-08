using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using PCL.Core.UI.Theme;

namespace PCL;

/// <summary>
///     「琉璃彩」主题的彩色渐变层。
///     <para>
///         用户需求（snapshot-87 最终口径）：
///         <list type="bullet">
///             <item>**不要任何贴图**（原版玻璃纹理 / 玻璃板边缘一律不用），整个启动器就是一层**彩色渐变**。</item>
///             <item>整个启动器透明化，四层不透明度锁定 30%（见 <see cref="ModMaterial.ApplyWindowOpacity" />）。</item>
///             <item>渐变随时间**持续流动**，每一帧都是一条新的彩色渐变（速度比之前的 96 s/圈快一些）。</item>
///             <item>界面上的小粒子（2~5 px，不透明度上限 40%）保留 —— 它们不是贴图，是程序画的小方块。</item>
///         </list>
///     </para>
///     <para>
///         实现：窗口里插一层 <see cref="Rectangle" />（跨标题栏与内容两行，压在页面内容之下），
///         它的填充是一条 5 色标的 <see cref="LinearGradientBrush" />；每帧只做两件事 ——
///         ① 按时间重算 5 个色标的颜色（色相沿 OKLCH 匀速旋转，48 秒转一圈）；
///         ② 稍微旋转渐变的走向（150 秒转一圈），让流动感更明显。
///         不重建资源字典、不走 <c>ThemeService</c> 全量刷新。
///     </para>
/// </summary>
internal static class ModLiuliGlass
{
    #region 参数

    /// <summary>粒子不透明度上限（用户要求 40%）。</summary>
    private const double ParticleOpacity = 0.40d;

    /// <summary>彩色渐变层的不透明度：这是琉璃彩唯一的视觉，要给足。</summary>
    private const double TintOpacity = 0.68d;

    /// <summary>色相转一整圈所需的时间（秒）。用户要求「渐变速度稍微快一点」，从 96 s 提到 48 s。</summary>
    private const double HueCycleSeconds = 48d;

    /// <summary>渐变方向转一整圈所需的时间（秒）：慢速旋转，让「每一帧都在流动」看得出来。</summary>
    private const double SwayCycleSeconds = 150d;

    /// <summary>色标数（一条渐变里铺开 5 个颜色，看起来才像彩色的琉璃）。</summary>
    private const int StopCount = 5;

    /// <summary>相邻色标之间的色相跨度（度）。5 个色标 × 66° ≈ 铺满 264°，整条渐变呈彩虹般流动。</summary>
    private const double StopHueStep = 66d;

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

    /// <summary>彩色渐变层（跨标题栏与内容两行）。</summary>
    private static Rectangle? _gradient;

    /// <summary>逐帧改色标 / 改走向的那条渐变画刷。</summary>
    private static LinearGradientBrush? _tint;

    /// <summary>粒子画布。</summary>
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

    /// <summary>当前是否已经装上了渐变层。</summary>
    public static bool IsEnabled { get; private set; }

    /// <summary>一颗小粒子。</summary>
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
    ///     按主题开关：<paramref name="enable" /> 为真则铺一层彩色渐变 + 粒子并开始逐帧流动。
    ///     整个过程包了 try/catch —— 这层出问题也不能连累主题切换与窗口本身。
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

            if (!IsEnabled || _gradient is null)
                Install(frm);

            StartAnimation();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[Liuli] 应用琉璃彩渐变层失败", ModBase.LogLevel.Normal);
        }
    }

    /// <summary>拆掉渐变层（切走主题 / 窗口关闭时用，不留定时器、不留控件）。</summary>
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

        // 整层（渐变 + 粒子）一起拔掉：_gradient 只是这一层里的一个子元素
        RemoveFromParent(_layer);
        _particleHost?.Children.Clear();
        _particleHost = null;
        _gradient = null;
        _tint = null;
        _layer = null;
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

        // ① 彩色渐变：5 个色标，色相沿渐变方向铺开（每帧重算颜色与走向）
        _tint = new LinearGradientBrush
        {
            StartPoint = new Point(0d, 0d),
            EndPoint = new Point(1d, 1d)
        };
        for (var i = 0; i < StopCount; i++)
            _tint.GradientStops.Add(new GradientStop(Colors.Transparent, i / (double)(StopCount - 1)));

        // ② 粒子：程序画的小方块（不是贴图）
        for (var i = 0; i < ParticleBrushes.Length; i++) ParticleBrushes[i] = new SolidColorBrush(Colors.White);

        _particleHost = new Canvas
        {
            IsHitTestVisible = false,
            ClipToBounds = true,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        SpawnParticles(_particleHost);

        // ③ 整层：渐变 + 粒子，跨标题栏与内容两行，压在页面内容之下
        var layer = new Grid
        {
            IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ClipToBounds = true
        };
        _gradient = new Rectangle
        {
            IsHitTestVisible = false,
            Fill = _tint,
            Opacity = TintOpacity
        };
        layer.Children.Add(_gradient);
        layer.Children.Add(_particleHost);

        Grid.SetRow(layer, 0);
        Grid.SetRowSpan(layer, 2);

        // 插到壁纸之上、标题栏底色与页面内容之下（PanForm 的 0 号是壁纸、1 号是标题栏）
        frm.PanForm.Children.Insert(Math.Min(1, frm.PanForm.Children.Count), layer);
        if (!_isAttached)
        {
            _particleHost.Unloaded += HandleHostUnloaded;
            _isAttached = true;
        }

        // 把「整层」记住，Clear 时按父级移除（_gradient 自己不是 PanForm 的直接子元素）
        _layer = layer;

        IsEnabled = true;
        ModBase.Log(
            $"[Liuli] 琉璃彩渐变层已装配：{StopCount} 色标彩色渐变（{TintOpacity:P0} 不透明度、" +
            $"色相 {HueCycleSeconds:F0}s/圈、走向 {SwayCycleSeconds:F0}s/圈）+ {Particles.Count} 颗粒子（{ParticleOpacity:P0}）",
            ModBase.LogLevel.Normal);
    }

    /// <summary>承载渐变与粒子的那一层（Clear 时整层拔掉）。</summary>
    private static Grid? _layer;

    #endregion

    #region 逐帧流动

    private static void StartAnimation()
    {
        if (_timer is not null) return;

        _clock ??= Stopwatch.StartNew();
        _lastSeconds = _clock.Elapsed.TotalSeconds;

        var dispatcher = _layer?.Dispatcher ??
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

            UpdateGradient(seconds);
            for (var i = 0; i < ParticleBrushes.Length; i++)
                UpdateParticleBrush(ParticleBrushes[i], seconds * (360d / HueCycleSeconds) + i * 41d);

            UpdateParticles(delta, seconds);
        }
        catch (Exception ex)
        {
            // 逐帧回调里出错就停掉动画，绝不弹窗、绝不刷屏
            ModBase.Log(ex, "[Liuli] 琉璃彩流动渐变的回调出错，已停止", ModBase.LogLevel.Normal);
            StopAnimation();
        }
    }

    /// <summary>
    ///     更新渐变：色标颜色按时间旋转色相，同时慢慢转动渐变方向。
    ///     <para>
    ///         明度跟着深浅色模式走（深色模式压暗），否则浅底的彩色渐变上，
    ///         深色模式的浅色文字会糊成一片。
    ///     </para>
    /// </summary>
    private static void UpdateGradient(double seconds)
    {
        var tint = _tint;
        if (tint is null) return;

        var hueBase = seconds * (360d / HueCycleSeconds);
        var lightScale = ThemeService.IsDarkMode ? 0.42d : 1d;

        var stops = tint.GradientStops;
        for (var i = 0; i < stops.Count; i++)
        {
            // 明度 / 色度沿渐变方向做一点变化，颜色不会「一条平涂」
            var t = stops.Count <= 1 ? 0d : i / (double)(stops.Count - 1);
            var lightness = (0.90d - 0.22d * t) * lightScale;
            var chroma = 0.14d + 0.06d * Math.Sin(Math.PI * t);
            stops[i].Color = LabColor.FromLch(lightness, chroma, WrapHue(hueBase + i * StopHueStep));
        }

        // 渐变走向慢慢转圈（每帧都给新的角度，肉眼看得出在流动）
        var swayAngle = seconds * (Math.PI * 2d / SwayCycleSeconds);
        var cos = Math.Cos(swayAngle);
        var sin = Math.Sin(swayAngle);
        tint.StartPoint = new Point(0.5d - cos * 0.5d, 0.5d - sin * 0.5d);
        tint.EndPoint = new Point(0.5d + cos * 0.5d, 0.5d + sin * 0.5d);
    }

    private static void UpdateParticleBrush(SolidColorBrush brush, double hue)
    {
        if (brush.IsFrozen) return;
        var lightScale = ThemeService.IsDarkMode ? 0.55d : 1d;
        brush.Color = LabColor.FromLch(0.93d * lightScale, 0.12d, WrapHue(hue));
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

            ModBase.Log($"[Liuli] 粒子画布就绪（{width:F0}×{height:F0}），已铺开", ModBase.LogLevel.Debug);
        }

        foreach (var particle in Particles)
        {
            particle.X += particle.DriftX * delta;
            particle.Y += particle.DriftY * delta;

            // 飘出边界就从另一侧回来
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
