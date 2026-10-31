using System.Windows;
using System.Windows.Controls;
using PCL.Core.App;
using PCL.PluginHost;
using PCL.PluginSdk;

using PCL.Core.App.Localization;
namespace PCL;

public partial class PageToolsLeft
{
    private bool isLoad;
    private bool isPageSwitched; // 如果在 Loaded 前切换到其他页面，会导致触发 Loaded 时再次切换一次

    public PageToolsLeft()
    {
        InitializeComponent();
        AnimatedControl = PanItem;
        Loaded += PageLinkLeft_Loaded;
        Unloaded += PageOtherLeft_Unloaded;
    }

    private void PageLinkLeft_Loaded(object sender, RoutedEventArgs e)
    {
        var isHiddenPage = false;
        var hide = Config.Preference.Hide;

        if (ItemGameLink.Checked && hide.ToolsGameLink) isHiddenPage = true;
        if (ItemTest.Checked && hide.ToolsTest) isHiddenPage = true;
        if (PageSetupUI.HiddenForceShow)
            isHiddenPage = false;
        // 若页面错误，或尚未加载，则继续
        if (isLoad && !isHiddenPage)
            return;
        isLoad = true;
        // 刷新子页面隐藏情况
        PageSetupUI.HiddenRefresh();
        // 插件注册的页面：每次进入侧栏都重建一次（插件可能刚加载完）
        RefreshPluginPages();
        SubscribePluginHost();
        // 选择第一个未被禁用的子页面
        if (isPageSwitched) 
            return;
        var hideCfg = Config.Preference.Hide;
        // 开发调试钩子：PCL_FORCE_PLUGIN_PAGE=1 时直接进入插件管理页（便于无人值守验证插件 UI）
        if (Environment.GetEnvironmentVariable("PCL_FORCE_PLUGIN_PAGE") == "1")
        {
            ItemPluginManage.SetChecked(true, false, false);
            return;
        }
        if (!hideCfg.ToolsGameLink)
            ItemGameLink.SetChecked(true, false, false);
        else if (!hideCfg.ToolsTest)
            ItemTest.SetChecked(true, false, false);
        else
            ItemGameLink.SetChecked(true, false, false);
    }

    private void PageOtherLeft_Unloaded(object sender, RoutedEventArgs e)
    {
        isPageSwitched = false;
        UnsubscribePluginHost();
    }

    public void Refresh(object sender, EventArgs e)
    {
        var button = (MyIconButton)sender;
        if (button.Tag is null)
            return;
        double id = ModBase.Val(button.Tag);
        switch (id)
        {
            case (double)FormMain.PageSubType.ToolsGameLink:
            {
                if (ModMain.frmToolsGameLink is null)
                    ModMain.frmToolsGameLink = new PageToolsGameLink();
                ModMain.frmToolsGameLink.Reload();
                ItemGameLink.Checked = true;
                break;
            }
        }
    }

    #region 页面切换

    /// <summary>
    ///     当前页面的编号。
    /// </summary>
    public FormMain.PageSubType pageID = FormMain.PageSubType.ToolsGameLink;

    /// <summary>
    ///     勾选事件改变页面。
    /// </summary>
    private void PageCheck(object senderRaw, ModBase.RouteEventArgs e)
    {
        var sender = (MyListItem)senderRaw;
        // 尚未初始化控件属性时，sender.Tag 为 Nothing，会导致切换到页面 0
        // 若使用 IsLoaded，则会导致模拟点击不被执行（模拟点击切换页面时，控件的 IsLoaded 为 False）
        if (sender.Tag is not null)
        {
            // 内置页面被选中：单选组已经把它兄弟里的高亮清掉了，这里只需要切页
            _activePluginPage = null;
            PageChange((FormMain.PageSubType)ModBase.Val(sender.Tag));
        }
    }

    public object PageGet(FormMain.PageSubType? id = null)
    {
        var targetID = id ?? pageID;
        switch (id)
        {
            case FormMain.PageSubType.ToolsGameLink:
            {
                if (ModMain.frmToolsGameLink is null)
                    ModMain.frmToolsGameLink = new PageToolsGameLink();
                return ModMain.frmToolsGameLink;
            }
            case FormMain.PageSubType.ToolsTest:
            {
                if (ModMain.frmToolsTest is null)
                    ModMain.frmToolsTest = new PageToolsTest();
                return ModMain.frmToolsTest;
            }
            case FormMain.PageSubType.ToolsPluginManage:
            {
                if (ModMain.frmToolsPluginManage is null)
                    ModMain.frmToolsPluginManage = new PageToolsPluginManage();
                return ModMain.frmToolsPluginManage;
            }
            case FormMain.PageSubType.ToolsPluginMarket:
            {
                if (ModMain.frmToolsPluginMarket is null)
                    ModMain.frmToolsPluginMarket = new PageToolsPluginMarket();
                return ModMain.frmToolsPluginMarket;
            }
            case FormMain.PageSubType.ToolsPluginPage:
            {
                ModMain.frmToolsPluginPage ??= new PagePluginHost();
                if (_activePluginPage is not null)
                    ModMain.frmToolsPluginPage.ShowPage(_activePluginPage.Descriptor, _activePluginPage.Definition);
                return ModMain.frmToolsPluginPage;
            }
            default:
            {
                throw new Exception("未知的更多子页面种类：" + (int)id);
            }
        }
    }

