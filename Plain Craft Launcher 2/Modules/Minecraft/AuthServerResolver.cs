using System.Net;
using PCL.Core.App.Localization;
using PCL.Core.IO.Net.Http;

namespace PCL;

/// <summary>
///     把「拖进启动器的网页链接」解析成 authlib-injector 验证服务器根地址。
///     <para>
///         参考 HMCL 的做法：用户从浏览器把 LittleSkin 等皮肤站的链接拖进启动器即可一键添加外置登录。
///         解析顺序依次尝试：链接本身、链接 + <c>/api/yggdrasil</c>、站点根 + <c>/api/yggdrasil</c>，
///         并遵循 authlib-injector 的 <c>X-Authlib-Injector-API-Location</c> 响应头；
///         最终以「能返回含 <c>meta</c> / <c>signaturePublickey</c> 的 JSON」作为有效判据。
///     </para>
/// </summary>
internal static class AuthServerResolver
{
    /// <summary>
    ///     已知「说明页 / 官网」到 API 根地址的映射（这类链接本身不是 API 根）。
    /// </summary>
    private static readonly (string HostSuffix, string ApiRoot)[] KnownRedirects =
    [
        ("manual.littlesk.in", "https://littleskin.cn/api/yggdrasil"),
        ("littleskin.cn", "https://littleskin.cn/api/yggdrasil"),
        ("littlesk.in", "https://littleskin.cn/api/yggdrasil")
    ];

    /// <summary>
    ///     解析结果。
    /// </summary>
    /// <param name="ApiRoot">可直接填入验证服务器的 API 根地址。</param>
    /// <param name="ServerName">服务端自报名称（可能为 null）。</param>
    internal sealed record Result(string ApiRoot, string? ServerName);

    /// <summary>
    ///     判断文本是否为可尝试解析的网页链接。
    /// </summary>
    internal static bool LooksLikeUrl(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        return Uri.TryCreate(text.Trim(), UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    /// <summary>
    ///     构造待验证的候选地址（去重，保持顺序）。
    /// </summary>
    internal static IEnumerable<string> BuildCandidates(string url)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Add(string? candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate)) return;
            var value = candidate.Trim().TrimEnd('/');
            if (value.Length == 0) return;
            if (seen.Add(value)) result.Add(value);
        }

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)) return result;

        foreach (var (suffix, apiRoot) in KnownRedirects)
            if (uri.Host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                Add(apiRoot);

        Add(url);
        Add($"{url.TrimEnd('/')}/api/yggdrasil");
        Add($"{uri.Scheme}://{uri.Authority}/api/yggdrasil");
        return result;
    }

    /// <summary>
    ///     依次探测候选地址，返回第一个可用的 authlib-injector API 根。
    /// </summary>
    internal static async Task<Result?> ResolveAsync(string url, CancellationToken ct = default)
    {
        foreach (var candidate in BuildCandidates(url))
        {
            ct.ThrowIfCancellationRequested();
            var resolved = await ProbeAsync(candidate, ct).ConfigureAwait(false);
            if (resolved is not null) return resolved;
        }

        return null;
    }

    private static async Task<Result?> ProbeAsync(string candidate, CancellationToken ct)
    {
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(8));
            using var response = await HttpRequest.Create(candidate)
                .SendAsync(cancellationToken: timeoutCts.Token)
                .ConfigureAwait(false);
            if (!response.IsSuccess) return null;

            // 遵循 authlib-injector 的官方重定向头
            string? redirectedRoot = null;
            foreach (var (name, values) in response.GetHeaders())
                if (string.Equals(name, "X-Authlib-Injector-API-Location", StringComparison.OrdinalIgnoreCase) &&
                    values.Length > 0)
                    redirectedRoot = values[0];
            if (!string.IsNullOrWhiteSpace(redirectedRoot) &&
                !string.Equals(redirectedRoot, candidate, StringComparison.OrdinalIgnoreCase))
            {
                var redirected = await ProbeAsync(redirectedRoot, ct).ConfigureAwait(false);
                if (redirected is not null) return redirected;
            }

            var text = await response.AsStringAsync(ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(text)) return null;

            JsonObject? json = null;
            try
            {
                json = ModBase.GetJson(text) as JsonObject;
            }
            catch
            {
                // 不是 JSON：多半是说明页/官网，交给下一个候选
            }

            if (json is null) return null;

            var meta = json["meta"] as JsonObject;
            var serverName = meta?["serverName"]?.ToString() ?? json["serverName"]?.ToString();
            var isAuthlib = meta is not null || json["signaturePublickey"] is not null || serverName is not null;
            if (!isAuthlib) return null;

            ModBase.Log($"[Authlib] 已识别验证服务器：{candidate}（{serverName ?? "未命名"}）", ModBase.LogLevel.Debug);
            return new Result(candidate, serverName);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, $"[Authlib] 探测 {candidate} 失败", ModBase.LogLevel.Debug);
            return null;
        }
    }
}
