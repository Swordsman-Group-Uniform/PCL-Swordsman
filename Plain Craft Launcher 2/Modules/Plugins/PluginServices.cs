using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using PCL.Core.App.Localization;
using PCL.PluginHost;
using PCL.PluginSdk;

namespace PCL;

/// <summary>
///     <see cref="IMinecraftService" /> 的启动器实现：只读地读 PCL 自己的状态。
///     <para>
///         只从内存里已经加载好的实例表取数据（不主动触发扫描），插件拿到的是**快照**，
///         所以「用户在启动页看到什么」插件就看到什么；启动页还没加载过时列表可能为空。
///     </para>
/// </summary>
internal sealed class AppMinecraftService : IMinecraftService
{
    public PluginInstanceInfo? CurrentInstance
    {
        get
        {
            var instance = ModInstanceList.McMcInstanceSelected;
            return instance is null ? null : Convert(instance);
        }
    }

    public IReadOnlyList<PluginInstanceInfo> Instances
    {
        get
        {
            try
            {
                return ModInstanceList.mcInstanceList
                    .SelectMany(pair => pair.Value ?? [])
                    .Where(instance => instance is not null)
                    .Distinct()
                    .Select(Convert)
                    .ToList();
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "[Plugin] 读取实例列表失败", ModBase.LogLevel.Debug);
                return [];
            }
        }
    }

    public string GameDirectory => ModFolder.mcFolderSelected ?? "";

    private static PluginInstanceInfo Convert(McInstance instance)
    {
        var info = new PluginInstanceInfo
        {
            Name = instance.Name ?? "",
            Path = instance.PathInstance ?? ""
        };

        try
        {
            var versionInfo = instance.Info;
            info.VersionName = versionInfo?.VanillaName ?? "";

            // 加载器：取第一个命中的（Forge / NeoForge / Fabric / Quilt / Cleanroom）
            if (versionInfo is not null)
            {
                if (versionInfo.HasForge)
                {
                    info.LoaderName = "Forge";
                    info.LoaderVersion = versionInfo.Forge;
                }
                else if (versionInfo.HasNeoForge)
                {
                    info.LoaderName = "NeoForge";
                    info.LoaderVersion = versionInfo.NeoForge;
                }
                else if (versionInfo.HasFabric)
                {
                    info.LoaderName = "Fabric";
                    info.LoaderVersion = versionInfo.Fabric;
                }
                else if (versionInfo.HasQuilt)
                {
                    info.LoaderName = "Quilt";
                    info.LoaderVersion = versionInfo.Quilt;
                }
                else if (versionInfo.HasCleanroom)
                {
                    info.LoaderName = "Cleanroom";
                    info.LoaderVersion = versionInfo.Cleanroom;
                }
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, $"[Plugin] 读取实例 {info.Name} 的版本信息失败", ModBase.LogLevel.Debug);
        }

        // mod 数量：快速数 mods 目录里的 .jar（不解析依赖，插件要细节请自己读）
        try
        {
            var mods = Path.Combine(info.Path, "mods");
            if (Directory.Exists(mods))
                info.ModCount = Directory.EnumerateFiles(mods, "*.jar", SearchOption.TopDirectoryOnly).Count();
        }
        catch
        {
            // 数不出来就算了
        }

        return info;
    }
}

/// <summary>
///     插件授权的持久化：<c>PCL\PluginData\permissions.json</c>。
///     <code>
///     { "sgu.example": { "granted": ["net-http"], "denied": ["file-game-dir"] } }
///     </code>
///     </summary>
internal static class PluginPermissionStore
{
    private static readonly object Lock = new();
    private static JsonObject? _root;

    private static string FilePath => Path.Combine(PluginHostBootstrap.PluginDataDirectory, "permissions.json");

    private static JsonObject Root
    {
        get
        {
            lock (Lock)
            {
                if (_root is not null) return _root;
                try
                {
                    _root = File.Exists(FilePath)
                        ? JsonNode.Parse(File.ReadAllText(FilePath)) as JsonObject ?? new JsonObject()
                        : new JsonObject();
                }
                catch (Exception ex)
                {
                    ModBase.Log(ex, "[Plugin] 读取 permissions.json 失败，按空表处理", ModBase.LogLevel.Hint);
                    _root = new JsonObject();
                }

                return _root;
            }
        }
    }

    /// <summary>能力 → 清单里写的字符串（写进 json 更可读）。</summary>
    public static string ToText(PluginCapability capability) => capability switch
    {
        PluginCapability.NetHttp => "net-http",
        PluginCapability.FileGameDir => "file-game-dir",
        PluginCapability.FileUserPick => "file-user-pick",
        PluginCapability.MinecraftRead => "minecraft.read",
        _ => capability.ToString().ToLowerInvariant()
    };

    public static PluginCapability[] GatedCapabilities { get; } =
    [
        PluginCapability.MinecraftRead,
        PluginCapability.NetHttp,
        PluginCapability.FileGameDir,
        PluginCapability.FileUserPick
    ];

    public static bool IsDecided(string pluginId, PluginCapability capability) =>
        IsIn(pluginId, capability, "granted") || IsIn(pluginId, capability, "denied");

