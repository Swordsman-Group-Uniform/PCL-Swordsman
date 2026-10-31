using System.Windows;
using System.Windows.Media;

namespace PCL;

/// <summary>
///     页面级的「功能不可用」占位：复用 PCL 加载控件 MyLoading 的错误态图形与动画
///     （红色镐子 + 红叉 + 叉下方横杠，与 Minecraft 下载报错页完全一致）。
///     用于已被本分支禁用、但仍在界面上可见的页面（反馈、更新）。
/// </summary>
public partial class MyUnavailablePlaceholder
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string),
        typeof(MyUnavailablePlaceholder), new PropertyMetadata("", (d, e) =>
        {
            var control = (MyUnavailablePlaceholder)d;
            if (control.LabText is not null)
                control.LabText.Text = (string)e.NewValue;
        }));

    /// <summary>
    ///     图标与文字的前景色，入场动画会把它从主题色渐变到 <c>ColorBrushRedLight</c>。
    /// </summary>
    public SolidColorBrush Foreground
    {
        get => (SolidColorBrush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public static readonly DependencyProperty ForegroundProperty =
        DependencyProperty.Register("Foreground", typeof(SolidColorBrush), typeof(MyUnavailablePlaceholder));

    private readonly int _uuid = ModBase.GetUuid();

    public MyUnavailablePlaceholder()
    {
        InitializeComponent();
        // 初始为主题色，图标会在加载后渐变到 ColorBrushRedLight（与 MyLoading 错误态一致）
        SetResourceReference(ForegroundProperty, "ColorBrush3");
        Loaded += (_, _) => StartErrorAnimation();
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>
    ///     直接照搬 <see cref="MyLoading" /> 错误态的入场动画：整体颜色 300ms 渐变为
    ///     <c>ColorBrushRedLight</c>，随后红叉淡入（100ms）并从 0.6 倍弹性放大到原尺寸（400ms，EaseOutBack）。
    ///     与 Minecraft 下载报错页使用的是同一套 ModAnimation 动画。
    /// </summary>
    private void StartErrorAnimation()
    {
        ModAnimation.AniStart(
            new[]
            {
                ModAnimation.AaColor(PanBack, ForegroundProperty, "ColorBrushRedLight", 300),
                ModAnimation.AaOpacity(PathCross, 1d - PathCross.Opacity, 100, 300),
                ModAnimation.AaScaleTransform(PathCross,
                    1d - ((ScaleTransform)PathCross.RenderTransform).ScaleX, 400, 300,
                    new ModAnimation.AniEaseOutBack())
            }, "Unavailable Placeholder " + _uuid);
    }
}
