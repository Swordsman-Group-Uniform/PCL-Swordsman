using System.IO;
using System.Linq;
using PCL.PluginSdk;

namespace Welcome;

/// <summary>
///     欢迎语插件：只提供「欢迎语池」，抽取与显示由宿主完成
///     （宿主知道启动次数、当前时段、玩家名，也负责右下角小提示）。
///     <para>
///         自定义：本插件数据目录下的 <c>welcome.txt</c>，一行一条，空行与 <c>#</c> 开头的行会被忽略。
///         首次运行若不存在会自动写入默认池（含占位符 <c>LAUNCHER_BOOT_COUNT</c> / <c>TIME</c> / <c>PLAYERNAME</c>）。
///     </para>
///     <para>
///         本插件同时是「插件设置」与「宿主事件」两个新能力的示范：
///         <list type="bullet">
///             <item>用 <see cref="IPluginSettingsProvider" /> 把开关交给宿主渲染（宿主写进本插件的配置，插件只读）；</item>
///             <item>用 <see cref="IPluginContext.Events" /> 订阅主题变化，示范事件总线用法。</item>
///         </list>
///     </para>
/// </summary>
public sealed class WelcomePlugin : IPlugin, IWelcomeMessagesProvider, IPluginSettingsProvider
{
    private const string PoolFileName = "welcome.txt";

    /// <summary>设置键：欢迎语总开关。</summary>
    private const string KeyEnabled = "welcome.enabled";

    /// <summary>设置键：欢迎语来源（file / builtin）。</summary>
    private const string KeySource = "welcome.source";

    private static readonly string[] DefaultPool =
    [
        "欢迎使用 PCL Swordsman！",
        "记得常常看看插件市场有没有上新！",
        "这是 PCL Swordsman 与你相见的第 LAUNCHER_BOOT_COUNT 次！",
        "TIME 好，PLAYERNAME！",
        "愿 PCL Swordsman 与你同在！",
        "你爱我，我爱你，PCLS 甜蜜蜜！",
        "愿你永远见不到 Crash Reports！",
        "PLAYERNAME，今天想玩点什么？",
        "也可以试试 PCLCE！"
    ];

    private IPluginContext? _context;
    private IReadOnlyList<string> _fileMessages = DefaultPool;
    private IDisposable? _themeSubscription;

    public void Initialize(IPluginContext context)
    {
        _context = context;
        try
        {
            var path = Path.Combine(context.DataDirectory, PoolFileName);
            if (!File.Exists(path))
            {
                File.WriteAllLines(path, DefaultPool);
                context.Log.Info($"已写入默认欢迎语池（{DefaultPool.Length} 条）：{path}");
            }

            _fileMessages = LoadPool(path);
            context.Log.Info($"欢迎语已加载：{_fileMessages.Count} 条");

            // 宿主事件示范：切主题时打一条日志（没有声明 events 能力时这里只会收到一条警告）
            _themeSubscription = context.Events.Subscribe<ThemeChangedEvent>(e =>
                context.Log.Info($"主题已切换：{e.ThemeDisplayName}（深色={e.IsDarkMode}）"));
        }
        catch (Exception ex)
        {
            context.Log.Error("读取欢迎语池失败，改用内置默认池", ex);
            _fileMessages = DefaultPool;
        }
    }

    public void Dispose() => _themeSubscription?.Dispose();

    public IReadOnlyList<string> GetWelcomeMessages() => CurrentPool();

    /// <summary>宿主「插件设置」面板读取的就是这里。</summary>
    public IReadOnlyList<PluginSettingDefinition> GetSettings() =>
    [
        new()
        {
            Key = KeyEnabled,
            Title = "启动时显示欢迎语",
            Description = "关掉之后启动时不再弹右下角的欢迎提示。",
            Type = PluginSettingType.Toggle,
            DefaultValue = "true"
        },
        new()
        {
            Key = KeySource,
            Title = "欢迎语来源",
            Description = "「自定义文件」用数据目录里的 welcome.txt，「内置」用插件自带的那几条。",
            Type = PluginSettingType.Choice,
            DefaultValue = "file",
            Options =
            [
                new PluginSettingOption { Value = "file", Title = "自定义文件（welcome.txt）" },
                new PluginSettingOption { Value = "builtin", Title = "内置（插件自带）" }
            ]
        }
    ];

    /// <summary>按当前配置算出要交给宿主的池（总开关关掉时为空池）。</summary>
    private IReadOnlyList<string> CurrentPool()
    {
        var context = _context;
        if (context is null)
            return _fileMessages;

        try
        {
            if (!context.Config.Get(KeyEnabled, true))
                return [];

            var source = context.Config.Get(KeySource, "file");
            return string.Equals(source, "builtin", StringComparison.OrdinalIgnoreCase)
                ? DefaultPool
                : _fileMessages;
        }
        catch (Exception ex)
        {
            context.Log.Error("读取欢迎语设置失败，按默认处理", ex);
            return _fileMessages;
        }
    }

    private static IReadOnlyList<string> LoadPool(string path)
    {
        var lines = File.ReadAllLines(path)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToArray();
        return lines.Length > 0 ? lines : DefaultPool;
    }
}
