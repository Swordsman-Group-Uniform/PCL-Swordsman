using System.IO;
using System.Windows;
using AdvancedInstance.Features;
using AdvancedInstance.Ui;
using PCL.PluginSdk;

namespace AdvancedInstance;

/// <summary>
///     「高级实例管理」插件入口。
///     <para>
///         四项功能：
///         <list type="number">
///             <item>存档自动备份（默认 30 分钟一次、每个存档留 10 份；存档被删后最后一份留 30 天）</item>
///             <item>Mod 依赖关系与冲突检查（页面 + 「Mod 管理」页面板 + 启动前警告）</item>
///             <item>存档基本数据编辑（level.dat）</item>
///             <item>Litematica 投影适配版本批量修改（首次使用弹风险提示）</item>
///         </list>
///     </para>
///     <para>
///         本插件是**外置**插件（不进启动器安装包），需要宿主提供两个扩展点：
///         <c>launch-gate</c>（启动前守门，snapshot-89 新增）与 <c>instance.mod-panel</c>
///         （Mod 管理页插件面板，snapshot-89 新增）。老宿主上会退化成「只有自己的页面可用」。
///     </para>
/// </summary>
public sealed class AdvancedInstancePlugin : IPlugin, IPluginUiProvider, IPluginLaunchGate,
    IPluginInstanceModPanelProvider
{
    private IPluginContext? _context;
    private SaveBackupService? _backup;
    private IDisposable? _instanceSubscription;

    /// <summary>当前实例（由事件 / 首次读取更新，后台备份线程只读这份缓存）。</summary>
    private (string Name, string Path)? _currentInstance;

    public void Initialize(IPluginContext context)
    {
        _context = context;
        _backup = new SaveBackupService(context);

        context.Log.Info($"高级实例管理已加载：自动备份 {(context.Config.Get("saveBackup.enabled", true) ? "开" : "关")}，" +
                         $"每 {context.Config.Get("saveBackup.intervalMinutes", 30)} 分钟一次");

        // 实例切换时更新缓存（后台备份线程要用，不能每次都去问宿主）
        _instanceSubscription = context.Events.Subscribe<InstanceSwitchedEvent>(e =>
        {
            _currentInstance = string.IsNullOrWhiteSpace(e.InstancePath) ? null : (e.InstanceName, e.InstancePath);
            _context?.Log.Info($"当前实例切换为 {e.InstanceName}");
        });
    }

    public void Enable()
    {
        try
        {
            // 插件是在**后台线程**加载的，而读实例信息可能触发宿主的 minecraft.read 授权弹窗。
            // 把这次读取挪回 UI 线程（启动完成后再问用户），后台备份线程只读缓存的实例路径。
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher is not null)
                dispatcher.BeginInvoke(new Action(RefreshCurrentInstance),
                    System.Windows.Threading.DispatcherPriority.Background);
            else
                RefreshCurrentInstance();

            _backup?.Start(() => _currentInstance);
        }
        catch (Exception ex)
        {
            _context?.Log.Warn($"启动自动备份失败：{ex.Message}");
        }
    }

    public void Disable() => _backup?.Stop();

    public void Dispose()
    {
        _backup?.Stop();
        _instanceSubscription?.Dispose();
        _instanceSubscription = null;
    }

    /// <summary>在 UI 线程读一次当前实例（顺带触发宿主的 minecraft.read 授权询问）。</summary>
    private void RefreshCurrentInstance()
    {
        try
        {
            var instance = _context?.Minecraft.CurrentInstance;
            _currentInstance = instance is null || string.IsNullOrWhiteSpace(instance.Path)
                ? null
                : (instance.Name, instance.Path);
        }
        catch (Exception ex)
        {
            _context?.Log.Warn($"读取当前实例失败：{ex.Message}");
        }
    }

    private (string Name, string Path)? CurrentInstanceCached()
    {
        // 页面里调用时顺便刷新一次：用户可能刚在启动页换了实例
        RefreshCurrentInstance();
        return _currentInstance;
    }

    #region 页面

    public IReadOnlyList<PluginPageDefinition> GetPages() =>
    [
        new()
        {
            Id = "advanced-instance.main",
            Title = "高级实例管理",
            Description = "存档自动备份 · Mod 依赖检查 · level.dat 编辑 · 投影版本适配",
            Icon = "lucide/archive",
            SortOrder = 20,
            CreatePage = context => MainPage.Build(context, GetBackup(context), CurrentInstanceCached)
        }
    ];

    private SaveBackupService GetBackup(IPluginContext context) => _backup ??= new SaveBackupService(context);

    #endregion

    #region Mod 管理页面板

    public IReadOnlyList<PluginModPanelDefinition> GetModPanels() =>
    [
        new()
        {
            Id = "advanced-instance.mod-deps",
            Title = "Mod 依赖关系",
            Description = "检查依赖缺失、依赖版本不匹配、重复安装与明确声明的不兼容。",
            SortOrder = 50,
            CreatePanel = context => ModPanel.Build(context, CurrentInstanceCached)
        }
    ];

    #endregion

    #region 启动前守门

    public PluginLaunchGateResult? BeforeLaunch(PluginLaunchGateContext context)
    {
        try
        {
            var plugin = _context;
            if (plugin is null) return null;

            var instancePath = string.IsNullOrWhiteSpace(context.InstancePath)
                ? _currentInstance?.Path
                : context.InstancePath;
            if (string.IsNullOrWhiteSpace(instancePath)) return null;

            var modsPath = Path.Combine(instancePath, "mods");
            if (!Directory.Exists(modsPath)) return null;

            var result = ModDependencyAnalyzer.Analyze(modsPath);
            if (!result.HasProblems) return null;

            // 只在有「严重问题」时打断启动，纯提示类（循环依赖）不打扰
            if (result.ErrorCount == 0) return null;

            var answer = MessageBox.Show(
                $"检测到 {result.ErrorCount} 个 Mod 问题，可能导致游戏崩溃或 Mod 不生效：\n\n" +
                result.ProblemText() + "\n\n" +
                "仍然要启动吗？（在「实例 → Mod 管理」页可以查看完整的依赖关系）",
                "Mod 依赖检查 · 高级实例管理",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (answer == MessageBoxResult.Yes)
            {
                plugin.Log.Warn($"用户选择忽略 {result.ErrorCount} 个 Mod 问题继续启动");
                return PluginLaunchGateResult.Permit();
            }

            plugin.Log.Info("用户因 Mod 依赖问题取消了启动");
            return PluginLaunchGateResult.Deny("已按你的选择取消启动（Mod 依赖检查）");
        }
        catch (Exception ex)
        {
            _context?.Log.Warn($"启动前检查 Mod 依赖失败：{ex.Message}");
            return null; // 自己出错不能拦启动
        }
    }

    #endregion
}
