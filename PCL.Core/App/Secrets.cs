using System;
using PCL.Core.Utils.Exts;
using PCL.Core.Utils.OS;

namespace PCL.Core.App;

// ReSharper disable InconsistentNaming
public static class Secrets
{
    /// <summary>
    /// 微软 OAuth 的 Client ID
    /// </summary>
    public static string MSOAuthClientId { get; } = EnvironmentInterop.GetSecret("MS_CLIENT_ID", readEnvDebugOnly: true).ReplaceNullOrEmpty();

    /// <summary>
    /// CurseForge API 的 Client ID
    /// </summary>
    public static string CurseForgeAPIKey { get; } = EnvironmentInterop.GetSecret("CURSEFORGE_API_KEY", readEnvDebugOnly: true).ReplaceNullOrEmpty();

    /// <summary>
    /// 遥测密钥
    /// </summary>
    public static string TelemetryKey { get; } = EnvironmentInterop.GetSecret("TELEMETRY_KEY", readEnvDebugOnly: true).ReplaceNullOrEmpty();

    /// <summary>
    /// Natayark ID OAuth 的 Client ID
    /// </summary>
    public static string NatayarkClientId { get; } = EnvironmentInterop.GetSecret("NAID_CLIENT_ID", readEnvDebugOnly: true).ReplaceNullOrEmpty();

    /// <summary>
    /// Natayark ID OAuth 的 Client ID
    /// </summary>
    public static string NatayarkClientSecret { get; } = EnvironmentInterop.GetSecret("NAID_CLIENT_SECRET", readEnvDebugOnly: true).ReplaceNullOrEmpty();

    /// <summary>
    /// 官方公共联机根服务器（Pysio MinIO 镜像）。
    /// </summary>
    private const string PublicLinkServerPysio = "https://s3.pysio.online/pcl2-ce";

    /// <summary>
    /// 官方公共联机根服务器（Naids 静态资源镜像）。
    /// </summary>
    private const string PublicLinkServerNaids = "https://staticassets.naids.com/resources/pclce";

    /// <summary>
    /// 联机根服务器。
    /// <para>
    /// 若构建时未注入 LINK_SERVER_ROOT 密钥（本下游分支即如此），则回退到内置的官方公共联机根服务器，
    /// 以保证联机大厅可以正常获取公告、中继列表与可用状态。
    /// </para>
    /// </summary>
    public static string[] LinkServers { get; } = ResolveLinkServers();

    private static string[] ResolveLinkServers()
    {
        var configured = EnvironmentInterop.GetSecret("LINK_SERVER_ROOT", readEnvDebugOnly: true).ReplaceNullOrEmpty();
        if (!string.IsNullOrWhiteSpace(configured))
        {
            var servers = configured.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (servers.Length > 0)
                return servers;
        }

        return [PublicLinkServerPysio, PublicLinkServerNaids];
    }

    /// <summary>
    /// 当前版本的 Git 提交 SHA
    /// </summary>
    public static string CommitHash { get; } = EnvironmentInterop.GetSecret("GITHUB_SHA", readEnvDebugOnly: true).ReplaceNullOrEmpty();
}
