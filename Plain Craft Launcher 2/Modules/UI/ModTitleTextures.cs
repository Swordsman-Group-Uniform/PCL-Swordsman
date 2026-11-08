using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PCL;

/// <summary>
///     贴图类主题（下界红 / 水晶紫 / 曜石黑 / 蜂蜜黄 / 黏液绿）用的原版方块贴图。
///     <para>
///         贴图都放在 <c>Images/Blocks/</c> 下，由 <c>tools/extract-vanilla-textures.py</c>
///         从原版客户端 jar 里解出。它们是 **Mojang 的美术资源，不进公开仓库**
///         （<c>.gitignore</c> 里逐条排除），所以干净 clone 里这些文件多半不存在 ——
///         因此这里**不把 pack URI 写死在 XAML 里**，而是运行时加载：缺哪张就返回
///         <see langword="null" />，对应那一层装饰留空，窗口照常起来。
///     </para>
///     <para>
///         加载成功后做成 16×16 绝对视口的平铺 <see cref="ImageBrush" />（原尺寸、不插值放大），
///         并冻结缓存 —— 主题切换时反复取用也不会重复解码。
///     </para>
/// </summary>
internal static class ModTitleTextures
{
    /// <summary>贴图在程序集中的目录（相对项目根）。</summary>
    private const string TextureFolder = "Images/Blocks/";

    /// <summary>原版方块贴图是 16×16，平铺视口按原尺寸走，不做插值放大。</summary>
    private const double TileSize = 16d;

    private static readonly Dictionary<string, Brush?> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     取一张方块贴图的平铺画刷（<c>Glass.png</c> 这种文件名，不带目录）。
    ///     贴图缺失时返回 <see langword="null" />，调用方跳过这一层即可。
    /// </summary>
    public static Brush? GetTiled(string fileName)
    {
        if (Cache.TryGetValue(fileName, out var cached)) return cached;

        var brush = BuildBrush(fileName);
        Cache[fileName] = brush;
        return brush;
    }

    /// <summary>取一张方块贴图的位图（<c>Glass.png</c> 这种文件名）。加载失败返回 null。</summary>
    public static BitmapSource? GetBitmap(string fileName)
    {
        var bitmap = LoadBitmap(TextureFolder + fileName);
        if (bitmap is null)
            ModBase.Log($"[Theme] 原版贴图缺失：{TextureFolder}{fileName}（用 tools/extract-vanilla-textures.py 从客户端 jar 里解出）",
                ModBase.LogLevel.Debug);
        return bitmap;
    }

    /// <summary>
    ///     取一张方块贴图的 BGRA32 像素（16×16，行宽 <c>Width*4</c>）。
    ///     用于 <see cref="BuildMosaic" /> 逐块拼图；贴图缺失返回 <see langword="null" />。
    /// </summary>
    public static TilePixels? GetPixels(string fileName)
    {
        if (PixelCache.TryGetValue(fileName, out var cached)) return cached;

        TilePixels? result = null;
        var bitmap = GetBitmap(fileName);
        if (bitmap is not null)
        {
            var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0d);
            converted.Freeze();
            var width = converted.PixelWidth;
            var height = converted.PixelHeight;
            var stride = width * 4;
            var buffer = new byte[stride * height];
            converted.CopyPixels(buffer, stride, 0);
            result = new TilePixels(width, height, stride, buffer);
        }

