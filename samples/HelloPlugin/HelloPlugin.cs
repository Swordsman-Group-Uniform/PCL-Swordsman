using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCL.PluginSdk;

namespace MyPlugin;

/// <summary>
///     示例插件（全功能版）：一个插件能用到的接口这里都有一份最小示范。
///     <list type="bullet">
///         <item><b>生命周期 + 配置</b>：<see cref="Initialize" /> 里读配置，<see cref="Enable" /> 里累加启用次数；</item>
///         <item><b>插件设置</b>：实现 <see cref="IPluginSettingsProvider" />，宿主「插件管理 → 齿轮」自动生成界面；</item>
///         <item><b>插件页面</b>：实现 <see cref="IPluginUiProvider" />，侧栏出现「Hello 示例」条目；</item>
///         <item><b>宿主事件</b>：订阅 <see cref="HostStartupCompletedEvent" /> / <see cref="GameLaunchedEvent" /> /
///             <see cref="LanguageChangedEvent" />；</item>
///         <item><b>文案本地化</b>：用 <see cref="IPluginContext.LanguageCode" /> + <see cref="PluginLocalization.Pick" />；</item>
///         <item><b>弹提示 / 确认框</b>：<see cref="IPluginNotify" />（页面上的两个按钮就是演示）。</item>
///     </list>
/// </summary>
public sealed class HelloPlugin : IPlugin, IPluginSettingsProvider, IPluginUiProvider
{
    // ==== 配置键（设置界面与页面共用，避免写两次字符串）====
    private const string KeyGreetingEnabled = "hello.greeting";
    private const string KeyTone = "hello.tone";
    private const string KeyPageNumber = "hello.pageNumber";
    private const string KeyLaunchCount = "launchCount";
    private const string KeyNotifyOnStart = "hello.notifyOnStart";

    private IPluginContext _context = null!;
    private IDisposable? _startupSubscription;
    private IDisposable? _launchedSubscription;
    private IDisposable? _languageSubscription;

    // ==== 文案表：只写中文也能在其它语言下正常显示（Pick 的回落规则见其文档）====
    private static readonly IReadOnlyDictionary<string, string> TextTitle = new Dictionary<string, string>
    {
        ["zh-CN"] = "Hello 示例",
        ["zh-TW"] = "Hello 範例",
        ["en-US"] = "Hello Sample"
    };

    private static readonly IReadOnlyDictionary<string, string> TextIntro = new Dictionary<string, string>
    {
        ["zh-CN"] = "这个页面、它的设置项和侧栏条目，都是由插件自己提供的。",
        ["zh-TW"] = "這個頁面、它的設定項和側欄條目，都是由外掛自己提供的。",
        ["en-US"] = "This page, its settings and its sidebar entry all come from the plugin itself."
    };

    private static readonly IReadOnlyDictionary<string, string> TextHint = new Dictionary<string, string>
    {
        ["zh-CN"] = "想改下面的值：工具 → 插件 → 插件管理，点这一行的齿轮。",
        ["zh-TW"] = "想改下面的值：工具 → 外掛 → 外掛管理，點這一行的齒輪。",
        ["en-US"] = "To change the values below: Tools → Plugins → Plugin manager, then the gear on this row."
    };

    public void Initialize(IPluginContext context)
    {
        _context = context;
        context.Log.Info($"已初始化。宿主版本={context.HostVersion}，API={PluginApiVersion.Current}，" +
                         $"语言={context.LanguageCode}，能力={context.Capabilities}，数据目录={context.DataDirectory}");

        // 事件订阅：回调在发布方线程执行，耗时操作请自己切后台
        _startupSubscription = context.Events.Subscribe<HostStartupCompletedEvent>(e =>
        {
            context.Log.Info($"宿主启动完成：版本 {e.HostVersion}，第 {e.LaunchCount} 次启动");

            // 演示 ui.notify：设置里打开「启动时弹提示」才会出现
            if (context.Config.Get(KeyNotifyOnStart, false))
                context.Notify.Success($"Hello 示例插件：第 {e.LaunchCount} 次启动");
        });

        _launchedSubscription = context.Events.Subscribe<GameLaunchedEvent>(e =>
            context.Log.Info($"游戏已启动：{e.InstanceName}"));

        _languageSubscription = context.Events.Subscribe<LanguageChangedEvent>(e =>
            context.Log.Info($"语言已切换为 {e.LanguageCode}，页面文案会在下次打开时更新"));
    }

    public void Enable()
    {
        var count = _context.Config.Get(KeyLaunchCount, 0) + 1;
        _context.Config.Set(KeyLaunchCount, count);
        _context.Log.Info($"这是第 {count} 次启用（配置已写入 config.json）");
    }

    public void Disable() => _context.Log.Info("已禁用");

    public void Dispose()
    {
        // 宿主卸载插件时会自动清空订阅，这里手动释放只是示范写法
        _startupSubscription?.Dispose();
        _launchedSubscription?.Dispose();
        _languageSubscription?.Dispose();
        _context.Log.Info("已卸载");
    }

    // ================= 插件设置（宿主渲染界面） =================

