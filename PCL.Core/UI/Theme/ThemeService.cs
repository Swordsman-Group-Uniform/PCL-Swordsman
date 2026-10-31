using System;
using System.Collections.Generic;
using PCL.Core.App;
using PCL.Core.App.Configuration;
using PCL.Core.App.IoC;

namespace PCL.Core.UI.Theme;

/// <summary>
/// 配色模式更改事件。
/// </summary>
/// <param name="isDarkMode">当前是否为深色模式</param>
/// <param name="theme">当前配色主题</param>
public delegate void ColorModeChangedEvent(bool isDarkMode, ColorTheme theme);

/// <summary>
/// 配色主题更改事件。
/// </summary>
/// <param name="theme">当前配色主题</param>
public delegate void ColorThemeChangedEvent(ColorTheme theme);

[LifecycleScope("theme", "主题", false)]
[LifecycleService(LifecycleState.WindowCreating)]
public sealed partial class ThemeService
{
    [AnyConfigItem<ToneProfileConfig>("UiToneProfiles", ConfigSource.Local)]
    public static partial ToneProfileConfig ToneProfiles { get; set; }

    [RegisterConfigEvent]
    public static ConfigEventRegistry OnColorModeConfigChanged => new(
        scope: Config.Preference.Theme.ColorModeConfig,
        trigger: ConfigEvent.Update,
        handler: _ => RefreshColorMode()
    );

    /// <summary>
    ///     自定义主题色改变后，若当前正在使用自定义主题则立即刷新配色。
    /// </summary>
    [RegisterConfigEvent]
    public static ConfigEventRegistry OnCustomColorConfigChanged => new(
        scope: Config.Preference.Theme.CustomColorConfig,
        trigger: ConfigEvent.Update,
        handler: e =>
        {
            if (e.OldValue == e.Value) return;
            if (CurrentTheme != ColorTheme.Custom) return;
            if (Lifecycle.CurrentState > LifecycleState.Loading)
            {
                Lifecycle.CurrentApplication.Dispatcher.BeginInvoke(() =>
                {
                    ApplyColorResources();
                    ColorThemeChanged?.Invoke(CurrentTheme);
                });
            }
        }
    );

    [RegisterConfigEvent]
    public static ConfigEventRegistry OnColorThemeConfigChanged => new(
        scope: [Config.Preference.Theme.DarkColorConfig, Config.Preference.Theme.LightColorConfig],
        trigger: ConfigEvent.Update,
        handler: e =>
        {
            // ignore no change or non-current color theme change
            if (e.OldValue == e.Value) return;
            if (IsDarkMode) { if (e.Item == Config.Preference.Theme.LightColorConfig) return; }
            else { if (e.Item == Config.Preference.Theme.DarkColorConfig) return; }
            // trigger color refresh
            if (Lifecycle.CurrentState > LifecycleState.Loading)
            {
                Lifecycle.CurrentApplication.Dispatcher.BeginInvoke(() =>
                {
                    ApplyColorResources();
                    ColorThemeChanged?.Invoke(CurrentTheme);
                    _AprilFoolLogic();
                });
            }
        }
    );

    [LifecycleStart]
    private static void _Start()
    {
        IsDarkMode = _IsDarkMode();
        _LogStatus();
        _RefreshAll();
    }

    private static void _LogStatus()
    {
        Context.Debug($"当前状态: {(IsDarkMode ? "Dark" : "Light")}, {CurrentTheme}");
    }

    private static bool _IsDarkMode() => Config.Preference.Theme.ColorMode switch
    {
        ColorMode.Light => false,
        ColorMode.Dark => true,
        ColorMode.System => SystemThemeHelper.IsSystemInDarkMode(),
        _ => false
    };

    /// <summary>
    /// 当前构建是否为发行版（正式版，即版本号不带 snapshot / experiment 之类的后缀）。
    /// <para>
    /// PCL 的主题色是原作者龙腾猫跃的主要赞助项目，因此发行版中不保留扩展主题
    /// （肥鱼蓝、天依蓝与自定义主题色），仅保留原有的几个蓝色系主题。
    /// </para>
    /// </summary>
    public static bool IsReleaseDistribution => string.IsNullOrWhiteSpace(Basics.Metadata.Version.Suffix);

