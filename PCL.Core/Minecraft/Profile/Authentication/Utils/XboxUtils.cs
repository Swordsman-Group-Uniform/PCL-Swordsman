using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.IO.Net.Http;
using PCL.Core.Minecraft.IdentityModel;
using PCL.Core.Minecraft.Profile.Authentication.Utils.Models;

namespace PCL.Core.Minecraft.Profile.Authentication.Utils;

public static class XboxUtils
{
    private const string XboxLiveAuthServer = "https://user.auth.xboxlive.com/user/authenticate";
    private const string XstsAuthorizeServer = "https://xsts.auth.xboxlive.com/xsts/authorize";
    public static async Task<XboxLiveResponse?> AuthenticateAsync<T>(XboxAuthenticate<T> authData, bool isXsts,
        CancellationToken token)
    {
        using var response = await HttpRequest.CreatePost(
                isXsts ? XstsAuthorizeServer:XboxLiveAuthServer
                ).WithJsonContent(authData).SendAsync(cancellationToken: token)
            .ConfigureAwait(false);
        if (!response.IsSuccess)
        {
            var content = await response.AsStringAsync(token).ConfigureAwait(false);
            try
            {
                var error = JsonNode.Parse(content);
                if (long.TryParse(error?["XErr"]?.ToString(), out var xErr))
                    throw new IdentityModelAuthenticationException($"{(isXsts ? "xsts" : "xbox")}_{xErr}",
                        error?["Message"]?.ToString() ??
                        $"{(isXsts ? "XSTS" : "Xbox Live")} authorization failed with XErr {xErr}.");
            }
            catch (JsonException)
            {
            }

            // 上游只在 XSTS 失败时带错误详情，Xbox Live 用户认证失败会抛出没有上下文的
            // HttpRequestException，导致「正版登录失败」看不出真正原因（例如票据前缀不对）。
            throw new IdentityModelAuthenticationException(
                isXsts ? "xsts_authentication_failed" : "xbox_authentication_failed",
                $"{(isXsts ? "XSTS" : "Xbox Live")} 认证失败（HTTP {(int)response.StatusCode}）：{content}");
        }

        return await response.AsJsonAsync<XboxLiveResponse>(cancellationToken: token);
    }
}
