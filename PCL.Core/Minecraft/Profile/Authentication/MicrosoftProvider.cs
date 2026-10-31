using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.App;
using PCL.Core.IO.Net;
using PCL.Core.IO.Net.Http;
using PCL.Core.Logging;
using PCL.Core.Minecraft.IdentityModel.OAuth;
using PCL.Core.Minecraft.IdentityModel;
using PCL.Core.Minecraft.Profile.Authentication.Utils;
using PCL.Core.Minecraft.Profile.Authentication.Utils.Models;
using PCL.Core.Minecraft.Profile.Models;
using PCL.Core.Utils;

namespace PCL.Core.Minecraft.Profile.Authentication;

/// <summary>
/// Microsoft consumer account provider. OAuth, Xbox Live, XSTS and Minecraft service calls
/// are kept here so launch code never needs to understand the six-step protocol.
/// </summary>
/// <remarks>
///     支持两种 OAuth 设备码流程，按 Client ID 的形式自动选择：<br/>
///     1. <b>Entra ID v2</b>（<c>login.microsoftonline.com/consumers</c>）：只接受自行注册的 GUID 客户端 ID，
///        权限范围 <c>XboxLive.signin offline_access</c>，Xbox 票据前缀 <c>d=</c>。<br/>
///     2. <b>live.com 旧版</b>（<c>login.live.com/oauth20_*.srf</c>）：微软第一方（Title）客户端 ID
///        （<c>00000000402b5328</c>）只能走这一条，权限范围 <c>service::user.auth.xboxlive.com::MBI_SSL</c>，
///        Xbox 票据前缀为 <c>t=</c>（依据 RaphiMC/MinecraftAuth 的 <c>isTitleClientId() ? "t=" : "d="</c>）。<br/>
///     把第一方 Client ID 送进 v2 端点会得到 <c>AADSTS700016</c>，因此必须分流。<br/>
///     注意：第一方流程只支持<b>个人</b>微软账号，单位/学校账号（<c>@xxx.onmicrosoft.com</c>）无法登录。
/// </remarks>
public sealed class MicrosoftProvider : IAuthenticateProvider
{
    private const string V2DeviceEndpoint = "https://login.microsoftonline.com/consumers/oauth2/v2.0/devicecode";
    private const string V2TokenEndpoint = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token";
    private const string V2Scope = "XboxLive.signin offline_access";

    private const string LegacyDeviceEndpoint = "https://login.live.com/oauth20_connect.srf";
    private const string LegacyTokenEndpoint = "https://login.live.com/oauth20_token.srf";
    private const string LegacyScope = "service::user.auth.xboxlive.com::MBI_SSL";

    /// <summary>
    ///     内置的微软第一方客户端 ID（Xbox Live），配合 live.com 旧版设备码流程使用，无需注册任何应用。
    ///     如需改用自行注册的应用，注入构建密钥 <c>MS_CLIENT_ID</c> 或设置环境变量 <c>PCL_MS_CLIENT_ID</c> 即可。
    /// </summary>
    private const string FallbackPublicClientId = "00000000402b5328";

    private const string LegacyAccountHint =
        "提示：live.com 旧版流程只支持个人微软账号（@outlook.com / @live.com / @hotmail.com 等）。" +
        "单位或学校的账号（@xxx.onmicrosoft.com），以及尚未创建 Xbox 档案的账号，都无法通过这一步。";

    private readonly string _clientId;
    private readonly bool _useLegacyFlow;