    public static bool IsGranted(string pluginId, PluginCapability capability) =>
        IsIn(pluginId, capability, "granted");

    public static bool IsDenied(string pluginId, PluginCapability capability) =>
        IsIn(pluginId, capability, "denied");

    public static void Decide(string pluginId, PluginCapability capability, bool granted)
    {
        lock (Lock)
        {
            var root = Root;
            if (root[pluginId] is not JsonObject entry)
            {
                entry = new JsonObject { ["granted"] = new JsonArray(), ["denied"] = new JsonArray() };
                root[pluginId] = entry;
            }

            var allowKey = granted ? "granted" : "denied";
            var denyKey = granted ? "denied" : "granted";
            AddToList(entry, allowKey, ToText(capability));
            RemoveFromList(entry, denyKey, ToText(capability));
            Save();
        }
    }

    /// <summary>撤回授权（管理页用）。</summary>
    public static void Revoke(string pluginId, PluginCapability capability)
    {
        lock (Lock)
        {
            if (Root[pluginId] is not JsonObject entry) return;
            RemoveFromList(entry, "granted", ToText(capability));
            RemoveFromList(entry, "denied", ToText(capability));
            Save();
        }
    }

    public static IReadOnlyList<PluginCapability> GrantedCapabilities(string pluginId) =>
        GatedCapabilities.Where(capability => IsGranted(pluginId, capability)).ToList();

    private static bool IsIn(string pluginId, PluginCapability capability, string listKey)
    {
        lock (Lock)
        {
            if (Root[pluginId] is not JsonObject entry) return false;
            if (entry[listKey] is not JsonArray array) return false;
            var text = ToText(capability);
            return array.Any(node => string.Equals(node?.GetValue<string>(), text, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static void AddToList(JsonObject entry, string listKey, string text)
    {
        if (entry[listKey] is not JsonArray array)
        {
            array = new JsonArray();
            entry[listKey] = array;
        }

        if (array.Any(node => string.Equals(node?.GetValue<string>(), text, StringComparison.OrdinalIgnoreCase)))
            return;
        array.Add(text);
    }

    private static void RemoveFromList(JsonObject entry, string listKey, string text)
    {
        if (entry[listKey] is not JsonArray array) return;
        var keep = array.Where(node =>
            !string.Equals(node?.GetValue<string>(), text, StringComparison.OrdinalIgnoreCase)).ToList();
        array.Clear();
        foreach (var node in keep) array.Add(node);
    }

    private static void Save()
    {
        try
        {
            Directory.CreateDirectory(PluginHostBootstrap.PluginDataDirectory);
            File.WriteAllText(FilePath, Root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "[Plugin] 写入 permissions.json 失败", ModBase.LogLevel.Hint);
        }
    }
}

/// <summary>
///     <see cref="IPluginPermissionHost" /> 的启动器实现：查表 + 首次使用时弹确认框。
/// </summary>
internal sealed class AppPluginPermissions : IPluginPermissionHost
{
    public bool IsGranted(string pluginId, PluginCapability capability) =>
        PluginPermissionStore.IsGranted(pluginId, capability);

    public bool IsDenied(string pluginId, PluginCapability capability) =>
        PluginPermissionStore.IsDenied(pluginId, capability);

    public bool RequestGrant(string pluginId, string pluginName, PluginCapability capability, string description)
    {
        // 弹窗必须回到 UI 线程，而调用方（插件）可能在后台线程：这里阻塞等 UI 线程给出结果
        var granted = false;
        // 必须用【阻塞】版本：RunInUi 是 InvokeAsync，后台线程会立刻读到 granted=false（权限永远拒绝）
        ModBase.RunInUiWait(() =>
        {
            try
            {
                var message = Lang.Text("Tools.Plugin.Permission.Message",
                    string.IsNullOrWhiteSpace(pluginName) ? pluginId : pluginName,
                    PluginChildText(capability), description);

                granted = ModMain.MyMsgBox(message, Lang.Text("Tools.Plugin.Permission.Title"),
                    button2: Lang.Text("Common.Action.Cancel")) == 1;

                PluginPermissionStore.Decide(pluginId, capability, granted);
                ModBase.Log($"[Plugin] {pluginId} 的 {PluginPermissionStore.ToText(capability)} 权限：" +
                            $"{(granted ? "已允许" : "已拒绝")}", ModBase.LogLevel.Normal);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, $"[Plugin] 申请 {pluginId} 的权限时出错", ModBase.LogLevel.Hint);
                granted = false;
            }
        });

        return granted;
    }

    /// <summary>能力的展示名（弹窗里用）。</summary>
    public static string PluginChildText(PluginCapability capability) => capability switch
    {
        PluginCapability.NetHttp => Lang.Text("Tools.Plugin.Permission.NetHttp"),
        PluginCapability.FileGameDir => Lang.Text("Tools.Plugin.Permission.FileGameDir"),
        PluginCapability.FileUserPick => Lang.Text("Tools.Plugin.Permission.FileUserPick"),
        PluginCapability.MinecraftRead => Lang.Text("Tools.Plugin.Permission.MinecraftRead"),
        _ => capability.ToString()
    };
}
