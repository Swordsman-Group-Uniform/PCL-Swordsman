using System;
using System.Runtime.InteropServices;
using PCL.Core.Logging;

namespace PCL.Core.Utils.OS.Window;

/// <summary>高级材质效果。</summary>
public enum MaterialKind
{
    None = 0,
    /// <summary>Windows 11 云母（Mica）/ 亚克力。</summary>
    Mica = 1,
    /// <summary>Windows Vista/10 风格的整体模糊（Aero）。</summary>
    Aero = 2,
    /// <summary>
    /// 液态玻璃：系统后台模糊 + 宿主自绘的玻璃层（整块玻璃 / 玻璃竖线 / 小玻璃卡片与侧边栏项）。
    /// </summary>
    LiquidGlass = 3
}

/// <summary>材质参数。标题栏与主界面各用一份。</summary>
public sealed class MaterialOptions
{
    /// <summary>模糊半径（0~40），Aero 后端换算成着色浓度。</summary>
    public double BlurRadius { get; set; } = 18d;

    /// <summary>着色强度（0~1）。</summary>
    public double TintStrength { get; set; } = 0.55d;

    /// <summary>标题栏不透明度（0.3~1）。</summary>
    public double TitleOpacity { get; set; } = 0.88d;

    /// <summary>主界面不透明度（0.3~1）。</summary>
    public double MainOpacity { get; set; } = 0.92d;

    /// <summary>侧边栏背景不透明度系数（0.3~1，1 表示保持主题原始浓度）。</summary>
    public double SidebarOpacity { get; set; } = 1d;

    /// <summary>卡片背景不透明度系数（0.3~1，1 表示保持主题原始浓度）。</summary>
    public double CardOpacity { get; set; } = 1d;

    /// <summary>
    ///     Windows 10 上是否允许使用旧版 AccentBlur。
    ///     它是唯一在 Win10 可用的窗口磨砂，但按窗口矩形绘制、无法裁剪成圆角界面。
    /// </summary>
    public bool AllowLegacyAccentBlur { get; set; }

    /// <summary>液态玻璃：边缘折射强度（像素，0 表示关闭）。</summary>
    public double GlassRefraction { get; set; }

    /// <summary>液态玻璃：整块玻璃的着色不透明度（0.3~1）。</summary>
    public double GlassOpacity { get; set; } = 0.45d;

    /// <summary>液态玻璃：标题栏底色的不透明度（0~1，0 表示标题栏完全透明）。</summary>
    public double GlassTitleOpacity { get; set; }

    /// <summary>液态玻璃：侧边栏分割竖线的不透明度（0~1）。</summary>
    public double DividerOpacity { get; set; } = 0.55d;

    /// <summary>大弹窗（对话框）表面不透明度（0.3~1）。</summary>
    public double DialogOpacity { get; set; } = 0.9d;

    /// <summary>右下角小弹窗（Toast）表面不透明度（0.3~1）。</summary>
    public double ToastOpacity { get; set; } = 0.85d;

    /// <summary>液态玻璃：登录方式药丸（正版 / 离线 / 外置）的玻璃不透明度（0.3~1）。</summary>
    public double PillOpacity { get; set; } = 0.55d;

    /// <summary>控件悬停发光半径（0~20，0 表示关闭）。</summary>
    public double GlowRadius { get; set; } = 8d;

    /// <summary>控件悬停发光透明度（0~1）。</summary>
    public double GlowOpacity { get; set; } = 0.35d;
}

