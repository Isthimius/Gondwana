using System.Reflection;
using System.Runtime.Loader;

namespace Gondwana.Tooling.Studio.Core.Extensibility;

/// <summary>
/// Discovers and manages studio plugins from assemblies in a <c>plugins/</c>
/// sub-directory of the application base directory.
/// Platform-specific subclasses extend this class to provide UI panel and
/// menu-item contributions.
/// </summary>
public class StudioPluginHost
{
    private readonly List<LoadedPlugin> _plugins = [];
    private readonly Action<string> _log;

    /// <summary>
    /// StudioPluginHost.
    /// </summary>
    /// <param name="log">Logging callback.</param>
    public StudioPluginHost(Action<string> log)
    {
        _log = log;
    }

    /// <summary>Gets the currently enabled plugins.</summary>
    public IReadOnlyList<IStudioPlugin> Plugins =>
        _plugins.Where(p => p.Enabled).Select(p => p.Instance).ToArray();

    /// <summary>Scans the <c>plugins/</c> directory and loads all valid assemblies.</summary>
    public void DiscoverAndLoad()
    {
        var pluginDir = Path.Combine(AppContext.BaseDirectory, "plugins");

        if (!Directory.Exists(pluginDir))
        {
            _log($"[Plugin] Directory not found: {pluginDir}");
            return;
        }

        foreach (var dllPath in Directory.EnumerateFiles(pluginDir, "*.dll", SearchOption.TopDirectoryOnly))
            LoadAssemblyPlugins(dllPath);
    }

    /// <summary>Notifies all enabled plugins that a project was opened.</summary>
    public void NotifyProjectOpened(string projectPath)
    {
        foreach (var plugin in _plugins.Where(p => p.Enabled))
        {
            try
            {
                plugin.Instance.OnProjectOpened(projectPath);
            }
            catch (Exception ex)
            {
                DisablePlugin(plugin, $"OnProjectOpened threw: {ex.Message}");
            }
        }
    }

    /// <summary>Notifies all enabled plugins that the current project was closed.</summary>
    public void NotifyProjectClosed()
    {
        foreach (var plugin in _plugins.Where(p => p.Enabled))
        {
            try
            {
                plugin.Instance.OnProjectClosed();
            }
            catch (Exception ex)
            {
                DisablePlugin(plugin, $"OnProjectClosed threw: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Returns a filtered, safely cast enumerable of enabled plugins that implement
    /// <typeparamref name="TPlugin"/>. Used by platform-specific subclasses to retrieve
    /// plugins that provide UI contributions.
    /// </summary>
    protected IEnumerable<TPlugin> GetPluginsAs<TPlugin>() where TPlugin : class
        => _plugins.Where(p => p.Enabled && p.Instance is TPlugin)
                   .Select(p => (TPlugin)p.Instance);

    /// <summary>Writes a plugin-related message to the configured log sink.</summary>
    protected void Log(string message) => _log(message);

    /// <summary>Disables a plugin that threw during a lifecycle call.</summary>
    protected void DisablePlugin(LoadedPlugin plugin, string reason)
    {
        plugin.Enabled = false;
        _log($"[Plugin] Disabled '{plugin.Instance.Name}': {reason}");
    }

    private void LoadAssemblyPlugins(string dllPath)
    {
        try
        {
            var loadContext = new PluginLoadContext(dllPath);
            var assembly = loadContext.LoadFromAssemblyPath(dllPath);
            var pluginTypes = assembly.GetTypes()
                .Where(t => !t.IsAbstract && typeof(IStudioPlugin).IsAssignableFrom(t))
                .ToArray();

            foreach (var type in pluginTypes)
            {
                try
                {
                    if (Activator.CreateInstance(type) is IStudioPlugin plugin)
                    {
                        _plugins.Add(new LoadedPlugin(plugin, loadContext, dllPath));
                        _log($"[Plugin] Loaded '{plugin.Name}' from {Path.GetFileName(dllPath)}");
                    }
                }
                catch (Exception ex)
                {
                    _log($"[Plugin] Failed to instantiate {type.FullName}: {ex.Message}");
                }
            }
        }
        catch (ReflectionTypeLoadException ex)
        {
            _log($"[Plugin] Failed loading {Path.GetFileName(dllPath)}: {ex.Message}");
            foreach (var loaderEx in ex.LoaderExceptions)
                _log($"[Plugin] Loader error: {loaderEx?.Message}");
        }
        catch (Exception ex)
        {
            _log($"[Plugin] Failed loading {Path.GetFileName(dllPath)}: {ex.Message}");
        }
    }

    private sealed class PluginLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;

        /// <summary>
        /// PluginLoadContext.
        /// </summary>
        /// <param name="dllPath">dllPath.</param>
        public PluginLoadContext(string dllPath)
            : base(name: $"studio-plugin:{Path.GetFileNameWithoutExtension(dllPath)}", isCollectible: true)
        {
            _resolver = new AssemblyDependencyResolver(dllPath);
        }

        /// <summary>Load.</summary>
        protected override Assembly? Load(AssemblyName assemblyName)
        {
            var path = _resolver.ResolveAssemblyToPath(assemblyName);
            return path is not null ? LoadFromAssemblyPath(path) : null;
        }

        /// <summary>LoadUnmanagedDll.</summary>
        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            return path is not null ? LoadUnmanagedDllFromPath(path) : IntPtr.Zero;
        }
    }

    /// <summary>Tracks a loaded plugin with its isolation context.</summary>
    protected sealed class LoadedPlugin
    {
        /// <summary>LoadedPlugin.</summary>
        public LoadedPlugin(IStudioPlugin instance, AssemblyLoadContext loadContext, string sourcePath)
        {
            Instance = instance;
            LoadContext = loadContext;
            SourcePath = sourcePath;
        }

        /// <summary>Gets get.</summary>
        public IStudioPlugin Instance { get; }
        /// <summary>Gets get.</summary>
        public AssemblyLoadContext LoadContext { get; }
        /// <summary>Gets get.</summary>
        public string SourcePath { get; }
        /// <summary>Gets or sets true.</summary>
        public bool Enabled { get; set; } = true;
    }
}
