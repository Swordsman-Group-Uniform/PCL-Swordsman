using PCL.Core.App;
using PCL.Core.Link.EasyTier;
using PCL.Core.Link.Scaffolding.Client.Models;
using PCL.Core.Logging;
using PCL.Core.Utils;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using PCL.Core.IO.Net;
using PCL.Core.IO.Net.Http;

namespace PCL.Core.Link.Scaffolding.EasyTier;

/// <summary>
/// Demonstrates the state of EasyTier entity.
/// </summary>
public enum EtState
{
    Stopped,
    Active,
    Ready
}

/// <summary>
/// An EasyTier entity that manages the EasyTier process and its interactions.
/// </summary>
public class EasyTierEntity
{
    private readonly Process _etProcess;
    private readonly int _rpcPort;

    /// <summary>
    ///     等待 EasyTier 网络就绪的最长时间。原先按「300 次重试」计数，
    ///     一旦每次 CLI 调用都超时，实际会卡住十几分钟甚至更久（表现为界面一直停在「正在加入……」）。
    /// </summary>
    private static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(180);

    private readonly StringBuilder _coreOutput = new();
    private readonly object _coreOutputLock = new();
    private volatile bool _coreExited;
    private readonly LobbyInfo _lobby;
    private readonly int _scfPort;

    public int ForwardPort { get; private set; }
    public int MinecraftPort { get; init; }
    public EtState State { get; private set; }
    public LobbyInfo Lobby => _lobby;

    public event Action? EasyTierProcessExisted;

    /// <summary>
    /// Constructor of EasyTierEntity
    /// </summary>
    /// <param name="lobby">The room information.</param>
    /// <param name="minecraftPort">Minecraft port.</param>
    /// <param name="scfPort">The server port.</param>
    /// <param name="asHost">Indicates whether the entity acts as a host.</param>
    /// <exception cref="FileNotFoundException">Thrown if EasyTier was broken.</exception>
    public EasyTierEntity(LobbyInfo lobby, int minecraftPort, int scfPort, bool asHost)
    {
        _lobby = lobby;
        MinecraftPort = minecraftPort;
        _scfPort = scfPort;
        State = EtState.Stopped;

        var existEntities = Process.GetProcessesByName("easytier-core");
        foreach (var entity in existEntities)
        {
            LogWrapper.Warn("EasyTier", $"Find exist EasyTier Entity, may affect something: {entity.Id}");
        }

        LogWrapper.Info("EasyTier", $"EasyTier folder path: {EasyTierMetadata.EasyTierFilePath}");

        if (!(File.Exists($"{EasyTierMetadata.EasyTierFilePath}\\easytier-core.exe") &&
              File.Exists($"{EasyTierMetadata.EasyTierFilePath}\\easytier-cli.exe") &&
              File.Exists($"{EasyTierMetadata.EasyTierFilePath}\\Packet.dll")))
        {
            LogWrapper.Error("EasyTier", "EasyTier was broken.");

            throw new FileNotFoundException("EasyTier was broken.");
        }

        State = EtState.Ready;

        _rpcPort = NetworkHelper.NewTcpPort();

        ForwardPort = NetworkHelper.NewTcpPort();

        _etProcess = _BuildProcessAsync(asHost).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Launches EasyTier process.
    /// </summary>
    /// <returns>
    /// - 1 means failed to launch EasyTier and can never launch again.<br/>
    /// - 0 means successful launch.
    /// </returns>
    public int Launch()
    {
        LogWrapper.Info("EasyTier", "Launch EasyTier Core.");

        try
        {
            // LogWrapper.Info("Test", _etProcess.StartInfo.Arguments);
            _etProcess.Start();
            State = EtState.Active;

            // 持续消费内核输出，并保留最后若干行用于诊断
            _etProcess.OutputDataReceived += (_, e) => _AppendCoreOutput(e.Data);
            _etProcess.ErrorDataReceived += (_, e) => _AppendCoreOutput(e.Data);
            _etProcess.BeginOutputReadLine();
            _etProcess.BeginErrorReadLine();

            var cli = _GetCliOutputDebugAsync();

            _etProcess.Exited += (_, _) =>
            {
                _coreExited = true;
                LogWrapper.Warn("EasyTier",
                    $"EasyTier Core 已退出（退出码 {_GetExitCodeText()}）。最后的输出：\n{GetCoreOutputTail()}");
                EasyTierProcessExisted?.Invoke();
            };
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "EasyTier", "Failed to launch EasyTier.");
            State = EtState.Stopped;
            return 1;
        }

        return 0;
    }

