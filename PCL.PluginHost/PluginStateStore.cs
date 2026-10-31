using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace PCL.PluginHost;

/// <summary>
///     记录哪些插件被用户禁用（<c>&lt;数据目录&gt;/plugins.state.json</c>）。
/// </summary>
public sealed class PluginStateStore
{
    private const string FileName = "plugins.state.json";

    private readonly string _filePath;
    private readonly HashSet<string> _disabled = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public PluginStateStore(string pluginDataDirectory)
    {
        Directory.CreateDirectory(pluginDataDirectory);
        _filePath = Path.Combine(pluginDataDirectory, FileName);
        Load();
    }

    public bool IsDisabled(string pluginId)
    {
        lock (_lock) return _disabled.Contains(pluginId);
    }

    public void SetDisabled(string pluginId, bool disabled)
    {
        lock (_lock)
        {
            if (disabled) _disabled.Add(pluginId);
            else _disabled.Remove(pluginId);
        }

        Save();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_filePath)) return;
            var node = JsonNode.Parse(File.ReadAllText(_filePath)) as JsonObject;
            if (node?["disabled"] is not JsonArray array) return;
            foreach (var item in array)
            {
                var id = item?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(id)) _disabled.Add(id);
            }
        }
        catch
        {
            // 状态文件损坏：当作空处理（不阻断启动）
        }
    }

    private void Save()
    {
        try
        {
            JsonArray array;
            lock (_lock) array = new JsonArray(_disabled.Select(id => (JsonNode?)JsonValue.Create(id)).ToArray());
            var root = new JsonObject { ["disabled"] = array };
            File.WriteAllText(_filePath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // 忽略写入失败
        }
    }
}
