using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using PCL.PluginSdk;

namespace PCL.PluginHost;

/// <summary>
///     基于 JSON 的插件配置实现：<c>&lt;插件数据目录&gt;/config.json</c>。
/// </summary>
internal sealed class JsonPluginConfig : IPluginConfig, IPluginContextSnapshot
{
    private static readonly JsonSerializerOptions _writeOptions = new() { WriteIndented = true };

    private readonly string _filePath;
    private readonly JsonObject _root;
    private readonly object _lock = new();

    public JsonPluginConfig(string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);
        _filePath = Path.Combine(dataDirectory, "config.json");
        _root = LoadRoot(_filePath);
    }

    public event Action<string>? Changed;

    public T Get<T>(string key, T defaultValue)
    {
        lock (_lock)
        {
            if (!_root.TryGetPropertyValue(key, out var node) || node is null) return defaultValue;
            try
            {
                return node.Deserialize<T>() ?? defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }
    }

    public void Set<T>(string key, T? value)
    {
        lock (_lock) _root[key] = value is null ? null : JsonSerializer.SerializeToNode(value);
        Save();
        Changed?.Invoke(key);
    }

    public bool Remove(string key)
    {
        bool removed;
        lock (_lock) removed = _root.Remove(key);
        if (removed)
        {
            Save();
            Changed?.Invoke(key);
        }

        return removed;
    }

    public void Save()
    {
        try
        {
            string json;
            lock (_lock) json = _root.ToJsonString(_writeOptions);
            File.WriteAllText(_filePath, json);
        }
        catch
        {
            // 配置写失败不应影响宿主
        }
    }

    private static JsonObject LoadRoot(string path)
    {
        try
        {
            if (!File.Exists(path)) return new JsonObject();
            return JsonNode.Parse(File.ReadAllText(path)) as JsonObject ?? new JsonObject();
        }
        catch
        {
            return new JsonObject();
        }
    }
}
