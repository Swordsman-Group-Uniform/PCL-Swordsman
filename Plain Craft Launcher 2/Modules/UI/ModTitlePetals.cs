using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace PCL;

/// <summary>
///     标题栏的樱花飘落效果 —— 复刻原版（Minecraft 1.20+）樱花树叶的樱花粒子。
///     <para>
///         原版粒子是 12 张 **3×3** 的精灵（`assets/minecraft/textures/particle/cherry_0..11.png`），
///         每次随机取一张，缓慢下落、左右轻摆、慢慢自旋，并在生命周期里淡入淡出。
///         这里直接用**原版贴图**（`Images/Particles/cherry_*.png`，由
///         `tools/extract-vanilla-textures.py` 从原版客户端 jar 里解出），
///         按 3~5 倍最近邻放大成 9~15 px，保证和游戏里一样是像素颗粒感。
///     </para>
///     <para>
///         只在「樱花粉」主题下由 <see cref="FormMain.RefreshTitleSword" /> 打开；
///         切到别的主题、窗口关闭时都会 <see cref="Stop" /> 并清空粒子，不留定时器。
///     </para>
/// </summary>
internal static class ModTitlePetals
{
    /// <summary>同屏粒子数上限（原版视距内也就十几片，多了会显得脏）。</summary>
    private const int MaxParticles = 14;

    /// <summary>下落速度范围（px/s）。比原版略快，否则 48 px 高的标题栏里看不出在动。</summary>
    private const double FallSpeedMin = 14d;
    private const double FallSpeedMax = 28d;

    /// <summary>单片花瓣的存在时间（s）。</summary>
    private const double LifeMin = 2.4d;
    private const double LifeMax = 5.4d;

    /// <summary>3×3 精灵的放大倍数与对应尺寸（px）：3~5 倍 → 9~15 px。</summary>
    private const double SpriteSizeMin = 9d;
    private const double SpriteSizeMax = 15d;

    /// <summary>单片花瓣的最大不透明度。与剑客黄钻石剑的 0.22 同一量级，只做背景点缀。</summary>
    private const double MaxOpacity = 0.30d;

    /// <summary>左右轻摆的幅度（px）与频率（rad/s）。</summary>
    private const double SwayAmplitude = 9d;
    private const double SwaySpeedMin = 0.8d;
    private const double SwaySpeedMax = 1.8d;

    /// <summary>自旋速度（度/秒）。</summary>
    private const double SpinSpeedMax = 40d;

    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(33);

    private static readonly Random Random = new();

    private static Canvas? _host;
    private static DispatcherTimer? _timer;
    private static readonly List<Petal> Petals = new();
    private static readonly BitmapSource?[] Sprites = new BitmapSource?[12];
    private static bool _spritesLoaded;
    private static Stopwatch? _clock;
    private static TimeSpan _lastTick;
    private static bool _isAttached;

    /// <summary>
    ///     首次 Start 时画布还没有尺寸（窗口刚 Loaded），那时算出来的横向位置全是 0；
    ///     等第一次拿到有效宽度，把粒子整体重新铺一遍。
    /// </summary>
    private static bool _needsSpread;

    /// <summary>一片樱花花瓣的运行时状态。</summary>
    private sealed class Petal
    {
        public required Image Element { get; init; }
        public double BaseX { get; set; }
        public double Y { get; set; }
        public double FallSpeed { get; set; }
        public double SwayPhase { get; set; }
        public double SwaySpeed { get; set; }
        public double Spin { get; set; }
        public double SpinSpeed { get; set; }
        public double Size { get; set; }
        public double Age { get; set; }
        public double Life { get; set; }
        public RotateTransform Rotation { get; set; } = null!;
    }

    /// <summary>
    ///     按主题开关：<paramref name="enable" /> 为真则在 <paramref name="host" /> 上开始飘落。
    ///     整个过程包了 try/catch —— 粒子就算出问题，也不能连累标题栏其它装饰和主题切换。
    /// </summary>
    public static void SetEnabled(Canvas? host, bool enable)
    {
        try
        {
            if (!enable || host is null)
            {
                Stop();
                return;
            }

            Start(host);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[Petal] 启动樱花飘落失败", ModBase.LogLevel.Normal);
        }
    }

