using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

namespace Gondwana.Tooling.Studio.Core.Extensibility;

internal sealed class StudioPluginLoadContext : AssemblyLoadContext
{
    // Framework assemblies can fall back to the runtime even in a self-contained Studio build.
    // Require a known framework signing key and an entry in the runtime's platform assembly list.
    private static readonly HashSet<string> FrameworkTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "b77a5c561934e089", "b03f5f7f11d50a3a", "cc7b13ffcd2ddd51", "31bf3856ad364e35",
        "7cec85d7bea7798e", "adb9793829ddae60"
    };

    private static readonly HashSet<string> PlatformAssemblies = new(
        ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => Path.GetFileNameWithoutExtension(path)!),
        StringComparer.OrdinalIgnoreCase);

    private readonly AssemblyDependencyResolver _resolver;
    private readonly Assembly[] _sharedAssemblies;
    private readonly string? _packageDirectory;

    internal StudioPluginLoadContext(string entryPath, Assembly[] sharedAssemblies, string? packageDirectory)
        : base(name: $"studio-plugin:{Path.GetFileNameWithoutExtension(entryPath)}", isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(entryPath);
        _sharedAssemblies = sharedAssemblies;
        _packageDirectory = packageDirectory;
    }

    internal static bool IsProtectedAssembly(string name, Assembly[] sharedAssemblies) =>
        FindSharedAssembly(name, sharedAssemblies) is not null
        || name.Equals("Gondwana", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("Gondwana.Studio.", StringComparison.OrdinalIgnoreCase)
        || (name.StartsWith("Gondwana.", StringComparison.OrdinalIgnoreCase)
            && PlatformAssemblies.Contains(name)
            && !name.StartsWith("Gondwana.Tooling.Studio.Plugin.", StringComparison.OrdinalIgnoreCase)
            && !name.Equals("Gondwana.Tooling.Importers", StringComparison.OrdinalIgnoreCase));

    private static Assembly? FindSharedAssembly(string name, Assembly[] sharedAssemblies) =>
        sharedAssemblies.FirstOrDefault(assembly =>
            string.Equals(assembly.GetName().Name, name, StringComparison.OrdinalIgnoreCase));

    // Resolve the static managed dependency graph before running any plugin constructor.
    internal void VerifyPackageReferences(Assembly entry)
    {
        var visited = new HashSet<Assembly>();
        var pending = new Stack<Assembly>();
        pending.Push(entry);
        while (pending.TryPop(out var assembly))
        {
            if (!visited.Add(assembly))
                continue;

            foreach (var reference in assembly.GetReferencedAssemblies())
            {
                var dependency = LoadFromAssemblyName(reference);
                if (GetLoadContext(dependency) == this)
                    pending.Push(dependency);
            }
        }
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var shared = FindSharedAssembly(assemblyName.Name!, _sharedAssemblies);
        if (shared is not null)
            return shared;

        if (_packageDirectory is null)
        {
            var legacyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            return legacyPath is null ? null : LoadFromAssemblyPath(legacyPath);
        }

        if (IsFrameworkAssembly(assemblyName))
            return null;

        var name = assemblyName.Name!;
        if (IsProtectedAssembly(name, _sharedAssemblies))
            throw new FileLoadException($"'{name}' belongs to Studio and is unavailable as a shared plugin dependency.", name);

        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        if (path is not null)
        {
            path = StudioPluginPackage.CheckContainedPath(_packageDirectory, path);
            StudioPluginPackage.ValidatePrivateAssembly(path, name => IsProtectedAssembly(name, _sharedAssemblies));
            return LoadFromAssemblyPath(path);
        }

        // FileNotFoundException permits default-context fallback in AssemblyLoadContext.
        throw new FileLoadException($"Private dependency '{assemblyName}' could not be resolved. Include it and its dependency metadata in the package.", name);
    }

    private static bool IsFrameworkAssembly(AssemblyName identity) =>
        PlatformAssemblies.Contains(identity.Name!)
        && identity.GetPublicKeyToken() is { Length: > 0 } token
        && FrameworkTokens.Contains(Convert.ToHexString(token));

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (_packageDirectory is null)
            return path is null ? IntPtr.Zero : LoadUnmanagedDllFromPath(path);

        if (path is not null)
            return LoadUnmanagedDllFromPath(StudioPluginPackage.CheckContainedPath(_packageDirectory, path));

        if (OperatingSystem.IsWindows() && Path.GetFileName(unmanagedDllName) == unmanagedDllName
            && NativeLibrary.TryLoad(unmanagedDllName, typeof(StudioPluginLoadContext).Assembly,
                DllImportSearchPath.System32, out var systemLibrary))
            return systemLibrary;

        throw new DllNotFoundException($"Native dependency '{unmanagedDllName}' could not be resolved. Include it and its dependency metadata in the package.");
    }
}
