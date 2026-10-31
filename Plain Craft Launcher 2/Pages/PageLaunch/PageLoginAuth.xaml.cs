using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using PCL.Core.App;
using PCL.Core.App.Localization;
using PCL.Core.IO.Net.Http;
using PCL.Core.Logging;
using PCL.Core.Minecraft.Yggdrasil;
using PCL.Core.Minecraft.Profile;
using PCL.Core.Minecraft.Profile.Authentication;
using PCL.Core.Minecraft.Profile.Models;
using PCL.Core.Utils;
using PCL.Core.Utils.Exts;
using PCL.Core.Utils.Validate;

namespace PCL;

public partial class PageLoginAuth
{
    public static string draggedAuthServer;
    public static bool? draggedAuthServerOAuthSupported;

    // 预设服务器
    internal const string DefaultAuthServer = "https://littleskin.cn/api/yggdrasil";

    /// <summary>
    ///     下拉框中“新建验证服务器”选项的标记值。
    /// </summary>
    private const string NewAuthServerMarker = "@@new-auth-server@@";

    internal static readonly IReadOnlyDictionary<string, string> PredefinedAuthServers = new Dictionary<string, string>
    {
        { Lang.Text("Launch.Account.Auth.Preset.LittleSkin"), DefaultAuthServer },
        { Lang.Text("Common.Option.Customize"), "" }
    };

    /// <summary>
    ///     当前选定的验证服务器地址，在页面之间切换时保持一致，默认为 LittleSkin。
    /// </summary>
    private static string _selectedAuthServer = DefaultAuthServer;

    private bool _isRegisterMode = true;
    private bool _isOAuthMode;
    private bool? _oauthSupported;
    private bool _hasRegisterLink;
    private bool _isRefreshingAuthServerCombo;
    private bool _loginModeInitialized;
    private string _authServerUrl = "";

    public PageLoginAuth()
    {
        InitializeComponent();
        Loaded += (_, _) => Reload();
        Loaded += (_, _) => ReloadRegisterButton();
        // Handles
        BtnBack.Click += BtnBack_Click;
        BtnBackOAuth.Click += BtnBack_Click;
        BtnLogin.Click += BtnLogin_Click;
        BtnOAuth.Click += BtnLogin_Click;
        BtnUsePassword.Click += (_, _) => _SetLoginMode(false);
        BtnUseOAuth.Click += (_, _) => _SetLoginMode(true);
        BtnWebsite.Click += BtnWebsite_Click;
        BtnPasswordWebsite.Click += BtnWebsite_Click;
        BtnLink.Click += Btn_Click;
    }

    private void Reload()
    {
        // 优先使用外部传入的服务器（例如从实例设置页跳转而来），否则使用上次选择的服务器
        var initialServer = !string.IsNullOrWhiteSpace(draggedAuthServer) ? draggedAuthServer : _selectedAuthServer;
        _authServerUrl = string.IsNullOrWhiteSpace(initialServer) ? DefaultAuthServer : initialServer;
        _selectedAuthServer = _authServerUrl;
        var knownOAuthSupport = draggedAuthServerOAuthSupported;
        draggedAuthServer = null;
        draggedAuthServerOAuthSupported = null;
        RefreshAuthServerCombo();
        _oauthSupported = knownOAuthSupport;
        _UpdateOAuthEntryVisibility();
        _SetLoginMode(knownOAuthSupport == true);
        if (knownOAuthSupport is null && !string.IsNullOrWhiteSpace(_authServerUrl))
        {
            var server = _authServerUrl;
            Dispatcher.BeginInvoke(new Func<Task>(async () =>
            {
                var supported = await IsOAuthSupportedAsync(server).ConfigureAwait(true);
                if (!string.Equals(_authServerUrl, server, StringComparison.OrdinalIgnoreCase))
                    return;
                _oauthSupported = supported;
                _UpdateOAuthEntryVisibility();
                _SetLoginMode(supported);
            }));
        }
    }

