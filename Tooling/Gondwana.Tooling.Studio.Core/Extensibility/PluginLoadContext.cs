using System.Reflection;
using System.Runtime.Loader;

namespace Gondwana.Tooling.Studio.Core.Extensibility;

internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly IReadOnlyDictionary<string, Assembly> _shared;
    private readonly string _directory;
    private readonly bool _legacy;
    private static readonly HashSet<string> FrameworkAssemblies = GetFrameworkAssemblies();

    internal PluginLoadContext(string entry, IReadOnlyDictionary<string, Assembly> shared, bool legacy)
        : base($"studio-plugin:{entry}", isCollectible: false)
    {
        _resolver = new AssemblyDependencyResolver(entry);
        _directory = Path.GetDirectoryName(entry)!;
        _shared = shared;
        _legacy = legacy;
    }

    internal static Dictionary<string, Assembly> GetSharedAssemblies(IEnumerable<Assembly> contracts)
    {
        var shared = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);
        var pending = new Queue<Assembly>(contracts);
        while (pending.TryDequeue(out var assembly))
        {
            if (!shared.TryAdd(assembly.GetName().Name!, assembly)) continue;
            foreach (var reference in assembly.GetReferencedAssemblies())
            {
                if (shared.ContainsKey(reference.Name!)) continue;
                try { pending.Enqueue(Default.LoadFromAssemblyName(reference)); }
                catch (FileNotFoundException) { /* Optional host dependency; never substitute a private Gondwana assembly. */ }
            }
        }
        return shared;
    }

    internal static bool IsReserved(string name, IReadOnlyDictionary<string, Assembly> shared)
        => shared.ContainsKey(name) || FrameworkAssemblies.Contains(name) ||
           name.Equals("Gondwana", StringComparison.OrdinalIgnoreCase) ||
           (name.StartsWith("Gondwana.", StringComparison.OrdinalIgnoreCase) &&
            !name.StartsWith("Gondwana.Tooling.Studio.Plugin.", StringComparison.OrdinalIgnoreCase) &&
            !name.Equals("Gondwana.Tooling.Importers", StringComparison.OrdinalIgnoreCase));

    internal static void ValidateAssemblies(string directory, IReadOnlyDictionary<string, Assembly> shared)
    {
        foreach (var path in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                     .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
        {
            // Check filename as well as CLR identity so renaming a contract cannot bypass validation.
            if (IsReserved(Path.GetFileNameWithoutExtension(path), shared))
                throw new InvalidDataException($"Remove shared host/framework assembly from package: {path}");
            try
            {
                if (IsReserved(AssemblyName.GetAssemblyName(path).Name!, shared))
                    throw new InvalidDataException($"Package contains a private copy of a shared host/framework assembly: {path}");
            }
            catch (BadImageFormatException) { /* Native libraries are permitted. Entry validation occurs during load. */ }
        }
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        if (_shared.TryGetValue(assemblyName.Name!, out var shared)) return shared;
        if (FrameworkAssemblies.Contains(assemblyName.Name!)) return null;
        if (!_legacy && IsReserved(assemblyName.Name!, _shared))
            throw new FileLoadException($"Host does not supply required shared assembly '{assemblyName.Name}'.");

        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        if (path is null)
        {
            var adjacent = Path.Combine(_directory, assemblyName.Name + ".dll");
            if (File.Exists(adjacent)) path = adjacent;
        }
        if (path is not null)
            return LoadFromAssemblyPath(_legacy ? path : PluginPackagePaths.RequireContained(_directory, path));
        if (_legacy) return null;
        throw new FileNotFoundException($"Private dependency '{assemblyName}' is missing from package '{_directory}'. Deploy it and its .deps.json, then restart Studio.");
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (path is not null)
            return LoadUnmanagedDllFromPath(_legacy ? path : PluginPackagePaths.RequireContained(_directory, path));
        if (_legacy) return IntPtr.Zero;
        throw new DllNotFoundException($"Native dependency '{unmanagedDllName}' is missing from package '{_directory}'.");
    }

    private static HashSet<string> GetFrameworkAssemblies()
    {
        var runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        // Self-contained apps place framework and application DLLs in the same directory.
        // Do not turn every app-local assembly into a framework fallback in that layout.
        return Directory.EnumerateFiles(runtimeDirectory, "*.dll")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(name => name is "mscorlib" or "netstandard" or "System" or "Microsoft.CSharp" or "Microsoft.VisualBasic" or "Microsoft.VisualBasic.Core" ||
                name!.StartsWith("System.", StringComparison.Ordinal))
            .ToHashSet(StringComparer.OrdinalIgnoreCase)!;
    }
}
