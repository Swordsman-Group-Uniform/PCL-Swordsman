using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Media3D;
using PCL.Core.Logging;

namespace PCL;

/// <summary>
///     高级材质的「悬停发光」：鼠标悬停在按钮、勾选框、列表项等控件上时，给控件加一圈柔和的外发光。
///     <para>
///         半径与透明度取自配置项 <c>UiMaterialGlowRadius</c> / <c>UiMaterialGlowOpacity</c>，
///         半径或透明度为 0、或插件未启用时完全不加效果；颜色跟随当前主题的强调色（<c>ColorBrush3</c>）。
///     </para>
///     <para>
///         实现方式：在**主窗口上**挂一个 <c>MouseMove</c> 处理器，从 <c>OriginalSource</c> 沿视觉树向上找最近的
///         可发光控件。这样页面里动态创建的控件同样生效，也不依赖 `Loaded` 事件的类处理器。
///     </para>
/// </summary>
internal static class MaterialGlow
{
    /// <summary>当前正在发光的控件（一次只可能有一个：鼠标只能悬停在一个控件上）。</summary>
    private static FrameworkElement? _current;

    private static bool _isInstalled;

    /// <summary>在主窗口上挂全局鼠标处理器（只生效一次）。</summary>
    public static void Install(Window window)
    {
        if (_isInstalled) return;
        _isInstalled = true;
        try
        {
            // MouseMove 是冒泡事件，handledEventsToo 保证即使子控件把事件标了 Handled 也能收到
            window.AddHandler(Mouse.MouseMoveEvent, new MouseEventHandler(OnMouseMove), true);
            window.MouseLeave += OnWindowMouseLeave;
            window.Deactivated += (_, _) => ClearGlow();
            window.Closed += (_, _) => ClearGlow();
        }
        catch (Exception ex)
        {
            LogWrapper.Warn("Material", "注册悬停发光失败：" + ex.Message);
        }
    }

    /// <summary>参数或功能开关变化后重新计算：关闭时立刻清掉，开着时按新参数重画当前悬停的控件。</summary>
    public static void Refresh()
    {
        if (!ModMaterial.IsGlowEnabled)
        {
            ClearGlow();
            return;
        }

        if (_current is null) return;
        if (_current.IsLoaded && _current.IsVisible && _current.IsMouseOver)
            ApplyGlow(_current);
        else
            ClearGlow();
    }

    /// <summary>清掉当前发光（禁用功能、窗口失去焦点时使用）。</summary>
    public static void ClearGlow()
    {
        if (_current is null) return;
        // 只撤自己写进去的那个实例：控件自带 DropShadowEffect 时不能一并抹掉
        if (_ownEffect is not null && ReferenceEquals(_current.Effect, _ownEffect))
            _current.Effect = null;
        _ownEffect = null;
        _current = null;
    }

    private static void OnWindowMouseLeave(object sender, MouseEventArgs e) => ClearGlow();

    private static void OnMouseMove(object sender, MouseEventArgs e)
    {
        var hovered = FindGlowTarget(e.OriginalSource as DependencyObject);
        if (ReferenceEquals(hovered, _current)) return;

        ClearGlow();
        if (hovered is null) return;

        _current = hovered;
        ApplyGlow(hovered);
    }

    /// <summary>从鼠标命中的元素沿视觉树向上找最近的可发光控件；找不到返回 <see langword="null" />。</summary>
    private static FrameworkElement? FindGlowTarget(DependencyObject? node)
    {
        var depth = 0;
        while (node is not null && depth++ < 64)
        {
            if (node is FrameworkElement element && ShouldGlow(element)) return element;
            node = GetParent(node);
        }

        return null;
    }

    private static DependencyObject? GetParent(DependencyObject node)
    {
        try
        {
            return node switch
            {
                Visual or Visual3D => VisualTreeHelper.GetParent(node),
                _ => LogicalTreeHelper.GetParent(node)
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>只给交互型控件加发光，避免给布局容器与文本块加 Effect（会很卡也很难看）。</summary>
    private static bool ShouldGlow(FrameworkElement element) => element switch
    {
        MyButton => true,
        MyIconButton => true,
        MyIconTextButton => true,
        MyTextButton => true,
        MyExtraButton => true,
        MyExtraTextButton => true,
        MyRadioButton => true,
        MyRadioBox => true,
        MyCheckBox => true,
        MyListItem => true,
        _ => false
    };

    /// <summary>我们自己写进控件的那份发光效果（用于精确还原）。</summary>
    private static DropShadowEffect? _ownEffect;

    private static void ApplyGlow(FrameworkElement element)
    {
        if (!ModMaterial.IsGlowEnabled)
        {
            ClearGlow();
            return;
        }

        // 别人已经设置过 Effect 的控件（壁纸模糊、自定义阴影等）不要去抢
        if (element.Effect is not null and not DropShadowEffect)
            return;

        try
        {
            var effect = new DropShadowEffect
            {
                Color = AccentColor(element),
                BlurRadius = ModMaterial.GlowRadius,
                ShadowDepth = 0d,
                Direction = 0d,
                Opacity = ModMaterial.GlowOpacity,
                RenderingBias = RenderingBias.Performance
            };
            element.Effect = effect;
            _ownEffect = effect; // 只清自己这份，别抹掉控件自带的阴影
        }
        catch (Exception ex)
        {
            LogWrapper.Warn("Material", "应用悬停发光失败：" + ex.Message);
        }
    }

    /// <summary>发光颜色跟随主题强调色；取不到时退化为白色。</summary>
    private static Color AccentColor(FrameworkElement element)
    {
        try
        {
            if (element.TryFindResource("ColorBrush3") is SolidColorBrush brush) return brush.Color;
            if (System.Windows.Application.Current?.TryFindResource("ColorBrush3") is SolidColorBrush appBrush)
                return appBrush.Color;
        }
        catch
        {
            // 忽略资源查询失败
        }

        return Colors.White;
    }
}
