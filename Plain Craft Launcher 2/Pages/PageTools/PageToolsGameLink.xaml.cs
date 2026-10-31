using System.Collections.Specialized;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.Link.EasyTier;
using PCL.Core.Link.Lobby;
using PCL.Core.Link.Scaffolding.Client.Models;
using PCL.Core.Link.Scaffolding.EasyTier;
using PCL.Core.Utils.Validate;

namespace PCL;

/// <summary>
///     联机页面：TerraCotta（陶瓦联机）。
///     <para>
///         本分支已弃用 PCL 的「联机大厅」，改为基于 EasyTier 的纯 P2P 联机（不使用 PCL 的中继服务器）。
///         该页面提供联机总开关、联机配置，以及房主 / 房客两种模式的入口与教程。
///     </para>
/// </summary>
public partial class PageToolsGameLink
{
    private bool _isLoaded;
    private bool _isBusy;

    /// <summary>
    ///     地区屏蔽：未启用「PCLS 核心插件」（unlock.region）时整页替换为红镐子占位页。
    /// </summary>
    private void RefreshRegionBlocked()
    {
        if (PanRegionBlocked is null || PanBack is null) return;
        var blocked = !Core.App.FeatureFlags.IsEnabled(Core.App.FeatureFlags.Keys.UnlockRegion);
        PanRegionBlocked.Visibility = blocked ? Visibility.Visible : Visibility.Collapsed;
        PanBack.Visibility = blocked ? Visibility.Collapsed : Visibility.Visible;
    }

