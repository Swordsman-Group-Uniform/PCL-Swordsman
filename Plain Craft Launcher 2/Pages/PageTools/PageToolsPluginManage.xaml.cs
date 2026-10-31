using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PCL.Core.App.Localization;
using PCL.Core.UI;
using PCL.PluginHost;
using PCL.PluginSdk;

namespace PCL;

/// <summary>
///     「工具 → 插件 → 插件管理」。布局与交互一比一移植自「实例设置 → 模组」
///     （<see cref="PageInstanceCompResource" />）：搜索框 + 操作栏 + 筛选/排序 + 列表 + 底部多选栏。
/// </summary>
public partial class PageToolsPluginManage
{
    /// <summary>实例级插件目录：&lt;启动器目录&gt;/PCL/Plugins/</summary>
    public static readonly string PluginRootPath = PluginHostBootstrap.PluginsDirectory;

    private readonly HashSet<string> _selected = new(StringComparer.OrdinalIgnoreCase);

    private bool _hostSubscribed;
    private bool _isBusy;
    private bool _isInitialized;
    private bool _isRendering;

    /// <summary>0=全部 1=已启用 2=已禁用 3=出错</summary>
    private int _filter;

    /// <summary>0=名称升序 1=名称降序 2=按状态</summary>
    private int _sortMode;