        PixelCache[fileName] = result;
        return result;
    }

    /// <summary>一张贴图的像素数据（BGRA32）。</summary>
    public sealed record TilePixels(int Width, int Height, int Stride, byte[] Bgra);

    private static readonly Dictionary<string, TilePixels?> PixelCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     把若干张贴图**随机密铺**成一张 <paramref name="width" />×<paramref name="height" /> 的位图。
    ///     <para>
    ///         用户要求：贴图要随机混在一起，但必须**密铺** —— 一个个 16×16 的方块严丝合缝地铺满，
    ///         **不重叠、不留缝、不错位**（不是按比例分带，也不是叠着乱砌）。
    ///         所以这里就是「整齐的 16×16 网格 + 每格按权重随机挑一张贴图」。
    ///         <paramref name="layers" /> 里的权重只决定**出现概率**（例如 40/40/10/10）。
    ///     </para>
    ///     <para>
    ///         用 <paramref name="seed" /> 固定随机数，同一主题同一尺寸的结果稳定（窗口拖动重算时不会闪）。
    ///     </para>
    /// </summary>
    public static BitmapSource? BuildMosaic(double width, double height,
        IReadOnlyList<(string FileName, double Weight)> layers, int seed = 20261108)
    {
        var pixelWidth = (int)Math.Ceiling(Math.Max(0d, width));
        var pixelHeight = (int)Math.Ceiling(Math.Max(0d, height));
        if (pixelWidth < 4 || pixelHeight < 4) return null;

        var tiles = layers
            .Select(layer => (Layer: layer, Pixels: GetPixels(layer.FileName)))
            .Where(item => item.Pixels is not null && item.Layer.Weight > 0d)
            .Select(item => (item.Layer.Weight, item.Layer.FileName, item.Pixels!))
            .ToArray();
        if (tiles.Length == 0) return null;

        var stride = pixelWidth * 4;
        var canvas = new byte[stride * pixelHeight];
        var random = new Random(seed);

        var tileSize = (int)TileSize; // 16

        // 16×16 网格逐格填满：每格只铺一次，位置严格对齐（不重叠、不留缝）
        for (var y = 0; y < pixelHeight; y += tileSize)
        for (var x = 0; x < pixelWidth; x += tileSize)
            Blit(canvas, stride, pixelWidth, pixelHeight, Pick(tiles, random), x, y);

        var bitmap = BitmapSource.Create(pixelWidth, pixelHeight, 96d, 96d,
            PixelFormats.Bgra32, null, canvas, stride);
        bitmap.Freeze();
        return bitmap;
    }

    /// <summary>按权重随机挑一张贴图。</summary>
    private static TilePixels Pick((double Weight, string FileName, TilePixels Pixels)[] tiles, Random random)
    {
        var total = tiles.Sum(tile => tile.Weight);
        var roll = random.NextDouble() * total;
        foreach (var tile in tiles)
        {
            roll -= tile.Weight;
            if (roll <= 0d) return tile.Pixels;
        }

        return tiles[^1].Pixels;
    }

    /// <summary>把一张 16×16 贴图按 src-over 混合贴到画布的 (offsetX, offsetY)（允许越界，自动裁剪）。</summary>
    private static void Blit(byte[] canvas, int dstStride, int dstWidth, int dstHeight, TilePixels tile,
        int offsetX, int offsetY)
    {
        for (var y = 0; y < tile.Height; y++)
        {
            var dy = offsetY + y;
            if (dy < 0 || dy >= dstHeight) continue;

            for (var x = 0; x < tile.Width; x++)
            {
                var dx = offsetX + x;
                if (dx < 0 || dx >= dstWidth) continue;

                var si = y * tile.Stride + x * 4;
                var di = dy * dstStride + dx * 4;
                var alpha = tile.Bgra[si + 3];

                if (alpha == 255)
                {
                    canvas[di] = tile.Bgra[si];
                    canvas[di + 1] = tile.Bgra[si + 1];
                    canvas[di + 2] = tile.Bgra[si + 2];
                    canvas[di + 3] = 255;
                }
                else if (alpha > 0)
                {
                    // 半透明贴图（黏液块那种）：普通 src-over
                    double a = alpha / 255d;
                    double d = canvas[di + 3] / 255d;
                    var outA = a + d * (1d - a);
                    if (outA <= 0d) continue;
                    canvas[di] = (byte)Math.Round((tile.Bgra[si] * a + canvas[di] * d * (1d - a)) / outA);
                    canvas[di + 1] = (byte)Math.Round((tile.Bgra[si + 1] * a + canvas[di + 1] * d * (1d - a)) / outA);
                    canvas[di + 2] = (byte)Math.Round((tile.Bgra[si + 2] * a + canvas[di + 2] * d * (1d - a)) / outA);
                    canvas[di + 3] = (byte)Math.Round(outA * 255d);
                }
            }
        }
    }

    /// <summary>
    ///     把混铺结果做成一条铺满整个矩形的画刷（<see cref="TileMode.None" />，
    ///     因为图本身就是按目标尺寸拼好的）。
    /// </summary>
    public static Brush? BuildMosaicBrush(double width, double height,
        IReadOnlyList<(string FileName, double Weight)> layers, int seed = 20261108)
    {
        var bitmap = BuildMosaic(width, height, layers, seed);
        if (bitmap is null) return null;

        var brush = new ImageBrush(bitmap) { Stretch = Stretch.Fill, TileMode = TileMode.None };
        brush.Freeze();
        return brush;
    }

    private static Brush? BuildBrush(string fileName)
    {
        var bitmap = GetBitmap(fileName);
        if (bitmap is null) return null;

        var brush = new ImageBrush(bitmap)
        {
            TileMode = TileMode.Tile,
            Viewport = new System.Windows.Rect(0d, 0d, TileSize, TileSize),
            ViewportUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.Fill
        };
        brush.Freeze();
        return brush;
    }

    /// <summary>
    ///     加载内嵌位图。三种写法依次兜底，因为单文件发布（PublishSingleFile）下
    ///     「相对 pack URI」不保证能解析（同 <see cref="ModTitlePetals" />）。
    /// </summary>
    private static BitmapSource? LoadBitmap(string resourcePath)
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
            ModBase.Log(ex, $"[Theme] 加载原版贴图 {resourcePath} 失败", ModBase.LogLevel.Debug);
            return null;
        }
    }

    /// <summary>清空缓存（插件重载 / 主题重扫后重新读贴图）。</summary>
    public static void ClearCache() => Cache.Clear();

    /// <summary>贴图文件是否真的存在（日志 / 诊断用）。</summary>
    public static bool Exists(string fileName) => LoadBitmap(TextureFolder + fileName) is not null;
}