    /// <summary>
    /// Stops EasyTier process.
    /// </summary>
    /// <returns>
    /// - 1 means failed to stop EasyTier.<br/>
    /// - 0 means successful stop.
    /// </returns>
    public Task<int> StopAsync()
    {
        return Task.Run(() =>
        {
            try
            {
                if (!_etProcess.HasExited)
                {
                    _etProcess.Kill(true);
                    _etProcess.WaitForExit(5000);
                }

                State = EtState.Stopped;
                return 0;
            }
            catch (Exception ex)
            {
                LogWrapper.Error(ex, "EasyTier", "Failed to stop EasyTier.");
                State = EtState.Stopped;
                return 1;
            }
        });
    }

    private async Task<Process> _BuildProcessAsync(bool asHost)
    {
        var process = new Process
        {
            EnableRaisingEvents = true,
            StartInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(EasyTierMetadata.EasyTierFilePath, "easytier-core.exe"),
                WorkingDirectory = EasyTierMetadata.EasyTierFilePath,
                WindowStyle = ProcessWindowStyle.Hidden,
                // 必须重定向才能知道内核为什么起不来（例如缺 Packet.dll、参数不兼容、端口被占用）。
                // 注意：重定向后一定要持续读取，否则管道写满会把内核卡死。
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            }
        };

        var args = new ArgumentsBuilder();

        args.AddFlag("no-tun")
            .AddFlag("multi-thread")
            .AddFlag("enable-kcp-proxy")
            .AddFlag("enable-quic-proxy")
            // 注意：EasyTier 的参数是 --disable-sym-hole-punching（sym = symmetric）。
            // 旧的 TerraCotta 实现里拼成了 disable-sys-hole-punching，EasyTier 2.6.4 会直接报
            // "unexpected argument" 并退出（退出码 2），导致「房主/房客都以为在联机、其实内核早死了」。
            .AddFlagIf(!Config.Link.TryPunchSym, "disable-sym-hole-punching")
            .AddFlagIf(!Config.Link.EnableIPv6, "disable-ipv6")
            .AddFlagIf(Config.Link.UseLatencyFirstMode, "latency-first")
            .Add("encryption-algorithm", "aes-gcm")
            .Add("compression", "zstd")
            .Add("default-protocol", Config.Link.ProtocolPreference.ToString().ToLowerInvariant())
            .Add("network-name", _lobby.NetworkName)
            .Add("network-secret", _lobby.NetworkSecret)
            //.Add("relay-network-whitelist", _lobby.NetworkName)
            .Add("machine-id", Utils.Secret.Identify.LauncherId)
            .Add("rpc-portal", _rpcPort.ToString())
            .Add("private-mode", "true")
            .AddFlag("p2p-only");


        if (asHost)
        {
            args.AddWithSpace("i", "10.114.51.41")
                .Add("hostname", $"scaffolding-mc-server-{_scfPort}")
                .Add("tcp-whitelist", _scfPort.ToString())
                .Add("udp-whitelist", _scfPort.ToString())
                .Add("tcp-whitelist", MinecraftPort.ToString())
                .Add("udp-whitelist", MinecraftPort.ToString())
                .Add("l", "tcp://0.0.0.0:0")
                .Add("l", "udp://0.0.0.0:0");
        }
        else
        {
            args.AddFlag("d")
                .Add("hostname", Guid.NewGuid().ToString())
                .Add("tcp-whitelist", "0")
                .Add("udp-whitelist", "0")
                .Add("l", "tcp://0.0.0.0:0")
                .Add("l", "udp://0.0.0.0:0");
        }