    /// <summary>开始（重复调用无副作用）。</summary>
    public static void Start(Canvas host)
    {
        if (_host == host && _timer is not null)
            return;

        Stop();

        EnsureSprites();
        if (Sprites.All(sprite => sprite is null))
        {
            ModBase.Log("[Petal] 樱花粒子贴图缺失（Images/Particles/cherry_*.png），跳过飘落效果",
                ModBase.LogLevel.Debug);
            return;
        }

        _host = host;

        // 刚被设为 Visible 时画布可能还没测量过（ActualWidth/Height 都是 0），
        // 先手动跑一次布局，粒子第一帧就能按真实尺寸撒开，而不是全挤在左上角
        try
        {
            if (host.ActualWidth <= 0d || host.ActualHeight <= 0d)
                host.UpdateLayout();
        }
        catch
        {
            // 布局还没准备好也没关系，OnTick 里的 _needsSpread 会补一次
        }

        _clock = Stopwatch.StartNew();
        _lastTick = TimeSpan.Zero;
        _timer = new DispatcherTimer(FrameInterval, DispatcherPriority.Background, OnTick, host.Dispatcher);
        if (!_isAttached)
        {
            host.Unloaded += HandleHostUnloaded;
            _isAttached = true;
        }

        // 先把屏幕铺满，避免刚切主题时右上角一片空白
        for (var i = 0; i < MaxParticles; i++)
            Spawn(randomizeAge: true);
        _needsSpread = true;

        _timer.Start();
        ModBase.Log($"[Petal] 樱花飘落已启动：{Petals.Count} 片粒子、{Sprites.Count(sprite => sprite is not null)} 张贴图",
            ModBase.LogLevel.Normal);
    }

    /// <summary>停止并清场。</summary>
    public static void Stop()
    {
        if (_timer is not null)
        {
            _timer.Stop();
            _timer = null;
        }

        Petals.Clear();

        if (_host is not null)
        {
            _host.Children.Clear();
            if (_isAttached)
            {
                _host.Unloaded -= HandleHostUnloaded;
                _isAttached = false;
            }
        }

        _host = null;
        _clock = null;
        _lastTick = TimeSpan.Zero;
        _needsSpread = false;
    }

    private static void HandleHostUnloaded(object sender, RoutedEventArgs e) => Stop();

    private static void OnTick(object? sender, EventArgs e)
    {
        var host = _host;
        var clock = _clock;
        if (host is null || clock is null || _timer is null)
            return;

        // 窗口最小化 / 隐藏时不要白烧 CPU
        var window = Window.GetWindow(host);
        if (window is not null && window.WindowState == WindowState.Minimized)
        {
            _lastTick = clock.Elapsed;
            return;
        }

        var now = clock.Elapsed;
        var delta = Math.Clamp((now - _lastTick).TotalSeconds, 0d, 0.25d);
        _lastTick = now;

        var width = host.ActualWidth;
        var height = host.ActualHeight;
        if (width <= 0 || height <= 0)
            return;

        if (_needsSpread)
        {
            // 布局好了：把刚才挤在 0 位置的粒子按真实宽度重新撒开
            _needsSpread = false;
            ModBase.Log($"[Petal] 画布就绪（{width:F0}×{height:F0}），粒子已铺开", ModBase.LogLevel.Debug);
            foreach (var petal in Petals)
            {
                petal.BaseX = Random.NextDouble() * Math.Max(1d, width - petal.Size);
                petal.Y = Random.NextDouble() * height;
                petal.Age = Random.NextDouble() * petal.Life * 0.55d;
                Canvas.SetLeft(petal.Element, petal.BaseX);
                Canvas.SetTop(petal.Element, petal.Y);
            }
        }

        foreach (var petal in Petals)
        {
            petal.Age += delta;
            petal.Y += petal.FallSpeed * delta;
            petal.SwayPhase += petal.SwaySpeed * delta;
            petal.Spin += petal.SpinSpeed * delta;

            if (petal.Y > height || petal.Age >= petal.Life)
            {
                Respawn(petal, width);
                continue;
            }

            var x = petal.BaseX + Math.Sin(petal.SwayPhase) * SwayAmplitude;
            Canvas.SetLeft(petal.Element, x);
            Canvas.SetTop(petal.Element, petal.Y);
            petal.Rotation.Angle = petal.Spin;
            petal.Element.Opacity = ComputeOpacity(petal);
        }
    }