    public MicrosoftProvider(string? clientId = null)
    {
        _clientId = ResolveClientId(clientId);
        if (string.IsNullOrWhiteSpace(_clientId))
            throw new InvalidOperationException("Microsoft OAuth client id is not configured.");
        // 自行注册的 Entra ID 应用 ID 是 GUID（形如 8f8c1f75-31e4-4bd2-a2f4-0d1e5c1dc0a2），
        // 微软第一方客户端 ID（00000000402b5328）不是。这里用 Guid.TryParseExact 判断，
        // 不手写正则 —— 之前用 Java 的 \p{XDigit} 写法在 .NET 里会在静态构造阶段抛 RegexParseException，
        // 直接导致整个类型初始化失败、正版登录连设备码弹窗都弹不出来。
        _useLegacyFlow = !Guid.TryParseExact(_clientId.Trim(), "D", out _);
        LogWrapper.Info("Profile", _useLegacyFlow
            ? $"微软登录：使用 live.com 旧版设备码流程（第一方 Client ID：{_clientId}）。"
            : $"微软登录：使用 Entra ID v2 设备码流程（Client ID：{_clientId}）。");
    }

    /// <summary>
    ///     解析可用的 Microsoft OAuth Client ID：
    ///     调用方指定 → 环境变量 <c>PCL_MS_CLIENT_ID</c> → 构建期注入密钥 → 内置第一方 ID。
    /// </summary>
    private static string ResolveClientId(string? explicitId)
    {
        if (!string.IsNullOrWhiteSpace(explicitId)) return explicitId.Trim();
        var fromEnv = Environment.GetEnvironmentVariable("PCL_MS_CLIENT_ID");
        if (!string.IsNullOrWhiteSpace(fromEnv)) return fromEnv.Trim();
        if (!string.IsNullOrWhiteSpace(Secrets.MSOAuthClientId)) return Secrets.MSOAuthClientId.Trim();
        LogWrapper.Info("Profile",
            $"未配置 Microsoft OAuth Client ID，使用内置的微软第一方客户端 ID（{FallbackPublicClientId}）+ live.com 旧版流程。");
        return FallbackPublicClientId;
    }

    public async Task<DeviceCodeData> GetDeviceCodeAsync(CancellationToken token)
    {
        return _useLegacyFlow
            ? await GetLegacyDeviceCodeAsync(token).ConfigureAwait(false)
            : await GetEntraIdDeviceCodeAsync(token).ConfigureAwait(false);
    }

