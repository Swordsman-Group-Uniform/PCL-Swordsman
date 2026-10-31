using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using PCL.Core.Logging;

namespace PCL;

/// <summary>
///     壁纸磨砂：把窗口内的壁纸（<see cref="FormMain.ImgBack" />）整体模糊掉，让后面的东西「糊」起来。
///     <para>
///         **谁在用**：液态玻璃（整块玻璃后面要有被糊过的画面）。云母 / Aero 在 Windows 10 上走
///         系统 AccentBlur（见 <c>MaterialService</c>），不再需要这一层。
///     </para>
///     <para>
///         之所以抽成共享工具：两处都要改 <c>ImgBack.Effect</c> 与 <c>Margin</c>（用负边距盖住模糊发散出的边缘），
///         各自记一份「原值」会在互相切换时把对方的还原值覆盖掉，出现「切回普通模式壁纸还是糊的」。
///         这里只保留一份原值，<see cref="Apply" /> / <see cref="Clear" /> 都是幂等的。
///     </para>
/// </summary>
internal static class ModWallpaperFrost
{
    private static bool _hasOriginals;
    private static Effect? _originalEffect;
    private static Thickness _originalMargin;
    private static bool _active;
    private static double _radius;

    /// <summary>当前是否已经糊了壁纸。</summary>
    public static bool IsActive => _active;

    /// <summary>
    ///     打开（或更新）壁纸磨砂。<paramref name="blurRadius" /> 为 0 或没有壁纸时自动 <see cref="Clear" />。
    /// </summary>
    public static void Apply(double blurRadius)
    {
        try
        {
            var imgBack = ModMain.frmMain?.ImgBack;
            if (imgBack is null) return;

            // 视频背景不糊（糊了也看不出来，还白烧 GPU）
            if (imgBack.Background is null)
            {
                Clear();
                return;
            }

            if (blurRadius <= 0.01d)
            {
                Clear();
                return;
            }

            if (!_hasOriginals)
            {
                _originalEffect = imgBack.Effect;
                _originalMargin = imgBack.Margin;
                _hasOriginals = true;
            }

            // 已经糊过同一等级就不重复设置（避免每帧重建 Effect）
            if (_active && Math.Abs(_radius - blurRadius) < 0.01d) return;

            _radius = blurRadius;
            _active = true;
            imgBack.Effect = new BlurEffect { Radius = blurRadius };
            // 模糊会把边缘「发散」出去，负边距把那圈糊边塞到窗口外面
            imgBack.Margin = new Thickness(-blurRadius / 1.8d);
        }
        catch (Exception ex)
        {
            LogWrapper.Warn("Material", "应用壁纸磨砂失败：" + ex.Message);
        }
    }

    /// <summary>还原壁纸（幂等）。</summary>
    public static void Clear()
    {
        if (!_active && !_hasOriginals) return;

        try
        {
            var imgBack = ModMain.frmMain?.ImgBack;
            if (imgBack is not null && _hasOriginals)
            {
                imgBack.Effect = _originalEffect;
                imgBack.Margin = _originalMargin;
            }
        }
        catch (Exception ex)
        {
            LogWrapper.Warn("Material", "还原壁纸磨砂失败：" + ex.Message);
        }
        finally
        {
            _hasOriginals = false;
            _active = false;
            _radius = 0d;
            _originalEffect = null;
            _originalMargin = default;
        }
    }
}
