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
    ///     CurseForge API Key（作为 <c>x-api-key</c> 请求头发给 <c>api.curseforge.com</c>）。
    ///     <para>
    ///         **源码里不放 key**（key 不入库、不随仓库公开）。取值顺序：
    ///         <list type="number">
    ///             <item>构建注入：构建时设置环境变量 <c>PCL_WRITE_SECRET=1</c> 与
    ///                 <c>PCL_CURSEFORGE_API_KEY=…</c>，源码生成器会把它们编进程序集
    ///                 （本仓库的 <c>build.sh</c> / <c>tools/build.ps1</c> 会自动读取本地
    ///                 <c>_tmp/secrets.env</c> 并注入，该文件被 <c>.gitignore</c> 排除）；</item>
    ///             <item>运行时的环境变量 <c>PCL_CURSEFORGE_API_KEY</c>（不用重新编译）；</item>
    ///             <item>本地覆盖文件 <c>&lt;启动器目录&gt;\PCL\curseforge-api-key.txt</c>（不进仓库）。</item>
    ///         </list>
    ///         三者都没有时返回空串：官方接口会 403，但 Mod 搜索 / 详情会自动退回
    ///         <c>mod.mcimirror.top</c> 镜像，功能不会整个消失。
    ///     </para>
    /// </summary>
    public static string CurseForgeAPIKey { get; } = ResolveCurseForgeApiKey();

    private static string ResolveCurseForgeApiKey()
    {
        // ① 构建注入（编译进程序集）/ 运行时环境变量
        try
        {
            var injected = EnvironmentInterop.GetSecret("CURSEFORGE_API_KEY", readEnv: true).ReplaceNullOrEmpty();
            if (!string.IsNullOrWhiteSpace(injected)) return injected.Trim();
        }
        catch
        {
            // 读不出来就用后面的
        }

        // ② 本地覆盖文件（放在启动器目录的 PCL 文件夹里，不进仓库）
        try
        {
            var path = System.IO.Path.Combine(Basics.ExecutableDirectory, "PCL", "curseforge-api-key.txt");
            if (System.IO.File.Exists(path))
            {
                var text = System.IO.File.ReadAllText(path).Trim();
                if (text.Length > 0) return text;
            }
        }
        catch
        {
            // 读不到就当没有
        }

        return "";
    }

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