    /// <summary>
    /// 是否启用扩展主题（非蓝色的追加主题与自定义主题色）。
    /// <para>
    /// 由**插件**决定：装了声明 <c>themes.extended</c> 功能的插件（SGU 插件包）才可用，
    /// 正式版默认不带该插件，因此只保留蓝色系主题。实现仍留在宿主里，插件只提供数据与开关。
    /// </para>
    /// </summary>
    public static bool IsExtendedThemesEnabled => FeatureFlags.IsEnabled(FeatureFlags.Keys.ExtendedThemes);

    /// <summary>
    ///     是否启用「自定义主题色」。
    ///     <para>
    ///         以前这个能力与扩展主题共用一个功能键；现在拆成 <c>themes.custom-color</c>，
    ///         这样插件可以只给主题、不给自定义主题色（或反之）。
    ///     </para>
    /// </summary>
    public static bool IsCustomColorEnabled => FeatureFlags.IsEnabled(FeatureFlags.Keys.CustomThemeColor);

    private static readonly Dictionary<ColorTheme, (string Hex, string? Flavor)> _extraThemeDefinitions = new();

    /// <summary>
    /// 由插件提供的附加主题定义（主题 → 主色 / 特殊配色风格）。未提供时使用内置常量兜底。
    /// </summary>
    public static void SetExtraThemeDefinitions(IReadOnlyDictionary<ColorTheme, (string Hex, string? Flavor)>? definitions)
    {
        _extraThemeDefinitions.Clear();
        if (definitions is null) return;
        foreach (var (theme, value) in definitions) _extraThemeDefinitions[theme] = value;
    }

    /// <summary>插件提供的主题显示名（没有则返回 null，由界面回退到内置本地化文案）。</summary>
    public static string? GetExtraThemeDisplayName(ColorTheme theme) =>
        _extraThemeNames.TryGetValue(theme, out var name) ? name : null;

    private static readonly Dictionary<ColorTheme, string> _extraThemeNames = new();

    public static void SetExtraThemeNames(IReadOnlyDictionary<ColorTheme, string>? names)
    {
        _extraThemeNames.Clear();
        if (names is null) return;
        foreach (var (theme, name) in names)
            if (!string.IsNullOrWhiteSpace(name))
                _extraThemeNames[theme] = name;
    }

    /// <summary>
    ///     插件**自带**的主题定义（id → 定义）。与 <see cref="_extraThemeDefinitions" /> 不同：
    ///     那边只是给内置槽位换名字 / 换色，这里是插件凭空定义的新主题，统一映射到
    ///     <see cref="ColorTheme.PluginDefined" /> 这一个槽位上，具体选哪个由配置里的 id 决定。
    /// </summary>
    private static readonly Dictionary<string, string> _pluginThemeHex = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> _pluginThemeNames = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string?> _pluginThemeFlavor = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>插件自带主题的展示顺序（下拉框用）。</summary>
    public static IReadOnlyList<(string Id, string DisplayName, int SortOrder)> PluginThemes =>
        _pluginThemeOrder;

    private static readonly List<(string Id, string DisplayName, int SortOrder)> _pluginThemeOrder = [];