    public PageToolsGameLink()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            RefreshRegionBlocked();
            if (PanRegionBlocked.Visibility == Visibility.Visible) return;
            Reload();
        };
        Unloaded += (_, _) => Unsubscribe();
        LobbyService.Players.CollectionChanged += OnPlayersChanged;
        LobbyService.DiscoveredWorlds.CollectionChanged += OnWorldsChanged;
        LobbyService.OnServerStarted += OnServerStarted;
        LobbyService.OnServerShutDown += OnServerShutDown;
        LobbyService.OnServerException += OnServerException;
        LobbyService.OnNeedDownloadEasyTier += OnNeedDownloadEasyTier;
    }

    /// <summary>
    ///     刷新页面显示。
    /// </summary>
    public void Reload()
    {
        PanBack.ScrollToHome();
        ModAnimation.AniBeginControl();

        CheckTerraCotta.Checked = Config.Link.TerraCottaEnabled == true;
        TextUserName.Text = Config.Link.Username;
        // 留空时的实际生效名称（当前游戏账号名 / Player）直接显示在输入框提示里
        TextUserName.HintText = string.IsNullOrWhiteSpace(Config.Link.Username)
            ? Lang.Text("Tools.GameLink.TerraCotta.Settings.Username.Hint.Resolved", LobbyInfoProvider.GetUsername())
            : Lang.Text("Tools.GameLink.TerraCotta.Settings.Username.Hint");
        ComboRelayMode.SelectedIndex = Config.Link.TerraCottaRelayMode == 0 ? 1 : 0;
        // 高级设置（来自已下线的「设置 → 联机」页）
        ComboPreferProtocol.SelectedIndex = (int)Config.Link.ProtocolPreference;
        CheckLatencyFirstMode.Checked = Config.Link.UseLatencyFirstMode;
        CheckTryPunchSym.Checked = Config.Link.TryPunchSym;
        CheckEnableIPv6.Checked = Config.Link.EnableIPv6;
        CheckEnableCliOutput.Checked = Config.Link.EnableCliOutput;

        RefreshEasyTierStatus();
        RefreshWorldList();
        RefreshConnectionUI();
        RefreshEnabledState();

        ModAnimation.AniEndControl();

        if (_isLoaded) return;
        _isLoaded = true;
        ModBase.RunInNewThread(async () => await LobbyService.InitializeAsync().ConfigureAwait(false),
            "TerraCotta Init");
    }

    private void Unsubscribe()
    {
        LobbyService.Players.CollectionChanged -= OnPlayersChanged;
        LobbyService.DiscoveredWorlds.CollectionChanged -= OnWorldsChanged;
        LobbyService.OnServerStarted -= OnServerStarted;
        LobbyService.OnServerShutDown -= OnServerShutDown;
        LobbyService.OnServerException -= OnServerException;
        LobbyService.OnNeedDownloadEasyTier -= OnNeedDownloadEasyTier;
    }

    #region 总开关与配置

    private void CheckTerraCotta_Change(object sender, bool user)
    {
        var enabled = CheckTerraCotta.Checked == true;

        // 首次启用时弹出免责声明；拒绝则直接把开关退回去
        if (enabled && user && !Config.Link.TerraCottaDisclaimerAccepted)
        {
            var agree = ModMain.MyMsgBox(Lang.Text("Tools.GameLink.TerraCotta.Disclaimer.Message"),
                Lang.Text("Tools.GameLink.TerraCotta.Disclaimer.Title"),
                Lang.Text("Tools.GameLink.TerraCotta.Disclaimer.Agree"),
                Lang.Text("Tools.GameLink.TerraCotta.Disclaimer.Refuse")) == 1;
            if (!agree)
            {
                CheckTerraCotta.Checked = false;
                Config.Link.TerraCottaEnabled = false;
                RefreshEnabledState();
                return;
            }

            Config.Link.TerraCottaDisclaimerAccepted = true;
        }

        Config.Link.TerraCottaEnabled = enabled;
        ModBase.Log($"[Link] TerraCotta 总开关：{(enabled ? "开启" : "关闭")}");
        RefreshEnabledState();
    }

    private void TextUserName_TextChanged(object sender, TextChangedEventArgs e)
    {
        var value = TextUserName.Text.Trim();
        if (value == Config.Link.Username) return;
        Config.Link.Username = value;
    }

    private void ComboRelayMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ComboRelayMode.SelectedIndex < 0) return;
        var mode = ComboRelayMode.SelectedIndex == 1 ? 0 : 1;
        if (mode == Config.Link.TerraCottaRelayMode) return;
        Config.Link.TerraCottaRelayMode = mode;
    }

    private void ComboPreferProtocol_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ComboPreferProtocol.SelectedIndex < 0) return;
        Config.Link.ProtocolPreference = ComboPreferProtocol.SelectedIndex == 1
            ? LinkProtocolPreference.Udp
            : LinkProtocolPreference.Tcp;
    }

    private void CheckLinkOption_Change(object sender, bool user)
    {
        Config.Link.UseLatencyFirstMode = CheckLatencyFirstMode.Checked == true;
        Config.Link.TryPunchSym = CheckTryPunchSym.Checked == true;
        Config.Link.EnableIPv6 = CheckEnableIPv6.Checked == true;
        Config.Link.EnableCliOutput = CheckEnableCliOutput.Checked == true;
    }

    // 总开关关闭时禁用联机操作
    private void RefreshEnabledState()
    {
        var enabled = Config.Link.TerraCottaEnabled;
        PanSettings.IsEnabled = true;
        PanHost.IsEnabled = enabled && !_isBusy;
        PanGuest.IsEnabled = enabled && !_isBusy;
        BtnCreate.IsEnabled = enabled && !_isBusy;
        BtnJoin.IsEnabled = enabled && !_isBusy;

        PanHost.Opacity = enabled ? 1d : 0.5d;
        PanGuest.Opacity = enabled ? 1d : 0.5d;
        HintTerraCotta.Theme = enabled ? MyHint.Themes.Blue : MyHint.Themes.Yellow;
        HintTerraCotta.Text = Lang.Text(enabled
            ? "Tools.GameLink.TerraCotta.Hint"
            : "Tools.GameLink.TerraCotta.Hint.Disabled");
    }

    private void RefreshEasyTierStatus()
    {
        var available = Directory.Exists(ETInfoProvider.ETPath) &&
                        File.Exists(Path.Combine(ETInfoProvider.ETPath, "easytier-core.exe"));
        LabEasyTierStatus.Text = Lang.Text(available
            ? "Tools.GameLink.TerraCotta.Switch.Core.Ready"
            : "Tools.GameLink.TerraCotta.Switch.Core.Missing");
        BtnDownloadEasyTier.Visibility = available ? Visibility.Collapsed : Visibility.Visible;
    }

    private void BtnDownloadEasyTier_Click(object sender, MouseButtonEventArgs e)
    {
        ModBase.RunInNewThread(() =>
        {
            try
            {
                ModLink.DownloadEasyTier();
                ModBase.RunInUi(() =>
                {
                    RefreshEasyTierStatus();
                    HintService.Hint(Lang.Text("Tools.GameLink.TerraCotta.Switch.Download.Success"),
                        HintType.Success);
                });
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "[Link] 下载联机核心失败", ModBase.LogLevel.Msgbox,
                    userSummary: Lang.Text("Tools.GameLink.TerraCotta.Switch.Download.Failed"));
            }
        }, "TerraCotta Core Download");
    }

    private void OnNeedDownloadEasyTier()
    {
        ModBase.RunInUi(() =>
        {
            RefreshEasyTierStatus();
            HintService.Hint(Lang.Text("Tools.GameLink.TerraCotta.Switch.Core.Missing"), HintType.Error);
        });
    }

    #endregion

    #region 房主

    private void RefreshWorldList()
    {
        var previousPort = (ComboWorldList.SelectedItem as MyComboBoxItem)?.Tag is int port ? port : -1;
        ComboWorldList.Items.Clear();
        foreach (var world in LobbyService.DiscoveredWorlds)
            ComboWorldList.Items.Add(new MyComboBoxItem
            {
                Content = $"{world.Name} ({world.Port})",
                Tag = world.Port
            });

        if (ComboWorldList.Items.Count == 0)
        {
            ComboWorldList.Items.Add(new MyComboBoxItem
            {
                Content = Lang.Text("Tools.GameLink.TerraCotta.Host.NoWorld"),
                Tag = -1
            });
            ComboWorldList.SelectedIndex = 0;
            return;
        }

        var index = 0;
        for (var i = 0; i < ComboWorldList.Items.Count; i++)
            if (ComboWorldList.Items[i] is MyComboBoxItem item && item.Tag is int p && p == previousPort)
                index = i;
        ComboWorldList.SelectedIndex = index;
    }

    private void OnWorldsChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        ModBase.RunInUi(RefreshWorldList);
    }

    private void BtnRefreshWorld_Click(object sender, MouseButtonEventArgs e)
    {
        HintService.Hint(Lang.Text("Tools.GameLink.TerraCotta.Host.Refreshing"));
        ModBase.RunInNewThread(async () =>
        {
            await LobbyService.DiscoverWorldAsync().ConfigureAwait(false);
            ModBase.RunInUi(RefreshWorldList);
        }, "TerraCotta Discover World");
    }

    private void BtnInputPort_Click(object sender, MouseButtonEventArgs e)
    {
        var input = ModMain.MyMsgBoxInput(Lang.Text("Tools.GameLink.Create.InputPort"),
            Lang.Text("Tools.GameLink.Create.InputPort"), "", [new RegexValidator("^[0-9]{1,5}$")]);
        if (!int.TryParse(input, out var port) || port is < 1 or > 65535)
        {
            HintService.Hint(Lang.Text("Tools.GameLink.Create.NotMcPort"), HintType.Error);
            return;
        }

        ComboWorldList.Items.Add(new MyComboBoxItem
        {
            Content = $"{Lang.Text("Tools.GameLink.TerraCotta.Host.CustomPort")} ({port})",
            Tag = port
        });
        ComboWorldList.SelectedIndex = ComboWorldList.Items.Count - 1;
    }

    private async void BtnCreate_Click(object sender, MouseButtonEventArgs e)
    {
        if (!Config.Link.TerraCottaEnabled)
        {
            HintService.Hint(Lang.Text("Tools.GameLink.TerraCotta.Hint.Disabled"), HintType.Error);
            return;
        }

        if (ComboWorldList.SelectedItem is not MyComboBoxItem item || item.Tag is not int port || port <= 0)
        {
            HintService.Hint(Lang.Text("Tools.GameLink.Create.NoWorld"), HintType.Error);
            return;
        }

        if (!await EnsureLobbyReadyAsync().ConfigureAwait(true)) return;

        _isBusy = true;
        RefreshEnabledState();
        BtnCreate.Text = Lang.Text("Tools.GameLink.TerraCotta.Host.Creating");
        try
        {
            var username = LobbyInfoProvider.GetUsername();
            ModBase.Log($"[Link] 创建 TerraCotta 房间，端口：{port}，用户名：{username}");
            if (!await LobbyService.CreateLobbyAsync(port, username).ConfigureAwait(true))
            {
                HintService.Hint(Lang.Text("Tools.GameLink.TerraCotta.Host.CreateFailed"), HintType.Error);
                return;
            }

            RefreshConnectionUI();
            var code = LobbyService.CurrentLobbyCode;
            if (!string.IsNullOrWhiteSpace(code))
            {
                ModBase.ClipboardSet(code);
                HintService.Hint(Lang.Text("Tools.GameLink.TerraCotta.Host.CreatedAndCopied"), HintType.Success);
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[Link] 创建 TerraCotta 房间失败", ModBase.LogLevel.Msgbox,
                userSummary: Lang.Text("Tools.GameLink.TerraCotta.Host.CreateFailed"));
        }
        finally
        {
            _isBusy = false;
            BtnCreate.Text = Lang.Text("Tools.GameLink.TerraCotta.Host.Create");
            RefreshEnabledState();
            RefreshConnectionUI();
        }
    }

    #endregion

    #region 房客

    private void TextJoinCode_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) BtnJoin_Click(sender, null);
    }

    private async void BtnJoin_Click(object sender, MouseButtonEventArgs e)
    {
        if (!Config.Link.TerraCottaEnabled)
        {
            HintService.Hint(Lang.Text("Tools.GameLink.TerraCotta.Hint.Disabled"), HintType.Error);
            return;
        }

        var code = TextJoinCode.Text.Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            HintService.Hint(Lang.Text("Tools.GameLink.TerraCotta.Guest.EmptyCode"), HintType.Error);
            return;
        }

        if (!await EnsureLobbyReadyAsync().ConfigureAwait(true)) return;

        _isBusy = true;
        RefreshEnabledState();
        BtnJoin.Text = Lang.Text("Tools.GameLink.TerraCotta.Guest.Joining");
        try
        {
            var username = LobbyInfoProvider.GetUsername();
            ModBase.Log($"[Link] 加入 TerraCotta 房间：{code}，用户名：{username}");
            if (!await LobbyService.JoinLobbyAsync(code, username).ConfigureAwait(true))
            {
                HintService.Hint(Lang.Text("Tools.GameLink.TerraCotta.Guest.JoinFailed"), HintType.Error);
                HintService.Hint(Lang.Text("Tools.GameLink.TerraCotta.Guest.JoinFailed.Nat"), HintType.Info);
                return;
            }

            RefreshConnectionUI();
            HintService.Hint(Lang.Text("Tools.GameLink.TerraCotta.Guest.Joined"), HintType.Success);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[Link] 加入 TerraCotta 房间失败", ModBase.LogLevel.Msgbox,
                userSummary: Lang.Text("Tools.GameLink.TerraCotta.Guest.JoinFailed"));
        }
        finally
        {
            _isBusy = false;
            BtnJoin.Text = Lang.Text("Tools.GameLink.TerraCotta.Guest.Join");
            RefreshEnabledState();
            RefreshConnectionUI();
        }
    }

    #endregion

    #region 连接状态

    private static async Task<bool> EnsureLobbyReadyAsync()
    {
        if (!ModLink.LobbyPrecheck()) return false;
        if (LobbyService.CurrentState is LobbyState.Initialized or LobbyState.Connected) return true;
        await LobbyService.InitializeAsync().ConfigureAwait(false);
        return LobbyService.CurrentState is LobbyState.Initialized or LobbyState.Connected;
    }

    private void OnServerStarted()
    {
        ModBase.RunInUi(RefreshConnectionUI);
    }

    private void OnServerShutDown()
    {
        ModBase.RunInUi(() =>
        {
            RefreshConnectionUI();
            HintService.Hint(Lang.Text("Tools.GameLink.TerraCotta.Connection.Closed"));
        });
    }

    private void OnServerException(Exception ex)
    {
        ModBase.RunInUi(() =>
        {
            RefreshConnectionUI();
            HintService.Hint(Lang.Text("Tools.GameLink.Error.ServerMessage", ex.Message), HintType.Error);
        });
    }

    private void OnPlayersChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        ModBase.RunInUi(RefreshConnectionUI);
    }

    private void RefreshConnectionUI()
    {
        var connected = LobbyService.CurrentState is LobbyState.Connected or LobbyState.Creating or LobbyState.Joining;
        var code = LobbyService.CurrentLobbyCode;
        CardConnection.Visibility = connected ? Visibility.Visible : Visibility.Collapsed;
        if (!connected) return;

        LabRoomCode.Text = string.IsNullOrWhiteSpace(code) ? "-" : code;
        LabConnectionInfo.Text = Lang.Text(
            LobbyService.IsHost
                ? "Tools.GameLink.TerraCotta.Connection.HostInfo"
                : "Tools.GameLink.TerraCotta.Connection.GuestInfo");

        if (LobbyService.Players.Count == 0)
        {
            LabPlayerList.Text = Lang.Text("Tools.GameLink.TerraCotta.Connection.NoPlayer");
            return;
        }

        var names = LobbyService.Players
            .Select(player => player.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();
        LabPlayerList.Text = Lang.Text("Tools.GameLink.TerraCotta.Connection.Players", string.Join("、", names));
    }

    private void BtnCopyCode_Click(object sender, MouseButtonEventArgs e)
    {
        var code = LobbyService.CurrentLobbyCode;
        if (string.IsNullOrWhiteSpace(code))
        {
            HintService.Hint(Lang.Text("Tools.GameLink.TerraCotta.Connection.NoCode"), HintType.Error);
            return;
        }

        ModBase.ClipboardSet(code);
        HintService.Hint(Lang.Text("Tools.GameLink.TerraCotta.Connection.Copied"), HintType.Success);
    }

    private async void BtnLeave_Click(object sender, MouseButtonEventArgs e)
    {
        BtnLeave.IsEnabled = false;
        try
        {
            await LobbyService.LeaveLobbyAsync().ConfigureAwait(true);
            HintService.Hint(Lang.Text("Tools.GameLink.TerraCotta.Connection.Left"));
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[Link] 退出 TerraCotta 房间失败", ModBase.LogLevel.Hint,
                userSummary: Lang.Text("Tools.GameLink.Error.ServerExit"));
        }
        finally
        {
            BtnLeave.IsEnabled = true;
            RefreshConnectionUI();
        }
    }

    #endregion

    #region 联机诊断

    /// <summary>
    ///     联机诊断：把 EasyTier 文件、进程状态与日志中的联机相关行汇总后复制到剪贴板。
    ///     加入房间失败时让用户把这段内容发回来，就能直接看出卡在哪一步。
    /// </summary>
    private void BtnDiagnostics_Click(object sender, MouseButtonEventArgs e)
    {
        BtnDiagnostics.IsEnabled = false;
        ModBase.RunInNewThread(() =>
        {
            string report;
            try
            {
                report = BuildLinkDiagnostics();
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "[Link] 收集联机诊断信息失败", ModBase.LogLevel.Hint,
                    userSummary: Lang.Text("Tools.GameLink.TerraCotta.Diagnostics.Failed"));
                return;
            }
            finally
            {
                ModBase.RunInUi(() => BtnDiagnostics.IsEnabled = true);
            }

            ModBase.ClipboardSet(report, false);
            ModBase.RunInUi(() => ModMain.MyMsgBox(report,
                Lang.Text("Tools.GameLink.TerraCotta.Diagnostics.Title"),
                Lang.Text("Common.Action.Confirm"), "", isWarn: false));
        }, "Link Diagnostics");
    }

    private static string BuildLinkDiagnostics()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== TerraCotta 联机诊断 ===");
        sb.AppendLine($"时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"版本：{ModBase.versionDisplayName}");
        sb.AppendLine($"EasyTier 版本：{EasyTierMetadata.CurrentEasyTierVer}");
        sb.AppendLine($"EasyTier 目录：{EasyTierMetadata.EasyTierFilePath}");
        foreach (var file in new[] { "easytier-core.exe", "easytier-cli.exe", "Packet.dll" })
        {
            var path = Path.Combine(EasyTierMetadata.EasyTierFilePath, file);
            sb.AppendLine(File.Exists(path)
                ? $"  {file}：{new FileInfo(path).Length} 字节"
                : $"  {file}：**缺失**");
        }

        sb.AppendLine($"EasyTier 总开关：{Config.Link.TerraCottaEnabled}");
        sb.AppendLine($"连接方式：{(Config.Link.TerraCottaRelayMode == 0 ? "仅 P2P" : "允许公共中继")}");
        sb.AppendLine($"协议偏好：{Config.Link.ProtocolPreference}，延迟优先：{Config.Link.UseLatencyFirstMode}，" +
                      $"端口猜测：{Config.Link.TryPunchSym}，IPv6：{Config.Link.EnableIPv6}");
        sb.AppendLine($"联机状态：{LobbyService.CurrentState}，当前房间：{LobbyService.CurrentLobbyCode ?? "<无>"}");
        sb.AppendLine($"显示名称：{LobbyInfoProvider.GetUsername()}");

        try
        {
            var cores = System.Diagnostics.Process.GetProcessesByName("easytier-core");
            sb.AppendLine($"正在运行的 easytier-core 进程：{cores.Length} 个");
            foreach (var core in cores)
            {
                sb.AppendLine($"  PID {core.Id}：{PCL.Core.Utils.OS.ProcessInterop.GetCommandLine(core.Id)}");
                core.Dispose();
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"读取 easytier-core 进程失败：{ex.Message}");
        }

        // 日志尾部中与联机相关的行
        sb.AppendLine();
        sb.AppendLine("--- 日志（最近 60 条联机相关） ---");
        try
        {
            var logDir = Path.Combine(Basics.ExecutableDirectory, "PCL", "Log");
            var latest = new DirectoryInfo(logDir).GetFiles("*.log")
                .OrderByDescending(file => file.LastWriteTime)
                .FirstOrDefault();
            if (latest is null)
            {
                sb.AppendLine("未找到日志文件。");
            }
            else
            {
                sb.AppendLine($"日志文件：{latest.FullName}");
                var lines = File.ReadAllLines(latest.FullName)
                    .Where(line => line.Contains("EasyTier", StringComparison.OrdinalIgnoreCase) ||
                                   line.Contains("Et Cli", StringComparison.OrdinalIgnoreCase) ||
                                   line.Contains("Lobby", StringComparison.OrdinalIgnoreCase) ||
                                   line.Contains("TerraCotta", StringComparison.OrdinalIgnoreCase))
                    .TakeLast(60);
                foreach (var line in lines) sb.AppendLine(line);
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"读取日志失败：{ex.Message}");
        }

        return sb.ToString();
    }

    #endregion
}
