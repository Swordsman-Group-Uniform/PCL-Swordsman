using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PCL.Core.App.Localization;
using PCL.PluginHost;
using PCL.PluginSdk;

namespace PCL;

/// <summary>
///     「设置 → 插件」：把**声明了 <c>ui.settings</c> 能力**的插件设置集中在这里。
///     <para>
///         每个插件一张卡片，卡片里是 <see cref="PluginSettingsForm" /> 按插件声明的 schema 生成的控件行；
///         与「工具 → 插件 → 插件管理」里点齿轮看到的是同一份设置、同一份渲染实现。
///     </para>
/// </summary>
public partial class PageSetupPlugins
{
    private bool _hostSubscribed;

    public PageSetupPlugins()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            PluginHostBootstrap.Ready -= OnHostReady;
            PluginHostBootstrap.Ready += OnHostReady;
            SubscribeHost();
            Refresh();
        };
        Unloaded += (_, _) =>
        {
            PluginHostBootstrap.Ready -= OnHostReady;
            UnsubscribeHost();
        };
    }

    #region 宿主事件

    private void SubscribeHost()
    {
        if (_hostSubscribed) return;
        var manager = PluginHostBootstrap.Manager;
        if (manager is null) return;
        manager.Changed += OnHostChanged;
        _hostSubscribed = true;
    }

    private void UnsubscribeHost()
    {
        if (!_hostSubscribed) return;
        PluginHostBootstrap.Manager?.Changed -= OnHostChanged;
        _hostSubscribed = false;
    }

    private void OnHostReady()
    {
        ModBase.RunInUi(() =>
        {
            if (!IsLoaded) return;
            SubscribeHost();
            Refresh();
        });
    }

    private void OnHostChanged()
    {
        ModBase.RunInUi(() =>
        {
            if (IsLoaded) Refresh();
        });
    }

    #endregion

    /// <summary>重建整页：一个插件一张卡片。</summary>
    public void Refresh()
    {
        try
        {
            PanPluginCards.Children.Clear();

            var manager = PluginHostBootstrap.Manager;
            var shown = 0;

            if (manager is { IsInitialized: true })
            {
                foreach (var (descriptor, provider) in manager.GetProviderEntries<IPluginSettingsProvider>())
                {
                    // 能力门禁：声明了 ui.settings 的插件才会出现在设置页
                    if ((descriptor.Capabilities & PluginCapability.UiSettings) == 0) continue;

                    var rows = PluginSettingsForm.BuildRows(descriptor, provider);
                    if (rows.Count == 0) continue;

                    PanPluginCards.Children.Add(BuildPluginCard(descriptor, rows));
                    shown++;
                }
            }

            PanEmpty.Visibility = shown == 0 ? Visibility.Visible : Visibility.Collapsed;

            if (shown > 0)
                ModBase.Log($"[Plugin] 设置页已渲染 {shown} 个插件的设置", ModBase.LogLevel.Debug);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[Plugin] 渲染设置页插件分区失败", ModBase.LogLevel.Hint);
        }
    }

    private static FrameworkElement BuildPluginCard(PluginDescriptor plugin, List<FrameworkElement> rows)
    {
        var card = new MyCard
        {
            Margin = new Thickness(0, 0, 0, 15),
            Title = string.IsNullOrWhiteSpace(plugin.Name) ? plugin.Id : plugin.Name
        };

        var panel = new StackPanel { Margin = new Thickness(20, 36, 18, 16) };

        // 来源说明：来自插件 x（版本），点齿轮可以改同一份设置
        var source = new TextBlock
        {
            Text = Lang.Text("Setup.Plugins.From", plugin.Name, plugin.Version),
            FontSize = 12d,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 12),
            Foreground = Application.Current?.TryFindResource("ColorBrushGray3") as Brush ?? Brushes.Gray
        };
        panel.Children.Add(source);

        foreach (var row in rows)
            panel.Children.Add(row);

        // MyCard 的内容入口
        card.Children.Add(panel);
        return card;
    }
}
