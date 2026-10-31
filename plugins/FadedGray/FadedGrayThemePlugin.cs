using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using PCL.PluginSdk;

namespace FadedGray;

/// <summary>
///     「Faded灰主题」插件：把自己定义的主题（<see cref="IPluginCustomThemesProvider" />）交给宿主，
///     主题本身（主色、名字、标题栏装饰）全部由本插件提供，不占宿主的内置主题槽位。
///     <para>
///         装饰图 <c>Faded.jpg</c> 以 <c>EmbeddedResource</c> 形式编进本 DLL，
///         运行时从清单流读出来 —— 插件包因此只需要 <c>dll + plugin.json</c> 两个文件，
///         也完全不用碰宿主的 pack URI 体系。
///     </para>
/// </summary>
public sealed class FadedGrayThemePlugin : IPlugin, IPluginCustomThemesProvider
{
    private const string ThemeId = "faded.gray";

    /// <summary>装饰图在 DLL 里的资源名（见 csproj 的 LogicalName）。</summary>
    private const string ImageResourceName = "FadedGray.Faded.jpg";

    /// <summary>
    ///     装饰的不透明度。原版 0.22 在灰色标题栏上几乎看不出来（实机反馈「不显示图片」），
    ///     这里提到 0.34：仍是背景点缀，但能明显看出是张图。
    /// </summary>
    private const double DecorationOpacity = 0.34d;

    /// <summary>装饰尺寸：横向铺开一些，才能看出图片内容（标题栏只有 48px 高，纵向会被裁）。</summary>
    private const double DecorationWidth = 420d;

    private const double DecorationHeight = 48d;

    private IPluginLogger? _log;
    private BitmapSource? _image;

    public void Initialize(IPluginContext context)
    {
        _log = context.Log;
        _image = LoadImage();
        _log.Info(_image is null
            ? "装饰图加载失败，Faded灰 主题将没有标题栏装饰"
            : $"装饰图已加载：{_image.PixelWidth}×{_image.PixelHeight}");
    }

    public IReadOnlyList<PluginCustomThemeDefinition> GetCustomThemes() =>
    [
        new()
        {
            Id = ThemeId,
            DisplayName = "Faded灰",
            Hex = "#5C5C5C",
            Description = "低饱和的中性灰，标题栏右上角带一张左右虚化的 Faded 图。",
            SortOrder = 20,
            CreateTitleDecoration = BuildDecoration
        }
    ];

    /// <summary>
    ///     标题栏装饰：右上角一块低透明度的图片，左右两侧用不透明度遮罩渐隐，再叠一点模糊做柔化。
    ///     宿主只负责把它放进标题栏右侧容器里，尺寸与对齐由这里决定。
    /// </summary>
    private FrameworkElement BuildDecoration()
    {
        var image = new Image
        {
            Source = _image,
            Width = DecorationWidth,
            Height = DecorationHeight,
            Stretch = Stretch.UniformToFill,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
            Opacity = DecorationOpacity
        };

        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);

        // 柔化：整体一点点高斯模糊，边缘才不是硬切的
        image.Effect = new BlurEffect
        {
            Radius = 6d,
            KernelType = KernelType.Gaussian,
            RenderingBias = RenderingBias.Performance
        };

        // 左右两侧虚化：横向不透明度遮罩（中间实、两边透明）
        var mask = new LinearGradientBrush
        {
            StartPoint = new Point(0d, 0d),
            EndPoint = new Point(1d, 0d)
        };
        mask.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 0d));
        mask.GradientStops.Add(new GradientStop(Color.FromArgb(140, 255, 255, 255), 0.10d));
        mask.GradientStops.Add(new GradientStop(Color.FromArgb(255, 255, 255, 255), 0.30d));
        mask.GradientStops.Add(new GradientStop(Color.FromArgb(255, 255, 255, 255), 0.72d));
        mask.GradientStops.Add(new GradientStop(Color.FromArgb(140, 255, 255, 255), 0.90d));
        mask.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 1d));
        mask.Freeze();
        image.OpacityMask = mask;

        // 落到界面上以后打一条尺寸日志：下次「看不见图」时能一眼分清是没创建、被裁成 0，还是太淡
        image.Loaded += (_, _) => _log?.Info(
            $"装饰已就位：实际 {image.ActualWidth:F0}×{image.ActualHeight:F0}" +
            $"（期望 {DecorationWidth:F0}×{DecorationHeight:F0}），不透明度 {image.Opacity:F2}");

        return image;
    }

    /// <summary>从 DLL 清单流里读装饰图（OnLoad + 冻结，避免占着流）。</summary>
    private BitmapSource? LoadImage()
    {
        try
        {
            using var stream = typeof(FadedGrayThemePlugin).Assembly
                .GetManifestResourceStream(ImageResourceName);
            if (stream is null)
            {
                _log?.Warn($"找不到内嵌图片资源：{ImageResourceName}");
                return null;
            }

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.StreamSource = stream;
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception ex)
        {
            _log?.Error("读取内嵌装饰图失败", ex);
            return null;
        }
    }
}