    /// <summary>
    ///     切换现有页面。
    /// </summary>
    public void PageChange(FormMain.PageSubType id)
    {
        if (pageID == id)
            return;
        ModAnimation.AniBeginControl();
        isPageSwitched = true;
        try
        {
            PageChangeRun((MyPageRight)PageGet(id));
            pageID = id;
        }
        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                $"切换分页面失败（ID {(int)id}）",
                ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Tools.Error.OperationFailed"));
        }
        finally
        {
            ModAnimation.AniEndControl();
        }
    }

    private static void PageChangeRun(MyPageRight target)
    {
        ModAnimation.AniStop("FrmMain PageChangeRight"); // 停止主页面的右页面切换动画，防止它与本动画一起触发多次 PageOnEnter
        if (target.Parent is not null)
            target.SetValue(ContentPresenter.ContentProperty, null);
        ModMain.frmMain.pageRight = target;
        ((MyPageRight)ModMain.frmMain.PanMainRight.Child).PageOnExit();
        ModAnimation.AniStart(new[]
        {
            ModAnimation.AaCode(() =>
            {
                ((MyPageRight)ModMain.frmMain.PanMainRight.Child).PageOnForceExit();
                ModMain.frmMain.PanMainRight.Child = ModMain.frmMain.pageRight;
                ModMain.frmMain.pageRight.Opacity = 0d;
            }, 130),
            ModAnimation.AaCode(() =>
            {
                // 延迟触发页面通用动画，以使得在 Loaded 事件中加载的控件得以处理
                ModMain.frmMain.pageRight.Opacity = 1d;
                ModMain.frmMain.pageRight.PageOnEnter();
            }, 30, true)
        }, "PageLeft PageChange");
    }

    #endregion

    #region 插件注册的页面

    /// <summary>侧栏里的一个插件页面条目。</summary>
    private sealed record PluginPageEntry(
        PluginDescriptor Descriptor,
        PluginPageDefinition Definition,
        MyListItem Item)
    {
        public string Key => Descriptor.Id + "::" + Definition.Id;
    }

    private readonly List<PluginPageEntry> _pluginPages = [];
    private PluginPageEntry? _activePluginPage;
    private bool _pluginHostSubscribed;

    private void SubscribePluginHost()
    {
        if (_pluginHostSubscribed) return;
        var manager = PluginHostBootstrap.Manager;
        if (manager is null)
        {
            // 宿主还没初始化：等它就绪再建条目
            PluginHostBootstrap.Ready -= OnPluginHostReady;
            PluginHostBootstrap.Ready += OnPluginHostReady;
            return;
        }

        manager.Changed += OnPluginHostChanged;
        _pluginHostSubscribed = true;
    }

    private void UnsubscribePluginHost()
    {
        PluginHostBootstrap.Ready -= OnPluginHostReady;
        if (!_pluginHostSubscribed) return;
        PluginHostBootstrap.Manager?.Changed -= OnPluginHostChanged;
        _pluginHostSubscribed = false;
    }

    private void OnPluginHostReady()
    {
        ModBase.RunInUi(() =>
        {
            if (!IsLoaded) return;
            SubscribePluginHost();
            RefreshPluginPages();
        });
    }

    private void OnPluginHostChanged()
    {
        // 可能在插件宿主的后台线程触发
        ModBase.RunInUi(() =>
        {
            if (IsLoaded) RefreshPluginPages();
        });
    }

    /// <summary>
    ///     按「已启用插件 + ui.page 能力」重建侧栏条目。
    ///     插件被禁用 / 删除后，对应条目与已打开的页面都会立刻收掉。
    /// </summary>
    private void RefreshPluginPages()
    {
        _isRefreshingPluginPages = true;
        try
        {
            RefreshPluginPagesCore();
        }
        finally
        {
            _isRefreshingPluginPages = false;
        }
    }

    /// <summary>刷新期间忽略 Check 回调（重设选中态会触发它）。</summary>
    private bool _isRefreshingPluginPages;

    private void RefreshPluginPagesCore()
    {
        var manager = PluginHostBootstrap.Manager;
        var entries = new List<PluginPageEntry>();

        if (manager is { IsInitialized: true })
        {
            foreach (var (descriptor, provider) in manager.GetProviderEntries<IPluginUiProvider>())
            {
                // 能力门禁：ui.page（给页面）或 ui.sidebar（只给侧栏条目）任一即可
                const PluginCapability pageCaps = PluginCapability.UiPage | PluginCapability.UiSidebar;
                if ((descriptor.Capabilities & pageCaps) == 0)
                {
                    ModBase.Log($"[Plugin] {descriptor.Id} 提供了页面但没有声明 ui.page / ui.sidebar 能力，已忽略",
                        ModBase.LogLevel.Debug);
                    continue;
                }

                IReadOnlyList<PluginPageDefinition> pages;
                try
                {
                    pages = provider.GetPages() ?? [];
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, $"[Plugin] {descriptor.Id} 的 GetPages() 抛出异常", ModBase.LogLevel.Hint);
                    continue;
                }

                foreach (var definition in pages)
                {
                    if (definition is null || string.IsNullOrWhiteSpace(definition.Id))
                        continue;

                    if (definition.CreatePage is null)
                    {
                        ModBase.Log($"[Plugin] {descriptor.Id}/{definition.Id} 没有 CreatePage，已忽略",
                            ModBase.LogLevel.Debug);
                        continue;
                    }

                    entries.Add(new PluginPageEntry(descriptor, definition,
                        CreatePluginPageItem(descriptor, definition)));
                }
            }
        }

        entries = entries
            .OrderBy(entry => entry.Definition.SortOrder)
            .ThenBy(entry => entry.Descriptor.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Definition.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // 先把上一批条目从 PanItem 里摘掉（它们就在内置条目之间，不能整块清空）
        foreach (var oldEntry in _pluginPages)
            PanItem.Children.Remove(oldEntry.Item);

        _pluginPages.Clear();

        // 重建：直接插在「插件页面」标题后面，与内置条目**同一个父面板** —— MyListItem 的单选逻辑
        // 只扫描同一父面板里的兄弟，放进独立面板就会出现「两个都能选中」的问题
        var insertIndex = PanItem.Children.IndexOf(TextPluginPages) + 1;
        foreach (var entry in entries)
        {
            _pluginPages.Add(entry);
            PanItem.Children.Insert(insertIndex++, entry.Item);
        }

        var hasPages = _pluginPages.Count > 0;
        TextPluginPages.Visibility = hasPages ? Visibility.Visible : Visibility.Collapsed;

        if (_activePluginPage is null)
            return;

        // 当前打开的插件页面还在不在？
        var stillThere = _pluginPages.FirstOrDefault(entry => entry.Key == _activePluginPage.Key);
        if (stillThere is not null)
        {
            _activePluginPage = stillThere;
            if (pageID == FormMain.PageSubType.ToolsPluginPage)
                stillThere.Item.SetChecked(true, false, false);
            return;
        }

        // 插件页面没了：清掉内容并退回插件管理页
        _activePluginPage = null;
        ModMain.frmToolsPluginPage?.ClearPage();
        if (pageID == FormMain.PageSubType.ToolsPluginPage)
        {
            ModBase.Log("[Plugin] 当前插件页面已不可用，退回插件管理页", ModBase.LogLevel.Debug);
            ItemPluginManage.SetChecked(true, false, false);
            PageChange(FormMain.PageSubType.ToolsPluginManage);
        }
    }

    private MyListItem CreatePluginPageItem(PluginDescriptor descriptor, PluginPageDefinition definition)
    {
        var item = new MyListItem
        {
            Type = MyListItem.CheckType.RadioBox,
            IsScaleAnimationEnabled = false,
            MinPaddingRight = 35,
            Height = 36d,
            VerticalAlignment = VerticalAlignment.Top,
            Title = string.IsNullOrWhiteSpace(definition.Title) ? definition.Id : definition.Title,
            SvgIcon = SvgIconControlHelper.IconExists(definition.Icon)
                ? definition.Icon!
                : SvgIconControlHelper.IconExists(descriptor.Manifest?.Ui?.Icon)
                    ? descriptor.Manifest!.Ui!.Icon!
                    : "lucide/puzzle",
            LogoScale = 0.9d,
            Tag = ((int)FormMain.PageSubType.ToolsPluginPage).ToString()
        };
        item.Check += (sender, _) =>
        {
            if (_isRefreshingPluginPages) return;
            if (sender is not MyListItem listItem) return;
            var entry = _pluginPages.FirstOrDefault(candidate => ReferenceEquals(candidate.Item, listItem));
            if (entry is null) return;
            OpenPluginPage(entry);
        };
        return item;
    }

    /// <summary>打开某个插件页面：已经在宿主页里就直接换内容，否则走正常切页流程。</summary>
    private void OpenPluginPage(PluginPageEntry entry)
    {
        _activePluginPage = entry;

        // 不再手动互斥：插件条目与内置条目在同一单选组里，MyListItem 自己会保证只有一个选中
        if (pageID == FormMain.PageSubType.ToolsPluginPage)
        {
            ModMain.frmToolsPluginPage?.ShowPage(entry.Descriptor, entry.Definition);
            return;
        }

        PageChange(FormMain.PageSubType.ToolsPluginPage);
    }

    #endregion
}
