using System.IO;
using System.Net.Http;
using System.Text;
using PCL.PluginSdk;

namespace PCL.PluginHost;

/// <summary>
///     <see cref="IPluginHttp" /> 的宿主实现：能力门禁 + **用户授权**双闸，然后才真的发请求。
///     <para>
///         没有 <c>net-http</c> 能力 → 直接拒绝；有能力但用户还没授权 → 弹一次确认框（宿主实现），
///         拒绝过的插件不再重复弹，直接拒绝。所有结果都写日志，便于排查。
///     </para>
/// </summary>
internal sealed class PluginHttp(
    string pluginId,
    string pluginName,
    bool hasCapability,
    IPluginPermissionHost permissions,
    IPluginLogger log) : IPluginHttp
{
    private static readonly HttpClient Client = new(new HttpClientHandler
    {
        AllowAutoRedirect = true,
        AutomaticDecompression = System.Net.DecompressionMethods.All
    })
    {
        Timeout = Timeout.InfiniteTimeSpan // 超时按每次请求传
    };

    public PluginHttpResponse Get(string url, IReadOnlyDictionary<string, string>? headers = null,
        int timeoutSeconds = 15)
        => Send(HttpMethod.Get, url, null, null, headers, timeoutSeconds);

    public PluginHttpResponse Post(string url, string body, string contentType = "application/json",
        IReadOnlyDictionary<string, string>? headers = null, int timeoutSeconds = 15)
        => Send(HttpMethod.Post, url, body, contentType, headers, timeoutSeconds);

    private PluginHttpResponse Send(HttpMethod method, string url, string? body, string? contentType,
        IReadOnlyDictionary<string, string>? headers, int timeoutSeconds)
    {
        if (string.IsNullOrWhiteSpace(url))
            return Failure("URL 为空");
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            return Failure("只支持 http / https 的绝对地址");

        if (!hasCapability)
        {
            log.Warn($"插件 {pluginId} 没有声明 net-http 能力，请求被拒绝：{url}");
            return Failure("没有 net-http 能力");
        }

        if (!EnsureGranted($"联网请求（{uri.Host}）"))
            return Failure("用户未授权联网");

        try
        {
            using var request = new HttpRequestMessage(method, uri);
            if (body is not null && method != HttpMethod.Get)
                request.Content = new StringContent(body, Encoding.UTF8,
                    string.IsNullOrWhiteSpace(contentType) ? "application/json" : contentType);
            if (headers is not null)
                foreach (var (key, value) in headers)
                    if (!string.IsNullOrWhiteSpace(key) && value is not null)
                        request.Headers.TryAddWithoutValidation(key, value);

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(
                Math.Clamp(timeoutSeconds, 1, 120)));
            using var response = Client.Send(request, timeout.Token);
            using var reader = new StreamReader(response.Content.ReadAsStream(timeout.Token), Encoding.UTF8);

            var responseHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in response.Headers)
                responseHeaders[header.Key] = string.Join(", ", header.Value);

            return new PluginHttpResponse
            {
                StatusCode = (int)response.StatusCode,
                Body = reader.ReadToEnd(),
                Headers = responseHeaders
            };
        }
        catch (Exception ex)
        {
            log.Error($"插件 {pluginId} 的 HTTP 请求失败：{url}", ex);
            return Failure(ex.Message);
        }
    }

    /// <summary>授权闸：已授权直接过；未决定就问一次（结果持久化）；拒绝过就直接否。</summary>
    private bool EnsureGranted(string description)
    {
        try
        {
            if (permissions.IsGranted(pluginId, PluginCapability.NetHttp)) return true;
            if (permissions.IsDenied(pluginId, PluginCapability.NetHttp)) return false;

            var granted = permissions.RequestGrant(pluginId, pluginName, PluginCapability.NetHttp, description);
            log.Info($"联网授权：{(granted ? "已允许" : "已拒绝")}");
            return granted;
        }
        catch (Exception ex)
        {
            log.Error($"插件 {pluginId} 的授权流程出错，按拒绝处理", ex);
            return false;
        }
    }

    private static PluginHttpResponse Failure(string message) => new()
    {
        StatusCode = 0,
        Body = "请求未发出：" + message,
        Headers = new Dictionary<string, string>()
    };
}
