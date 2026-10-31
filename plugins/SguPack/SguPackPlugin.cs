using PCL.PluginSdk;

namespace SguPack;

/// <summary>
///     SGU 插件包：声明并提供「扩展主题」与「回声洞」两组功能。
///     <para>
///         配色渲染、主题下拉框、回声洞卡片都在宿主（PCL）里实现，
///         本插件只提供**数据**（主题名称 / 主色 / 特殊配色风格，以及回声洞语录）。
///         另外它还注册了一个自己的页面（「SGU 插件包」），用于示范 <see cref="IPluginUiProvider" />：
///         侧栏条目、容器、主题配色都由宿主负责，页面内容在这里现搭。
///     </para>
/// </summary>
public sealed class SguPackPlugin : IPlugin, IExtraThemesProvider, IEchoCaveQuotesProvider, IPluginUiProvider
{
    private IPluginLogger? _log;

    public void Initialize(IPluginContext context)
    {
        _log = context.Log;
        _log.Info($"SGU 插件包已加载：{GetThemes().Count} 个主题、{GetQuotes().Count} 条语录");
    }

    /// <summary>扩展主题（全部非蓝色主题）。</summary>
    public IReadOnlyList<PluginThemeDefinition> GetThemes() =>
    [
        new()
        {
            Key = "SwordsmanYellow",
            DisplayName = "剑客黄",
            Hex = "#E8B33C",
            Description = "神圣黄昏配色 + 渐变，附带剑客主题材质的标题栏装饰。",
            Flavor = "golden-dusk"
        },
        new()
        {
            Key = "SakuraPink",
            DisplayName = "樱花粉",
            Hex = "#FF94D6",
            Description = "樱花披风配色 + 花瓣渐变，标题栏右上角附带樱花枝条装饰。",
            Flavor = "sakura-bloom"
        },
        new()
        {
            Key = "NetherRed",
            DisplayName = "下界红",
            Hex = "#B3392E",
            Description = "地狱疣块红，整条标题栏覆盖一层低透明度的地狱疣块贴图。"
        },
        new()
        {
            Key = "MethylOrange",
            DisplayName = "甲基橙",
            Hex = "#EE7B1E",
            Description = "甲基橙指示剂橙，右上角用像素字写化学式 C14H14N3NaO3S。"
        },
        new()
        {
            Key = "AmethystPurple",
            DisplayName = "水晶紫",
            Hex = "#9A5CE0",
            Description = "紫水晶块紫，整条标题栏覆盖一层低透明度的紫水晶块贴图。"
        },
        new()
        {
            Key = "MikuGreen",
            DisplayName = "初音绿",
            Hex = "#39C5BB",
            Description = "初音未来主题色。"
        },
        new()
        {
            Key = "KaoYinGreen",
            DisplayName = "烤音绿",
            Hex = "#33CCBB",
            Description = "烤音主题色。"
        }
    ];

    /// <summary>回声洞语录。</summary>
    public IReadOnlyList<string> GetQuotes() =>
    [
        "帝国之辉，终不熄灭的光！",
        "Welcome to the SGU！",
        "PCL Swordsman！",
        "剑客在此，谁敢造次。",
        "今天也是给主城建墙的一天。",
        "服主说这个 bug 是特性，那就当它是特性。",
        "存档备份做了吗？没做就先备份。",
        "回声洞的回声，来自 SGU 的每一块方块。",
        "红石一时爽，一直红石一直爽。",
        "别在主城放 TNT，真的。",
        "先有帝国，后有辉光。",
        "这把剑磨了三年，还没钝。"
    ];

    #region 插件页面（示范 IPluginUiProvider）

    /// <summary>
    ///     注册一个页面：只给标题 / 图标 / 内容工厂，侧栏条目和容器由宿主负责。
    ///     宿主每次打开都会调用 <see cref="PluginPageDefinition.CreatePage" />，所以这里直接返回新控件。
    /// </summary>
    public IReadOnlyList<PluginPageDefinition> GetPages() =>
    [
        new()
        {
            Id = "sgu.pack.overview",
            Title = "SGU 插件包",
            Description = "这个插件给启动器提供了哪些东西 —— 页面本身也是插件给的。",
            Icon = "lucide/palette",
            SortOrder = 10,
            CreatePage = BuildOverviewPage
        }
    ];

    /// <summary>页面内容：扩展主题一览 + 回声洞语录数量。</summary>
    private System.Windows.FrameworkElement BuildOverviewPage(IPluginContext context)
    {
        var panel = new System.Windows.Controls.StackPanel();

        panel.Children.Add(SectionTitle("扩展主题"));
        foreach (var theme in GetThemes())
            panel.Children.Add(ThemeRow(theme));

        panel.Children.Add(SectionTitle($"回声洞语录（{GetQuotes().Count} 条）"));
        panel.Children.Add(TextRow(string.Join(" / ", GetQuotes().Take(4)) + " ……"));

        panel.Children.Add(SectionTitle("数据目录"));
        panel.Children.Add(TextRow(context.DataDirectory));

        return panel;
    }

    private static System.Windows.Controls.TextBlock SectionTitle(string text) => new()
    {
        Text = text,
        FontSize = 13,
        FontWeight = System.Windows.FontWeights.Bold,
        Margin = new System.Windows.Thickness(2, 8, 0, 6)
    };

    private static System.Windows.FrameworkElement TextRow(string text) => new System.Windows.Controls.TextBlock
    {
        Text = text,
        FontSize = 12,
        TextWrapping = System.Windows.TextWrapping.Wrap,
        Margin = new System.Windows.Thickness(4, 0, 0, 6),
        Opacity = 0.85
    };

    /// <summary>一行主题：色块 + 名称 + 主色 + 说明。</summary>
    private static System.Windows.FrameworkElement ThemeRow(PluginThemeDefinition theme)
    {
        var grid = new System.Windows.Controls.Grid { Margin = new System.Windows.Thickness(4, 0, 0, 6) };
        grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition
            { Width = new System.Windows.GridLength(22) });
        grid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition());

        var swatch = new System.Windows.Controls.Border
        {
            Width = 16,
            Height = 16,
            CornerRadius = new System.Windows.CornerRadius(4),
            VerticalAlignment = System.Windows.VerticalAlignment.Top,
            Background = ParseBrush(theme.Hex)
        };
        grid.Children.Add(swatch);

        var text = new System.Windows.Controls.StackPanel();
        text.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = $"{theme.DisplayName}（{theme.Hex}）",
            FontSize = 12,
            FontWeight = System.Windows.FontWeights.SemiBold
        });
        if (!string.IsNullOrWhiteSpace(theme.Description))
            text.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = theme.Description,
                FontSize = 12,
                Opacity = 0.8,
                TextWrapping = System.Windows.TextWrapping.Wrap
            });

        System.Windows.Controls.Grid.SetColumn(text, 1);
        grid.Children.Add(text);
        return grid;
    }

    private static System.Windows.Media.Brush ParseBrush(string? hex)
    {
        try
        {
            return (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(hex!)!;
        }
        catch
        {
            return System.Windows.Media.Brushes.Gray;
        }
    }

    #endregion
}
