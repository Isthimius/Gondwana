using System.Reflection;
using System.Runtime.Loader;
using Avalonia.Controls;

namespace Gondwana.Studio.Extensibility;

/// <summary>
/// StudioPluginHost.
/// </summary>
public sealed class StudioPluginHost
{
    private readonly List<LoadedPlugin> _plugins = [];
    private readonly Action<string> _log;

    /// <summary>
    /// StudioPluginHost.
    /// </summary>
    /// <param name="log">log.</param>
    public StudioPluginHost(Action<string> log)
    {
        _log = log;
    }

    /// <summary>
    /// ToArray.
    /// </summary>
    /// <returns>The result.</returns>
    public IReadOnlyList<IStudioPlugin> Plugins => _plugins.Where(p => p.Enabled).Select(p => p.Instance).ToArray();

    /// <summary>
    /// DiscoverAndLoad.
    /// </summary>
    public void DiscoverAndLoad()
    {
        var baseDirectory = AppContext.BaseDirectory;
        var pluginDir = Path.Combine(baseDirectory, "plugins");

        if (!Directory.Exists(pluginDir))
        {
            _log($"[Plugin] Directory not found: {pluginDir}");
            return;
        }

        foreach (var dllPath in Directory.EnumerateFiles(pluginDir, "*.dll", SearchOption.TopDirectoryOnly))
            LoadAssemblyPlugins(dllPath);
    }

    /// <summary>
    /// GetPluginPanels.
    /// </summary>
    /// <returns>The result.</returns>
    public IEnumerable<(string pluginName, Control panel)> GetPluginPanels()
    {
        var panels = new List<(string pluginName, Control panel)>();
        foreach (var plugin in _plugins.Where(p => p.Enabled))
        {
            try
            {
                var panel = plugin.Instance.CreatePanel();
                if (panel is not null)
                    panels.Add((plugin.Instance.Name, panel));
            }
            catch (Exception ex)
            {
                DisablePlugin(plugin, $"CreatePanel threw: {ex.Message}");
            }
        }

        return panels;
    }

    /// <summary>
    /// GetPluginMenuItems.
    /// </summary>
    /// <returns>The result.</returns>
    public IEnumerable<MenuItem> GetPluginMenuItems()
    {
        var items = new List<MenuItem>();
        foreach (var plugin in _plugins.Where(p => p.Enabled))
        {
            try
            {
                var menu = plugin.Instance.CreateMenuItem();
                if (menu is not null)
                    items.Add(menu);
            }
            catch (Exception ex)
            {
                DisablePlugin(plugin, $"CreateMenuItem threw: {ex.Message}");
            }
        }

        return items;
    }

    /// <summary>
    /// NotifyProjectOpened.
    /// </summary>
    /// <param name="projectPath">projectPath.</param>
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

    /// <summary>
    /// NotifyProjectClosed.
    /// </summary>
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

        /// <summary>
        /// Load.
        /// </summary>
        /// <param name="assemblyName">assemblyName.</param>
        /// <returns>The result.</returns>
        protected override Assembly? Load(AssemblyName assemblyName)
        {
            var path = _resolver.ResolveAssemblyToPath(assemblyName);
            return path is not null ? LoadFromAssemblyPath(path) : null;
        }

        /// <summary>
        /// LoadUnmanagedDll.
        /// </summary>
        /// <param name="unmanagedDllName">unmanagedDllName.</param>
        /// <returns>The result.</returns>
        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            return path is not null ? LoadUnmanagedDllFromPath(path) : IntPtr.Zero;
        }
    }

    private void DisablePlugin(LoadedPlugin plugin, string reason)
    {
        plugin.Enabled = false;
        _log($"[Plugin] Disabled '{plugin.Instance.Name}': {reason}");
    }

    private sealed class LoadedPlugin
    {
        /// <summary>
        /// LoadedPlugin.
        /// </summary>
        /// <param name="instance">instance.</param>
        /// <param name="loadContext">loadContext.</param>
        /// <param name="sourcePath">sourcePath.</param>
        public LoadedPlugin(IStudioPlugin instance, AssemblyLoadContext loadContext, string sourcePath)
        {
            Instance = instance;
            LoadContext = loadContext;
            SourcePath = sourcePath;
        }

        /// <summary>
        /// Gets get.
        /// </summary>
        public IStudioPlugin Instance { get; }
        /// <summary>
        /// Gets get.
        /// </summary>
        public AssemblyLoadContext LoadContext { get; }
        /// <summary>
        /// Gets get.
        /// </summary>
        public string SourcePath { get; }
        /// <summary>
        /// Gets or sets true.
        /// </summary>
        public bool Enabled { get; set; } = true;
    }
}