/// <summary>
///     高级材质后端：把「云母 / Aero」落到具体 Win32 调用，并在系统不支持时降级。
///     渲染逻辑留在宿主，插件只负责声明功能与提供默认参数。
/// </summary>
public static partial class MaterialService
{
    private const int WcaAccentPolicy = 19;
    private const int AccentEnableBlurBehind = 3;

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public int AccentState;
        public int AccentFlags;
        public int GradientColor; // AABBGGRR
        public int AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public int Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    [LibraryImport("user32.dll", EntryPoint = "SetWindowCompositionAttribute", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoZorder = 0x0004;
    private const uint SwpFrameChanged = 0x0020;
    private const uint RdwInvalidate = 0x0001;
    private const uint RdwAllChildren = 0x0080;
    private const uint RdwUpdateNow = 0x0100;
    private const uint RdwFrame = 0x0400;

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RedrawWindow(IntPtr hwnd, IntPtr updateRect, IntPtr updateRgn, uint flags);

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmFlush();

    /// <summary>实际生效的效果（可能因系统不支持而降级）。</summary>
    public static MaterialKind Applied { get; private set; } = MaterialKind.None;

    /// <summary>降级原因（无降级时为 null）。</summary>
    public static string? FallbackReason { get; private set; }

    /// <summary>实际生效时用的实现（云母 / DWM 亚克力 / 旧版 AccentBlur / 应用内壁纸磨砂），供日志排查。</summary>
    public static string? AppliedVia { get; private set; }

    /// <summary>
    ///     是否必须保持直角窗口。Windows 10 的旧版 AccentBlur 按整个窗口矩形绘制、裁不住圆角，
    ///     一旦生效就只能用方角，否则磨砂会溢出到圆角之外（老 bug）。
    /// </summary>
    public static bool ForcesSquareCorners { get; private set; }

    /// <summary>系统是否具备 DWM 窗口材质能力（Windows 11 build 22621+）。</summary>
    public static bool IsWindows11 => DwmInterop.IsBackdropSupported;

    /// <summary>当前系统可用的最高效果。</summary>
    public static MaterialKind MaxSupported =>
        DwmInterop.IsBackdropSupported ? MaterialKind.Mica : MaterialKind.Aero;

    /// <summary>应用材质；返回实际生效的效果。</summary>
    public static MaterialKind Apply(IntPtr hwnd, MaterialKind kind, MaterialOptions? options = null)
    {
        options ??= new MaterialOptions();
        FallbackReason = null;
        AppliedVia = null;
        ForcesSquareCorners = false;

        if (hwnd == IntPtr.Zero) return Applied = MaterialKind.None;

        switch (kind)
        {
            case MaterialKind.Mica:
                if (DwmInterop.IsBackdropSupported &&
                    DwmInterop.TrySetBackdropType(hwnd, DwmInterop.BackdropType.MainWindow))
                {
                    _ClearAero(hwnd);
                    AppliedVia = "DWM 云母";
                    return Applied = MaterialKind.Mica;
                }

                FallbackReason = "当前系统不支持云母（需要 Windows 11 build 22621 及以上），改用 DWM 亚克力 / 旧版模糊";
                LogWrapper.Warn("Material", FallbackReason);
                goto case MaterialKind.Aero;

            case MaterialKind.Aero:
                // Windows 11 上用 DWM 亚克力：它是「正规」的后台材质，会跟着窗口圆角与区域走；
                // Windows 10 没有这个属性，只能退回旧版 AccentBlur（它按整个窗口矩形绘制，裁不住）
                if (DwmInterop.IsBackdropSupported &&
                    DwmInterop.TrySetBackdropType(hwnd, DwmInterop.BackdropType.TransientWindow))
                {
                    _ClearAero(hwnd);
                    AppliedVia = "DWM 亚克力";
                    return Applied = MaterialKind.Aero;
                }

                // 没有 DWM 后台材质的系统（Windows 10 全系，以及 Windows 11 21H2 / 22000–22620）：
                // AccentBlur 是唯一能糊住**桌面真实内容**的实现。
                // 它按整个窗口矩形绘制 → 宿主把界面铺满窗口，磨砂区域与界面**完全重合**，
                // 不再出现「磨砂比界面大一圈」；四个圆角的小三角里会露出一点磨砂（已知且可接受）。
                if (!IsWindows11 && _ApplyAero(hwnd, options))
                {
                    AppliedVia = "旧版 AccentBlur（界面铺满窗口，磨砂与界面等大）";
                    FallbackReason = "系统没有 DWM 后台材质：使用旧版 AccentBlur；界面已铺满窗口，磨砂与界面同尺寸";
                    LogWrapper.Info("Material", AppliedVia);
                    return Applied = MaterialKind.Aero;
                }

                FallbackReason = "当前系统不支持窗口磨砂，已回退为纯色";
                LogWrapper.Warn("Material", FallbackReason);
                return Applied = MaterialKind.None;

            case MaterialKind.LiquidGlass:
                // 玻璃靠「系统后台模糊 + 宿主自绘着色层」实现，系统着色只留一点点，避免变浑
                if (DwmInterop.IsBackdropSupported &&
                    DwmInterop.TrySetBackdropType(hwnd, DwmInterop.BackdropType.TransientWindow))
                {
                    _ClearAero(hwnd);
                    AppliedVia = "DWM 亚克力（玻璃模式）";
                    return Applied = MaterialKind.LiquidGlass;
                }

                // 同样没有 DWM 后台材质的系统：用 AccentBlur 糊住桌面（低着色），界面铺满窗口以保证磨砂不外扩
                if (!IsWindows11 && _ApplyAero(hwnd, options, LiquidGlassTintScale))
                {
                    AppliedVia = "旧版 AccentBlur（玻璃模式，界面铺满窗口，磨砂与界面等大）";
                    FallbackReason = "系统没有 DWM 后台材质：玻璃用旧版 AccentBlur（界面铺满窗口，磨砂与界面同尺寸）";
                    return Applied = MaterialKind.LiquidGlass;
                }

                // 宿主自绘玻璃（着色 / 光泽 / hairline / 壁纸磨砂）不依赖系统磨砂，形状完全可控，
                // 只是少了「桌面被糊住」的那一层，所以这里仍然算生效
                AppliedVia = IsWindows11
                    ? "宿主自绘玻璃（系统磨砂未启用）"
                    : "宿主自绘玻璃（Windows 10 无系统磨砂）";
                FallbackReason = IsWindows11
                    ? null
                    : "Windows 10 没有可裁剪的系统磨砂：玻璃只糊住壁纸，桌面部分是单纯的半透明";
                return Applied = MaterialKind.LiquidGlass;

            default:
                Clear(hwnd);
                return Applied = MaterialKind.None;
        }
    }

    /// <summary>液态玻璃模式下系统着色只保留 1/5，否则会和宿主玻璃层叠成一片浑浊。</summary>
    private const double LiquidGlassTintScale = 0.2d;

    private static bool _ApplyAero(IntPtr hwnd, MaterialOptions options, double tintScale = 1d)
    {
        try
        {
            // 用模糊半径与着色强度合成 alpha：半径越大越不透明，最多 0xC0
            var alpha = (int)Math.Clamp((options.BlurRadius / 40d * 0.55d + options.TintStrength * 0.45d) * tintScale,
                0d, 0.75d) * 0xC0;
            var policy = new AccentPolicy
            {
                AccentState = AccentEnableBlurBehind,
                // 只画在客户区内。原先的 0x20 | 0x40 | 0x80（四边延伸）会把模糊画到客户区之外，
                // 表现为「磨砂区比界面大一圈、而且没有圆角」，且窗口区域裁不住它。
                AccentFlags = 0x20,
                GradientColor = (alpha << 24) | 0x1A1A1A // AABBGGRR，深色着色
            };

            var size = Marshal.SizeOf<AccentPolicy>();
            var buffer = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(policy, buffer, false);
                var data = new WindowCompositionAttributeData
                {
                    Attribute = WcaAccentPolicy,
                    Data = buffer,
                    SizeOfData = size
                };
                return SetWindowCompositionAttribute(hwnd, ref data);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch (Exception ex)
        {
            LogWrapper.Warn("Material", "应用 Aero 模糊失败：" + ex.Message);
            return false;
        }
    }

    private static bool _ClearAero(IntPtr hwnd)
    {
        try
        {
            var policy = new AccentPolicy { AccentState = 0 };
            var size = Marshal.SizeOf<AccentPolicy>();
            var buffer = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(policy, buffer, false);
                var data = new WindowCompositionAttributeData
                {
                    Attribute = WcaAccentPolicy,
                    Data = buffer,
                    SizeOfData = size
                };
                return SetWindowCompositionAttribute(hwnd, ref data);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    ///     彻底关掉系统的窗口磨砂（AccentBlur 与 DWM 后台材质）。
    ///     <para>
    ///         只把 <c>AccentState</c> 置 0 时，部分 Windows 10 build 要等窗口重新合成才真正撤掉，
    ///         表现就是「把高级材质改成关闭以后，界面外面还挂着一圈磨砂」（用户实测）。
    ///         这里连发两次 + 关掉 DWM backdrop + 强制重绘，确保撤干净。
    ///     </para>
    /// </summary>
    public static void Clear(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            Applied = MaterialKind.None;
            return;
        }

        try
        {
            var cleared = _ClearAero(hwnd);
            if (!cleared) _ClearAero(hwnd); // 个别 build 第一次不生效，补一发（幂等）

            DwmInterop.TrySetBackdropType(hwnd, DwmInterop.BackdropType.None);
            ForceRepaint(hwnd);

            var hadMaterial = Applied != MaterialKind.None || AppliedVia is not null;
            Applied = MaterialKind.None;
            AppliedVia = null;
            FallbackReason = null;
            ForcesSquareCorners = false;
            // 只在真的撤掉了东西时写日志：启动时本来就是「无材质」，不该刷这一行
            if (hadMaterial)
                LogWrapper.Info("Material", "已关闭系统窗口磨砂" + (cleared ? "" : "（AccentBlur 首次撤销失败，已重试）"));
        }
        catch (Exception ex)
        {
            LogWrapper.Warn(ex, "Material", "关闭系统窗口磨砂失败");
        }
    }

    /// <summary>让窗口强制重新合成一次（撤掉磨砂后立刻生效，不用等用户拖窗口）。</summary>
    private static void ForceRepaint(IntPtr hwnd)
    {
        try
        {
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
                SwpNoMove | SwpNoSize | SwpNoZorder | SwpFrameChanged);
            RedrawWindow(hwnd, IntPtr.Zero, IntPtr.Zero, RdwInvalidate | RdwFrame | RdwAllChildren | RdwUpdateNow);
            DwmFlush();
        }
        catch
        {
            // 重绘失败不影响功能
        }
    }
}
