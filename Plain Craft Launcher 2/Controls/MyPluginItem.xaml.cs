using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using PCL.Core.App.Localization;
using PCL.PluginHost;
using PCL.PluginSdk;

namespace PCL;

/// <summary>
///     插件列表项（视觉对齐「实例设置 → 模组」的 <see cref="MyLocalCompItem" />：44px 行高、
///     左侧色条、34px 图标、标题 + 副信息、右侧图标按钮），并支持多选高亮。
/// </summary>
public partial class MyPluginItem
{
    private PluginDescriptor? _plugin;

    public MyPluginItem()
    {
        InitializeComponent();
        MouseLeftButtonUp += (_, e) =>
        {
            Clicked?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        };
    }

    /// <summary>点击了条目本体（用于切换选中）。</summary>
    public event EventHandler? Clicked;

    /// <summary>点击了「启用 / 禁用」。</summary>
    public event EventHandler? ToggleRequested;

    /// <summary>点击了「打开插件目录」。</summary>
    public event EventHandler? OpenFolderRequested;

    /// <summary>点击了「插件设置」（只有插件实现了 <see cref="IPluginSettingsProvider" /> 时才显示按钮）。</summary>
    public event EventHandler? SettingsRequested;

    /// <summary>点击了「检查更新」（只有清单里写了 updateUrl 时才显示按钮）。</summary>
    public event EventHandler? UpdateRequested;

    public PluginDescriptor? Plugin => _plugin;

    public bool IsSelected { get; private set; }

    public void SetSelected(bool selected)
    {
        IsSelected = selected;
        Background = selected
            ? TryFindResource("ColorBrush6") as Brush ?? Brushes.LightSkyBlue
            : TryFindResource("ColorBrushSemiTransparent") as Brush ?? Brushes.Transparent;
    }

    public void Bind(PluginDescriptor plugin)
    {
        _plugin = plugin;

        LabTitle.Text = string.IsNullOrWhiteSpace(plugin.Name) ? plugin.Id : plugin.Name;

        // 图标：优先用清单里的 ui.icon（内置图标包里没有就退回拼图）
        var icon = plugin.Manifest?.Ui?.Icon;
        ImgIcon.Icon = SvgIconControlHelper.IconExists(icon) ? icon! : "lucide/puzzle";

        var info = $"{plugin.Version}  ·  {plugin.Id}  ·  {StateText(plugin.State)}  ·  {IntegrityText(plugin.Integrity)}";
        if (!string.IsNullOrWhiteSpace(plugin.Error))
            info += "  ·  " + plugin.Error;
        LabInfo.Text = info;

        // 左侧色条：可用=主题强调色，其余=灰
        BarAccent.Background = TryFindResource(plugin.IsUsable ? "ColorBrush3" : "ColorBrushGray4") as Brush
                              ?? Brushes.Gray;

        // 出错 / 不兼容用红字提示，其余保持灰色副信息
        var isError = plugin.State is PluginState.Faulted or PluginState.Incompatible;
        LabInfo.Foreground = TryFindResource(isError ? "ColorBrushRedLight" : "ColorBrushGray3") as Brush
                            ?? Brushes.Gray;

        // 清单里写了 updateUrl 才显示「检查更新」
        BtnUpdate.Visibility = string.IsNullOrWhiteSpace(plugin.Manifest?.UpdateUrl)
            ? Visibility.Collapsed
            : Visibility.Visible;
        BtnUpdate.ToolTip = Lang.Text("Tools.Plugin.Action.CheckUpdate");

        // 只有实现了 IPluginSettingsProvider 的插件才显示齿轮
        var hasSettings = plugin.SettingsProvider is not null;
        BtnSettings.Visibility = hasSettings ? Visibility.Visible : Visibility.Collapsed;
        BtnSettings.ToolTip = Lang.Text("Tools.Plugin.Action.Settings");

        var enabledByUser = plugin.State != PluginState.Disabled;
        BtnToggle.SvgIcon = enabledByUser ? "lucide/power" : "lucide/play";
        BtnToggle.ToolTip = Lang.Text(enabledByUser ? "Tools.Plugin.Action.Disable" : "Tools.Plugin.Action.Enable");
        BtnFolder.ToolTip = Lang.Text("Tools.Plugin.Action.OpenFolder");
    }

    private static string IntegrityText(PluginIntegrityState state) => state switch
    {
        PluginIntegrityState.Verified => Lang.Text("Tools.Plugin.Integrity.Verified"),
        PluginIntegrityState.Mismatch => Lang.Text("Tools.Plugin.Integrity.Failed"),
        PluginIntegrityState.UntrustedKey => Lang.Text("Tools.Plugin.Integrity.UntrustedKey"),
        PluginIntegrityState.BadSignature => Lang.Text("Tools.Plugin.Integrity.Failed"),
        _ => Lang.Text("Tools.Plugin.Integrity.Undeclared")
    };

    private static string StateText(PluginState state) => state switch
    {
        PluginState.Enabled => Lang.Text("Tools.Plugin.State.Enabled"),
        PluginState.Loaded => Lang.Text("Tools.Plugin.State.Loaded"),
        PluginState.Disabled => Lang.Text("Tools.Plugin.State.Disabled"),
        PluginState.Incompatible => Lang.Text("Tools.Plugin.State.Incompatible"),
        PluginState.Faulted => Lang.Text("Tools.Plugin.State.Faulted"),
        _ => Lang.Text("Tools.Plugin.State.Discovered")
    };

    private void BtnToggle_Click(object sender, EventArgs e)
    {
        ToggleRequested?.Invoke(this, EventArgs.Empty);
    }

    private void BtnFolder_Click(object sender, EventArgs e)
    {
        OpenFolderRequested?.Invoke(this, EventArgs.Empty);
    }

    private void BtnSettings_Click(object sender, EventArgs e)
    {
        SettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void BtnUpdate_Click(object sender, EventArgs e)
    {
        UpdateRequested?.Invoke(this, EventArgs.Empty);
    }
}
