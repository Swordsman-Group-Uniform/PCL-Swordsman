using System.IO;
using PCL.PluginSdk;

namespace PCL.PluginHost;

/// <summary>
///     插件发现与加载。所有失败都被收敛成 <see cref="PluginDescriptor.State" /> + <see cref="PluginDescriptor.Error" />，
///     不会向宿主抛异常。
///     <para>
///         插件在磁盘上有两种形态：
///         <list type="number">
///             <item>
///                 包格式（推荐）：<c>PCL\Plugins\Foo.pclpkg</c>（启用）或
///                 <c>Foo.pclpkg.disabled</c>（禁用）。加载前解压到 <c>PCL\PluginCache\</c>，
///                 因此包文件不被占用，改后缀即可热开关。
///             </item>
///             <item>历史目录格式：<c>PCL\Plugins\Foo\plugin.json</c>，启用状态记录在 plugins.state.json。</item>
///         </list>
///     </para>
///     <para>
///         扫描分四个阶段，顺序不能乱：
///         <list type="number">
///             <item><b>发现</b>：只读清单（包格式还不急着解压），算好初始状态；</item>
///             <item><b>宿主版本</b>：<c>minHostVersion</c> 高于宿主 → <see cref="PluginState.Incompatible" />；</item>
///             <item><b>依赖</b>：缺依赖 / 依赖被禁用 / 依赖不可用 / 版本不满足 / 循环依赖 → <see cref="PluginState.Faulted" />；</item>
///             <item><b>加载</b>：按依赖拓扑序解压 + 反射创建，依赖先加载；加载前再确认一次依赖真的可用。</item>
///         </list>
///     </para>
/// </summary>
public sealed class PluginLoader
{
    private readonly IPluginHostEnvironment _environment;
    private readonly PluginStateStore _stateStore;
    private readonly List<PluginDescriptor> _plugins = [];
    private readonly object _lock = new();
    private readonly HashSet<string> _pendingDeletes = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _cacheDirectory;
    private readonly PluginEventBus _eventBus = new();

    /// <summary>熔断窗口：这段时间内失败达到 <see cref="FaultLimit" /> 次就自动禁用。</summary>
    private static readonly TimeSpan FaultWindow = TimeSpan.FromMinutes(5);

    /// <summary>熔断阈值。</summary>
    private const int FaultLimit = 3;

    /// <summary>共享级插件目录（可空）：实例级目录优先，同名插件只加载实例级那份。</summary>
    private readonly string? _sharedDirectory;

    public PluginLoader(IPluginHostEnvironment environment, string? cacheDirectory = null,
        string? sharedDirectory = null)
    {
        _sharedDirectory = string.IsNullOrWhiteSpace(sharedDirectory) ? null : sharedDirectory;
        _environment = environment;
        Directory.CreateDirectory(environment.PluginsDirectory);
        Directory.CreateDirectory(environment.PluginDataDirectory);
        _cacheDirectory = string.IsNullOrWhiteSpace(cacheDirectory)
            ? Path.Combine(environment.PluginDataDirectory, "Cache")
            : cacheDirectory;
        Directory.CreateDirectory(_cacheDirectory);
        _stateStore = new PluginStateStore(environment.PluginDataDirectory);
        _eventBus.PublishFailure = (pluginId, ex) =>
            _environment.Log($"[PluginHost] 插件 {pluginId} 的事件回调抛出异常", true, ex);
        RefreshPendingDeletes();
    }

    /// <summary>解压缓存目录（插件包在这里展开后再加载）。</summary>
    public string CacheDirectory => _cacheDirectory;

    /// <summary>宿主事件总线（宿主 publish、插件 subscribe）。</summary>
    public PluginEventBus Events => _eventBus;

    /// <summary>
    ///     已被标记「下次启动删除」的历史插件目录：本次运行内直接不加载、也不出现在列表里。
    ///     （包格式不需要它——包文件本身没被占用，可以立刻删掉。）
    /// </summary>
    private void RefreshPendingDeletes()
    {
        _pendingDeletes.Clear();
        foreach (var folder in PluginInstaller.GetPendingDeletes(_environment.PluginDataDirectory))
            _pendingDeletes.Add(folder);
    }

    public IReadOnlyList<PluginDescriptor> Plugins
    {
        get
        {
            lock (_lock) return _plugins.ToArray();
        }
    }