    /// <summary>淡入 / 淡出：开头 18% 淡入，最后 30% 淡出。</summary>
    private static double ComputeOpacity(Petal petal)
    {
        var progress = petal.Life <= 0d ? 1d : Math.Clamp(petal.Age / petal.Life, 0d, 1d);
        var fadeIn = Math.Clamp(progress / 0.18d, 0d, 1d);
        var fadeOut = Math.Clamp((1d - progress) / 0.30d, 0d, 1d);
        return MaxOpacity * Math.Min(fadeIn, fadeOut);
    }

    private static void Spawn(bool randomizeAge)
    {
        var host = _host;
        if (host is null)
            return;

        var sprite = RandomSprite();
        if (sprite is null)
            return;

        var size = SpriteSizeMin + Random.NextDouble() * (SpriteSizeMax - SpriteSizeMin);
        var image = new Image
        {
            Source = sprite,
            Width = size,
            Height = size,
            Stretch = Stretch.Fill,
            IsHitTestVisible = false,
            SnapsToDevicePixels = true,
            UseLayoutRounding = false
        };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);

        var rotation = new RotateTransform(0d, size / 2d, size / 2d);
        image.RenderTransform = rotation;

        var petal = new Petal
        {
            Element = image,
            Rotation = rotation,
            Size = size,
            BaseX = Random.NextDouble() * Math.Max(1d, host.ActualWidth - size),
            Y = -size - Random.NextDouble() * 12d,
            FallSpeed = FallSpeedMin + Random.NextDouble() * (FallSpeedMax - FallSpeedMin),
            SwayPhase = Random.NextDouble() * Math.PI * 2d,
            SwaySpeed = SwaySpeedMin + Random.NextDouble() * (SwaySpeedMax - SwaySpeedMin),
            Spin = Random.NextDouble() * 360d,
            SpinSpeed = (Random.NextDouble() - 0.5d) * 2d * SpinSpeedMax,
            Life = LifeMin + Random.NextDouble() * (LifeMax - LifeMin),
            Age = 0d
        };

        if (randomizeAge)
        {
            // 初次铺满：让每片各自处在生命周期的不同阶段，看起来是「一直在飘」而不是同时开始
            petal.Age = Random.NextDouble() * petal.Life * 0.55d;
            petal.Y = petal.Age * petal.FallSpeed + Random.NextDouble() * Math.Max(1d, host.ActualHeight * 0.4d);
        }

        Canvas.SetLeft(image, petal.BaseX);
        Canvas.SetTop(image, petal.Y);
        rotation.Angle = petal.Spin;
        image.Opacity = ComputeOpacity(petal);