    /// <summary>由插件注册的自带主题（启动时 / 插件重扫后由宿主调用）。</summary>
    public static void SetPluginThemes(
        IReadOnlyList<(string Id, string DisplayName, string Hex, string? Flavor, int SortOrder)> themes)
    {
        _pluginThemeHex.Clear();
        _pluginThemeNames.Clear();
        _pluginThemeFlavor.Clear();
        _pluginThemeOrder.Clear();

        foreach (var theme in themes)
        {
            if (string.IsNullOrWhiteSpace(theme.Id)) continue;
            _pluginThemeHex[theme.Id] = theme.Hex ?? "";
            _pluginThemeNames[theme.Id] = string.IsNullOrWhiteSpace(theme.DisplayName) ? theme.Id : theme.DisplayName;
            _pluginThemeFlavor[theme.Id] = theme.Flavor;
            _pluginThemeOrder.Add((theme.Id, _pluginThemeNames[theme.Id], theme.SortOrder));
        }

        _pluginThemeOrder.Sort((a, b) =>
            a.SortOrder != b.SortOrder ? a.SortOrder.CompareTo(b.SortOrder)
            : string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>插件自带主题的显示名（不是插件主题时返回 null）。</summary>
    public static string? GetPluginThemeDisplayName(string? id) =>
        id is not null && _pluginThemeNames.TryGetValue(id, out var name) ? name : null;

    /// <summary>
    ///     当前生效的插件主题 id（不是插件主题时返回 null）。
    /// </summary>
    public static string? CurrentPluginThemeId
    {
        get
        {
            var theme = Config.Preference.Theme;
            var id = IsDarkMode ? theme.DarkPluginTheme : theme.LightPluginTheme;
            return string.IsNullOrWhiteSpace(id) ? null : id;
        }
    }

    /// <summary>取主题主色：优先插件提供的定义，其次内置常量。</summary>
    private static string _ThemeHex(ColorTheme theme, string fallback)
    {
        if (theme == ColorTheme.PluginDefined)
        {
            var id = CurrentPluginThemeId;
            if (id is not null && _pluginThemeHex.TryGetValue(id, out var pluginHex) &&
                !string.IsNullOrWhiteSpace(pluginHex))
                return pluginHex;
            return fallback;
        }

        return _extraThemeDefinitions.TryGetValue(theme, out var definition) && !string.IsNullOrWhiteSpace(definition.Hex)
            ? definition.Hex
            : fallback;
    }

    /// <summary>取主题的特殊配色风格（<c>golden-dusk</c> / <c>sakura-bloom</c>）。</summary>
    private static string? _ThemeFlavor(ColorTheme theme)
    {
        if (theme == ColorTheme.PluginDefined)
        {
            var id = CurrentPluginThemeId;
            return id is not null && _pluginThemeFlavor.TryGetValue(id, out var flavor) ? flavor : null;
        }

        return _extraThemeDefinitions.TryGetValue(theme, out var definition) ? definition.Flavor : null;
    }

    /// <summary>
    /// 该主题是否属于蓝色系。发行版只保留蓝色系主题，
    /// 其余主题（剑客黄、初音绿、烤音绿、自定义等）仅在快照版 / 实验版中提供。
    /// </summary>
    public static bool IsBlueTheme(ColorTheme theme) => theme switch
    {
        ColorTheme.SkyBlue => true,
        ColorTheme.CatBlue => true,
        ColorTheme.DeathBlue => true,
        ColorTheme.FatFishBlue => true,
        ColorTheme.TianyiBlue => true,
        ColorTheme.MermanBlue => true,
        ColorTheme.IchikaBlue => true,
        // 剑客黄、初音绿、烤音绿、HMCL 蓝、自定义：非蓝色系，发行版中隐藏
        _ => false
    };

    /// <summary>
    /// 当前构建中该主题是否可选。
    /// </summary>
    public static bool IsThemeSelectable(ColorTheme theme)
    {
        if (theme == ColorTheme.PluginDefined) return _pluginThemeOrder.Count > 0;
        return theme == ColorTheme.Custom ? IsCustomColorEnabled : IsExtendedThemesEnabled || IsBlueTheme(theme);
    }

    /// <summary>
    /// 当前是否为深色主题。
    /// </summary>
    public static bool IsDarkMode { get; private set; }

    /// <summary>
    /// 配色模式更改事件。
    /// </summary>
    public static event ColorModeChangedEvent? ColorModeChanged;

    /// <summary>
    /// 配色主题更改事件。
    /// </summary>
    public static event ColorThemeChangedEvent? ColorThemeChanged;

    private static void _RefreshAll()
    {
        ApplyGrayResources();
        ApplyColorResources();
        ColorModeChanged?.Invoke(IsDarkMode, CurrentTheme);
        _AprilFoolLogic();
    }

    private static void _AprilFoolLogic()
    {
        // for HMCL theme on April Fools' Day
        if (Basics.IsAprilFool) Config.Preference.WindowTitleTypeConfig.TriggerEvent(ConfigEvent.Changed, null);
        else if (CurrentTheme == ColorTheme.HmclBlue) CurrentTheme = ColorTheme.CatBlue;
    }

    /// <summary>
    /// 刷新配色模式，若检测到当前配色模式有实际更改，则会触发主题刷新。
    /// </summary>
    public static void RefreshColorMode()
    {
        var isDarkMode = _IsDarkMode();
        if (IsDarkMode == isDarkMode) return;
        Context.Info("正在更改配色模式");
        IsDarkMode = isDarkMode;
        _LogStatus();
        if (Lifecycle.CurrentState > LifecycleState.Loading)
        {
            Lifecycle.CurrentApplication.Dispatcher.BeginInvoke(_RefreshAll);
        }
    }

    /// <summary>
    /// 当前使用的色彩属性。
    /// </summary>
    public static ToneProfile CurrentTone => IsDarkMode ? ToneProfiles.Dark : ToneProfiles.Light;

    /// <summary>
    /// 当前使用的主题色。
    /// </summary>
    public static ColorTheme CurrentTheme
    {
        get
        {
            var theme = Config.Preference.Theme;
            return IsDarkMode ? theme.DarkColor : theme.LightColor;
        }
        set
        {
            var theme = Config.Preference.Theme;
            var config = IsDarkMode ? theme.DarkColorConfig : theme.LightColorConfig;
            config.SetValue(value);
        }
    }

    /// <summary>
    /// 获取当前色彩主题对应的各种参数。
    /// 色相使用 <see cref="double" /> 以便自定义主题色能够被精确还原。
    /// </summary>
    public static (double Hue, double LightAdjust, double ChromaAdjust) GetCurrentThemeArgs()
    {
        var theme = CurrentTheme;
        return theme switch
        {
            ColorTheme.SkyBlue => (235d, 0.36, 0.2),
            ColorTheme.CatBlue => (255d, 0, -0.2),
            ColorTheme.DeathBlue => (268d, -0.05, -0.1),
            ColorTheme.HmclBlue => (275d, -0.03, -0.35),
            ColorTheme.Custom => _GetThemeArgsFromHex(Config.Preference.Theme.CustomColor, (255d, 0d, -0.2)),
            ColorTheme.FatFishBlue => _GetThemeArgsFromHex(_ThemeHex(ColorTheme.FatFishBlue, "#5A67AC"), (250d, 0d, -0.2)),
            ColorTheme.TianyiBlue => _GetThemeArgsFromHex(_ThemeHex(ColorTheme.TianyiBlue, "#66CCFF"), (230d, 0.2d, 0.1d)),
            ColorTheme.SwordsmanYellow => _GetThemeArgsFromHex(_ThemeHex(ColorTheme.SwordsmanYellow, "#E8B33C"), (75d, 0d, 0d)),
            ColorTheme.MikuGreen => _GetThemeArgsFromHex(_ThemeHex(ColorTheme.MikuGreen, "#39C5BB"), (190d, 0d, 0d)),
            ColorTheme.MermanBlue => _GetThemeArgsFromHex(_ThemeHex(ColorTheme.MermanBlue, "#3366CC"), (245d, 0d, 0d)),
            ColorTheme.KaoYinGreen => _GetThemeArgsFromHex(_ThemeHex(ColorTheme.KaoYinGreen, "#33CCBB"), (185d, 0d, 0d)),
            ColorTheme.IchikaBlue => _GetThemeArgsFromHex(_ThemeHex(ColorTheme.IchikaBlue, "#33AAEE"), (235d, 0d, 0d)),
            ColorTheme.SakuraPink => _GetThemeArgsFromHex(_ThemeHex(ColorTheme.SakuraPink, "#FF94D6"), (340d, 0d, 0d)),
            ColorTheme.NetherRed => _GetThemeArgsFromHex(_ThemeHex(ColorTheme.NetherRed, "#B3392E"), (30d, 0d, 0d)),
            ColorTheme.MethylOrange => _GetThemeArgsFromHex(_ThemeHex(ColorTheme.MethylOrange, "#EE7B1E"), (55d, 0d, 0d)),
            ColorTheme.AmethystPurple => _GetThemeArgsFromHex(_ThemeHex(ColorTheme.AmethystPurple, "#9A5CE0"), (300d, 0d, 0d)),
            ColorTheme.PluginDefined => _GetThemeArgsFromHex(_ThemeHex(ColorTheme.PluginDefined, "#5C5C5C"), (255d, 0d, -0.2d)),
#if DEBUG
            _ => ((double)theme, 0, 0)
#else
            _ => throw new IndexOutOfRangeException($"Invalid theme index: {(int)theme}")
#endif
        };
    }

    /// <summary>
    ///     解析 #RRGGBB / RRGGBB 形式的颜色。
    /// </summary>
    public static bool TryParseHexColor(string? hex, out byte r, out byte g, out byte b)
    {
        r = g = b = 0;
        if (string.IsNullOrWhiteSpace(hex)) return false;
        var text = hex.Trim();
        if (text.StartsWith('#')) text = text[1..];
        if (text.Length != 6) return false;
        if (!int.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out var value)) return false;
        r = (byte)((value >> 16) & 0xFF);
        g = (byte)((value >> 8) & 0xFF);
        b = (byte)(value & 0xFF);
        return true;
    }

    /// <summary>
    ///     规范化自定义主题色文本，返回形如 <c>#39C5BB</c> 的字符串；无法解析时返回 <see langword="null" />。
    /// </summary>
    public static string? NormalizeHexColor(string? hex)
        => TryParseHexColor(hex, out var r, out var g, out var b) ? $"#{r:X2}{g:X2}{b:X2}" : null;

    /// <summary>
    ///     根据用户指定的自定义主题色反解出 (色相, 明度调整, 色度调整)，使生成的主色（Color3）与该颜色一致。
    /// </summary>
    private static (double Hue, double LightAdjust, double ChromaAdjust) _GetThemeArgsFromHex(
        string? hex, (double Hue, double LightAdjust, double ChromaAdjust) fallback)
    {
        if (!TryParseHexColor(hex, out var r, out var g, out var b))
            return fallback;

        var color = LabColor.FromRgb(r, g, b);
        var tone = CurrentTone;

        // OKLab 的 A/B 通道换算出 OKLCH 的色度与色相
        var chroma = Math.Sqrt(color.A * color.A + color.B * color.B);
        var hue = Math.Atan2(color.B, color.A) * 180d / Math.PI;
        if (hue < 0) hue += 360d;

        // 反解调整量，使 Color3 的 L / C 等于用户颜色
        var lightAdjust = _InvertAdjustLinear(tone.L3, color.L);
        var chromaAdjust = _InvertAdjustLinear(tone.C3, chroma);

        return (hue, lightAdjust, chromaAdjust);
    }

    /// <summary>
    ///     _AdjustLinear 的反函数：求使 <c>_AdjustLinear(baseValue, result) == target</c> 的调整量。
    /// </summary>
    private static double _InvertAdjustLinear(double baseValue, double target)
    {
        baseValue = Math.Clamp(baseValue, 1e-6, 1.0 - 1e-6);
        var adjustment = target >= baseValue
            ? (target - baseValue) / (1.0 - baseValue)
            : (target - baseValue) / baseValue;
        return Math.Clamp(adjustment, -1.0, 1.0);
    }

    private static double _AdjustLinear(double value, double adjustment)
    {
        if (adjustment == 0) return value;
        // 确保输入在合理范围内
        value = Math.Clamp(value, 0.0, 1.0);
        adjustment = Math.Clamp(adjustment, -1.0, 1.0);
        // 非对称线性插值
        return adjustment switch
        {
            > 0 => value + (1.0 - value) * adjustment,
            _ => value + value * adjustment
        };
    }

    private static CatColorResource[] _CalculateGrays(ToneProfile tone) => [
        LabColor.FromLch(tone.L1).ToCatColor("Gray1"),
        LabColor.FromLch(tone.L2).ToCatColor("Gray2"),
        LabColor.FromLch(tone.L3).ToCatColor("Gray3"),
        LabColor.FromLch(tone.L4).ToCatColor("Gray4"),
        LabColor.FromLch(tone.L5).ToCatColor("Gray5"),
        LabColor.FromLch(tone.L6).ToCatColor("Gray6"),
        LabColor.FromLch(tone.L7).ToCatColor("Gray7"),
        LabColor.FromLch(tone.L8).ToCatColor("Gray8"),
        LabColor.FromLch(tone.LWhite, alpha:tone.AHalfWhite).ToCatColor("HalfWhite", false),
        LabColor.FromLch(tone.LWhite, alpha:tone.ASemiWhite).ToCatColor("SemiWhite", false),
        LabColor.FromLch(tone.LWhite).ToCatColor("White", false),
        LabColor.FromLch(tone.LWhite, alpha:tone.ATransparent).ToCatColor("Transparent", false),
        LabColor.FromLch(tone.LBackground, alpha:tone.ABackground).ToCatColor("TransparentBackground", false),
        LabColor.FromLch(tone.LBackground).ToCatColor("Background", false),
        LabColor.FromLch(tone.LBackground, alpha:tone.AToolTip).ToCatColor("ToolTip", false),
        LabColor.FromLch(tone.L7, 0.25, 30, tone.AHalfTransparent).ToCatColor("RedBack", false),
        LabColor.FromLch(tone.LForeground).ToCatColor("Memory", false),
    ];

    /// <summary>
    ///     计算当前主题的 11 个彩色资源。
    ///     「剑客黄」走黄昏金色调色板、「樱花粉」走樱粉花瓣调色板（两者都是逐档色相渐变），
    ///     其余十六进制主题按档位加权，上游预设主题沿用统一算法。
    /// </summary>
    private static CatColorResource[] _CalculateColors(ToneProfile tone, (double hue, double lightAdj, double chromaAdj) args)
    {
        // 「剑客黄」：黄昏金色渐变
        if (CurrentTheme == ColorTheme.SwordsmanYellow ||
            string.Equals(_ThemeFlavor(CurrentTheme), "golden-dusk", StringComparison.OrdinalIgnoreCase))
            return _CalculateColorsGoldenDusk(tone, args);
        // 「樱花粉」：樱白粉 → 樱花粉 → 桃红 → 梅紫的花瓣渐变
        if (CurrentTheme == ColorTheme.SakuraPink ||
            string.Equals(_ThemeFlavor(CurrentTheme), "sakura-bloom", StringComparison.OrdinalIgnoreCase))
            return _CalculateColorsSakuraBloom(tone, args);
        // 其余「十六进制指定色」主题：加权应用明度 / 色度调整（只让强调色及其邻档变色，
        // 文字与背景仍沿用 ToneProfile 的明度阶梯，避免浅色模式下文字被整体提亮而发白）
        if (IsHexTheme(CurrentTheme)) return _CalculateColorsFromHex(tone, args);
        // 上游预设主题：保持原算法，配色与上游完全一致
        return _CalculateColorsDefault(tone, args);
    }

    /// <summary>
    ///     该主题的颜色是否由 #RRGGBB 反解而来（自定义主题色与各命名主题）。
    /// </summary>
    private static bool IsHexTheme(ColorTheme theme) => theme is
        ColorTheme.Custom or ColorTheme.FatFishBlue or ColorTheme.TianyiBlue or ColorTheme.MermanBlue or
        ColorTheme.IchikaBlue or ColorTheme.MikuGreen or ColorTheme.KaoYinGreen or ColorTheme.SwordsmanYellow or
        ColorTheme.SakuraPink or ColorTheme.NetherRed or ColorTheme.MethylOrange or ColorTheme.AmethystPurple or
        ColorTheme.PluginDefined;

    /// <summary>
    ///     十六进制主题的配色。
    ///     <para>
    ///         关键点：<paramref name="args" /> 里的明度 / 色度调整量是按"让 Color3 等于目标色"反解出来的，
    ///         若像上游预设那样**整条明度阶梯一起偏移**，亮色（如 #E8B33C、#66CCFF）会把 Color1/2
    ///         这些深色文字档一起提亮，浅色模式下表现为「所有控件都像半透明」。
    ///         因此这里按档位加权：仅 Color3 全量应用，2/4 部分应用，文字档 1 与背景档 5~8 几乎不动。
    ///     </para>
    /// </summary>
    private static CatColorResource[] _CalculateColorsFromHex(ToneProfile tone,
        (double hue, double lightAdj, double chromaAdj) args)
        => BuildWeightedColors(tone, args, null);

    /// <summary>
    ///     「樱花粉」专属配色：逐档把色相往紫红方向压，形成樱白粉 → 樱花粉 → 桃红 → 梅紫的花瓣渐变。
    ///     与「剑客黄」一样只作用于 2~5 档，文字档与背景档基本不动。
    /// </summary>
    private static CatColorResource[] _CalculateColorsSakuraBloom(ToneProfile tone,
        (double hue, double lightAdj, double chromaAdj) args)
    {
        (double Hue, double Light, double Chroma)[] ramp =
        [
            (0, 0.00, -0.06),   // 1 文字档：不染
            (4, 0.03, -0.10),   // 2 樱白粉
            (0, 0.00, 0.00),    // 3 樱花粉（主色，等于用户选的颜色）
            (-6, -0.01, 0.02),  // 4 桃红
            (-12, -0.01, 0.03), // 5 梅红
            (-20, -0.02, 0.03), // 6 深梅
            (-28, -0.02, 0.02), // 7 梅紫
            (-36, -0.02, -0.02) // 8 深梅紫
        ];
        return BuildWeightedColors(tone, args, ramp);
    }

    /// <summary>
    ///     「剑客黄」专属配色：在加权算法基础上叠加逐档色相偏移，形成浅金 → 金 → 琥珀 → 暮色橙的黄昏渐变。
    /// </summary>
    private static CatColorResource[] _CalculateColorsGoldenDusk(ToneProfile tone,
        (double hue, double lightAdj, double chromaAdj) args)
    {
        // 逐档色相偏移（相对基准金色）；数值只作用于 2~5 档，避免文字与背景被染色
        (double Hue, double Light, double Chroma)[] ramp =
        [
            (-14, 0.02, -0.10), // 1 文字档：几乎不动，仅微微偏暖
            (-20, 0.02, -0.06), // 2 浅金
            (-18, 0.00, 0.00),  // 3 金（主色）
            (-26, -0.01, 0.02), // 4 暖金
            (-34, -0.01, 0.04), // 5 琥珀
            (-44, -0.02, 0.04), // 6 深琥珀
            (-54, -0.02, 0.02), // 7 暮色橙
            (-70, -0.02, -0.04),// 8 深暮紫橙
        ];
        return BuildWeightedColors(tone, args, ramp);
    }

    /// <summary>
    ///     按档位加权生成 11 个彩色资源；<paramref name="ramp" /> 为可选的逐档色相 / 明度 / 色度微调。
    /// </summary>
    private static CatColorResource[] BuildWeightedColors(ToneProfile tone,
        (double hue, double lightAdj, double chromaAdj) args,
        (double Hue, double Light, double Chroma)[]? ramp)
    {
        // (明度基准, 色度基准, 明度权重, 色度权重, 名称, alpha, 是否写入 ColorObject)
        (double L, double C, double LightWeight, double ChromaWeight, string Suffix, double? Alpha, bool ApplyToColor)[] slots =
        [
            (tone.L1, tone.C1, 0.00, 0.10, "1", null, true),
            (tone.L2, tone.C2, 0.15, 0.30, "2", null, true),
            (tone.L3, tone.C3, 1.00, 1.00, "3", null, true),
            (tone.L4, tone.C4, 0.50, 0.80, "4", null, true),
            (tone.L5, tone.C5, 0.15, 0.40, "5", null, true),
            (tone.L6, tone.C6, 0.05, 0.30, "6", null, true),
            (tone.L7, tone.C7, 0.00, 0.30, "7", null, true),
            (tone.L8, tone.C8, 0.00, 0.30, "8", null, true),
            (tone.L8, tone.C8, 0.00, 0.30, "SemiTransparent", tone.ASemiTransparent, false),
            (tone.L5, tone.C5, 0.15, 0.40, "Bg0", null, true),
            (tone.L7, tone.C7, 0.00, 0.30, "Bg1", tone.ASemiWhite, false)
        ];

        var result = new CatColorResource[slots.Length];
        for (var i = 0; i < slots.Length; i++)
        {
            var (lightBase, chromaBase, lightWeight, chromaWeight, suffix, alpha, applyToColor) = slots[i];
            var hueDelta = ramp is null ? 0d : ramp[Math.Min(i, ramp.Length - 1)].Hue;
            var lightExtra = ramp is null ? 0d : ramp[Math.Min(i, ramp.Length - 1)].Light * lightWeight;
            var chromaExtra = ramp is null ? 0d : ramp[Math.Min(i, ramp.Length - 1)].Chroma * chromaWeight;

            var lightness = _AdjustLinear(lightBase, args.lightAdj * lightWeight + lightExtra);
            var chroma = _AdjustLinear(chromaBase, args.chromaAdj * chromaWeight + chromaExtra);
            var color = alpha is null
                ? LabColor.FromLch(lightness, chroma, args.hue + hueDelta)
                : LabColor.FromLch(lightness, chroma, args.hue + hueDelta, alpha.Value);
            result[i] = color.ToCatColor(suffix, applyToColor);
        }

        return result;
    }

    private static CatColorResource[] _CalculateColorsDefault(ToneProfile tone, (double hue, double lightAdj, double chromaAdj) args) => [
        LabColor.FromLch(_AdjustLinear(tone.L1, args.lightAdj * 0.1), _AdjustLinear(tone.C1, args.chromaAdj * 0.25), args.hue).ToCatColor("1"),
        LabColor.FromLch(_AdjustLinear(tone.L2, args.lightAdj), _AdjustLinear(tone.C2, args.chromaAdj), args.hue).ToCatColor("2"),
        LabColor.FromLch(_AdjustLinear(tone.L3, args.lightAdj), _AdjustLinear(tone.C3, args.chromaAdj), args.hue).ToCatColor("3"),
        LabColor.FromLch(_AdjustLinear(tone.L4, args.lightAdj), _AdjustLinear(tone.C4, args.chromaAdj), args.hue).ToCatColor("4"),
        LabColor.FromLch(_AdjustLinear(tone.L5, args.lightAdj), _AdjustLinear(tone.C5, args.chromaAdj), args.hue).ToCatColor("5"),
        LabColor.FromLch(_AdjustLinear(tone.L6, args.lightAdj), _AdjustLinear(tone.C6, args.chromaAdj), args.hue).ToCatColor("6"),
        LabColor.FromLch(_AdjustLinear(tone.L7, args.lightAdj), _AdjustLinear(tone.C7, args.chromaAdj), args.hue).ToCatColor("7"),
        LabColor.FromLch(_AdjustLinear(tone.L8, args.lightAdj), _AdjustLinear(tone.C8, args.chromaAdj), args.hue).ToCatColor("8"),
        LabColor.FromLch(_AdjustLinear(tone.L8, args.lightAdj), _AdjustLinear(tone.C8, args.chromaAdj), args.hue, tone.ASemiTransparent).ToCatColor("SemiTransparent", false),
        LabColor.FromLch(_AdjustLinear(tone.L5, args.lightAdj), _AdjustLinear(tone.C5, args.chromaAdj), args.hue).ToCatColor("Bg0"),
        LabColor.FromLch(_AdjustLinear(tone.L7, args.lightAdj), _AdjustLinear(tone.C7, args.chromaAdj), args.hue, tone.ASemiWhite).ToCatColor("Bg1"),
    ];

    private static CatColorResource[] LightGrayCache { get => field ??= _CalculateGrays(ToneProfiles.Light); set; } = null!;

    private static CatColorResource[] DarkGrayCache { get => field ??= _CalculateGrays(ToneProfiles.Dark); set; } = null!;

    /// <summary>
    /// 清除灰度配色的计算缓存。
    /// </summary>
    public static void InvalidateGrayCache()
    {
        LightGrayCache = null!;
        DarkGrayCache = null!;
    }

    /// <summary>
    /// 应用灰度配色到 WPF 资源字典。
    /// </summary>
    public static void ApplyGrayResources()
    {
        var cache = IsDarkMode ? DarkGrayCache : LightGrayCache;
        foreach (var c in cache) c.Apply();
    }

    /// <summary>
    /// 应用彩色配色到 WPF 资源字典。
    /// </summary>
    public static void ApplyColorResources()
    {
        var colors = _CalculateColors(CurrentTone, GetCurrentThemeArgs());
        foreach (var c in colors) c.Apply();
    }
}