        // TerraCotta：中继模式为「仅 P2P」时不连接任何公共中继节点。
        // 注意：原有 ETRelay.RelayList 由 PCL 联机大厅服务器下发，纯 P2P 模式下不再使用。
        var usePublicRelay = Config.Link.TerraCottaRelayMode != 0;
        if (usePublicRelay)
        {
            var relays = new List<string>(_fallbackNodeLinks);
            // 兜底列表里的节点可能整体不可达，这里再向 EasyTier 官方节点列表要一份（最多等 4 秒）。
            // 中继是双方唯一的碰面途径，能连上任意一个就能建立房间，因此多给几个候选意义很大。
            try
            {
                var liveNodes = await _GetPublicNodeAsync().WaitAsync(TimeSpan.FromSeconds(4)).ConfigureAwait(false);
                foreach (var node in liveNodes)
                    if (!relays.Contains(node, StringComparer.OrdinalIgnoreCase))
                        relays.Add(node);
            }
            catch (Exception ex)
            {
                LogWrapper.Warn("EasyTier", $"获取 EasyTier 公共节点列表失败，继续使用兜底节点：{ex.Message}");
            }

            var usedRelays = relays.Take(8).ToList();
            foreach (var address in usedRelays) args.Add("p", address);
            LogWrapper.Info("EasyTier", "本次使用的公共中继：\n\t" + string.Join("\n\t", usedRelays));
        }
        
        // foreach (var address in await _GetEtRelayListAsync().ConfigureAwait(false))
        // {
        //     args.Add("p", address);
        // }

        // if (Config.Link.RelayType == 1)
        // {
        //     args.AddFlag("disable-p2p");
        // }

        process.StartInfo.Arguments = args.GetResult();

        LogWrapper.Debug("EasyTier", process.StartInfo.Arguments);