    private async Task<DeviceCodeData> GetEntraIdDeviceCodeAsync(CancellationToken token)
    {
        using var response = await HttpRequest.CreatePost(V2DeviceEndpoint)
            .WithContent(new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = _clientId,
                ["scope"] = V2Scope
            }))
            .SendAsync(NetworkService.GetClient(NetworkService.MicrosoftEntraId), cancellationToken: token)
            .ConfigureAwait(false);
        var result = await response.AsJsonAsync<DeviceCodeData>(cancellationToken: token).ConfigureAwait(false)
                     ?? throw new InvalidOperationException("Microsoft device authorization response was empty.");
        if (result.IsError) throw new IdentityModelAuthenticationException(result.Error, result.ErrorDescription);
        result.Validate();
        return result;
    }

    /// <summary>
    ///     live.com 旧版设备码流程：返回的 <c>verification_uri</c> 为 <c>https://www.microsoft.com/link</c>，
    ///     个别响应里字段名是 <c>verification_url</c>，因此手工解析而不用 DTO 反序列化。
    /// </summary>
    private async Task<DeviceCodeData> GetLegacyDeviceCodeAsync(CancellationToken token)
    {
        HttpResponseMessage response;
        try
        {
            response = await HttpRequest.CreatePost(LegacyDeviceEndpoint)
                .WithContent(new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = _clientId,
                    ["response_type"] = "device_code",
                    ["scope"] = LegacyScope
                }))
                .SendAsync(NetworkService.GetClient(NetworkService.MicrosoftEntraId), cancellationToken: token)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogWrapper.Error(ex, "Profile", "无法连接 login.live.com 获取设备授权码");
            throw new IdentityModelAuthenticationException("network_error",
                $"无法连接 login.live.com（{ex.Message}）。若开启了网络加速或代理，请把 login.live.com 与 login.microsoftonline.com 设为直连。");
        }

        using var _ = response;
        var raw = (await response.AsStringAsync(token).ConfigureAwait(false)).Trim();
        LogWrapper.Debug("Profile", $"live.com 设备授权响应（HTTP {(int)response.StatusCode}）：{raw}");
        var obj = _TryParseObject(raw, "获取设备授权码");
        var error = obj?["error"]?.ToString();
        if (!string.IsNullOrWhiteSpace(error))
            throw new IdentityModelAuthenticationException(error,
                obj?["error_description"]?.ToString() ?? $"微软设备授权失败（HTTP {(int)response.StatusCode}）：{raw}");
        var result = new DeviceCodeData
        {
            DeviceCode = obj?["device_code"]?.ToString(),
            UserCode = obj?["user_code"]?.ToString(),
            VerificationUri = obj?["verification_uri"]?.ToString() ?? obj?["verification_url"]?.ToString(),
            VerificationUriComplete = obj?["verification_uri_complete"]?.ToString(),
            Interval = _TryParseInt(obj?["interval"]?.ToString()) ?? 5,
            ExpiresIn = _TryParseInt(obj?["expires_in"]?.ToString())
        };
        result.Validate();
        LogWrapper.Info("Profile",
            $"已获取旧版设备授权码：{result.VerificationUri} 输入 {result.UserCode}（{result.ExpiresIn} 秒内有效）。");
        return result;
    }

    public async Task<AuthorizeResult?> PollDeviceCodeAsync(DeviceCodeData data, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(data);
        var form = new Dictionary<string, string>
        {
            ["client_id"] = _clientId,
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
            ["device_code"] = data.DeviceCode ?? string.Empty
        };
        if (_useLegacyFlow) form["scope"] = LegacyScope;

        using var response = await HttpRequest.CreatePost(_useLegacyFlow ? LegacyTokenEndpoint : V2TokenEndpoint)
            .WithContent(new FormUrlEncodedContent(form))
            .SendAsync(NetworkService.GetClient(NetworkService.MicrosoftEntraId), cancellationToken: token)
            .ConfigureAwait(false);
        var result = await response.AsJsonAsync<AuthorizeResult>(cancellationToken: token).ConfigureAwait(false);
        if (result is null)
        {
            LogWrapper.Warn("Profile", "微软设备授权轮询返回了空响应。");
            return null;
        }

        if (result.IsError && result.Error is not ("authorization_pending" or "slow_down"))
        {
            LogWrapper.Warn("Profile", $"微软设备授权轮询失败：{result.Error}（{result.ErrorDescription}）");
            result = result with { ErrorDescription = _WithLegacyHint(result.Error, result.ErrorDescription) };
        }

        result.Validate();
        return result;
    }

    public async Task<AuthenticationResult> AuthenticateAsync(AuthenticationRequest request, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(request);
        var oauth = request.OAuthResult;

        if (oauth is null && !string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            try
            {
                oauth = await RefreshOAuthAsync(request.RefreshToken, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // 旧档案里的刷新令牌可能来自另一套流程或另一个客户端 ID，刷新必然失败。
                // 这种情况不该让整次登录卡在「刷新失败」，而是直接走一次全新的设备码登录。
                if (request.DeviceCodeHandler is null) throw;
                LogWrapper.Warn("Profile", $"刷新微软账号令牌失败，改为重新进行设备码登录：{ex.Message}");
            }
        }

        if (oauth is null && request.DeviceCodeHandler is not null)
        {
            var device = await GetDeviceCodeAsync(token).ConfigureAwait(false);
            oauth = await request.DeviceCodeHandler(
                new DeviceCodeAuthenticationContext(device, pollToken => PollDeviceCodeAsync(device, pollToken)), token)
                .ConfigureAwait(false);
        }

        if (oauth is null)
            throw new IdentityModelConfigurationException("Microsoft login requires an OAuth result or device-code handler.");
        return await CompleteAsync(oauth, token).ConfigureAwait(false);
    }

    public async Task<AuthenticationResult> RefreshAsync(McProfile profile, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(profile.RefreshToken))
            throw new IdentityModelAuthenticationException("invalid_grant", "Microsoft refresh token is empty.");
        return await AuthenticateAsync(new AuthenticationRequest { RefreshToken = profile.RefreshToken }, token)
            .ConfigureAwait(false);
    }

    public async Task<bool> ValidateAsync(McProfile profile, CancellationToken token)
    {
        if (profile.IsExpired || string.IsNullOrWhiteSpace(profile.AccessToken)) return false;
        return await MojangUtils.CheckLicenseAsync(profile, token).ConfigureAwait(false);
    }

    public async Task<AuthorizeResult> RefreshOAuthAsync(string refreshToken, CancellationToken token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);
        using var response = await HttpRequest.CreatePost(_useLegacyFlow ? LegacyTokenEndpoint : V2TokenEndpoint)
            .WithContent(new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = _clientId,
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["scope"] = _useLegacyFlow ? LegacyScope : V2Scope
            }))
            .SendAsync(NetworkService.GetClient(NetworkService.MicrosoftEntraId), cancellationToken: token)
            .ConfigureAwait(false);
        var result = await response.AsJsonAsync<AuthorizeResult>(cancellationToken: token).ConfigureAwait(false)
                     ?? throw new InvalidOperationException("Microsoft token response was empty.");
        if (result.IsError)
        {
            LogWrapper.Warn("Profile", $"刷新微软令牌返回错误：{result.Error}（{result.ErrorDescription}）");
            throw new IdentityModelAuthenticationException(result.Error,
                _WithLegacyHint(result.Error, result.ErrorDescription));
        }

        result.Validate();
        return result with { RefreshToken = result.RefreshToken ?? refreshToken };
    }

    public async Task<AuthenticationResult> CompleteAsync(AuthorizeResult oauth, CancellationToken token)
    {
        if (oauth.IsError) throw new IdentityModelAuthenticationException(oauth.Error, oauth.ErrorDescription);
        if (string.IsNullOrWhiteSpace(oauth.AccessToken))
            throw new IdentityModelAuthenticationException("invalid_token", "Microsoft OAuth did not return an access token.");

        var xbl = await _AcquireXboxUserTokenAsync(oauth.AccessToken!, token).ConfigureAwait(false);

        var xsts = await XboxUtils.AuthenticateAsync(new XboxAuthenticate<XSTSProperty>
        {
            Properties = new XSTSProperty { UserTokens = [xbl.Token] },
            RelyingParty = "rp://api.minecraftservices.com/",
            TokenType = "JWT"
        }, isXsts: true, token).ConfigureAwait(false)
                   ?? throw new IdentityModelAuthenticationException("xsts_authentication_failed", "XSTS returned no token.");

        var userHash = xsts.DisplayClaims?.Xui is { Count: > 0 } ? xsts.DisplayClaims.Xui[0].UserHash : null;
        if (string.IsNullOrWhiteSpace(userHash))
            throw new IdentityModelAuthenticationException("xsts_missing_user_hash", "XSTS response did not contain a user hash.");

        var minecraft = await MojangUtils.AuthenticateAsync(xsts.Token, userHash, token).ConfigureAwait(false)
                        ?? throw new IdentityModelAuthenticationException("minecraft_authentication_failed", "Minecraft authentication returned no token.");
        if (string.IsNullOrWhiteSpace(minecraft.AccessToken))
            throw new IdentityModelAuthenticationException("minecraft_authentication_failed", "Minecraft access token is empty.");

        var safe = new SafeProfile
        {
            UserName = minecraft.UserName,
            AccessToken = minecraft.AccessToken,
            Uuid = string.Empty,
            TokenType = minecraft.TokenType
        };
        if (!await MojangUtils.CheckLicenseAsync(safe, token).ConfigureAwait(false))
            throw new IdentityModelAuthenticationException("minecraft_not_owned", "The Microsoft account does not own Minecraft.");

        var profile = await MojangUtils.GetProfileAsync(safe, token).ConfigureAwait(false);
        return new AuthenticationResult
        {
            ProfileType = ProfileType.Microsoft,
            UserName = profile.Name,
            Uuid = profile.Uuid,
            AccessToken = minecraft.AccessToken,
            RefreshToken = oauth.RefreshToken ?? string.Empty,
            ClientToken = profile.Uuid,
            TokenType = minecraft.TokenType,
            ExpiresAt = minecraft.ExpiresIn > 0 ? DateTimeOffset.UtcNow.AddSeconds(minecraft.ExpiresIn) : null,
            RawJson = System.Text.Json.JsonSerializer.Serialize(profile),
            Provider = "microsoft"
        };
    }

    /// <summary>
    ///     用 MSA 访问令牌换取 Xbox Live 用户令牌。<br/>
    ///     票据前缀取决于客户端类型：第一方（Title）客户端用 <c>t=</c>，自行注册的应用用 <c>d=</c>
    ///     （依据 RaphiMC/MinecraftAuth 里 <c>isTitleClientId() ? "t=" : "d="</c> 的分支）。<br/>
    ///     旧版流程另外再兜底尝试其余两种写法，避免因为前缀差异导致整次登录失败。
    /// </summary>
    private async Task<XboxLiveResponse> _AcquireXboxUserTokenAsync(string accessToken, CancellationToken token)
    {
        (string Label, string Ticket)[] candidates = _useLegacyFlow
            ? [("t=（第一方票据）", $"t={accessToken}"), ("d=", $"d={accessToken}"), ("无前缀", accessToken)]
            : [("d=", $"d={accessToken}")];

        Exception? lastError = null;
        foreach (var (label, ticket) in candidates)
        {
            try
            {
                var xbl = await XboxUtils.AuthenticateAsync(new XboxAuthenticate<XboxProperty>
                {
                    Properties = new XboxProperty { RpsTicket = ticket },
                    TokenType = "JWT"
                }, isXsts: false, token).ConfigureAwait(false);
                if (xbl is not null)
                {
                    LogWrapper.Info("Profile", $"Xbox Live 用户认证成功（票据前缀：{label}）。");
                    return xbl;
                }

                lastError = new IdentityModelAuthenticationException("xbox_authentication_failed",
                    "Xbox Live returned no token.");
            }
            catch (Exception ex)
            {
                lastError = ex;
                LogWrapper.Warn("Profile", $"Xbox Live 用户认证失败（票据前缀：{label}）：{ex.Message}");
            }
        }

        throw lastError ?? new IdentityModelAuthenticationException("xbox_authentication_failed",
            "Xbox Live authentication failed.");
    }

    /// <summary>
    ///     给旧版流程的常见错误补一句可操作的说明（个人账号限制等）。
    /// </summary>
    private string? _WithLegacyHint(string? error, string? description)
    {
        if (!_useLegacyFlow || string.IsNullOrWhiteSpace(error)) return description;
        return error.ToLowerInvariant() switch
        {
            "invalid_grant" or "access_denied" or "unauthorized_client" =>
                $"{description}\n\n{LegacyAccountHint}",
            _ => description
        };
    }

    private static JsonObject? _TryParseObject(string raw, string action)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            LogWrapper.Warn("Profile", $"{action}时微软返回了空响应。");
            return null;
        }

        try
        {
            return JsonCompat.ParseNode(raw) as JsonObject;
        }
        catch (Exception ex)
        {
            LogWrapper.Warn("Profile", $"{action}时无法解析微软返回的内容：{ex.Message}；原始内容：{raw}");
            return null;
        }
    }

    private static int? _TryParseInt(string? raw) => int.TryParse(raw, out var value) ? value : null;
}