    /// <summary>卸载全部插件，重新扫描并加载。</summary>
    public void ReloadAll()
    {
        UnloadAll();
        RefreshPendingDeletes();

        var candidates = new List<PluginCandidate>();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // ① 发现：包格式（*.pclpkg / *.pclpkg.disabled）
        foreach (var file in EnumeratePackageFiles())
        {
            // 同一个插件可能残留多份副本（老版本按源文件名安装造成）：
            // 只加载第一份，其余只记日志，避免同一个插件被启用两次。
            var manifestId = PluginPackage.ReadManifest(file).Manifest?.Id;
            if (!string.IsNullOrWhiteSpace(manifestId) && !seenIds.Add(manifestId))
            {
                _environment.Log($"[PluginHost] 跳过重复的插件包：{Path.GetFileName(file)}（{manifestId} 已加载）");
                continue;
            }

            candidates.Add(DiscoverPackage(file));
        }

        // ① 发现：共享级目录（%APPDATA%\PCLCE\Plugins），实例级已有的插件以实例级为准
        if (_sharedDirectory is not null)
        {
            foreach (var file in EnumeratePackageFiles(_sharedDirectory))
            {
                var manifestId = PluginPackage.ReadManifest(file).Manifest?.Id;
                if (!string.IsNullOrWhiteSpace(manifestId) && !seenIds.Add(manifestId))
                {
                    _environment.Log($"[PluginHost] 共享级插件 {Path.GetFileName(file)} 与实例级重复，按实例级处理");
                    continue;
                }

                candidates.Add(DiscoverPackage(file));
            }

            foreach (var directory in EnumeratePluginDirectories(_sharedDirectory))
            {
                var folderName = Path.GetFileName(directory);
                if (_pendingDeletes.Contains(folderName)) continue;

                var manifestId = PluginManifestReader.Read(directory).Manifest?.Id;
                if (!string.IsNullOrWhiteSpace(manifestId) && !seenIds.Add(manifestId))
                {
                    _environment.Log($"[PluginHost] 共享级插件 {folderName} 与实例级重复，按实例级处理", false);
                    continue;
                }

                candidates.Add(DiscoverDirectory(directory));
            }
        }

        // ① 发现：历史目录格式 <Plugins>\<Name>\plugin.json
        foreach (var directory in EnumeratePluginDirectories())
        {
            var folderName = Path.GetFileName(directory);
            if (_pendingDeletes.Contains(folderName))
            {
                _environment.Log($"[PluginHost] 跳过待删除的插件目录：{folderName}");
                continue;
            }

            candidates.Add(DiscoverDirectory(directory));
        }

        // ②③ 宿主版本 + 依赖（只影响「本来想启用」的候选）
        ValidateHostVersion(candidates);
        ResolveDependencies(candidates);

        // ④ 按依赖拓扑序加载
        var keepCache = new List<string>();
        var loaded = new List<PluginDescriptor>();
        foreach (var candidate in OrderByDependencies(candidates, out _))
        {
            var descriptor = candidate.Descriptor;
            if (!candidate.PendingEnable)
            {
                loaded.Add(descriptor);
                continue;
            }

            // 依赖必须已经真的可用（已加载 / 已启用），否则连带停用
            var brokenDependency = FirstUnusableDependency(candidate, candidates);
            if (brokenDependency is not null)
            {
                descriptor.State = PluginState.Faulted;
                descriptor.Error = $"依赖插件 {brokenDependency} 不可用（未启用或加载失败）";
                _environment.Log($"[PluginHost] {descriptor.DisplayFileName}：依赖 {brokenDependency} 不可用，跳过加载", true);
                loaded.Add(descriptor);
                continue;
            }

            try
            {
                LoadCandidate(candidate, keepCache);
            }
            catch (Exception ex)
            {
                descriptor.State = PluginState.Faulted;
                descriptor.Error = ex.Message;
                _environment.Log($"[PluginHost] 加载 {descriptor.DisplayFileName} 失败", true, ex);
            }

            // 熔断：连续失败太多就自动禁用，避免每次启动都卡在同一个坏插件上
            if (descriptor.State == PluginState.Faulted && candidate.PendingEnable)
                ApplyCircuitBreaker(descriptor);

            loaded.Add(descriptor);
        }

        lock (_lock)
        {
            _plugins.Clear();
            _plugins.AddRange(loaded.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase));
        }

