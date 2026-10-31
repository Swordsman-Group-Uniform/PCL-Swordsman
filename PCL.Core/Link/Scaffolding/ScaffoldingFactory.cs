using PCL.Core.Link.Scaffolding.Client;
using PCL.Core.Link.Scaffolding.Client.Models;
using PCL.Core.Link.Scaffolding.EasyTier;
using PCL.Core.Link.Scaffolding.Exceptions;
using PCL.Core.Link.Scaffolding.Server;
using PCL.Core.App;
using System;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.IO.Net;

namespace PCL.Core.Link.Scaffolding;

public static class ScaffoldingFactory
{
    // Please update ScaffoldingServerContext.cs at the same time.
    private static readonly string _LobbyVendor = $"PCL CE {Basics.VersionName}, EasyTier {EasyTierMetadata.CurrentEasyTierVer}";
    private const string HostIp = "10.114.51.41";

    /// <exception cref="ArgumentException">Invalid lobby code.</exception>
    /// <exception cref="FailedToGetPlayerException">Thrown if failed to get host player info.</exception>
    /// <exception cref="InvalidOperationException">Failed to get EasyTier Info.</exception>
    public static async Task<ScaffoldingClientEntity> CreateClientAsync
        (string playerName, string lobbyCode, LobbyType from, CancellationToken ct = default)
    {
        var machineId = Utils.Secret.Identify.LauncherId;

        if (!LobbyCodeGenerator.TryParse(lobbyCode, out var info))
        {
            throw new ArgumentException("Invalid lobby code.", nameof(lobbyCode));
        }

        var etEntity = _CreateEasyTierEntity(info, 0, 0, false);
        if (etEntity.Launch() != 0)
        {
            throw new InvalidOperationException("Failed to launch EasyTier Core.");
        }

        try
        {
            var (etStatus, players) = await etEntity.CheckEasyTierStatusAsync(ct).ConfigureAwait(false);

            if (!etStatus || players is null)
            {
                throw new InvalidOperationException("Failed to get EasyTier Info.");
            }

            if (players.Players is null)
            {
                throw new FailedToGetPlayerException();
            }

            var hostInfo = players.Host;

            if (hostInfo is null)
            {
                throw new FailedToGetPlayerException("Can not get the host information.");
            }

            var hostName = string.IsNullOrEmpty(hostInfo.HostName) ? string.Empty : hostInfo.HostName;
            if (hostName.Length <= 22 || !int.TryParse(hostName[22..], out var scfPort) || scfPort <= 0)
            {
                throw new ArgumentException($"Invalid hostname: {hostName}", nameof(hostInfo));
            }

            var localPort = await etEntity.AddPortForwardAsync(hostInfo.Ip, scfPort).ConfigureAwait(false);

            var client = new ScaffoldingClient("127.0.0.1", localPort, playerName, machineId, _LobbyVendor);

            return new ScaffoldingClientEntity(client, etEntity, hostInfo);
        }
        catch
        {
            await etEntity.StopAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Create Scaffolding Server.
    /// </summary>
    /// <param name="mcPort">Target forward Miencraft shared port.</param>
    /// <param name="playerName">Game player name.</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">Fialed to launch EasyTier Core.</exception>
    public static ScaffoldingServerEntity CreateServer(int mcPort, string playerName)
    {
        var context = ScaffoldingServerContext.Create(playerName, mcPort);
        var scfPort = NetworkHelper.NewTcpPort();

        var etEntity = _CreateEasyTierEntity(context.UserLobbyInfo, mcPort, scfPort, true);
        var res = etEntity.Launch();
        if (res != 0)
        {
            throw new InvalidOperationException("Failed to launch EasyTier Core.");
        }

        // 内核可能「启动了但立刻退出」（例如参数不被当前版本接受）。
        // 以前这种情形会被当成创建成功：房主拿到房间号、界面上一切正常，
        // 但他的 EasyTier 根本不在网络里，房客永远等不到房主。这里直接检查一次。
        if (etEntity.WaitCoreExited(TimeSpan.FromSeconds(2.5)))
        {
            throw new InvalidOperationException(
                $"EasyTier Core 启动后立即退出，房间无法建立。内核输出：\n{etEntity.GetCoreOutputTail(15)}");
        }

        var server = new ScaffoldingServer(scfPort, context);

        return new ScaffoldingServerEntity(server, etEntity);
    }

    private static EasyTierEntity _CreateEasyTierEntity(LobbyInfo lobby, int mcPort, int port, bool asHost) =>
        new(lobby, mcPort, port, asHost);
}

public record ScaffoldingClientEntity(ScaffoldingClient Client, EasyTierEntity EasyTier, EasyPlayerInfo HostInfo);

public record ScaffoldingServerEntity(ScaffoldingServer Server, EasyTierEntity EasyTier);