        host.Children.Add(image);
        Petals.Add(petal);
    }

    /// <summary>让一片花瓣重新从顶部开始（复用控件，避免频繁创建）。</summary>
    private static void Respawn(Petal petal, double hostWidth)
    {
        var sprite = RandomSprite();
        if (sprite is not null)
            petal.Element.Source = sprite;

        petal.BaseX = Random.NextDouble() * Math.Max(1d, hostWidth - petal.Size);
        petal.Y = -petal.Size - Random.NextDouble() * 16d;
        petal.FallSpeed = FallSpeedMin + Random.NextDouble() * (FallSpeedMax - FallSpeedMin);
        petal.SwayPhase = Random.NextDouble() * Math.PI * 2d;
        petal.SwaySpeed = SwaySpeedMin + Random.NextDouble() * (SwaySpeedMax - SwaySpeedMin);
        petal.SpinSpeed = (Random.NextDouble() - 0.5d) * 2d * SpinSpeedMax;
        petal.Life = LifeMin + Random.NextDouble() * (LifeMax - LifeMin);
        petal.Age = 0d;
    }

    private static BitmapSource? RandomSprite()
    {
        var candidates = Sprites.Where(sprite => sprite is not null).ToArray();
        if (candidates.Length == 0)
            return null;
        return candidates[Random.Next(candidates.Length)];
    }

    /// <summary>
    ///     原版 12 张樱花粒子贴图（3×3 像素），只加载一次并冻结。
    ///     <para>
    ///         三种写法依次兜底，因为单文件发布（PublishSingleFile）下「相对 pack URI」不保证能解析：
    ///         <list type="number">
    ///             <item>绝对 pack URI：`pack://application:,,,/Plain Craft Launcher 2;component/Images/Particles/cherry_N.png`</item>
    ///             <item><see cref="ModBase.GetResourceStream" />（同样走绝对 pack URI，但拿到的是流）</item>
    ///             <item>直接按资源名从程序集清单里取（`Plain Craft Launcher 2.g.resources`）</item>
    ///         </list>
    ///     </para>
    /// </summary>
    private static void EnsureSprites()
    {
        if (_spritesLoaded)
            return;
        _spritesLoaded = true;

        for (var i = 0; i < Sprites.Length; i++)
        {
            var name = $"Images/Particles/cherry_{i}.png";
            Sprites[i] = LoadSprite(name) ??
                         LoadFromResourceManifest($"images/particles/cherry_{i}.png");
        }

        var loaded = Sprites.Count(sprite => sprite is not null);
        if (loaded == 0)
        {
            // 兜底：用原版同款配色现画一张 3×3 的花瓣，保证「樱花粉至少能看到花瓣在飘」，
            // 同时在日志里把问题喊出来（正常情况永远走不到这里）
            Sprites[0] = BuildFallbackSprite();
            ModBase.Log("[Petal] 原版樱花粒子贴图一张都没加载成功，已改用内置 3×3 兜底花瓣（请检查 Images/Particles/cherry_*.png）",
                ModBase.LogLevel.Normal);
        }
        else if (loaded < Sprites.Length)
        {
            ModBase.Log($"[Petal] 樱花粒子贴图只加载到 {loaded}/{Sprites.Length} 张", ModBase.LogLevel.Normal);
        }
    }

    /// <summary>用绝对 pack URI 加载一张内嵌资源图；失败返回 null。</summary>
    private static BitmapSource? LoadSprite(string resourcePath)
    {
        var candidates = new[]
        {
            $"pack://application:,,,/Plain Craft Launcher 2;component/{resourcePath}",
            $"pack://application:,,,/{resourcePath}"
        };

        foreach (var candidate in candidates)
        {
            try
            {
                var frame = BitmapFrame.Create(new Uri(candidate, UriKind.Absolute),
                    BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                frame.Freeze();
                return frame;
            }
            catch
            {
                // 换下一个写法
            }
        }

        try
        {
            using var stream = ModBase.GetResourceStream(resourcePath);
            if (stream is null)
                return null;
            var frame = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            frame.Freeze();
            return frame;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, $"[Petal] 加载 {resourcePath} 失败", ModBase.LogLevel.Debug);
            return null;
        }
    }

    /// <summary>
    ///     贴图全挂了时的兜底花瓣：照原版 cherry 精灵的样子现画一张 3×3
    ///     （对角两个透明角 + 两种粉色），保证樱花瓣至少是能看见的。
    /// </summary>
    private static BitmapSource BuildFallbackSprite()
    {
        // BGRA，按原版 cherry_0 的排布：左上 / 右下透明
        byte[] pixels =
        [
            0, 0, 0, 0, /* 透明 */ 220, 185, 247, 255, 205, 167, 239, 255,
            220, 185, 247, 255, 220, 185, 247, 255, 220, 185, 247, 255,
            220, 185, 247, 255, 220, 185, 247, 255, 0, 0, 0, 0 /* 透明 */
        ];
        var bitmap = BitmapSource.Create(3, 3, 96d, 96d, PixelFormats.Bgra32, null, pixels, 3 * 4);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>最后一道兜底：直接读程序集里的 .g.resources 清单。</summary>
    private static BitmapSource? LoadFromResourceManifest(string resourceKey)
    {
        try
        {
            var assembly = typeof(ModTitlePetals).Assembly;
            using var stream = assembly.GetManifestResourceStream(assembly.GetName().Name + ".g.resources");
            if (stream is null)
                return null;
            using var reader = new System.Resources.ResourceReader(stream);
            foreach (System.Collections.DictionaryEntry entry in reader)
            {
                if (entry.Key is not string key || !key.Equals(resourceKey, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (entry.Value is not Stream data)
                    continue;
                var frame = BitmapFrame.Create(data, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                frame.Freeze();
                return frame;
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[Petal] 从资源清单读取樱花粒子贴图失败", ModBase.LogLevel.Debug);
        }

        return null;
    }
}