    public PageToolsPluginManage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            _isInitialized = true;
            SubscribeHost();
            // 宿主可能还在后台加载插件；等它初始化完成再刷一次，避免"插件列表一直是空的"
            PluginHostBootstrap.Ready -= OnHostReady;
            PluginHostBootstrap.Ready += OnHostReady;
            RefreshList();
        };
        Unloaded += (_, _) =>
        {
            PluginHostBootstrap.Ready -= OnHostReady;
            UnsubscribeHost();
        };
        SearchBox.TextChanged += (_, _) => RefreshList();
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
        var manager = PluginHostBootstrap.Manager;
        if (manager is not null) manager.Changed -= OnHostChanged;
        _hostSubscribed = false;
    }

    private void OnHostReady()
    {
        SubscribeHost();
        OnHostChanged();
    }

    private void OnHostChanged()
    {
        // 该事件可能在插件宿主的后台线程触发：所有 UI 访问（含 IsLoaded）都回到 UI 线程
        ModBase.RunInUi(() =>
        {
            if (IsLoaded) RefreshList();
        });
    }

    #endregion

    #region 列表

    private IReadOnlyList<PluginDescriptor> CurrentPlugins()
    {
        var manager = PluginHostBootstrap.Manager;
        return manager is { IsInitialized: true } ? manager.Plugins : Array.Empty<PluginDescriptor>();
    }

    /// <summary>重新读取宿主状态并重建列表（不触发插件加载）。</summary>
    public void Reload()
    {
        RefreshList();
    }

    private void RefreshList()
    {
        if (_isRendering) return;
        _isRendering = true;
        try
        {
            RefreshListCore();
        }
        finally
        {
            _isRendering = false;
        }
    }

    private void RefreshListCore()
    {
        var all = CurrentPlugins();
        var keyword = (SearchBox.Text ?? "").Trim();

        var enabledCount = all.Count(p => p.State is PluginState.Enabled or PluginState.Loaded);
        var disabledCount = all.Count(p => p.State == PluginState.Disabled);
        var errorCount = all.Count(p => p.State is PluginState.Faulted or PluginState.Incompatible);

        // 当前筛选已经空了（例如原来是「已禁用」、然后把所有插件都启用）→ 按 已启用 > 已禁用 > 出错 > 全部 顺延
        if (_filter != 0 && FilterCount(_filter, enabledCount, disabledCount, errorCount) == 0)
            _filter = PickFallbackFilter(enabledCount, disabledCount, errorCount);

        // 计数为 0 的筛选项隐藏；「已启用」在有内容时始终显示（即使全部都已启用）
        BtnFilterAll.Text = Lang.Text("Tools.Plugin.Filter.AllWithCount", all.Count);
        BtnFilterAll.Visibility = Visibility.Visible;

        BtnFilterEnabled.Text = Lang.Text("Tools.Plugin.Filter.EnabledWithCount", enabledCount);
        BtnFilterEnabled.Visibility = enabledCount > 0 ? Visibility.Visible : Visibility.Collapsed;

        BtnFilterDisabled.Text = Lang.Text("Tools.Plugin.Filter.DisabledWithCount", disabledCount);
        BtnFilterDisabled.Visibility = disabledCount > 0 ? Visibility.Visible : Visibility.Collapsed;

        BtnFilterError.Text = Lang.Text("Tools.Plugin.Filter.ErrorWithCount", errorCount);
        BtnFilterError.Visibility = errorCount > 0 ? Visibility.Visible : Visibility.Collapsed;

        // 同步单选状态（自动顺延后要让高亮跟上，不能出现「一个都没选中」）
        BtnFilterAll.SetChecked(_filter == 0, false, false);
        BtnFilterEnabled.SetChecked(_filter == 1, false, false);
        BtnFilterDisabled.SetChecked(_filter == 2, false, false);
        BtnFilterError.SetChecked(_filter == 3, false, false);

        var visible = all
            .Where(p => keyword.Length == 0 ||
                        p.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                        p.Id.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .Where(MatchesFilter)
            .ToList();

        visible = _sortMode switch
        {
            1 => visible.OrderByDescending(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            2 => visible.OrderBy(p => p.State).ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList(),
            // 默认排序遵守清单里的 ui.sortOrder（缺省 100），同档再按名称
            _ => visible
                .OrderBy(p => p.Manifest?.Ui?.SortOrder ?? 100)
                .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };

        // 清掉已不存在的选中项
        _selected.RemoveWhere(id => all.All(p => p.Id != id));

        PanList.Children.Clear();
        foreach (var plugin in visible)
        {
            var item = new MyPluginItem();
            item.Bind(plugin);
            item.SetSelected(_selected.Contains(plugin.Id));
            item.Clicked += OnItemClicked;
            item.ToggleRequested += OnItemToggleRequested;
            item.OpenFolderRequested += OnItemOpenFolderRequested;
            item.SettingsRequested += OnItemSettingsRequested;
            item.UpdateRequested += OnItemUpdateRequested;
            PanList.Children.Add(item);
        }

        // 一个插件都没有时：只保留「还没有发现插件」卡片，隐藏搜索框 / 操作栏 / 列表卡片
        var hasAny = all.Count > 0;
        SearchBox.Visibility = hasAny ? Visibility.Visible : Visibility.Collapsed;
        PanManage.Visibility = hasAny ? Visibility.Visible : Visibility.Collapsed;
        PanListBack.Visibility = hasAny ? Visibility.Visible : Visibility.Collapsed;

        // 空状态
        var isEmpty = visible.Count == 0;
        PanEmpty.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
        PanList.Visibility = isEmpty ? Visibility.Collapsed : Visibility.Visible;
        TxtEmptyTitle.Text = hasAny && keyword.Length > 0
            ? Lang.Text("Tools.Plugin.Manage.Empty.NoMatch", keyword)
            : Lang.Text("Tools.Plugin.Manage.Empty.Title");
        TxtEmptyDescription.Text = Lang.Text("Tools.Plugin.Manage.Empty.Description");

        UpdateSelectBar();

        // 设置面板开着的插件被删掉 / 停用了，就把面板收起来
        if (_settingsPlugin is not null && all.All(p => p.Id != _settingsPlugin.Id))
            HideSettings();
    }

    private bool MatchesFilter(PluginDescriptor plugin) => _filter switch
    {
        1 => plugin.State is PluginState.Enabled or PluginState.Loaded,
        2 => plugin.State == PluginState.Disabled,
        3 => plugin.State is PluginState.Faulted or PluginState.Incompatible,
        _ => true
    };

    private static int FilterCount(int filter, int enabledCount, int disabledCount, int errorCount) => filter switch
    {
        1 => enabledCount,
        2 => disabledCount,
        3 => errorCount,
        _ => 0
    };

    /// <summary>当前筛选空了以后顺延到哪个筛选：已启用 &gt; 已禁用 &gt; 出错 &gt; 全部。</summary>
    private static int PickFallbackFilter(int enabledCount, int disabledCount, int errorCount)
    {
        if (enabledCount > 0) return 1;
        if (disabledCount > 0) return 2;
        if (errorCount > 0) return 3;
        return 0;
    }

    // 事件签名必须与控件定义一致：MyRadioButton.Check 是 (MyRadioButton, bool)
    private void Filter_Check(MyRadioButton sender, bool raiseByMouse)
    {
        // _isRendering 期间是代码在同步单选状态，不能反过来改写 _filter
        if (!_isInitialized || _isRendering || sender.Tag is null) return;
        _filter = (int)ModBase.Val(sender.Tag);
        RefreshList();
    }

    // MyIconTextButton.Click 是 (object, ModBase.RouteEventArgs)
    private void BtnSort_Click(object sender, ModBase.RouteEventArgs e)
    {
        _sortMode = (_sortMode + 1) % 3;
        BtnSort.Text = Lang.Text(_sortMode switch
        {
            1 => "Tools.Plugin.Sort.NameDesc",
            2 => "Tools.Plugin.Sort.Status",
            _ => "Tools.Plugin.Sort.NameAsc"
        });
        RefreshList();
    }

    #endregion

    #region 多选

    private void UpdateSelectBar()
    {
        var count = _selected.Count;
        var any = count > 0;

        CardSelect.Visibility = any ? Visibility.Visible : Visibility.Collapsed;
        CardSelect.Opacity = any ? 1d : 0d;
        TransSelect.Y = any ? 0d : -10d;
        PanMain.Margin = new Thickness(25, 10, 25, any ? 92 : 10);

        if (!any) return;

        LabSelect.Text = Lang.Text("Tools.Plugin.SelectedCount", count);

        var selectedPlugins = CurrentPlugins().Where(p => _selected.Contains(p.Id)).ToList();
        BtnSelectEnable.IsEnabled = selectedPlugins.Any(p => p.State == PluginState.Disabled);
        BtnSelectDisable.IsEnabled = selectedPlugins.Any(p => p.State != PluginState.Disabled);
        BtnSelectDelete.IsEnabled = selectedPlugins.Count > 0;
    }

    private void OnItemUpdateRequested(object? sender, EventArgs e)
    {
        if (sender is not MyPluginItem { Plugin: { } plugin }) return;
        PluginUpdateService.CheckAndInstall(plugin);
    }

    private void OnItemClicked(object? sender, EventArgs e)
    {
        if (sender is not MyPluginItem { Plugin: { } plugin } item) return;
        if (!_selected.Remove(plugin.Id)) _selected.Add(plugin.Id);
        item.SetSelected(_selected.Contains(plugin.Id));
        UpdateSelectBar();
    }

    private void BtnManageSelectAll_Click(object sender, MouseButtonEventArgs e)
    {
        var visible = PanList.Children.OfType<MyPluginItem>()
            .Select(i => i.Plugin)
            .Where(p => p is not null)
            .Select(p => p!.Id)
            .ToList();

        if (_selected.Count >= visible.Count)
            _selected.Clear();
        else
            foreach (var id in visible) _selected.Add(id);

        RefreshList();
    }

    // MyIconTextButton.Click 是 (object, ModBase.RouteEventArgs)
    private void BtnSelectEnable_Click(object sender, ModBase.RouteEventArgs e) => SetSelectionEnabled(true);

    private void BtnSelectDisable_Click(object sender, ModBase.RouteEventArgs e) => SetSelectionEnabled(false);

    private void BtnSelectCancel_Click(object sender, ModBase.RouteEventArgs e)
    {
        _selected.Clear();
        RefreshList();
    }

    private void SetSelectionEnabled(bool enable)
    {
        var manager = PluginHostBootstrap.Manager;
        if (manager is null || _selected.Count == 0) return;

        // 批量启用时，先统计有多少「未校验」的包，一次性警告
        if (enable)
        {
            var unverified = _selected
                .Select(id => CurrentPlugins().FirstOrDefault(p => p.Id == id))
                .Where(plugin => plugin is not null &&
                                 plugin.State == PluginState.Disabled &&
                                 plugin.Integrity == PluginIntegrityState.Undeclared)
                .ToList();

            if (unverified.Count > 0 &&
                ModMain.MyMsgBox(
                    Lang.Text("Tools.Plugin.Enable.Unverified.BatchMessage", unverified.Count, _selected.Count),
                    Lang.Text("Tools.Plugin.Enable.Unverified.Title"),
                    button2: Lang.Text("Common.Action.Cancel"), isWarn: true) != 1)
                return;
        }

        var failed = new List<string>();
        foreach (var id in _selected.ToList())
        {
            var plugin = CurrentPlugins().FirstOrDefault(p => p.Id == id);
            if (!manager.SetEnabled(id, enable))
                failed.Add(plugin?.DisplayFileName ?? id);
        }

        _selected.Clear();

        if (failed.Count > 0)
            ModMain.MyMsgBox(Lang.Text("Tools.Plugin.Toggle.Failed", string.Join("、", failed)),
                Lang.Text("Tools.Plugin.Toggle.FailedTitle"));

        RerunHost(Lang.Text("Tools.Plugin.Toggle.Applied"));
    }

    private void BtnSelectDelete_Click(object sender, ModBase.RouteEventArgs e)
    {
        var manager = PluginHostBootstrap.Manager;
        if (manager is null) return;

        var targets = CurrentPlugins().Where(p => _selected.Contains(p.Id)).ToList();
        if (targets.Count == 0)
        {
            ModMain.MyMsgBox(Lang.Text("Tools.Plugin.Delete.NothingSelected"),
                Lang.Text("Tools.Plugin.Delete.Title"));
            return;
        }

        if (ModMain.MyMsgBox(Lang.Text("Tools.Plugin.Delete.Confirm", targets.Count),
                Lang.Text("Tools.Plugin.Delete.Title"),
                Lang.Text("Common.Action.Confirm"), Lang.Text("Common.Action.Cancel")) != 1)
            return;

        if (_isBusy) return;
        _isBusy = true;
        BtnSelectDelete.IsEnabled = false;
        HintBusy(Lang.Text("Tools.Plugin.Delete.Working"));

        var dataDirectory = PluginHostBootstrap.PluginDataDirectory;
        ModBase.RunInNewThread(() =>
        {
            var failed = new List<string>();
            foreach (var plugin in targets)
            {
                try
                {
                    // 包格式：PCL\Plugins 下属于该插件的所有包文件（任意文件名）直接删掉
                    var removed = PluginInstaller.Uninstall(PluginRootPath, plugin.Id,
                        plugin.IsPackage ? null : dataDirectory);
                    ModBase.Log(
                        $"[Plugin] 删除 {plugin.Id}（{plugin.DisplayFileName}）：{(removed ? "已从磁盘移除" : "已登记下次启动删除")}",
                        ModBase.LogLevel.Debug);
                    if (!removed) failed.Add(plugin.DisplayFileName);
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, $"删除插件 {plugin.Id} 失败", ModBase.LogLevel.Debug);
                    failed.Add(plugin.DisplayFileName);
                }
            }

            // 重新扫描 → 已删除的插件立刻从列表里消失
            try
            {
                PluginHostBootstrap.Reload();
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "删除后重新加载插件失败", ModBase.LogLevel.Debug);
            }

            ModBase.RunInUi(() =>
            {
                _isBusy = false;
                BtnSelectDelete.IsEnabled = true;
                _selected.Clear();
                RestoreHint();
                RefreshList();

                if (failed.Count > 0)
                    ModMain.MyMsgBox(Lang.Text("Tools.Plugin.Delete.Scheduled", string.Join("、", failed)),
                        Lang.Text("Tools.Plugin.Delete.Title"));
                else
                    HintService.Hint(Lang.Text("Tools.Plugin.Delete.Done", targets.Count), HintType.Success);
            });
        }, "PluginDelete", System.Threading.ThreadPriority.BelowNormal);
    }

    #endregion

    #region 条目操作

    private void OnItemToggleRequested(object? sender, EventArgs e)
    {
        if (sender is not MyPluginItem { Plugin: { } plugin }) return;
        var manager = PluginHostBootstrap.Manager;
        if (manager is null) return;

        var target = plugin.State == PluginState.Disabled;

        // 启用一个没有哈希 / 签名的包时先警告一次：宿主没法确认它没被改过
        if (target && plugin.Integrity == PluginIntegrityState.Undeclared &&
            ModMain.MyMsgBox(Lang.Text("Tools.Plugin.Enable.Unverified.Message", plugin.Name),
                Lang.Text("Tools.Plugin.Enable.Unverified.Title"),
                button2: Lang.Text("Common.Action.Cancel"), isWarn: true) != 1)
            return;

        if (!manager.SetEnabled(plugin.Id, target))
        {
            // 常见原因：文件被别的程序占用、目录只读
            ModMain.MyMsgBox(Lang.Text("Tools.Plugin.Toggle.Failed", plugin.DisplayFileName),
                Lang.Text("Tools.Plugin.Toggle.FailedTitle"));
            return;
        }

        RerunHost(Lang.Text("Tools.Plugin.Toggle.Applied"));
    }

    private void OnItemOpenFolderRequested(object? sender, EventArgs e)
    {
        if (sender is not MyPluginItem { Plugin: { } plugin }) return;

        // 包格式：在资源管理器里选中这个插件文件本身；老目录格式：打开目录
        if (plugin.PackagePath is { } package && File.Exists(package))
            ModBase.OpenExplorer(package);
        else
            ModBase.OpenExplorer(plugin.DirectoryPath.EndsWith(@"\") ? plugin.DirectoryPath : plugin.DirectoryPath + @"\");
    }

    #endregion

    #region 操作栏

    // MyButton.Click 是 (object, MouseButtonEventArgs)
    private void BtnManageOpen_Click(object sender, MouseButtonEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(PluginRootPath);
        }
        catch
        {
            // 忽略：让资源管理器自己报错
        }

        ModBase.OpenExplorer(PluginRootPath);
    }

    private void BtnManageInstall_Click(object sender, MouseButtonEventArgs e)
    {
        var files = SystemDialogs.SelectFiles(
            Lang.Text("Tools.Plugin.Install.Filter"),
            Lang.Text("Tools.Plugin.Install.Title"));
        if (files.Length == 0) return;

        InstallPluginPackages(files);
    }

    private void BtnManageDownload_Click(object sender, MouseButtonEventArgs e)
    {
        // 注意：不能直接调 FormMain.PageChange(PageType.Tools, …)，
        // 因为 Tools 分支会把 subType 强制覆盖成 frmToolsLeft.pageID（等于没跳）。
        // 这里直接驱动工具侧边栏，效果与用户点击「插件市场」一致。
        try
        {
            var left = ModMain.frmToolsLeft ??= new PageToolsLeft();
            left.ItemPluginMarket.SetChecked(true, false, false);
            left.PageChange(FormMain.PageSubType.ToolsPluginMarket);
            ModBase.Log("[Plugin] 已跳转到插件市场", ModBase.LogLevel.Debug);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "跳转到插件市场失败", ModBase.LogLevel.Debug);
        }
    }

    private void BtnManageRescan_Click(object sender, MouseButtonEventArgs e)
    {
        RerunHost(Lang.Text("Tools.Plugin.Manage.Rescanning"));
    }

    #endregion

    #region 后台重载

    private void RerunHost(string busyText)
    {
        if (_isBusy) return;
        _isBusy = true;
        BtnManageRescan.IsEnabled = false;
        HintBusy(busyText);

        ModBase.RunInNewThread(() =>
        {
            try
            {
                PluginHostBootstrap.Reload();
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "重新加载插件失败", ModBase.LogLevel.Debug);
            }
            finally
            {
                ModBase.RunInUi(() =>
                {
                    _isBusy = false;
                    BtnManageRescan.IsEnabled = true;
                    RestoreHint();
                    RefreshList();
                });
            }
        }, "PluginRescan", System.Threading.ThreadPriority.BelowNormal);
    }

    /// <summary>忙碌提示：模组页没有对应控件，这里临时借用搜索框的 HintText。</summary>
    private void HintBusy(string text)
    {
        try
        {
            SearchBox.HintText = text;
        }
        catch
        {
            // 忽略
        }
    }

    private void RestoreHint()
    {
        try
        {
            SearchBox.HintText = Lang.Text("Tools.Plugin.Search.Hint");
        }
        catch
        {
            // 忽略
        }
    }

    #endregion

    #region 静态安装入口（拖入文件 / 从文件安装共用）

    /// <summary>是不是插件包（<c>*.pclpkg</c> / <c>*.pclpkg.disabled</c>）。</summary>
    public static bool IsPluginPackage(string path) => PluginPackage.IsPackageFile(path);

    /// <summary>
    ///     安装若干插件包。任意页面拖入 <c>.pclpkg</c>、或「从文件安装」都走这里。
    ///     <paramref name="requestConfirm" /> 为 true 时先弹「插件确认安装」对话框（拖入文件时用）。
    /// </summary>
    public static void InstallPluginPackages(IReadOnlyList<string> files, bool requestConfirm = false)
    {
        var candidates = files.Where(f => !string.IsNullOrWhiteSpace(f) && File.Exists(f)).ToList();
        if (candidates.Count == 0) return;

        if (requestConfirm)
        {
            var message = Lang.Text("Tools.Plugin.Install.Confirm.Message",
                string.Join("\n", candidates.Select(Path.GetFileName)));
            if (ModMain.MyMsgBox(message, Lang.Text("Tools.Plugin.Install.Confirm.Title"),
                    Lang.Text("Common.Action.Confirm"), Lang.Text("Common.Action.Cancel")) != 1)
                return;
        }

        ModBase.RunInNewThread(() =>
        {
            var installed = new List<string>();
            var errors = new List<string>();

            foreach (var file in candidates)
            {
                try
                {
                    var result = PluginInstaller.InstallPackage(PluginRootPath, file);
                    if (result.Success)
                        installed.Add(Path.GetFileName(file));
                    else
                        errors.Add($"{Path.GetFileName(file)}：{result.Error}");
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, $"安装插件 {file} 失败", ModBase.LogLevel.Debug);
                    errors.Add($"{Path.GetFileName(file)}：{ex.Message}");
                }
            }

            if (installed.Count > 0)
            {
                try
                {
                    PluginHostBootstrap.Reload();
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "安装插件后重新加载失败", ModBase.LogLevel.Debug);
                }
            }

            ModBase.RunInUi(() =>
            {
                if (errors.Count > 0)
                    ModMain.MyMsgBox(string.Join("\n", errors), Lang.Text("Tools.Plugin.Install.FailedTitle"));

                foreach (var name in installed)
                    HintService.Hint(Lang.Text("Tools.Plugin.Install.Success", name), HintType.Success);

                // 插件管理页开着的话立刻刷新（新插件默认启用）
                ModMain.frmToolsPluginManage?.RefreshFromHost();
            });
        }, "PluginInstall", System.Threading.ThreadPriority.BelowNormal);
    }

    /// <summary>外部（拖入文件、市场安装）触发的刷新。</summary>
    public void RefreshFromHost()
    {
        try
        {
            ModBase.RunInUi(() =>
            {
                if (IsLoaded) RefreshList();
            });
        }
        catch
        {
            // 忽略
        }
    }

    #endregion

    /// <summary>
    ///     权限区：把插件声明且需要授权的能力列成一行行，按钮显示当前状态并可切换。
    ///     状态读的是 <c>permissions.json</c>（与插件首次使用时弹的确认框共用同一份记录）。
    /// </summary>
    private List<FrameworkElement> BuildPermissionRows(PluginDescriptor plugin)
    {
        var rows = new List<FrameworkElement>();

        foreach (var capability in PluginHostBootstrap.GatedCapabilities(plugin))
        {
            var granted = PluginHostBootstrap.IsPermissionGranted(plugin.Id, capability);
            var denied = PluginHostBootstrap.IsPermissionDenied(plugin.Id, capability);
            var state = granted
                ? Lang.Text("Tools.Plugin.Permission.Granted")
                : denied
                    ? Lang.Text("Tools.Plugin.Permission.Denied")
                    : Lang.Text("Tools.Plugin.Permission.Ask");

            var row = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190d) });

            row.Children.Add(new TextBlock
            {
                Text = $"{AppPluginPermissions.PluginChildText(capability)}  ·  {state}",
                FontSize = 12d,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            });

            // 按钮文字一律写「点了会发生什么」（动作），当前状态只看左边小字，避免两处状态词互相打架
            var toggle = new MyButton
            {
                MinWidth = 110d,
                Padding = new Thickness(10, 5, 10, 5),
                HorizontalAlignment = HorizontalAlignment.Right,
                Text = granted
                    ? Lang.Text("Tools.Plugin.Permission.ActionRevoke")
                    : denied
                        ? Lang.Text("Tools.Plugin.Permission.ActionRegrant")
                        : Lang.Text("Tools.Plugin.Permission.ActionGrant"),
                ToolTip = Lang.Text("Tools.Plugin.Permission.Toggle")
            };
            toggle.Click += (_, _) =>
            {
                var id = plugin.Id;
                var name = plugin.Name;
                var target = capability;
                var next = !PluginHostBootstrap.IsPermissionGranted(id, target);
                PluginHostBootstrap.SetPermission(id, name, target, next);
                ShowSettings(plugin); // 重新渲染，状态立刻更新
            };
            Grid.SetColumn(toggle, 1);
            row.Children.Add(toggle);

            rows.Add(row);
        }

        return rows;
    }

    #region 插件设置（宿主按插件声明的 schema 生成界面）

    /// <summary>当前设置面板对应的插件。</summary>
    private PluginDescriptor? _settingsPlugin;

    private void OnItemSettingsRequested(object? sender, EventArgs e)
    {
        if (sender is not MyPluginItem { Plugin: { } plugin }) return;
        ShowSettings(plugin);
    }

    // MyButton.Click 是 (object sender, MouseButtonEventArgs e) —— 与其它 MyButton 处理函数一致
    private void BtnSettingsClose_Click(object sender, MouseButtonEventArgs e) => HideSettings();

    private void HideSettings()
    {
        _settingsPlugin = null;
        PanPluginSettings.Visibility = Visibility.Collapsed;
        PanSettingsRows.Children.Clear();
    }

    /// <summary>
    ///     打开某个插件的设置面板：向插件要 schema，再逐个生成控件。
    ///     读写一律走 <see cref="IPluginConfig" />（就是插件自己 <c>Context.Config</c> 拿到的那个对象），
    ///     所以宿主改完插件立刻能读到，插件也不需要自己写界面。
    /// </summary>
    private void ShowSettings(PluginDescriptor plugin)
    {
        try
        {
            var provider = plugin.SettingsProvider;
            if (provider is null) return;

            _settingsPlugin = plugin;

            TxtSettingsTitle.Text = Lang.Text("Tools.Plugin.Settings.Title", plugin.Name);
            PanSettingsRows.Children.Clear();

            // 权限区：插件声明了需要用户授权的能力时，先列出来（可点切换）
            try
            {
                var permissionRows = BuildPermissionRows(plugin);
                if (permissionRows.Count > 0)
                {
                    PanSettingsRows.Children.Clear();
                    var header = new TextBlock
                    {
                        Text = Lang.Text("Tools.Plugin.Permission.Section"),
                        FontSize = 13d,
                        FontWeight = System.Windows.FontWeights.Bold,
                        Margin = new Thickness(0, 0, 0, 8)
                    };
                    PanSettingsRows.Children.Add(header);
                    foreach (var row in permissionRows)
                        PanSettingsRows.Children.Add(row);
                }
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "[Plugin] 渲染权限区失败", ModBase.LogLevel.Hint);
            }

            // 渲染行统一交给 PluginSettingsForm（与「设置 → 插件」页共用同一份实现）
            var rows = PluginSettingsForm.BuildRows(plugin, provider);
            TxtSettingsEmpty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            foreach (var row in rows)
                PanSettingsRows.Children.Add(row);

            PanPluginSettings.Visibility = Visibility.Visible;
            PanPluginSettings.BringIntoView();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "打开插件设置失败", ModBase.LogLevel.Hint);
        }
    }

    #endregion
}
