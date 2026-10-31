using System;
using System.Runtime.InteropServices;

namespace PCL.Core.Utils.OS.Window;

/// <summary>
///     Windows 11 系统背景材质（Mica / Acrylic）与 Windows 10 模糊后端的互操作封装。
/// </summary>
public static partial class DwmInterop
{
    private const int DwmwaSystemBackdropType = 38;
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaWindowCornerPreference = 33;

    /// <summary>窗口圆角偏好（Windows 11 build 22000 起）。</summary>
    public enum WindowCornerPreference
    {
        /// <summary>跟随系统默认。</summary>
        Default = 0,
        /// <summary>不要圆角（直角）。</summary>
        DoNotRound = 1,
        /// <summary>圆角（系统自带抗锯齿）。</summary>
        Round = 2,
        /// <summary>小圆角。</summary>
        RoundSmall = 3
    }

    /// <summary>
    ///     设置窗口圆角偏好。系统不支持（Win10 及更早）时返回 <see langword="false" />，由调用方退回窗口区域。
    /// </summary>
    public static bool TrySetWindowCornerPreference(IntPtr hwnd, WindowCornerPreference preference)
    {
        if (!IsCornerPreferenceSupported) return false;
        try
        {
            var value = (int)preference;
            return DwmSetWindowAttribute(hwnd, DwmwaWindowCornerPreference, ref value, sizeof(int)) == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>系统是否支持设置窗口圆角（Windows 11 build 22000+）。</summary>
    public static bool IsCornerPreferenceSupported => Environment.OSVersion.Version.Build >= 22000;

    /// <summary>系统背景材质类型（Win11 22H2 / build 22621 起）。</summary>
    public enum BackdropType
    {
        None = 1,
        MainWindow = 2, // 云母 Mica
        TransientWindow = 3, // 亚克力 Acrylic
        TabbedWindow = 4 // Mica Alt
    }

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    /// <summary>尝试设置系统背景材质；系统不支持时返回 false（不抛异常）。</summary>
    public static bool TrySetBackdropType(IntPtr hwnd, BackdropType type)
    {
        try
        {
            var value = (int)type;
            return DwmSetWindowAttribute(hwnd, DwmwaSystemBackdropType, ref value, sizeof(int)) == 0;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    /// <summary>沉浸式深色标题栏（供深色模式下跟随主题）。</summary>
    public static bool TrySetImmersiveDarkMode(IntPtr hwnd, bool dark)
    {
        try
        {
            var value = dark ? 1 : 0;
            return DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref value, sizeof(int)) == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>系统是否支持 Mica/Acrylic 背景（Win11 build 22621+）。</summary>
    public static bool IsBackdropSupported => Environment.OSVersion.Version.Build >= 22621;

    private const int DwmwaBorderColor = 34;
    private const int DwmColorDefault = unchecked((int)0xFFFFFFFF);
    private const int DwmColorNone = unchecked((int)0xFFFFFFFE);

    /// <summary>
    ///     开关 Windows 11 画在窗口最外圈的那条 1px 描边（`DWMWA_BORDER_COLOR`，build 22000 起）。
    ///     <para>
    ///         系统描边是沿**窗口矩形**画的：我们用自己的窗口区域裁了圆角，这条矩形描边会和圆角对不上，
    ///         在材质模式下看起来就像「界面外面还多一圈边」。所以材质生效时把它关掉，关闭材质时还原。
    ///         Windows 10 上这个属性不存在，直接返回 <see langword="false" />。
    ///     </para>
    /// </summary>
    public static bool TrySetWindowBorder(IntPtr hwnd, bool visible)
    {
        if (hwnd == IntPtr.Zero || !IsCornerPreferenceSupported) return false;
        try
        {
            var value = visible ? DwmColorDefault : DwmColorNone;
            return DwmSetWindowAttribute(hwnd, DwmwaBorderColor, ref value, sizeof(int)) == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
