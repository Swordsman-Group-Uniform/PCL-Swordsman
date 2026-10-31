using System;
using System.Runtime.InteropServices;
using System.ComponentModel;

namespace PCL.Core.Utils.OS;

public static partial class WindowInterop
{
    // ReSharper disable InconsistentNaming UnusedMember.Local

    // DWM 外边缘结构定义
    [StructLayout(LayoutKind.Sequential)]
    private struct MARGINS { public int leftWidth, rightWidth, topHeight, bottomHeight; }

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmIsCompositionEnabled([MarshalAs(UnmanagedType.Bool)] out bool pfEnabled);

    // Win32 矩形结构定义
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int left; public int top; public int right; public int bottom; }

    // Win32 点结构定义
    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    // MONITOR_DPI_TYPE enum
    private enum MONITOR_DPI_TYPE {
        MDT_EFFECTIVE_DPI = 0,
        MDT_ANGULAR_DPI = 1,
        MDT_RAW_DPI = 2,
        MDT_DEFAULT = MDT_EFFECTIVE_DPI
    }

    [LibraryImport("user32.dll")]
    private static partial IntPtr MonitorFromWindow(IntPtr hWnd, uint dwFlags);

    // Get the primary monitor handle
    private const int MONITOR_DEFAULTTOPRIMARY = 1;

    [LibraryImport("shcore.dll", EntryPoint = "GetDpiForMonitor")]
    private static partial int GetDpiForMonitor(
        IntPtr hMonitor,
        MONITOR_DPI_TYPE dpiType,
        out uint dpiX,
        out uint dpiY
    );

    [LibraryImport("gdi32.dll", SetLastError = true)]
    private static partial IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int ellipseWidth,
        int ellipseHeight);

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, [MarshalAs(UnmanagedType.Bool)] bool bRedraw);

    [LibraryImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteObject(IntPtr hObject);

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial int GetWindowRgn(IntPtr hWnd, IntPtr hRgn);

    [LibraryImport("gdi32.dll", SetLastError = true)]
    private static partial int GetRgnBox(IntPtr hrgn, out RECT lprc);

    // ReSharper enable InconsistentNaming UnusedMember.Local

    /// <summary>
    /// 检测 DWM 组合是否可用
    /// </summary>
    public static bool IsCompositionEnabled()
    {
        var hResult = DwmIsCompositionEnabled(out var enabled);
        return hResult != 0 ? throw new Win32Exception(hResult, "Failed to check DWM status") : enabled;
    }

    /// <summary>
    /// 把整个窗口裁成圆角矩形（尺寸单位为物理像素）。<br/>
    /// 用于「系统后台模糊（云母 / Aero）按窗口矩形绘制」时把磨砂区裁到界面本体的圆角形状上，
    /// 否则磨砂会比界面多出一圈直角边。
    /// </summary>
    /// <param name="hWnd">窗口句柄</param>
    /// <param name="width">窗口宽度（物理像素）</param>
    /// <param name="height">窗口高度（物理像素）</param>
    /// <param name="radius">圆角半径（物理像素）</param>
    public static bool SetRoundedWindowRegion(IntPtr hWnd, int width, int height, int radius)
    {
        if (hWnd == IntPtr.Zero || width <= 0 || height <= 0) return false;
        radius = Math.Clamp(radius, 0, Math.Min(width, height) / 2);

        var region = radius <= 0
            ? CreateRoundRectRgn(0, 0, width + 1, height + 1, 1, 1)
            : CreateRoundRectRgn(0, 0, width + 1, height + 1, radius * 2, radius * 2);
        if (region == IntPtr.Zero) return false;

        // 成功时窗口会接管该区域（由系统释放），失败才需要自己删掉
        if (SetWindowRgn(hWnd, region, true) == 0)
        {
            DeleteObject(region);
            return false;
        }

        return true;
    }

    /// <summary>
    ///     按**客户区**把窗口裁成圆角矩形。
    ///     <para>
    ///         必须用客户区（`GetClientRect` + `ClientToScreen` 求偏移）而不是 WPF 的 `ActualWidth`：
    ///         `SetWindowRgn` 的坐标是**窗口坐标系（含非客户区）**，直接用客户端 DIP 尺寸会因为窗口边框偏移
    ///         而多裁 / 错位一圈 —— 表现出来就是「磨砂区比界面大」或「边缘多出一条重叠的边框」。
    ///     </para>
    /// </summary>
    /// <param name="hWnd">窗口句柄</param>
    /// <param name="radius">圆角半径（物理像素）</param>
    /// <param name="inset">客户区内缩量（物理像素，0 表示整个客户区）</param>
    public static bool SetClientAreaRoundedRegion(IntPtr hWnd, int radius, int inset = 0)
    {
        if (hWnd == IntPtr.Zero) return false;
        if (!GetWindowRect(hWnd, out var windowRect)) return false;
        if (!GetClientRect(hWnd, out var clientRect)) return false;

        var origin = new POINT();
        if (!ClientToScreen(hWnd, ref origin)) return false;

        var offsetX = origin.X - windowRect.left;
        var offsetY = origin.Y - windowRect.top;
        var width = clientRect.right - clientRect.left;
        var height = clientRect.bottom - clientRect.top;
        if (width <= 0 || height <= 0) return false;

        // 内缩不能吃光整个客户区
        inset = Math.Clamp(inset, 0, Math.Max(0, Math.Min(width, height) / 2 - 1));
        var left = offsetX + inset;
        var top = offsetY + inset;
        width -= inset * 2;
        height -= inset * 2;
        if (width <= 2 || height <= 2) return false;

        radius = Math.Clamp(radius, 0, Math.Min(width, height) / 2);
        var region = CreateRoundRectRgn(left, top, left + width + 1, top + height + 1,
            Math.Max(1, radius * 2), Math.Max(1, radius * 2));
        if (region == IntPtr.Zero) return false;

        if (SetWindowRgn(hWnd, region, true) == 0)
        {
            DeleteObject(region);
            return false;
        }

        return true;
    }

    /// <summary>
    ///     读回当前窗口区域的外接矩形（诊断用）：证明区域到底设成什么样了。
    /// </summary>
    public static bool TryGetWindowRegionBox(IntPtr hWnd, out int left, out int top, out int right, out int bottom)
    {
        left = top = right = bottom = 0;
        if (hWnd == IntPtr.Zero) return false;

        // 先建一个空区域占位，GetWindowRgn 会把窗口区域复制进去
        var region = CreateRectRgn(0, 0, 0, 0);
        if (region == IntPtr.Zero) return false;
        try
        {
            var type = GetWindowRgn(hWnd, region);
            if (type == 0) return false; // ERROR：没有区域

            if (GetRgnBox(region, out var box) == 0) return false;
            left = box.left;
            top = box.top;
            right = box.right;
            bottom = box.bottom;
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            DeleteObject(region);
        }
    }

    [LibraryImport("gdi32.dll", SetLastError = true)]
    private static partial IntPtr CreateRectRgn(int x1, int y1, int x2, int y2);

    /// <summary>取消窗口区域裁剪（恢复成普通矩形窗口）。</summary>
    public static void ClearWindowRegion(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return;
        SetWindowRgn(hWnd, IntPtr.Zero, true);
    }

    /// <summary>
    /// 设置 DWM 窗口边框到客户区域的扩展大小
    /// </summary>
    public static void ExtendFrameIntoClientArea(
        IntPtr hWnd, int marginLeft, int marginTop, int marginRight, int marginBottom)
    {
        MARGINS margins = new()
        {
            leftWidth = marginLeft,
            rightWidth = marginRight,
            topHeight = marginTop,
            bottomHeight = marginBottom
        };
        if (!IsCompositionEnabled()) return;
        var hResult = DwmExtendFrameIntoClientArea(hWnd, ref margins);
        if (hResult != 0) throw new Win32Exception(hResult, "Failed to extend frame into client area");
    }

    /// <summary>
    /// See <see cref="ExtendFrameIntoClientArea(IntPtr, int, int, int, int)"/>
    /// </summary>
    public static void ExtendFrameIntoClientArea(IntPtr hWnd, int margin)
        => ExtendFrameIntoClientArea(hWnd, margin, margin, margin, margin);

    /// <summary>
    /// 获取 Win32 窗口矩形定义
    /// </summary>
    public static (int Left, int Top, int Right, int Bottom) GetWindowRectangle(IntPtr hWnd)
    {
        var hResult = GetWindowRect(hWnd, out var rect);
        return hResult ? (rect.left, rect.top, rect.right, rect.bottom)
            : throw new Win32Exception("Failed to get window rectangle");
    }

    /// <summary>
    /// 获取 Win32 窗口位置与大小
    /// </summary>
    public static (int X, int Y, int Width, int Height) ToWindowBounds(
        this (int Left, int Top, int Right, int Bottom) rect)
    {
        var (l, t, r, b) = rect;
        var x = l;
        var y = t;
        var width = r - l;
        var height = b - t;
        return (x, y, width, height);
    }

    /// <summary>
    /// 获取指定屏幕的系统 DPI
    /// </summary>
    /// <param name="hWnd">位于指定屏幕上的任意窗口句柄，默认指定主屏</param>
    public static int GetSystemDpi(IntPtr hWnd = 0) {
        // Get the monitor handle
        var hMonitor = MonitorFromWindow(hWnd, MONITOR_DEFAULTTOPRIMARY);
        // 0 is S_OK
        var hr = GetDpiForMonitor(hMonitor, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, out var dpiX, out _);
        if (hr == 0)
            return (int)dpiX;
        // fallback to default DPI (96)
        return 96;
    }
}
