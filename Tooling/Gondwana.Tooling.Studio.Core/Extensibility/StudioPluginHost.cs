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
    private readonly List<DiscoveryEntry> _discovery = [];
    private readonly HashSet<string> _scannedPaths = new(
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    private readonly HashSet<string> _packageIds = new(StringComparer.Ordinal);
    private readonly HashSet<string> _implementations = new(StringComparer.OrdinalIgnoreCase);

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

    /// <summary>Gets a read-only snapshot of package and legacy discovery results.</summary>
    public IReadOnlyList<StudioPluginDiscovery> DiscoveryResults =>
        Array.AsReadOnly(_discovery.Select(entry => entry.Snapshot()).ToArray());

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

    /// <summary>Discovers manifest packages and legacy flat DLLs.</summary>
    /// <param name="pluginDir">Plugin directory; relative paths are resolved against the process's current directory.</param>
    /// <remarks>
    /// Call on the Studio UI thread. Discovery is additive and each source path is scanned once per host.
    /// Restart Studio to apply changes to a previously scanned package or assembly.
    /// </remarks>
    public void DiscoverAndLoad(string pluginDir)
    {
        pluginDir = Path.GetFullPath(pluginDir);
        string[] directories;
        string[] legacyAssemblies;
        try
        {
            if (!Directory.Exists(pluginDir))
            {
                _log($"[Plugin] Directory not found: {pluginDir}");
                return;
            }

            StudioPluginPackage.RejectLink(new DirectoryInfo(pluginDir));
            directories = Directory.GetDirectories(pluginDir);
            legacyAssemblies = Directory.GetFiles(pluginDir, "*.dll");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            _log($"Error: [Plugin] Cannot enumerate '{pluginDir}': {ex.Message}");
            return;
        }

        var packages = new List<(StudioPluginPackage Package, string? ManifestError)>();
        foreach (var directory in directories.Order(StringComparer.Ordinal))
        {
            if (!_scannedPaths.Add(directory))
                continue;

            var package = new StudioPluginPackage(directory);
            try
            {
                package.ReadManifest();
                packages.Add((package, null));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                or System.Text.Json.JsonException or InvalidDataException)
            {
                packages.Add((package, ex.Message));
            }
        }

        var conflicts = packages.Where(candidate => candidate.Package.HasValidId)
            .GroupBy(candidate => candidate.Package.Metadata!.Id!, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .ToDictionary(group => group.Key, group => string.Join(", ", group.Select(x => x.Package.DirectoryPath)),
                StringComparer.Ordinal);

        foreach (var (package, manifestError) in packages)
        {
            if (manifestError is not null)
            {
                Report(package.Result(StudioPluginStatus.InvalidPackage, manifestError));
                continue;
            }

            try
            {
                package.Validate(IsProtectedAssembly);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                if (package.HasValidId && conflicts.TryGetValue(package.Metadata!.Id!, out var paths))
                    Report(package.Result(StudioPluginStatus.Duplicate, $"Multiple packages claim this ID: {paths}. {ex.Message}"));
                else
                    Report(package.Result(StudioPluginStatus.InvalidPackage, ex.Message));
                continue;
            }

            var id = package.Metadata!.Id!;
            if (conflicts.TryGetValue(id, out var conflictPaths))
            {
                Report(package.Result(StudioPluginStatus.Duplicate, $"Multiple packages claim this ID: {conflictPaths}. Keep one package and restart Studio."));
                continue;
            }

            if (package.Compatibility == StudioPluginCompatibility.Incompatible)
            {
                Report(package.Result(StudioPluginStatus.Incompatible,
                    $"The package targets Studio contract {package.Metadata.ContractVersion}. This host supports {StudioPluginPackage.CurrentContractVersion}."));
                continue;
            }

            if (_packageIds.Contains(id))
            {
                Report(package.Result(StudioPluginStatus.Duplicate, $"ID '{id}' is already active. Keep one package and restart Studio."));
                continue;
            }

            if (LoadAssemblyPlugins(package.EntryPath!, package))
                _packageIds.Add(id);
        }

        foreach (var dllPath in legacyAssemblies.Order(StringComparer.Ordinal))
        {
            if (_scannedPaths.Add(dllPath))
                LoadAssemblyPlugins(dllPath, null);
        }
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
        plugin.DisableReason = reason;
        _log($"Warning: [Plugin] Disabled '{plugin.DisplayName}': {reason}");
    }

    private bool IsProtectedAssembly(string name) => StudioPluginLoadContext.IsProtectedAssembly(name, _sharedContracts);

    private void Report(StudioPluginDiscovery result, LoadedPlugin[]? plugins = null)
    {
        _discovery.Add(new DiscoveryEntry(result, plugins ?? []));
        if (result.Diagnostic is not null)
            _log($"Warning: [Plugin] {result.Status} in '{result.SourceDirectory}' ({result.EntryAssembly}): {result.Diagnostic}");
    }

    private bool LoadAssemblyPlugins(string dllPath, StudioPluginPackage? package)
    {
        var fileName = Path.GetFileName(dllPath);
        StudioPluginDiscovery LegacyResult(StudioPluginStatus status, string? diagnostic = null,
            string? id = null, string? name = null) => new(id, name, null,
                Path.GetDirectoryName(dllPath)!, fileName, StudioPluginSource.LegacyAssembly,
                StudioPluginCompatibility.Unknown, status, diagnostic);

        var loaded = new List<LoadedPlugin>();
        var failures = new List<string>();
        var duplicates = new List<string>();

        try
        {
            var context = new StudioPluginLoadContext(dllPath, _sharedContracts, package?.DirectoryPath);
            var assembly = context.LoadFromAssemblyPath(dllPath);
            if (package is not null)
                context.VerifyPackageReferences(assembly);

            var types = assembly.GetTypes()
                .Where(type => !type.IsAbstract && typeof(IStudioPlugin).IsAssignableFrom(type))
                .OrderBy(type => type.FullName, StringComparer.Ordinal);

            foreach (var type in types)
            {
                var id = $"legacy:{assembly.GetName().Name}:{type.FullName}";
                if (_implementations.Contains(id))
                {
                    var diagnostic = $"Implementation '{type.FullName}' is already loaded.";
                    duplicates.Add(diagnostic);
                    if (package is null)
                        Report(LegacyResult(StudioPluginStatus.Duplicate, diagnostic, id));
                    continue;
                }

                LoadedPlugin plugin;
                try
                {
                    var instance = (IStudioPlugin)Activator.CreateInstance(type)!;
                    plugin = new LoadedPlugin(instance, context, dllPath) { DisplayName = instance.Name };
                }
                catch (Exception ex)
                {
                    var diagnostic = $"Failed to instantiate {type.FullName}: {ex.GetBaseException().Message}";
                    failures.Add(diagnostic);
                    if (package is null)
                        Report(LegacyResult(StudioPluginStatus.FailedToLoad, diagnostic, id));
                    continue;
                }

                _implementations.Add(id);
                _plugins.Add(plugin);
                loaded.Add(plugin);
                _log($"[Plugin] Loaded '{plugin.DisplayName}' from {fileName}");
                if (package is null)
                    Report(LegacyResult(StudioPluginStatus.Loaded, id: id, name: plugin.DisplayName), [plugin]);
            }
        }
        catch (Exception ex)
        {
            var details = ex is ReflectionTypeLoadException reflection
                ? string.Join(" ", reflection.LoaderExceptions.Select(error => error?.Message))
                : ex.GetBaseException().Message;
            var diagnostic = $"Failed loading {fileName}: {details}";
            failures.Add(diagnostic);
            if (package is null)
                Report(LegacyResult(StudioPluginStatus.FailedToLoad, diagnostic));
        }

        if (package is not null)
        {
            var diagnostics = failures.Concat(duplicates).ToArray();
            var status = StudioPluginStatus.Loaded;
            if (loaded.Count > 0 && diagnostics.Length > 0)
                status = StudioPluginStatus.PartiallyLoaded;
            else if (loaded.Count == 0)
            {
                status = failures.Count == 0 && duplicates.Count > 0
                    ? StudioPluginStatus.Duplicate : StudioPluginStatus.FailedToLoad;
                if (diagnostics.Length == 0)
                    diagnostics = ["The entry assembly contains no concrete Studio plugin implementations."];
            }

            Report(package.Result(status, diagnostics.Length == 0 ? null : string.Join(" ", diagnostics)),
                loaded.ToArray());
        }

        return loaded.Count > 0;
    }

    private sealed record DiscoveryEntry(StudioPluginDiscovery Result, LoadedPlugin[] Plugins)
    {
        internal StudioPluginDiscovery Snapshot()
        {
            var disabled = Plugins.Where(plugin => !plugin.Enabled).ToArray();
            if (disabled.Length == 0)
                return Result;

            var diagnostics = disabled.Select(plugin => $"{plugin.DisplayName}: {plugin.DisableReason}");
            return Result with
            {
                Status = disabled.Length == Plugins.Length ? StudioPluginStatus.Disabled : StudioPluginStatus.PartiallyLoaded,
                Diagnostic = string.Join(" ", new[] { Result.Diagnostic }.Concat(diagnostics).OfType<string>())
            };
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

        internal string? DisplayName { get; init; }
        internal string? DisableReason { get; set; }
    }
}