        // 清掉已经没有对应插件包的缓存目录（尽力而为）
        PluginPackage.CleanupCache(_cacheDirectory, keepCache);

        var usable = loaded.Count(p => p.IsUsable);
        _environment.Log($"[PluginHost] 扫描完成：{loaded.Count} 个插件（{loaded.Count(p => p.IsPackage)} 个包 / " +
                         $"{loaded.Count(p => !p.IsPackage)} 个目录），{usable} 个已启用");
        foreach (var plugin in loaded.Where(p => p.Error is not null))
            _environment.Log($"[PluginHost] {plugin.DisplayFileName}：{plugin.State} - {plugin.Error}", true);
    }

    /// <summary>
    ///     启用 / 禁用某个插件。
    ///     <para>
    ///         包格式：直接改文件名后缀（<c>.pclpkg</c> ↔ <c>.pclpkg.disabled</c>），立即生效于下次扫描；
    ///         历史目录格式：写入 plugins.state.json。
    ///     </para>
    /// </summary>
    public bool SetEnabled(string pluginId, bool enabled)
    {
        PluginDescriptor? descriptor;
        lock (_lock)
        {
            descriptor = _plugins.FirstOrDefault(p =>
                string.Equals(p.Id, pluginId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.FolderName, pluginId, StringComparison.OrdinalIgnoreCase));
        }

        // 包格式：以清单 id 找出该插件的**所有**包文件一起改名。
        // 老版本按源文件名安装，同一个插件可能留下多份不同文件名的副本；
        // 只改其中一份会让插件看起来「禁用失败、数量不变」，所以这里必须全改。
        var packages = PluginPackage.FindPackages(_environment.PluginsDirectory, pluginId).ToList();
        // 共享级目录（%APPDATA%\PCLCE\Plugins）里的包同样要能启停，否则管理页永远「找不到插件包」
        if (_sharedDirectory is not null)
            packages.AddRange(PluginPackage.FindPackages(_sharedDirectory, pluginId)
                .Where(path => !packages.Contains(path, StringComparer.OrdinalIgnoreCase)));
        if (descriptor is { IsPackage: true } || packages.Count > 0)
        {
            if (packages.Count == 0)
            {
                _environment.Log($"[PluginHost] 找不到插件 {pluginId} 的插件包，无法{(enabled ? "启用" : "禁用")}");
                return false;
            }

            var changed = 0;
            var failed = 0;
            string? lastError = null;
            foreach (var packagePath in packages)
            {
                if (PluginPackage.TrySetEnabled(packagePath, enabled, out var newPath, out var error))
                {
                    changed++;
                    _environment.Log(
                        $"[PluginHost] 插件 {pluginId}：{Path.GetFileName(packagePath)} → {Path.GetFileName(newPath)}");
                }
                else
                {
                    failed++;
                    lastError = error;
                }
            }

            _stateStore.SetDisabled(pluginId, false); // 包格式以文件名为准，清掉旧记录
            _environment.Log(
                $"[PluginHost] 插件 {pluginId} 已{(enabled ? "启用" : "禁用")}（{changed} 个包文件" +
                $"{(failed > 0 ? $"，{failed} 个失败：{lastError}" : "")}），重新扫描后生效");

            return changed > 0 || failed == 0;
        }

        _stateStore.SetDisabled(pluginId, !enabled);
        _environment.Log($"[PluginHost] 插件 {pluginId} 已{(enabled ? "启用" : "禁用")}，重新扫描后生效");
        return true;
    }

    public bool IsEnabledByUser(string pluginId)
    {
        PluginDescriptor? descriptor;
        lock (_lock)
        {
            descriptor = _plugins.FirstOrDefault(p => string.Equals(p.Id, pluginId, StringComparison.OrdinalIgnoreCase));
        }

        if (descriptor is not null) return descriptor.State != PluginState.Disabled;

        return !_stateStore.IsDisabled(pluginId);
    }

    #region 枚举

    private IEnumerable<string> EnumeratePackageFiles() => EnumeratePackageFiles(_environment.PluginsDirectory);

    private IEnumerable<string> EnumeratePackageFiles(string directory)
    {
        try
        {
            if (!Directory.Exists(directory)) return [];
            return Directory.EnumerateFiles(directory)
                .Where(PluginPackage.IsPackageFile)
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            _environment.Log("[PluginHost] 无法枚举插件包：" + _environment.PluginsDirectory, true, ex);
            return [];
        }
    }

    private IEnumerable<string> EnumeratePluginDirectories() =>
        EnumeratePluginDirectories(_environment.PluginsDirectory);

    private IEnumerable<string> EnumeratePluginDirectories(string directory)
    {
        try
        {
            if (!Directory.Exists(directory)) return [];
            return Directory.GetDirectories(directory)
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            _environment.Log("[PluginHost] 无法枚举插件目录：" + _environment.PluginsDirectory, true, ex);
            return [];
        }
    }

    #endregion

    #region ① 发现

    /// <summary>一个待处理的插件（发现阶段产出，加载阶段消耗）。</summary>
    private sealed class PluginCandidate
    {
        public required PluginDescriptor Descriptor { get; init; }

        /// <summary>包文件绝对路径；历史目录格式为 <see langword="null" />。</summary>
        public string? PackagePath { get; init; }

        /// <summary>是否需要先解压到缓存目录再加载（包格式）。</summary>
        public bool NeedsExtract { get; init; }

        /// <summary>是否准备启用；宿主版本 / 依赖校验失败会被置 false。</summary>
        public bool PendingEnable { get; set; }

        public IReadOnlyList<PluginDependency> Dependencies =>
            Descriptor.Manifest?.Dependencies ?? [];

        public string? ManifestId => Descriptor.Manifest?.Id;
    }

    private PluginCandidate DiscoverPackage(string packagePath)
    {
        var fileName = Path.GetFileName(packagePath);
        var descriptor = new PluginDescriptor
        {
            DirectoryPath = Path.GetDirectoryName(packagePath)!,
            FolderName = PluginPackage.GetBaseName(fileName),
            PackagePath = packagePath,
            PackageFileName = fileName
        };

        var result = PluginPackage.ReadManifest(packagePath);
        descriptor.Manifest = result.Manifest;
        descriptor.Capabilities = result.Capabilities;

        if (result.Manifest is null)
        {
            descriptor.State = PluginState.Faulted;
            descriptor.Error = result.Error;
            return new PluginCandidate { Descriptor = descriptor, PackagePath = packagePath };
        }

        // 文件名就是启用状态的唯一真相
        if (!PluginPackage.IsEnabledFileName(fileName))
        {
            descriptor.State = PluginState.Disabled;
            return new PluginCandidate { Descriptor = descriptor, PackagePath = packagePath };
        }

        if (_stateStore.IsDisabled(result.Manifest.Id))
        {
            // 旧版本把状态记在 plugins.state.json 里，包格式下以文件名为准，顺手清掉
            _stateStore.SetDisabled(result.Manifest.Id, false);
        }

        if (!result.IsCompatible)
        {
            descriptor.State = PluginState.Incompatible;
            descriptor.Error = result.Error;
            return new PluginCandidate { Descriptor = descriptor, PackagePath = packagePath };
        }

        if (result.Error is not null)
        {
            descriptor.State = PluginState.Faulted;
            descriptor.Error = result.Error;
            return new PluginCandidate { Descriptor = descriptor, PackagePath = packagePath };
        }

        // 入口程序集粗查：手工改坏的包会在这里就被指出，而不是等到 BadImageFormatException
        if (!PluginIntegrity.LooksLikeAssembly(packagePath, result.Manifest.Entry.Assembly))
        {
            descriptor.State = PluginState.Faulted;
            descriptor.Error = $"入口程序集不是有效的 .NET 程序集（{result.Manifest.Entry.Assembly}），包可能已损坏或不是编译产物";
            _environment.Log($"[PluginHost] {fileName}：{descriptor.Error}", true);
            return new PluginCandidate { Descriptor = descriptor, PackagePath = packagePath };
        }

        // 完整性与签名：声明了就必须过，过不了直接拒绝加载
        var integrity = PluginIntegrity.Verify(packagePath, result.Manifest);
        descriptor.Integrity = integrity.State;
        if (!integrity.CanLoad)
        {
            descriptor.State = PluginState.Faulted;
            descriptor.Error = integrity.Error;
            _environment.Log($"[PluginHost] {fileName}：完整性校验未通过 - {integrity.Error}", true);
            return new PluginCandidate { Descriptor = descriptor, PackagePath = packagePath };
        }

        return new PluginCandidate
        {
            Descriptor = descriptor,
            PackagePath = packagePath,
            NeedsExtract = true,
            PendingEnable = true
        };
    }

    private PluginCandidate DiscoverDirectory(string directory)
    {
        var descriptor = new PluginDescriptor
        {
            DirectoryPath = directory,
            FolderName = Path.GetFileName(directory)
        };

        var result = PluginManifestReader.Read(directory);
        descriptor.Manifest = result.Manifest;
        descriptor.Capabilities = result.Capabilities;

        if (result.Manifest is null)
        {
            descriptor.State = PluginState.Faulted;
            descriptor.Error = result.Error;
            return new PluginCandidate { Descriptor = descriptor };
        }

        if (!result.IsCompatible)
        {
            descriptor.State = PluginState.Incompatible;
            descriptor.Error = result.Error;
            return new PluginCandidate { Descriptor = descriptor };
        }

        if (result.Error is not null)
        {
            descriptor.State = PluginState.Faulted;
            descriptor.Error = result.Error;
            return new PluginCandidate { Descriptor = descriptor };
        }

        // 入口程序集粗查（目录格式同样）
        if (!PluginIntegrity.LooksLikeAssembly(directory, result.Manifest.Entry.Assembly))
        {
            descriptor.State = PluginState.Faulted;
            descriptor.Error = $"入口程序集不是有效的 .NET 程序集（{result.Manifest.Entry.Assembly}），插件目录可能不完整";
            _environment.Log($"[PluginHost] {Path.GetFileName(directory)}：{descriptor.Error}", true);
            return new PluginCandidate { Descriptor = descriptor };
        }

        // 完整性与签名（目录格式同样校验）
        var integrity = PluginIntegrity.Verify(directory, result.Manifest);
        descriptor.Integrity = integrity.State;
        if (!integrity.CanLoad)
        {
            descriptor.State = PluginState.Faulted;
            descriptor.Error = integrity.Error;
            _environment.Log($"[PluginHost] {Path.GetFileName(directory)}：完整性校验未通过 - {integrity.Error}", true);
            return new PluginCandidate { Descriptor = descriptor };
        }

        if (_stateStore.IsDisabled(descriptor.Manifest.Id) ||
            Path.GetFileName(directory).EndsWith(".disabled", StringComparison.OrdinalIgnoreCase))
        {
            descriptor.State = PluginState.Disabled;
            return new PluginCandidate { Descriptor = descriptor };
        }

        return new PluginCandidate { Descriptor = descriptor, PendingEnable = true };
    }

    #endregion

    #region ② 宿主版本

    private void ValidateHostVersion(IEnumerable<PluginCandidate> candidates)
    {
        var hostVersion = _environment.HostVersion;
        foreach (var candidate in candidates)
        {
            if (!candidate.PendingEnable) continue;

            var required = candidate.Descriptor.Manifest?.MinHostVersion;
            if (string.IsNullOrWhiteSpace(required)) continue;
            if (PluginVersion.IsAtLeast(hostVersion, required)) continue;

            candidate.PendingEnable = false;
            candidate.Descriptor.State = PluginState.Incompatible;
            candidate.Descriptor.Error = $"需要启动器 {required} 或更高（当前 {hostVersion}）";
            _environment.Log($"[PluginHost] {candidate.Descriptor.DisplayFileName}：{candidate.Descriptor.Error}", true);
        }
    }

    #endregion

    #region ③ 依赖

    /// <summary>
    ///     依赖校验：缺依赖 / 依赖被禁用 / 依赖不可用 / 版本不满足 / 循环依赖 → <see cref="PluginState.Faulted" />。
    /// </summary>
    private void ResolveDependencies(IReadOnlyList<PluginCandidate> candidates)
    {
        var byId = new Dictionary<string, PluginCandidate>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            var id = candidate.ManifestId;
            if (!string.IsNullOrWhiteSpace(id))
                byId.TryAdd(id, candidate);
        }

        foreach (var candidate in candidates)
        {
            if (!candidate.PendingEnable) continue;

            var manifest = candidate.Descriptor.Manifest;
            if (manifest is null) continue;

            foreach (var dependency in manifest.Dependencies)
            {
                var depId = dependency.Id?.Trim();
                if (string.IsNullOrWhiteSpace(depId)) continue;

                if (string.Equals(depId, manifest.Id, StringComparison.OrdinalIgnoreCase))
                {
                    Fail(candidate, "不能依赖自己");
                    break;
                }

                if (!byId.TryGetValue(depId, out var dependencyCandidate))
                {
                    Fail(candidate, $"缺少依赖插件 {depId}{DescribeRange(dependency.Version)}");
                    break;
                }

                switch (dependencyCandidate.Descriptor.State)
                {
                    case PluginState.Disabled:
                        Fail(candidate, $"依赖插件 {depId} 已被禁用");
                        continue;
                    case PluginState.Faulted:
                        Fail(candidate, $"依赖插件 {depId} 加载失败");
                        continue;
                    case PluginState.Incompatible:
                        Fail(candidate, $"依赖插件 {depId} 与宿主不兼容");
                        continue;
                }

                if (!PluginVersion.SatisfiesRange(dependency.Version,
                        dependencyCandidate.Descriptor.Version, out var versionError))
                {
                    Fail(candidate, $"依赖插件 {depId} 版本不满足：{versionError}");
                    break;
                }
            }
        }

        // 循环依赖：拓扑排序剩下的就是环里的人
        OrderByDependencies(candidates, out var cyclic, detectCycles: true);
        foreach (var id in cyclic)
        {
            if (!byId.TryGetValue(id, out var candidate) || !candidate.PendingEnable) continue;
            Fail(candidate, $"循环依赖：{string.Join(" → ", cyclic)}");
        }

        void Fail(PluginCandidate candidate, string reason)
        {
            if (!candidate.PendingEnable) return;
            candidate.PendingEnable = false;
            candidate.Descriptor.State = PluginState.Faulted;
            candidate.Descriptor.Error = reason;
            _environment.Log($"[PluginHost] {candidate.Descriptor.DisplayFileName}：{reason}", true);
        }
    }

    private static string DescribeRange(string? range) =>
        string.IsNullOrWhiteSpace(range) || range.Trim() == "*" ? "" : $"（需要 {range}）";

    /// <summary>依赖里第一个「尚未可用」的插件 id（用于加载阶段的连带停用）。</summary>
    private static string? FirstUnusableDependency(PluginCandidate candidate, IReadOnlyList<PluginCandidate> all)
    {
        foreach (var dependency in candidate.Dependencies)
        {
            var depId = dependency.Id?.Trim();
            if (string.IsNullOrWhiteSpace(depId)) continue;

            var match = all.FirstOrDefault(other =>
                string.Equals(other.ManifestId, depId, StringComparison.OrdinalIgnoreCase));
            if (match is null) return depId;
            if (!match.Descriptor.IsUsable) return depId;
        }

        return null;
    }

    /// <summary>
    ///     依赖拓扑序：依赖在前。同层按名称稳定排序；
    ///     <paramref name="detectCycles" /> 为真时把环里的成员 id 通过 <paramref name="cycles" /> 返回。
    /// </summary>
    private List<PluginCandidate> OrderByDependencies(IReadOnlyList<PluginCandidate> candidates,
        out List<string> cycles, bool detectCycles = false)
    {
        cycles = [];

        var byId = new Dictionary<string, PluginCandidate>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            var id = candidate.ManifestId;
            if (!string.IsNullOrWhiteSpace(id))
                byId.TryAdd(id, candidate);
        }

        var result = new List<PluginCandidate>(candidates.Count);
        var placed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = candidates
            .OrderBy(c => c.Descriptor.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var progressed = true;
        while (pending.Count > 0 && progressed)
        {
            progressed = false;
            foreach (var candidate in pending.ToList())
            {
                var ready = true;
                foreach (var dependency in candidate.Dependencies)
                {
                    var depId = dependency.Id?.Trim();
                    if (string.IsNullOrWhiteSpace(depId)) continue;
                    if (placed.Contains(depId)) continue;
                    if (!byId.TryGetValue(depId, out var dependencyCandidate)) continue;

                    // 依赖本身不打算加载（禁用 / 出错 / 缺依赖）：不阻塞排序，
                    // 由 FirstUnusableDependency 在加载阶段判断是否连带停用。
                    if (!dependencyCandidate.PendingEnable) continue;

                    ready = false;
                    break;
                }

                if (!ready) continue;

                pending.Remove(candidate);
                result.Add(candidate);
                var ownId = candidate.ManifestId;
                if (!string.IsNullOrWhiteSpace(ownId)) placed.Add(ownId);
                progressed = true;
            }
        }

        if (detectCycles)
            cycles.AddRange(pending
                .Select(c => c.ManifestId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id!));

        // 环里的（或依赖了「压根不存在」的插件）最后追加，保证都出现在列表里
        result.AddRange(pending);
        return result;
    }

    #endregion

    /// <summary>
    ///     熔断：同一个插件在 <see cref="FaultWindow" /> 内失败达到 <see cref="FaultLimit" /> 次，
    ///     就把它的包文件改成 <c>.pclpkg.disabled</c>（包格式）并记日志，下次启动不再尝试。
    ///     失败记录落在 <c>PCL\PluginData\PluginFaults.json</c>，用户可以手动删掉该文件复位。
    /// </summary>
    private void ApplyCircuitBreaker(PluginDescriptor descriptor)
    {
        try
        {
            var path = Path.Combine(_environment.PluginDataDirectory, "PluginFaults.json");
            var now = DateTimeOffset.UtcNow;

            System.Text.Json.Nodes.JsonObject root;
            try
            {
                root = File.Exists(path)
                    ? System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path)) as System.Text.Json.Nodes.JsonObject
                      ?? new System.Text.Json.Nodes.JsonObject()
                    : new System.Text.Json.Nodes.JsonObject();
            }
            catch
            {
                root = new System.Text.Json.Nodes.JsonObject();
            }

            var timestamps = (root[descriptor.Id] as System.Text.Json.Nodes.JsonArray)?.ToList()
                             ?? [];
            var recent = timestamps
                .Select(node => node?.GetValue<string>())
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Select(text => DateTimeOffset.TryParse(text, out var time) ? time : DateTimeOffset.MinValue)
                .Where(time => now - time <= FaultWindow)
                .ToList();
            recent.Add(now);

            var array = new System.Text.Json.Nodes.JsonArray();
            foreach (var time in recent) array.Add(time.ToString("O"));
            root[descriptor.Id] = array;

            try
            {
                File.WriteAllText(path, root.ToJsonString());
            }
            catch
            {
                // 记录失败就算了，不影响本次判定
            }

            if (recent.Count < FaultLimit) return;

            descriptor.Error = $"{descriptor.Error}（{FaultWindow.TotalMinutes:F0} 分钟内失败 {recent.Count} 次，已自动禁用）";
            _environment.Log($"[PluginHost] {descriptor.DisplayFileName} 连续失败 {recent.Count} 次，已自动禁用", true);

            string? disableError = null;
            if (descriptor.PackagePath is not null &&
                PluginPackage.TrySetEnabled(descriptor.PackagePath, false, out _, out disableError))
            {
                _environment.Log($"[PluginHost] 已把 {descriptor.DisplayFileName} 改名为禁用状态");
            }
            else if (!string.IsNullOrWhiteSpace(disableError))
            {
                _environment.Log($"[PluginHost] 自动禁用 {descriptor.DisplayFileName} 失败：{disableError}", true);
            }
        }
        catch (Exception ex)
        {
            _environment.Log("[PluginHost] 熔断处理失败", true, ex);
        }
    }

    #region ④ 加载

    private void LoadCandidate(PluginCandidate candidate, ICollection<string> keepCache)
    {
        var descriptor = candidate.Descriptor;

        // 包格式：解压到缓存目录再加载（包文件本身保持空闲，随时可以改名或删除）
        if (candidate.NeedsExtract && candidate.PackagePath is not null)
        {
            var cacheDirectory = PluginPackage.ExtractToCache(candidate.PackagePath, _cacheDirectory);
            keepCache.Add(Path.GetFileName(cacheDirectory));
            descriptor.DirectoryPath = cacheDirectory;

            var extracted = PluginManifestReader.Read(cacheDirectory);
            if (!extracted.IsCompatible)
            {
                descriptor.State = extracted.Manifest is null ? PluginState.Faulted : PluginState.Incompatible;
                descriptor.Error = extracted.Error;
                return;
            }

            try
            {
                LoadPlugin(descriptor);
                return;
            }
            catch (BadImageFormatException)
            {
                // 解压缓存可能损坏：删掉缓存重解压再试一次（一次性自愈）
                _environment.Log($"[PluginHost] {descriptor.DisplayFileName}：程序集格式无效，正在重建解压缓存后重试", true);
                PluginPackage.TryDeleteCacheDirectory(descriptor.DirectoryPath);
                var rebuilt = PluginPackage.ExtractToCache(candidate.PackagePath, _cacheDirectory);
                keepCache.Add(Path.GetFileName(rebuilt));
                descriptor.DirectoryPath = rebuilt;

                try
                {
                    LoadPlugin(descriptor);
                }
                catch (BadImageFormatException ex)
                {
                    descriptor.State = PluginState.Faulted;
                    descriptor.Error = "入口程序集不是有效的 .NET 程序集（BadImageFormat）：" + ex.Message;
                    _environment.Log($"[PluginHost] {descriptor.DisplayFileName}：{descriptor.Error}", true);
                }

                return;
            }
        }

        LoadPlugin(descriptor);
    }

    private void LoadPlugin(PluginDescriptor descriptor)
    {
        var manifest = descriptor.Manifest!;
        var entryPath = Path.Combine(descriptor.DirectoryPath, manifest.Entry.Assembly);
        PluginLoadContext? loadContext = null;
        IPlugin? instance = null;

        try
        {
            loadContext = new PluginLoadContext(entryPath, manifest.Id);
            var assembly = loadContext.LoadFromAssemblyPath(entryPath);

            var type = assembly.GetType(manifest.Entry.Type, throwOnError: false);
            if (type is null)
                throw new PluginException($"找不到入口类型：{manifest.Entry.Type}");
            if (!typeof(IPlugin).IsAssignableFrom(type))
                throw new PluginException($"入口类型未实现 IPlugin：{manifest.Entry.Type}");

            instance = Activator.CreateInstance(type) as IPlugin
                       ?? throw new PluginException($"无法创建插件实例：{manifest.Entry.Type}");

            var dataDirectory = Path.Combine(_environment.PluginDataDirectory, PluginPackage.Sanitize(manifest.Id));
            var pluginContext = new PluginContext(descriptor, _environment, dataDirectory, _eventBus);

            instance.Initialize(pluginContext);

            descriptor.LoadContext = loadContext;
            descriptor.Instance = instance;
            descriptor.ContextSnapshot = pluginContext;
            descriptor.HostContext = pluginContext;
            descriptor.State = PluginState.Loaded;

            instance.Enable();
            descriptor.State = PluginState.Enabled;

            _environment.Log(
                $"[PluginHost] 已加载 {manifest.Id} {manifest.Version}（API {manifest.ApiVersion}，" +
                $"能力 {descriptor.Capabilities}" +
                $"{(manifest.Dependencies.Count > 0 ? $"依赖 {string.Join(",", manifest.Dependencies.Select(d => d.Id))}" : "")}）");
        }
        catch (Exception ex)
        {
            descriptor.State = PluginState.Faulted;
            descriptor.Error = ex.Message;

            // 回滚：尽量把已经初始化了一半的实例清理掉
            try
            {
                instance?.Disable();
            }
            catch
            {
                // ignored
            }

            try
            {
                instance?.Dispose();
            }
            catch
            {
                // ignored
            }

            try
            {
                loadContext?.Unload();
            }
            catch
            {
                // ignored
            }

            descriptor.Instance = null;
            descriptor.LoadContext = null;
            descriptor.HostContext = null;
            _environment.Log($"[PluginHost] 加载 {manifest.Id} 失败", true, ex);
        }
    }

    private void UnloadAll()
    {
        List<PluginDescriptor> snapshot;
        lock (_lock)
        {
            snapshot = _plugins.ToList();
            _plugins.Clear();
        }

        // 插件即将卸载：它们的订阅一并作废
        _eventBus.Clear();

        foreach (var descriptor in snapshot)
        {
            try
            {
                descriptor.Instance?.Disable();
            }
            catch (Exception ex)
            {
                _environment.Log($"[PluginHost] {descriptor.Id} Disable 失败", true, ex);
            }

            try
            {
                descriptor.Instance?.Dispose();
            }
            catch (Exception ex)
            {
                _environment.Log($"[PluginHost] {descriptor.Id} Dispose 失败", true, ex);
            }

            descriptor.ContextSnapshot?.Save();

            try
            {
                descriptor.LoadContext?.Unload();
            }
            catch
            {
                // ignored
            }

            descriptor.Instance = null;
            descriptor.LoadContext = null;
            descriptor.ContextSnapshot = null;
            descriptor.HostContext = null;
        }
    }

    #endregion
}
