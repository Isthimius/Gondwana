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
    private readonly Assembly[] _sharedContracts;

    /// <summary>Creates a plugin host that shares the framework-neutral plugin contract assembly.</summary>
    /// <param name="log">Callback receiving discovery messages and plugin failures on the calling thread.</param>
    public StudioPluginHost(Action<string> log) : this(log, []) { }

    /// <summary>Creates a host sharing the core contract and the supplied platform contracts.</summary>
    /// <param name="log">Callback receiving discovery messages and plugin failures on the calling thread.</param>
    /// <param name="sharedContracts">Host assemblies to reuse by simple name instead of loading private plugin copies.</param>
    public StudioPluginHost(Action<string> log, params Assembly[] sharedContracts)
    {
        _log = log;
        _sharedContracts = [typeof(IStudioPlugin).Assembly, .. sharedContracts];
    }

    /// <summary>Gets a new snapshot of enabled plugin instances in discovery order.</summary>
    public IReadOnlyList<IStudioPlugin> Plugins =>
        _plugins.Where(p => p.Enabled).Select(p => p.Instance).ToArray();

    /// <summary>Attaches services to enabled plugins implementing <see cref="IStudioPluginHostServicesAware"/>.</summary>
    /// <param name="services">Borrowed host services supplied to each participating plugin.</param>
    /// <remarks>Call on the UI thread. A plugin that throws is disabled and its failure is logged.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="services"/> is <see langword="null"/>.</exception>
    public void AttachHostServices(IStudioPluginHostServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        foreach (var plugin in _plugins.Where(p => p.Enabled))
        {
            if (plugin.Instance is not IStudioPluginHostServicesAware aware) continue;
            try { aware.AttachHostServices(services); }
            catch (Exception ex) { DisablePlugin(plugin, $"AttachHostServices threw: {ex.Message}"); }
        }
    }

    /// <summary>Discovers plugins in the <c>plugins</c> directory beneath the application base directory.</summary>
    /// <remarks>Uses the same additive discovery and failure handling as <see cref="DiscoverAndLoad(string)"/>.</remarks>
    public void DiscoverAndLoad()
        => DiscoverAndLoad(Path.Combine(AppContext.BaseDirectory, "plugins"));

    /// <summary>Loads DLLs directly in a directory and instantiates their concrete plugin types with parameterless constructors.</summary>
    /// <param name="pluginDir">Plugin directory; relative paths are resolved against the process's current directory.</param>
    /// <remarks>
    /// Missing directories and assembly or constructor failures are logged. Path normalization and directory enumeration
    /// errors propagate. Repeated calls append instances without deduplication; existing plugins are not unloaded.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="pluginDir"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="pluginDir"/> is empty or is not a valid path.</exception>
    public void DiscoverAndLoad(string pluginDir)
    {
        pluginDir = Path.GetFullPath(pluginDir);

        if (!Directory.Exists(pluginDir))
        {
            _log($"[Plugin] Directory not found: {pluginDir}");
            return;
        }

        foreach (var dllPath in Directory.EnumerateFiles(pluginDir, "*.dll", SearchOption.TopDirectoryOnly))
            LoadAssemblyPlugins(dllPath);
    }

    /// <summary>Notifies enabled plugins of the selected working directory, disabling and logging plugins that throw.</summary>
    /// <param name="projectPath">The authoring directory path to pass unchanged to each plugin.</param>
    /// <remarks>Studio invokes this on its UI thread. This method does not send a preceding close notification.</remarks>
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

    /// <summary>Notifies enabled plugins that the working directory is closing, disabling and logging plugins that throw.</summary>
    /// <remarks>Studio invokes this on its UI thread; plugins remain loaded after the notification.</remarks>
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

    /// <summary>Filters enabled plugins by a platform-specific or other plugin contract.</summary>
    /// <typeparam name="TPlugin">The reference type that returned plugin instances must implement or derive from.</typeparam>
    /// <returns>A deferred sequence of matching instances in discovery order, evaluated when enumerated.</returns>
    protected IEnumerable<TPlugin> GetPluginsAs<TPlugin>() where TPlugin : class
        => _plugins.Where(p => p.Enabled && p.Instance is TPlugin)
                   .Select(p => (TPlugin)p.Instance);

    /// <summary>Writes a plugin-related message to the configured log sink.</summary>
    /// <param name="message">The complete diagnostic text to pass to the callback.</param>
    protected void Log(string message) => _log(message);

    /// <summary>Excludes a plugin from future enabled-plugin queries and logs the reason without unloading or disposing it.</summary>
    /// <param name="plugin">The loaded-plugin record to mark disabled.</param>
    /// <param name="reason">Failure details included in the log message alongside the plugin name.</param>
    protected void DisablePlugin(LoadedPlugin plugin, string reason)
    {
        plugin.Enabled = false;
        _log($"Warning: [Plugin] Disabled '{plugin.Instance.Name}': {reason}");
    }

    private void LoadAssemblyPlugins(string dllPath)
    {
        try
        {
            var loadContext = new PluginLoadContext(dllPath, _sharedContracts);
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
                    _log($"Error: [Plugin] Failed to instantiate {type.FullName}: {ex.Message}");
                }
            }
        }
        catch (ReflectionTypeLoadException ex)
        {
            _log($"Error: [Plugin] Failed loading {Path.GetFileName(dllPath)}: {ex.Message}");
            foreach (var loaderEx in ex.LoaderExceptions)
                _log($"Error: [Plugin] Loader error: {loaderEx?.Message}");
        }
        catch (Exception ex)
        {
            _log($"Error: [Plugin] Failed loading {Path.GetFileName(dllPath)}: {ex.Message}");
        }
    }

    private sealed class PluginLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;
        private readonly Assembly[] _sharedContracts;

        /// <summary>Creates an isolated load context that resolves dependencies beside a plugin assembly.</summary>
        /// <param name="dllPath">The full path to the plugin's main assembly.</param>
        /// <param name="sharedContracts">Contract assemblies to reuse from the host.</param>
        public PluginLoadContext(string dllPath, Assembly[] sharedContracts)
            : base(name: $"studio-plugin:{Path.GetFileNameWithoutExtension(dllPath)}", isCollectible: true)
        {
            _resolver = new AssemblyDependencyResolver(dllPath);
            _sharedContracts = sharedContracts;
        }

        /// <summary>Reuses a host contract by simple name or loads a managed dependency resolved for this plugin.</summary>
        /// <param name="assemblyName">The requested dependency identity.</param>
        /// <returns>The shared or loaded assembly, or <see langword="null"/> to allow normal fallback resolution.</returns>
        protected override Assembly? Load(AssemblyName assemblyName)
        {
            // Contracts must have host identity even when a plugin ships private copies.
            var shared = _sharedContracts.FirstOrDefault(assembly =>
                string.Equals(assembly.GetName().Name, assemblyName.Name, StringComparison.OrdinalIgnoreCase));
            if (shared is not null) return shared;
            var path = _resolver.ResolveAssemblyToPath(assemblyName);
            return path is not null ? LoadFromAssemblyPath(path) : null;
        }

        /// <summary>Loads a native dependency from the path resolved for this plugin.</summary>
        /// <param name="unmanagedDllName">The native library name requested by the plugin.</param>
        /// <returns>The loaded library handle, or zero to allow normal fallback resolution.</returns>
        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            return path is not null ? LoadUnmanagedDllFromPath(path) : IntPtr.Zero;
        }
    }

    /// <summary>Tracks a loaded plugin with its isolation context.</summary>
    protected sealed class LoadedPlugin
    {
        /// <summary>Records a newly enabled plugin and retains its load context and source assembly path.</summary>
        /// <param name="instance">The constructed plugin instance.</param>
        /// <param name="loadContext">The assembly load context that owns the plugin's code.</param>
        /// <param name="sourcePath">The path of the DLL from which the plugin was discovered.</param>
        public LoadedPlugin(IStudioPlugin instance, AssemblyLoadContext loadContext, string sourcePath)
        {
            Instance = instance;
            LoadContext = loadContext;
            SourcePath = sourcePath;
        }

        /// <summary>Gets the plugin instance created during discovery.</summary>
        public IStudioPlugin Instance { get; }
        /// <summary>Gets the retained load context for the plugin assembly and its dependencies.</summary>
        public AssemblyLoadContext LoadContext { get; }
        /// <summary>Gets the path of the DLL from which the plugin was discovered.</summary>
        public string SourcePath { get; }
        /// <summary>Gets or sets whether the plugin participates in host queries and lifecycle notifications; initially true.</summary>
        public bool Enabled { get; set; } = true;
    }
}
