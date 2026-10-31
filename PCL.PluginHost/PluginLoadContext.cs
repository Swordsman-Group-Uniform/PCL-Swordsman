using System.Reflection;
using System.Runtime.Loader;

namespace PCL.PluginHost;

/// <summary>
///     每个插件一个可卸载的加载上下文。
///     <para>
///         契约程序集（<c>PCL.PluginSdk</c>）必须回落到默认 ALC，否则插件里的 <c>IPlugin</c>
///         与宿主看到的会是两个不同的类型，导致 InvalidCastException。
///     </para>
/// </summary>
internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private static readonly HashSet<string> _sharedAssemblies =
        new(StringComparer.OrdinalIgnoreCase) { "PCL.PluginSdk" };

    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginEntryPath, string pluginId)
        : base("plugin:" + pluginId, isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginEntryPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var name = assemblyName.Name;
        if (name is not null && _sharedAssemblies.Contains(name))
            return null; // 回落默认 ALC → 与宿主共享同一份契约

        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path is null ? null : LoadFromAssemblyPath(path);
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(path);
    }
}