        return process;
    }

    private async Task<IReadOnlyList<string>> _GetEtRelayListAsync()
    {
        var relays = ETRelay.RelayList;
        var customedNodes = Config.Link.CustomRelayServer.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var node in customedNodes)
        {
            if (node.Contains("tcp://", StringComparison.OrdinalIgnoreCase) ||
                node.Contains("udp://", StringComparison.OrdinalIgnoreCase))
            {
                relays.Add(new ETRelay
                {
                    Url = node,
                    Name = "Custom",
                    Type = ETRelayType.Custom
                });
            }
            else
            {
                LogWrapper.Warn("EasyTier", $"Invalid custom node URL: {node}.");
            }
        }

        var setupRelayList = relays.Select(relay => new { relay, serverType = Config.Link.ServerType })
            .Where(rl =>
                (rl.relay.Type == ETRelayType.Selfhosted && rl.serverType != 2) ||
                (rl.relay.Type == ETRelayType.Community && rl.serverType == 1) ||
                rl.relay.Type == ETRelayType.Custom)
            .Select(rl => rl.relay.Url).ToImmutableList();

        var pubNode = await _GetPublicNodeAsync().ConfigureAwait(false);

        var result = setupRelayList
            .Concat(pubNode)
            .Take(6)
            .ToImmutableList();

        LogWrapper.Debug($"Get public node:\n{string.Join("\n\t", result)}");

        return result;
    }

    private readonly string[] _fallbackNodeLinks =
    [
        "tcp://public.easytier.top:11010",
        "tcp://public2.easytier.cn:54321",
        "https://etnode.zkitefly.eu.org/node1",
        "https://etnode.zkitefly.eu.org/node2",
        "https://etnode.zkitefly.eu.org/-node1",
        "https://etnode.zkitefly.eu.org/-node2"
    ];


    private async Task<IReadOnlyList<string>> _GetPublicNodeAsync()
    {
        using var rep = await HttpRequest
            .Create("https://uptime.easytier.cn/api/nodes?page=1&per_page=50&is_active=true")
            .SendAsync()
            .ConfigureAwait(false);

        rep.EnsureSuccessStatusCode();

        var dto = await rep
            .AsJsonAsync<PublicNodeDto>()
            .ConfigureAwait(false);

        ArgumentNullException.ThrowIfNull(dto);

        var result = dto.Data.Items
            .Where(it => it is { IsActive: true, IsAllowRelay: true })
            .Select(it => it.Host)
            .Union(_fallbackNodeLinks)
            .ToImmutableList();

        return result;
    }


    #region Information

    /// <summary>
    /// Checks the status of EasyTier network until it is ready or time-out.
    /// </summary>
    /// <returns>Returns 0 when the network is ready, otherwise returns 1 for timeout.</returns>
    public async Task<(bool, EtPlayerList?)> CheckEasyTierStatusAsync(CancellationToken ct = default)
    {
        if (_etProcess is null) return (false, null);

        var startedAt = DateTime.UtcNow;
        var lastReport = -10;

        while (State is EtState.Active)
        {
            ct.ThrowIfCancellationRequested();

            // 内核已经退出就不必再等了，直接把它的输出带出去，便于定位
            if (_coreExited || _etProcess.HasExited)
            {
                LogWrapper.Error("EasyTier",
                    $"EasyTier Core 在联机就绪前退出，无法获取房间信息。最后的输出：\n{GetCoreOutputTail()}");
                return (false, null);
            }

            var elapsed = (int)(DateTime.UtcNow - startedAt).TotalSeconds;
            if (DateTime.UtcNow - startedAt > ReadyTimeout)
            {
                LogWrapper.Warn("EasyTier",
                    $"等待 EasyTier 网络就绪超时（{elapsed} 秒），未能发现房主节点。" +
                    (string.IsNullOrWhiteSpace(GetCoreOutputTail(5)) ? "" : $"\nEasyTier Core 最后的输出：\n{GetCoreOutputTail(20)}"));
                return (false, null);
            }

            var info = await _GetPlayersAsync().ConfigureAwait(false);
            if (info.Host is null)
            {
                LogWrapper.Debug("EasyTierEntity", "Retry to get EasyTier Info.");
                if (elapsed - lastReport >= 10)
                {
                    lastReport = elapsed;
                    LogWrapper.Info("EasyTier",
                        $"仍在等待房主的 EasyTier 节点出现（已等待 {elapsed} 秒，最多 {ReadyTimeout.TotalSeconds} 秒）：当前可见节点 {info.Players?.Count ?? 0} 个。");
                }

                await Task.Delay(1000, ct).ConfigureAwait(false);
                continue;
            }

            LogWrapper.Debug("EtEntity", "Successfully to get player info from EasyTier CLI.");

            if (info.Host.Ping < 1000)
            {
                State = EtState.Ready;
                return (true, info);
            }

            if (elapsed - lastReport >= 10)
            {
                lastReport = elapsed;
                LogWrapper.Info("EasyTier",
                    $"已发现房主节点 {info.Host.HostName}（{info.Host.Ip}），但延迟 {info.Host.Ping} ms 仍不达标，继续等待（已等待 {elapsed} 秒）。");
            }

            await Task.Delay(1000, ct).ConfigureAwait(false);
        }

        LogWrapper.Warn("EasyTier", "EasyTier 进程不在活动状态，停止等待网络就绪。");
        return (false, null);
    }

    /// <summary>
    ///     EasyTier Core 是否已退出（用于快速失败）。
    /// </summary>
    public bool HasCoreExited
    {
        get
        {
            if (_coreExited) return true;
            try
            {
                return _etProcess is { HasExited: true };
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    ///     等待一小段时间，若内核在这段时间内退出则返回 <c>true</c>。
    ///     用于「启动成功但立刻崩掉」的场景（参数不被版本接受、缺少依赖等）。
    /// </summary>
    public bool WaitCoreExited(TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (HasCoreExited) return true;
            Thread.Sleep(100);
        }

        return HasCoreExited;
    }

    /// <summary>
    ///     取 EasyTier Core 最后的输出（用于日志与联机诊断）。
    /// </summary>
    public string GetCoreOutputTail(int lines = 40)
    {
        string[] all;
        lock (_coreOutputLock) all = _coreOutput.ToString().Split('\n');
        var take = Math.Max(1, lines);
        var start = Math.Max(0, all.Length - take);
        return string.Join("\n", all[start..]).Trim();
    }

    private string _GetExitCodeText()
    {
        try
        {
            return _etProcess.HasExited ? _etProcess.ExitCode.ToString() : "未知";
        }
        catch
        {
            return "未知";
        }
    }

    private void _AppendCoreOutput(string? line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        lock (_coreOutputLock)
        {
            _coreOutput.AppendLine(line);
            // 只保留尾部，避免长时间运行后无限增长
            if (_coreOutput.Length > 60_000) _coreOutput.Remove(0, _coreOutput.Length - 40_000);
        }
    }

    /// <summary>
    /// Add a port forward to the EasyTier instance.
    /// </summary>
    /// <param name="targetIp">Remote IP</param>
    /// <param name="targetPort">Remote Port</param>
    /// <returns>Forwarded local port</returns>
    public async Task<int> AddPortForwardAsync(string targetIp, int targetPort)
    {
        var localPort = NetworkHelper.NewTcpPort();
        using var cliProcess = new Process();
        cliProcess.StartInfo = new ProcessStartInfo
        {
            FileName = $"{EasyTierMetadata.EasyTierFilePath}\\easytier-cli.exe",
            WorkingDirectory = EasyTierMetadata.EasyTierFilePath,
            ErrorDialog = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            StandardInputEncoding = Encoding.UTF8
        };
        cliProcess.EnableRaisingEvents = true;
        try
        {
            cliProcess.StartInfo.Arguments =
                $"--rpc-portal 127.0.0.1:{_rpcPort} port-forward add tcp 127.0.0.1:{localPort} {targetIp}:{targetPort}";
            cliProcess.Start();
            await cliProcess.WaitForExitAsync().ConfigureAwait(false);
            
            cliProcess.StartInfo.Arguments =
                $"--rpc-portal 127.0.0.1:{_rpcPort} port-forward add udp 127.0.0.1:{localPort} {targetIp}:{targetPort}";
            cliProcess.Start();
            await cliProcess.WaitForExitAsync().ConfigureAwait(false);
            
            cliProcess.StartInfo.Arguments =
                $"--rpc-portal 127.0.0.1:{_rpcPort} port-forward add tcp [::]:{localPort} {targetIp}:{targetPort}";
            cliProcess.Start();
            await cliProcess.WaitForExitAsync().ConfigureAwait(false);
            
            cliProcess.StartInfo.Arguments =
                $"--rpc-portal 127.0.0.1:{_rpcPort} port-forward add udp [::]:{localPort} {targetIp}:{targetPort}";
            cliProcess.Start();
            await cliProcess.WaitForExitAsync().ConfigureAwait(false);

            LogWrapper.Debug("ET Cli", await cliProcess.StandardOutput.ReadToEndAsync().ConfigureAwait(false) +
                                       await cliProcess.StandardError.ReadToEndAsync().ConfigureAwait(false));
        }
        catch (Exception e)
        {
            LogWrapper.Error(e, "ET Cli", "Failed to add port forward.");
        }
        return localPort;
    }

    private async Task _GetCliOutputDebugAsync()
    {
        while (State != EtState.Stopped && Config.Link.EnableCliOutput)
        {
            using var cliProcess = new Process();
            cliProcess.StartInfo = new ProcessStartInfo
            {
                FileName = $"{EasyTierMetadata.EasyTierFilePath}\\easytier-cli.exe",
                WorkingDirectory = EasyTierMetadata.EasyTierFilePath,
                Arguments = $"--rpc-portal 127.0.0.1:{_rpcPort} peer",
                ErrorDialog = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                StandardInputEncoding = Encoding.UTF8
            };

            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(5000));
        
            try
            {
                cliProcess.Start();
                cliProcess.StandardInput.Close();

                var stdOut = await cliProcess.StandardOutput.ReadToEndAsync(cts.Token).ConfigureAwait(false);
                var stdErr = await cliProcess.StandardError.ReadToEndAsync(cts.Token).ConfigureAwait(false);

                await cliProcess.WaitForExitAsync(cts.Token).ConfigureAwait(false);
                
                var output = stdOut + stdErr;
                
                LogWrapper.Info("EasyTier Cli Debug", "EasyTier Cli 抽样输出: \n" + output);
            }
            catch (Exception e)
            {
                LogWrapper.Error(e, "EasyTier Cli", "Failed to get EasyTier Cli info");
            }

            await Task.Delay(30000);
        }
    }
    
    /// <exception cref="ArgumentException">Thrown if host is duplicated.</exception>
    private async Task<EtPlayerList> _GetPlayersAsync()
    {
        using var cliProcess = new Process();
        cliProcess.StartInfo = new ProcessStartInfo
        {
            FileName = $"{EasyTierMetadata.EasyTierFilePath}\\easytier-cli.exe",
            WorkingDirectory = EasyTierMetadata.EasyTierFilePath,
            Arguments = $"--rpc-portal 127.0.0.1:{_rpcPort} -o json peer",
            ErrorDialog = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            StandardInputEncoding = Encoding.UTF8
        };
        cliProcess.EnableRaisingEvents = true;

        // 首次运行一个刚下载的 exe 时可能被杀软扫描，5 秒经常不够，放宽到 15 秒。
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        try
        {
            LogWrapper.Debug("Et Cli",
                $"Trying to get player info: \"{cliProcess.StartInfo.FileName}\" {cliProcess.StartInfo.Arguments}");

            cliProcess.Start();
            cliProcess.StandardInput.Close();

            var stdOut = await cliProcess.StandardOutput.ReadToEndAsync(cts.Token).ConfigureAwait(false);
            var stdErr = await cliProcess.StandardError.ReadToEndAsync(cts.Token).ConfigureAwait(false);

            await cliProcess.WaitForExitAsync(cts.Token).ConfigureAwait(false);

            var output = (stdOut + "\n" + stdErr).Trim();
            var exitCode = cliProcess.ExitCode;
            //LogWrapper.Debug("ET Cli", output);

            if (string.IsNullOrWhiteSpace(output))
            {
                LogWrapper.Warn("EasyTier",
                    $"EasyTier CLI 没有任何输出（退出码 {exitCode}），说明它还没能连上 EasyTier Core 的 RPC 端口 127.0.0.1:{_rpcPort}。");
                return new EtPlayerList(null, null);
            }

            var peerNodes = _ExtractPeerNodes(output);
            if (peerNodes is null)
            {
                LogWrapper.Warn("EasyTier",
                    $"EasyTier CLI 输出无法解析为玩家列表（退出码 {exitCode}），原始输出：{output}");
                return new EtPlayerList(null, null);
            }

            List<EasyPlayerInfo> players = [];
            EasyPlayerInfo? host = null;
            foreach (var peerNode in peerNodes)
            {
                LogWrapper.Debug("Et Cli", "Getting player info.");

                var info = _TryParsePeer(peerNode);
                if (info is null)
                {
                    LogWrapper.Debug("Et Cli", "Player info is null.");
                    continue;
                }

                if (info.IsHost)
                {
                    LogWrapper.Debug("Et Cli", $"Find host player: {info.HostName}");

                    if (host is not null)
                    {
                        LogWrapper.Debug("Et Cli", "Duplicated host player.");
                        throw new ArgumentException("Duplicated host.", nameof(host));
                    }

                    host = info;
                    continue;
                }

                LogWrapper.Debug("Et Cli", $"Find player: {info.HostName}");
                players.Add(info);
            }

            if (host is not null)
                LogWrapper.Info("EasyTier",
                    $"已通过 EasyTier CLI 获取房间成员：主机 {host.HostName}（{host.Ip}，延迟 {host.Ping} ms），房客 {players.Count} 人。");

            LogWrapper.Debug("Et Cli", "Return from GetPlayersAsync().");

            var result = host is null ? players : [host, .. players];

            return new EtPlayerList(result, host);
        }
        catch (OperationCanceledException tce)
        {
            // 超时（15 秒内没读完输出）不应该算是致命错误，返回空列表让上层继续重试即可。
            LogWrapper.Warn("EasyTier",
                $"读取 EasyTier CLI 输出超时（15 秒），本轮先跳过：{tce.Message}");
            return new EtPlayerList(null, null);
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogWrapper.Error(ex, "EasyTier", $"获取 EasyTier 玩家列表失败：{ex.Message}");
            return new EtPlayerList(null, null);
        }
    }

    /// <summary>
    ///     解析 EasyTier CLI 里的数值字段。
    ///     其输出可能是 <c>"-"</c>、<c>"12ms"</c>、<c>"1.5%"</c>、<c>"12.345"</c> 等形态，
    ///     因此统一抽取数字部分并用不变区域性解析，避免因格式差异把整张玩家表判定为解析失败。
    /// </summary>
    private static double _ParseMetric(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Trim() == "-") return 0;
        var match = System.Text.RegularExpressions.Regex.Match(raw, @"-?\d+(?:[\.,]\d+)?");
        if (!match.Success) return 0;
        var text = match.Value.Replace(',', '.');
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? Math.Round(value)
            : 0;
    }

    /// <summary>
    ///     从 EasyTier CLI 输出里取出节点列表。<br/>
    ///     兼容三种情况：直接输出数组、输出 <c>{ "peers": [...] }</c> 之类的包装对象、
    ///     以及 JSON 前后混入 EasyTier 日志行（此时截取第一段 JSON）。
    /// </summary>
    private static List<JsonNode?>? _ExtractPeerNodes(string output)
    {
        var text = output.Trim();
        foreach (var candidate in new[] { text, _SliceJsonText(text) })
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            try
            {
                var node = JsonCompat.ParseNode(candidate);
                switch (node)
                {
                    case JsonArray array:
                        return [..array];
                    case JsonObject obj:
                        foreach (var key in (string[]) ["peers", "peer_list", "peerList", "data", "items", "result"])
                            if (obj[key] is JsonArray inner)
                                return [..inner];
                        return [obj];
                    case null:
                        continue;
                    default:
                        continue;
                }
            }
            catch (Exception ex) when (ex is JsonException or ArgumentException or FormatException
                                           or InvalidOperationException)
            {
                LogWrapper.Debug("Et Cli", $"解析 EasyTier CLI 输出失败：{ex.Message}");
            }
        }

        return null;
    }

    /// <summary>
    ///     截取文本中第一段「看起来像 JSON」的内容，用于剔除混进来的日志行。
    /// </summary>
    private static string? _SliceJsonText(string text)
    {
        var start = text.IndexOfAny(['[', '{']);
        if (start < 0) return null;
        var endArray = text.LastIndexOf(']');
        var endObject = text.LastIndexOf('}');
        var end = Math.Max(endArray, endObject);
        return end > start ? text[start..(end + 1)] : null;
    }

    /// <summary>
    ///     把单个节点对象转成 <see cref="EasyPlayerInfo" />。<br/>
    ///     EasyTier 各版本、各语言环境下的字段名与数值格式都不完全一致，
    ///     因此这里对字段名做多别名匹配、对数值做宽松解析，绝不让单个字段差异导致整张玩家表解析失败。
    /// </summary>
    private static EasyPlayerInfo? _TryParsePeer(JsonNode? node)
    {
        if (node is not JsonObject obj)
        {
            LogWrapper.Debug("Et Cli", $"忽略无法识别的节点信息：{node?.ToJsonString() ?? "null"}");
            return null;
        }

        var hostname = _GetPeerField(obj, "hostname", "host_name", "name", "peer_name") ?? string.Empty;
        return new EasyPlayerInfo
        {
            IsHost = hostname.StartsWith("scaffolding-mc-server", StringComparison.Ordinal),
            HostName = hostname,
            Ip = _GetPeerField(obj, "ipv4", "ip", "virtual_ipv4", "virtual_ip") ?? string.Empty,
            Ping = _ParseMetric(_GetPeerField(obj, "lat_ms", "latency_ms", "latency", "ping", "rtt_ms")),
            Loss = _ParseMetric(_GetPeerField(obj, "loss_rate", "loss", "packet_loss_rate", "packet_loss")),
            NatType = _GetPeerField(obj, "nat_type", "nat", "natType") ?? string.Empty,
            EasyTierVer = _GetPeerField(obj, "version", "easytier_version", "et_version") ?? string.Empty
        };
    }

    private static string? _GetPeerField(JsonObject obj, params string[] names)
    {
        foreach (var name in names)
            if (obj.TryGetPropertyValue(name, out var value) && value is not null)
                return value is JsonValue jsonValue ? jsonValue.ToString() : value.ToJsonString();

        // 少数环境下的键名大小写不一致，再按忽略大小写匹配一次
        foreach (var (key, value) in obj)
        {
            if (value is null) continue;
            if (!names.Any(name => string.Equals(name, key, StringComparison.OrdinalIgnoreCase))) continue;
            return value is JsonValue jsonValue ? jsonValue.ToString() : value.ToJsonString();
        }

        return null;
    }

    #endregion
}

public record EtPlayerList(IReadOnlyList<EasyPlayerInfo>? Players, EasyPlayerInfo? Host);
