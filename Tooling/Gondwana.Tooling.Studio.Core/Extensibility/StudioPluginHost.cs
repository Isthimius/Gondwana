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
    private IReadOnlyDictionary<string, Assembly>? _sharedAssemblies;
    private readonly Dictionary<string, List<PluginDiscoveryInfo>> _discovery = new(PluginPackagePaths.Comparer);
    private readonly HashSet<string> _implementations = new(StringComparer.Ordinal);

    /// <summary>Gets an immutable snapshot including invalid packages and activation failures.</summary>
    public IReadOnlyList<PluginDiscoveryInfo> Discovery =>
        Array.AsReadOnly(_discovery.Values.SelectMany(rows => rows).ToArray());

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

    /// <summary>Discovers immediate package directories, followed by legacy top-level DLLs.</summary>
    /// <param name="pluginDir">Root containing packages and optional legacy DLLs.</param>
    /// <remarks>
    /// Call on the UI thread. Sources are attempted once per host; restart after changing files.
    /// Packages precede legacy copies, with ordinal source ordering. All colliding explicit IDs
    /// are rejected before activation. A later collision disables the earlier instance without unloading it.
    /// </remarks>
    public void DiscoverAndLoad(string pluginDir)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginDir);
        pluginDir = Path.GetFullPath(pluginDir);
        try
        {
            if (!Directory.Exists(pluginDir))
            {
                _log($"[Plugin] Directory not found: {pluginDir}");
                return;
            }
            _sharedAssemblies ??= PluginLoadContext.GetSharedAssemblies(_sharedContracts);
            var candidates = new List<string>();
            foreach (var directory in Directory.GetDirectories(pluginDir).Order(StringComparer.Ordinal))
            {
                if (_discovery.ContainsKey(directory)) continue;
                var info = InspectPackage(directory);
                _discovery.Add(directory, [info]);
                candidates.Add(directory);
            }

            var conflicts = _discovery.SelectMany(pair => pair.Value.Select(info => (Source: pair.Key, Info: info)))
                .Where(row => !row.Info.IsLegacy && row.Info.Id is not null)
                .GroupBy(row => row.Info.Id, StringComparer.Ordinal)
                .Where(group => group.Select(row => row.Source).Distinct(PluginPackagePaths.Comparer).Count() > 1)
                .ToArray();
            foreach (var conflict in conflicts)
            {
                foreach (var source in conflict.Select(row => row.Source).Distinct(PluginPackagePaths.Comparer))
                {
                    _discovery[source] = _discovery[source].Select(info => info with
                    {
                        Status = PluginLoadStatus.Conflict,
                        Diagnostic = $"Duplicate plugin id '{conflict.Key}'. Remove or rename the conflicting package and restart Studio."
                    }).ToList();
                    foreach (var loaded in _plugins.Where(p => PluginPackagePaths.Comparer.Equals(p.SourcePath, _discovery[source][0].EntryAssembly)))
                        loaded.Enabled = false;
                    LogResult(_discovery[source][0]);
                }
            }
            foreach (var source in candidates)
            {
                var info = _discovery[source][0];
                if (info.Status == PluginLoadStatus.Loaded) LoadAssemblyPlugins(source, info);
                else LogResult(info);
            }
            foreach (var dll in Directory.GetFiles(pluginDir).Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)).Order(StringComparer.Ordinal))
            {
                if (_discovery.ContainsKey(dll)) continue;
                var info = new PluginDiscoveryInfo(null, null, null, pluginDir, dll, true,
                    PluginCompatibility.Unknown, PluginLoadStatus.Loaded, null);
                _discovery.Add(dll, [info]);
                LoadAssemblyPlugins(dll, info);
            }
        }
        catch (Exception ex)
        {
            var info = new PluginDiscoveryInfo(null, null, null, pluginDir, null, false,
                PluginCompatibility.Unknown, PluginLoadStatus.InvalidPackage, $"Cannot discover plugins: {ex.Message}");
            // Preserve successful child records if root enumeration subsequently fails.
            _discovery[pluginDir + Path.DirectorySeparatorChar] = [info];
            LogResult(info);
        }
    }

    private PluginDiscoveryInfo InspectPackage(string directory)
    {
        var info = new PluginDiscoveryInfo(null, null, null, directory, null, false,
            PluginCompatibility.Unknown, PluginLoadStatus.InvalidPackage, null);
        try
        {
            PluginPackagePaths.EnsureNoLinks(directory);
            PluginPackagePaths.EnsureNoLinks(Path.Combine(directory, "plugin.json"));
            var manifest = PluginManifest.Read(directory);
            info = info with { Id = manifest.Id, DisplayName = manifest.Name, Version = manifest.Version };
            PluginPackagePaths.ValidateTree(directory);
            manifest.Validate();
            var entry = Path.Combine(directory, manifest.EntryAssembly!);
            info = info with { EntryAssembly = entry };
            if (manifest.ContractVersion != PluginManifest.SupportedContractVersion)
                return info with
                {
                    Compatibility = PluginCompatibility.Incompatible,
                    Status = PluginLoadStatus.Incompatible,
                    Diagnostic = $"Contract {manifest.ContractVersion} is unsupported; this host supports contract {PluginManifest.SupportedContractVersion}. Obtain a compatible plugin."
                };
            info = info with { Compatibility = PluginCompatibility.Compatible };
            if (!File.Exists(entry)) throw new FileNotFoundException($"Entry assembly not found: {entry}");
            PluginLoadContext.ValidateAssemblies(directory, _sharedAssemblies!);
            return info with { Status = PluginLoadStatus.Loaded };
        }
        catch (Exception ex)
        {
            return info with { Diagnostic = $"Invalid package: {ex.Message}" };
        }
    }

    private void LogResult(PluginDiscoveryInfo info)
        => _log($"{(info.Status == PluginLoadStatus.Loaded ? "" : "Error: ")}[Plugin] {info.Status}: {info.Id ?? info.SourceDirectory}: {info.Diagnostic}");

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
        foreach (var source in _discovery.Keys.ToArray())
            _discovery[source] = _discovery[source].Select(info =>
                PluginPackagePaths.Comparer.Equals(info.EntryAssembly, plugin.SourcePath) && info.Implementation == plugin.Instance.GetType().FullName
                    ? info with { Status = PluginLoadStatus.Disabled, Diagnostic = reason } : info).ToList();
        var name = _discovery.Values.SelectMany(rows => rows)
            .FirstOrDefault(info => info.EntryAssembly == plugin.SourcePath && info.Implementation == plugin.Instance.GetType().FullName)?.DisplayName
            ?? plugin.Instance.GetType().FullName;
        _log($"Warning: [Plugin] Disabled '{name}': {reason}");
    }

    private void LoadAssemblyPlugins(string source, PluginDiscoveryInfo info)
    {
        var rows = new List<PluginDiscoveryInfo>();
        _discovery[source] = rows;
        var dllPath = info.EntryAssembly!;
        try
        {
            var loadContext = new PluginLoadContext(dllPath, _sharedAssemblies!, info.IsLegacy);
            var assembly = loadContext.LoadFromAssemblyPath(dllPath);
            var pluginTypes = assembly.GetTypes()
                .Where(t => !t.IsAbstract && !t.ContainsGenericParameters && typeof(IStudioPlugin).IsAssignableFrom(t))
                .OrderBy(t => t.FullName, StringComparer.Ordinal).ToArray();
            foreach (var type in pluginTypes)
            {
                var identity = $"{assembly.GetName().Name!.ToUpperInvariant()}:{Convert.ToHexString(assembly.GetName().GetPublicKeyToken() ?? [])}:{type.FullName}";
                var row = info with { Id = info.Id ?? $"legacy:{identity}", Implementation = type.FullName };
                if (!_implementations.Add(identity))
                {
                    row = row with { Status = PluginLoadStatus.Conflict, Diagnostic = $"Implementation '{identity}' was already discovered. Packages precede legacy DLLs; restart after removing redundant copies." };
                }
                else
                {
                    try
                    {
                        var plugin = (IStudioPlugin)Activator.CreateInstance(type)!;
                        var name = plugin.Name;
                        _plugins.Add(new LoadedPlugin(plugin, loadContext, dllPath));
                        row = row with { DisplayName = row.DisplayName ?? name };
                        _log($"[Plugin] Loaded '{name}' from {Path.GetFileName(dllPath)}");
                    }
                    catch (Exception ex)
                    {
                        row = row with
                        {
                            Status = PluginLoadStatus.FailedToLoad,
                            Diagnostic = $"Failed to instantiate {type.FullName}: {ex.GetBaseException().Message}"
                        };
                    }
                }
                rows.Add(row);
                if (row.Status != PluginLoadStatus.Loaded) LogResult(row);
            }
            if (rows.Count == 0 && !info.IsLegacy)
            {
                rows.Add(info with { Status = PluginLoadStatus.FailedToLoad, Diagnostic = "No concrete IStudioPlugin implementation found in entry assembly." });
                LogResult(rows[0]);
            }
        }
        catch (Exception ex)
        {
            var detail = ex is ReflectionTypeLoadException reflection
                ? string.Join("; ", reflection.LoaderExceptions.Select(error => error?.Message))
                : ex.GetBaseException().Message;
            rows.Add(info with
            {
                Status = PluginLoadStatus.FailedToLoad,
                Diagnostic = $"Failed loading {Path.GetFileName(dllPath)}: {detail}"
            });
            LogResult(rows[^1]);
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