    #region 验证服务器下拉框

    /// <summary>
    ///     已保存的自定义验证服务器地址列表。
    /// </summary>
    private static List<string> GetSavedAuthServers()
    {
        try
        {
            return (Config.Launch.AuthServerList ?? string.Empty)
                .Split('|', StringSplitOptions.RemoveEmptyEntries)
                .Select(address => address.Trim())
                .Where(address => address.Length > 0)
                .ToList();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "读取已保存的验证服务器列表失败", ModBase.LogLevel.Debug);
            return [];
        }
    }

    /// <summary>
    ///     记录一个自定义验证服务器，供下拉框下次选择。其他入口（如新建档案对话框）也会调用它。
    /// </summary>
    internal static void RememberAuthServer(string address)
    {
        if (string.IsNullOrWhiteSpace(address)) return;
        try
        {
            var list = GetSavedAuthServers();
            if (list.Any(item => string.Equals(item, address, StringComparison.OrdinalIgnoreCase)))
                return;
            list.Add(address);
            Config.Launch.AuthServerList = list.Join("|");
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, $"保存验证服务器列表失败（{address}）", ModBase.LogLevel.Debug);
        }
    }

    /// <summary>
    ///     验证服务器在下拉框中的显示名称，通常即其域名。
    /// </summary>
    private static string GetAuthServerDisplayName(string address)
    {
        if (string.IsNullOrWhiteSpace(address)) return string.Empty;
        return Uri.TryCreate(address, UriKind.Absolute, out var uri) ? uri.Host : address;
    }

    /// <summary>
    ///     重建验证服务器下拉框的选项，并选中指定的服务器。
    /// </summary>
    private void RefreshAuthServerCombo()
    {
        _isRefreshingAuthServerCombo = true;
        try
        {
            var items = new List<(string Name, string Address)>();
            void AddServer(string address)
            {
                if (string.IsNullOrWhiteSpace(address)) return;
                if (items.Any(item => string.Equals(item.Address, address, StringComparison.OrdinalIgnoreCase)))
                    return;
                var name = GetAuthServerDisplayName(address);
                items.Add((string.IsNullOrWhiteSpace(name) ? address : name, address));
            }

            AddServer(DefaultAuthServer);
            foreach (var address in GetSavedAuthServers()) AddServer(address);
            AddServer(_authServerUrl);

            ComboAuthServer.Items.Clear();
            MyComboBoxItem? target = null;
            foreach (var (name, address) in items)
            {
                var item = new MyComboBoxItem { Content = name, Tag = address };
                ComboAuthServer.Items.Add(item);
                if (string.Equals(address, _authServerUrl, StringComparison.OrdinalIgnoreCase)) target = item;
            }

            ComboAuthServer.Items.Add(new MyComboBoxItem
            {
                Content = Lang.Text("Launch.Account.Auth.Server.New"),
                Tag = NewAuthServerMarker
            });
            ComboAuthServer.SelectedItem = target ?? ComboAuthServer.Items[0];
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "初始化验证服务器下拉框失败", ModBase.LogLevel.Feedback,
                userSummary: Lang.Text("Application.Control.Error.OperationFailed"));
        }
        finally
        {
            _isRefreshingAuthServerCombo = false;
        }
    }

    // 下拉框选择改变
    private void ComboAuthServer_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isRefreshingAuthServerCombo) return;
        if (ComboAuthServer.SelectedItem is not MyComboBoxItem item) return;
        var address = item.Tag?.ToString() ?? string.Empty;
        if (address == NewAuthServerMarker)
        {
            // 先复原选择，再弹出新建对话框
            RefreshAuthServerCombo();
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(NewAuthServer));
            return;
        }

        if (string.IsNullOrWhiteSpace(address)) return;
        if (string.Equals(address, _authServerUrl, StringComparison.OrdinalIgnoreCase)) return;
        _authServerUrl = address;
        _selectedAuthServer = address;
        ModBase.Log($"[Profile] 外置登录验证服务器切换为 {address}");
        ReloadRegisterButton();
        var server = address;
        Dispatcher.BeginInvoke(new Func<Task>(async () =>
        {
            var supported = await IsOAuthSupportedAsync(server).ConfigureAwait(true);
            if (!string.Equals(_authServerUrl, server, StringComparison.OrdinalIgnoreCase))
                return;
            _oauthSupported = supported;
            _UpdateOAuthEntryVisibility();
            _SetLoginMode(supported);
        }));
    }

    // 新建验证服务器
    private void NewAuthServer()
    {
        var server = ModMain.MyMsgBoxAuthServer(_authServerUrl, PredefinedAuthServers,
            Lang.Text("Launch.Account.Auth.SelectServer"));
        if (string.IsNullOrWhiteSpace(server)) return;
        RememberAuthServer(server);
        _authServerUrl = server;
        _selectedAuthServer = server;
        RefreshAuthServerCombo();
        ReloadRegisterButton();
        var address = server;
        Dispatcher.BeginInvoke(new Func<Task>(async () =>
        {
            var supported = await IsOAuthSupportedAsync(address).ConfigureAwait(true);
            if (!string.Equals(_authServerUrl, address, StringComparison.OrdinalIgnoreCase))
                return;
            _oauthSupported = supported;
            _UpdateOAuthEntryVisibility();
            _SetLoginMode(supported);
        }));
    }

    #endregion

    private void BtnBack_Click(object sender, EventArgs e)
    {
        ProfileService.IsCreatingProfile = false;
        TextName.Text = null;
        TextPass.Password = null;
        ModMain.frmLaunchLeft.RefreshPage(true);
    }

    private void BtnLogin_Click(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_authServerUrl))
        {
            HintService.Hint(Lang.Text("Launch.Account.Auth.EmptyFields"), HintType.Error);
            return;
        }

        if (!_authServerUrl.IsMatch(RegexPatterns.HttpUri))
        {
            HintService.Hint(Lang.Text("Launch.Account.Auth.InvalidServer"), HintType.Error);
            return;
        }

        BtnLogin.IsEnabled = false;
        BtnOAuth.IsEnabled = false;
        BtnBack.IsEnabled = false;
        BtnBackOAuth.IsEnabled = false;
        BtnUsePassword.IsEnabled = false;
        BtnUseOAuth.IsEnabled = false;
        BtnWebsite.IsEnabled = false;
        BtnPasswordWebsite.IsEnabled = false;
        Dispatcher.BeginInvoke(new Func<Task>(async () =>
        {
            var keepControlsDisabled = false;
            try
            {
                ProfileService.IsCreatingProfile = true;
                if (_isOAuthMode)
                {
                    if (!await _TryStartYggdrasilConnectAsync().ConfigureAwait(true))
                    {
                        HintService.Hint(Lang.Text("Launch.Account.Auth.LoginFailed"), HintType.Error);
                        return;
                    }
                    keepControlsDisabled = true;
                    return;
                }
                if (string.IsNullOrWhiteSpace(TextName.Text) || string.IsNullOrWhiteSpace(TextPass.Password))
                {
                    HintService.Hint(Lang.Text("Launch.Account.Auth.EmptyFields"), HintType.Error);
                    return;
                }
                var loginData = new ModLaunch.McLoginServer(ModLaunch.McLoginType.Auth)
                {
                    BaseUrl = await ApiLocation.TryRequestAsync(_authServerUrl).ConfigureAwait(true),
                    UserName = TextName.Text, Password = TextPass.Password, Description = "Authlib-Injector",
                    LoginType = ModLaunch.McLoginType.Auth
                };
                ModLaunch.mcLoginAuthLoader.Start(loginData, true);
                while (ModLaunch.mcLoginAuthLoader.State == ModBase.LoadState.Loading)
                {
                    BtnLogin.Text = Lang.Number(ModLaunch.mcLoginAuthLoader.Progress, "P0");
                    await Task.Delay(50);
                }

                switch (ModLaunch.mcLoginAuthLoader.State)
                {
                    case ModBase.LoadState.Finished:
                        ModMain.frmLaunchLeft.RefreshPage(true);
                        break;
                    case ModBase.LoadState.Aborted:
                        HintService.Hint(Lang.Text("Launch.Account.Auth.Cancelled"));
                        break;
                    case ModBase.LoadState.Waiting:
                    case ModBase.LoadState.Loading:
                    case ModBase.LoadState.Failed:
                    default:
                    {
                        if (ModLaunch.mcLoginAuthLoader.Error is null)
                            throw new InvalidOperationException(Lang.Text("Launch.Account.Microsoft.Error.Unknown"));
                        throw ModLaunch.mcLoginAuthLoader.Error;
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex.Message == "$$")
                {
                }
                else if (ex.Message.StartsWith("$"))
                {
                    HintService.Hint(
                        Lang.Text("Launch.Account.Auth.LoginFailed.WithDetail",ex.Message.TrimStart('$')),
                        HintType.Error);
                }
                else
                {
                    ModBase.Log(
                        ex,
                        Lang.Text("Launch.Account.Auth.LoginFailed"),
                        ModBase.LogLevel.Msgbox,
                        userSummary: Lang.Text("Launch.Account.Auth.LoginFailed"));
                }
            }
            finally
            {
                if (!keepControlsDisabled) _FinishLoginAttempt();
            }
        }));
    }

    private void _FinishLoginAttempt()
    {
        ProfileService.IsCreatingProfile = false;
        BtnLogin.IsEnabled = true;
        BtnOAuth.IsEnabled = true;
        BtnBack.IsEnabled = true;
        BtnBackOAuth.IsEnabled = true;
        BtnUsePassword.IsEnabled = true;
        BtnUseOAuth.IsEnabled = true;
        BtnWebsite.IsEnabled = true;
        BtnPasswordWebsite.IsEnabled = true;
        BtnLogin.Text = Lang.Text("Launch.Account.Auth.Login");
    }

    private void _SetLoginMode(bool useOAuth)
    {
        if (_loginModeInitialized && _isOAuthMode == useOAuth)
            return;

        var wasOAuth = _loginModeInitialized && _isOAuthMode;
        _isOAuthMode = useOAuth;
        _loginModeInitialized = true;

        var oldElements = wasOAuth
            ? new FrameworkElement[] { PanOAuth, BtnUsePassword, BtnWebsite, BtnBackOAuth }
            : new FrameworkElement[] { TextName, TextNameTitle, TextPass, TextPassTitle, BtnLink, BtnLogin, PanPasswordLinks, BtnBack };
        var newElements = useOAuth
            ? new FrameworkElement[] { PanOAuth, BtnUsePassword, BtnWebsite, BtnBackOAuth }
            : new FrameworkElement[] { TextName, TextNameTitle, TextPass, TextPassTitle, BtnLink, BtnLogin, PanPasswordLinks, BtnBack };

        var animations = new List<ModAnimation.AniData>();
        if (wasOAuth || !_loginModeInitialized)
        {
            foreach (var element in oldElements)
            {
                if (element.Visibility == Visibility.Visible)
                    animations.Add(ModAnimation.AaOpacity(element, -element.Opacity, 90));
            }
        }

        animations.Add(ModAnimation.AaCode(() =>
        {
            foreach (var element in oldElements)
            {
                element.Visibility = Visibility.Collapsed;
                element.Opacity = 1d;
            }

            foreach (var element in newElements)
            {
                element.Visibility = ReferenceEquals(element, BtnLink) && !_hasRegisterLink
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                element.Opacity = 0d;
            }
        }, after: true));

        foreach (var element in newElements)
        {
            var targetOpacity = ReferenceEquals(element, BtnBackOAuth) || ReferenceEquals(element, PanPasswordLinks)
                ? 0.3d
                : 1d;
            animations.Add(ModAnimation.AaOpacity(element, targetOpacity, 130, 20,
                new ModAnimation.AniEaseInFluent()));
        }

        ModAnimation.AniStart(animations, "Profile Authentication Mode");
    }

    private void _UpdateOAuthEntryVisibility()
    {
        var isOAuthAvailable = _oauthSupported == true;
        BtnUseOAuth.Visibility = isOAuthAvailable ? Visibility.Visible : Visibility.Collapsed;
        BtnPasswordWebsite.SetValue(Grid.ColumnSpanProperty, isOAuthAvailable ? 1 : 3);
    }

    private async Task<bool> _TryStartYggdrasilConnectAsync()
    {
        var server = await ApiLocation.TryRequestAsync(_authServerUrl).ConfigureAwait(true);
        using var response = await HttpRequest.Create(server).SendAsync().ConfigureAwait(true);
        if (!response.IsSuccess) return false;
        var metadata = (JsonObject)ModBase.GetJson(await response.AsStringAsync().ConfigureAwait(true));
        var discovery = metadata["meta"]?["feature.openid_configuration_url"]?.ToString()
                        ?? metadata["feature.openid_configuration_url"]?.ToString();
        if (string.IsNullOrWhiteSpace(discovery)) return false;

        using var discoveryResponse = await HttpRequest.Create(discovery).SendAsync().ConfigureAwait(true);
        if (!discoveryResponse.IsSuccess) return false;
        var discoveryMetadata = (JsonObject)ModBase.GetJson(await discoveryResponse.AsStringAsync().ConfigureAwait(true));
        var clientId = _GetClientId(server, discoveryMetadata);
        if (string.IsNullOrWhiteSpace(clientId)) return false;

        ModBase.Log($"[Profile] Yggdrasil Connect client id resolved for {new Uri(server).Host}", ModBase.LogLevel.Debug);
        _ = _CompleteYggdrasilConnectLoginAsync(server, discovery, clientId);
        return true;
    }

    private async Task _CompleteYggdrasilConnectLoginAsync(string server, string discovery, string clientId)
    {
        try
        {
            var profile = await ProfileService.AuthenticateAsync(ProfileType.YggdrasilConnect, new AuthenticationRequest
            {
                Server = server,
                DiscoveryAddress = discovery,
                ClientId = clientId,
                DeviceCodeHandler = ProfileUi.ShowDeviceCodeLoginAsync,
                ProfileSelector = (candidates, _) => Task.FromResult(_SelectYggdrasilProfile(candidates))
            }, existing: null, select: true, token: CancellationToken.None).ConfigureAwait(false);
            ModBase.RunInUi(() =>
            {
                ModMain.frmLaunchLeft.RefreshPage(true);
            });
            LogWrapper.Info("Profile","Yggdrasil Connect 登录成功：" + profile.UserName);
        }
        catch (ThreadInterruptedException)
        {
            HintService.Hint(Lang.Text("Launch.Account.Auth.Cancelled"));
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, Lang.Text("Launch.Account.Auth.LoginFailed"), ModBase.LogLevel.Msgbox,
                userSummary: Lang.Text("Launch.Account.Auth.LoginFailed"));
        }
        finally
        {
            ModBase.RunInUi(_FinishLoginAttempt);
        }
    }

    private static AuthenticationCandidate? _SelectYggdrasilProfile(IReadOnlyList<AuthenticationCandidate> candidates)
    {
        if (candidates.Count == 0) return null;
        if (candidates.Count == 1) return candidates[0];
        AuthenticationCandidate? selected = null;
        ModBase.RunInUiWait(() =>
        {
            var controls = candidates.Select(candidate => (IMyRadio)new MyRadioBox { Text = candidate.Name }).ToList();
            var index = ModMain.MyMsgBoxSelect(controls, Lang.Text("Launch.Account.Auth.ChangeProfile"));
            if (index is >= 0 and < 100000) selected = candidates[index.Value];
        });
        return selected;
    }

    internal static async Task<bool> IsOAuthSupportedAsync(string authServerUrl)
    {
        if (Uri.TryCreate(authServerUrl, UriKind.Absolute, out var serverUri) &&
            YggdrasilConnectProvider.TryGetBuiltInClientId(serverUri.Host, out _))
            return true;

        try
        {
            var server = await ApiLocation.TryRequestAsync(authServerUrl).ConfigureAwait(false);
            using var response = await HttpRequest.Create(server).SendAsync().ConfigureAwait(false);
            if (!response.IsSuccess) return false;
            var metadata = (JsonObject)ModBase.GetJson(await response.AsStringAsync().ConfigureAwait(false));
            var discovery = metadata["meta"]?["feature.openid_configuration_url"]?.ToString()
                            ?? metadata["feature.openid_configuration_url"]?.ToString();
            if (string.IsNullOrWhiteSpace(discovery)) return false;

            using var discoveryResponse = await HttpRequest.Create(discovery).SendAsync().ConfigureAwait(false);
            if (!discoveryResponse.IsSuccess) return false;
            var discoveryMetadata = (JsonObject)ModBase.GetJson(await discoveryResponse.AsStringAsync().ConfigureAwait(false));
            var clientId = _GetClientId(server, discoveryMetadata);
            var deviceEndpoint = discoveryMetadata["device_authorization_endpoint"]?.ToString();
            var tokenEndpoint = discoveryMetadata["token_endpoint"]?.ToString();
            return !string.IsNullOrWhiteSpace(clientId) &&
                   !string.IsNullOrWhiteSpace(deviceEndpoint) &&
                   !string.IsNullOrWhiteSpace(tokenEndpoint);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "检测 Yggdrasil Connect 支持失败", ModBase.LogLevel.Debug);
            return false;
        }
    }

    private static string? _GetClientId(string authServerUrl, JsonObject discoveryMetadata)
    {
        if (!Uri.TryCreate(authServerUrl, UriKind.Absolute, out var serverUri)) return null;
        if (YggdrasilConnectProvider.TryGetBuiltInClientId(serverUri.Host, out var builtInClientId))
            return builtInClientId;
        return discoveryMetadata["shared_client_id"]?.ToString();
    }

    // 链接处理
    private void ComboName_TextChanged(object sender, TextChangedEventArgs e)
    {
        _isRegisterMode = string.IsNullOrEmpty(TextName.Text);
        BtnLink.Content = _isRegisterMode
            ? Lang.Text("Launch.Account.Auth.Register")
            : Lang.Text("Launch.Account.Auth.ForgotPassword");
    }

    private void Btn_Click(object sender, EventArgs e)
    {
        ModBase.OpenWebsite(_isRegisterMode
            ? Config.InstanceAuth.AuthRegisterAddress.ToString()
            : Config.InstanceAuth.AuthRegisterAddress.ToString().Replace("/auth/register", "/auth/forgot"));
    }

    private void BtnWebsite_Click(object sender, EventArgs e)
    {
        var websiteUri = new UriBuilder(new Uri(_authServerUrl))
        {
            Path = "/",
            Query = "",
            Fragment = ""
        }.Uri;
        ModBase.OpenWebsite(websiteUri.ToString());
    }

    // 切换注册按钮可见性
    private void ReloadRegisterButton()
    {
        var address = Config.InstanceAuth.AuthRegisterAddress.ToString();
        _hasRegisterLink = new HttpValidator().Validate(address).IsValid;
        BtnLink.Visibility = !_isOAuthMode && _hasRegisterLink ? Visibility.Visible : Visibility.Collapsed;
    }

}