    public IReadOnlyList<PluginSettingDefinition> GetSettings() =>
    [
        new()
        {
            Key = KeyGreetingEnabled,
            Title = "在页面里打招呼",
            Description = "关掉以后示例页面只显示数据，不显示那句问候。",
            Type = PluginSettingType.Toggle,
            DefaultValue = "true"
        },
        new()
        {
            Key = KeyTone,
            Title = "示例下拉项",
            Description = "演示 PluginSettingType.Choice：值存的是选项的 Value。",
            Type = PluginSettingType.Choice,
            DefaultValue = "friendly",
            Options =
            [
                new PluginSettingOption { Value = "friendly", Title = "友好" },
                new PluginSettingOption { Value = "formal", Title = "正式" },
                new PluginSettingOption { Value = "playful", Title = "俏皮" }
            ]
        },
        new()
        {
            Key = KeyNotifyOnStart,
            Title = "启动时弹一条提示",
            Description = "演示 ui.notify 能力：启动完成后在右下角弹一条来自插件的提示。",
            Type = PluginSettingType.Toggle,
            DefaultValue = "false"
        },
        new()
        {
            Key = KeyPageNumber,
            Title = "示例整数（滑条）",
            Description = "演示 PluginSettingType.Integer + Max：宿主渲染成滑条，范围 0~10。",
            Type = PluginSettingType.Integer,
            DefaultValue = "3",
            Min = 0,
            Max = 10
        }
    ];

    // ================= 插件页面（侧栏条目 + 内容） =================

    public IReadOnlyList<PluginPageDefinition> GetPages() =>
    [
        new()
        {
            Id = "hello.page",
            Title = PluginLocalization.Pick(TextTitle, _context.LanguageCode, "Hello 示例"),
            Description = PluginLocalization.Pick(TextIntro, _context.LanguageCode),
            Icon = "lucide/sparkles",
            SortOrder = 90,
            CreatePage = BuildPage
        }
    ];

    /// <summary>每次打开都会调用：必须返回**新**控件，不要复用同一个实例。</summary>
    private FrameworkElement BuildPage(IPluginContext context)
    {
        var panel = new StackPanel();

        if (context.Config.Get(KeyGreetingEnabled, true))
            panel.Children.Add(Line(
                PluginLocalization.Pick(TextTitle, context.LanguageCode), 15d, FontWeights.Bold));

        panel.Children.Add(Line(PluginLocalization.Pick(TextHint, context.LanguageCode), 12d, opacity: 0.75));
        panel.Children.Add(Line(
            $"宿主版本：{context.HostVersion}　语言：{context.LanguageCode}", 12d, opacity: 0.75));

        // Minecraft 只读服务（需要 minecraft.read 能力 + 用户授权）
        var instance = context.Minecraft.CurrentInstance;
        panel.Children.Add(Line(
            instance is null
                ? "当前实例：（还没选实例 / 未授权 minecraft.read）"
                : $"当前实例：{instance.Name}　版本 {instance.VersionName}" +
                  $"{(string.IsNullOrEmpty(instance.LoaderName) ? "" : $"　加载器 {instance.LoaderName} {instance.LoaderVersion}")}" +
                  $"　mods {instance.ModCount} 个",
            12d));
        panel.Children.Add(Line(
            $"示例下拉项 = {context.Config.Get(KeyTone, "friendly")}　" +
            $"示例整数 = {context.Config.Get(KeyPageNumber, 3)}　" +
            $"启用次数 = {context.Config.Get(KeyLaunchCount, 0)}", 12d));

        // 一个宿主配色的分隔线，示意插件页面也跟随主题
        panel.Children.Add(new Border
        {
            Height = 1d,
            Margin = new Thickness(0, 8, 0, 8),
            Background = Application.Current?.TryFindResource("ColorBrush6") as Brush ?? Brushes.Gray
        });

        panel.Children.Add(Line(
            "小提示：把本页的 CreatePage 换成自己的控件，就是一个完整的功能页了。", 12d, opacity: 0.75));

        // ui.notify 演示：两个按钮（宿主会把 UI 操作切回主线程，插件随便在哪个线程调）
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };

        var toastButton = new Button
        {
            Content = "弹一条提示",
            Padding = new Thickness(14, 6, 14, 6),
            Margin = new Thickness(0, 0, 8, 0)
        };
        toastButton.Click += (_, _) => context.Notify.Info("这是插件弹出的提示（右下角）");

        var confirmButton = new Button
        {
            Content = "弹一个确认框",
            Padding = new Thickness(14, 6, 14, 6)
        };
        confirmButton.Click += (_, _) => context.Notify.Confirm(
            "Hello 示例插件",
            "这是插件弹出的确认框，点「确定」会把结果写进日志。",
            result => context.Log.Info($"确认框结果：{(result ? "确定" : "取消")}"));

        buttons.Children.Add(toastButton);
        buttons.Children.Add(confirmButton);
        panel.Children.Add(buttons);

        return panel;
    }

    private static TextBlock Line(string text, double fontSize, FontWeight? weight = null, double opacity = 1d) => new()
    {
        Text = text,
        FontSize = fontSize,
        FontWeight = weight ?? FontWeights.Normal,
        TextWrapping = TextWrapping.Wrap,
        Opacity = opacity,
        Margin = new Thickness(0, 0, 0, 6)
    };
}
