using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using PCL.Core.UI;
using PCL.Core.UI.Controls.SvgIcon;

namespace PCL;

internal static class SvgIconControlHelper
{
    internal static bool HasSvgIcon(string? icon)
    {
        return !string.IsNullOrWhiteSpace(icon);
    }

    /// <summary>
    ///     图标名在 <c>PCL.Core</c> 的内置图标包里是否真的存在。
    ///     <para>
    ///         <see cref="HasSvgIcon" /> 只判非空，写错名字（如 <c>lucide/settings-2</c>）不会报错，
    ///         而是**渲染成空白**。插件清单里给的 <c>ui.icon</c> 来自外部，必须用它校验后兜底。
    ///     </para>
    /// </summary>
    internal static bool IconExists(string? icon)
    {
        if (string.IsNullOrWhiteSpace(icon))
            return false;

        var text = icon.Trim();
        var pack = "default";
        var name = text;
        var slash = text.IndexOf('/');
        if (slash > 0)
        {
            pack = text[..slash];
            name = text[(slash + 1)..];
        }

        if (name.Length == 0) return false;

        try
        {
            var uri = new Uri(
                $"pack://application:,,,/PCL.Core;component/UI/Assets/IconPacks/{pack}/{name}.svg",
                UriKind.Absolute);
            return Application.GetResourceStream(uri) is not null;
        }
        catch
        {
            return false;
        }
    }

    internal static void ApplyVisibility(
        Path legacyIcon,
        SvgIcon svgIcon,
        bool useSvgIcon)
    {
        legacyIcon.Visibility = useSvgIcon ? Visibility.Collapsed : Visibility.Visible;
        svgIcon.Visibility = useSvgIcon ? Visibility.Visible : Visibility.Collapsed;
    }

    internal static void ApplyIcon(Path legacyIcon, SvgIcon svgIcon, string? svgIconName)
    {
        var useSvgIcon = HasSvgIcon(svgIconName);
        svgIcon.Icon = svgIconName ?? string.Empty;
        ApplyVisibility(legacyIcon, svgIcon, useSvgIcon);
    }

    internal static void SetIconBrush(Path legacyIcon, SvgIcon svgIcon, bool useSvgIcon, Brush brush)
    {
        if (useSvgIcon)
            svgIcon.IconBrush = brush;
        else
            legacyIcon.Fill = brush;
    }

    internal static void SetIconResource(Path legacyIcon, SvgIcon svgIcon, bool useSvgIcon, string resourceKey)
    {
        if (useSvgIcon)
            svgIcon.SetResourceReference(SvgIcon.IconBrushProperty, resourceKey);
        else
            legacyIcon.SetResourceReference(Shape.FillProperty, resourceKey);
    }

    internal static void AnimateSvgIconBrushTo(
        SvgIcon svgIcon,
        string resourceKey,
        int duration,
        string? animationKey = null)
    {
        if (svgIcon.Visibility == Visibility.Visible)
            svgIcon.AnimateIconBrushTo(
                ResolveResourceColor(resourceKey),
                TimeSpan.FromMilliseconds(duration),
                animationKey: animationKey);
    }

    internal static void AnimateSvgIconBrushTo(
        SvgIcon svgIcon,
        ModBase.MyColor color,
        int duration,
        string? animationKey = null)
    {
        if (svgIcon.Visibility == Visibility.Visible)
            svgIcon.AnimateIconBrushTo(
                new NColor((Color)color),
                TimeSpan.FromMilliseconds(duration),
                animationKey: animationKey);
    }

    private static NColor ResolveResourceColor(string resourceKey)
    {
        if (!ThemeManager.AppResources.Contains(resourceKey))
            return new NColor(resourceKey);

        var resource = ThemeManager.AppResources[resourceKey];
        return resource switch
        {
            SolidColorBrush brush => new NColor(brush),
            Color color => new NColor(color),
            Brush brush => new NColor(brush),
            _ => new NColor(resourceKey)
        };
    }
}