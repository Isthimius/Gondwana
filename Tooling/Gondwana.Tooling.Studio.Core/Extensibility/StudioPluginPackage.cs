using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Gondwana.Tooling.Studio.Core.Extensibility;

internal sealed partial class StudioPluginPackage
{
    internal const int CurrentContractVersion = 1;
    private const int CurrentSchemaVersion = 1;

    internal StudioPluginPackage(string directory)
    {
        DirectoryPath = directory;
    }

    internal string DirectoryPath { get; }
    internal string? EntryPath { get; private set; }
    internal Manifest? Metadata { get; private set; }
    internal bool HasValidId => Metadata?.Id is string id && PluginIdPattern().IsMatch(id);
    internal StudioPluginCompatibility Compatibility { get; private set; }

    internal void ReadManifest()
    {
        var path = CheckContainedPath(DirectoryPath, Path.Combine(DirectoryPath, "plugin.json"));
        if (!File.Exists(path))
            throw new InvalidDataException("Missing plugin.json. Add a manifest to the package directory.");

        using var stream = File.OpenRead(path);
        Metadata = JsonSerializer.Deserialize<Manifest>(stream)
            ?? throw new InvalidDataException("plugin.json must contain a JSON object.");
    }

    internal void Validate(Func<string, bool> isProtectedAssembly)
    {
        var manifest = Metadata!;
        if (manifest.SchemaVersion != CurrentSchemaVersion)
            throw new InvalidDataException($"plugin.json must declare schemaVersion {CurrentSchemaVersion}.");
        if (!HasValidId)
            throw new InvalidDataException("plugin.json requires an id using lowercase letters, digits, dots, underscores or hyphens, starting with a letter or digit.");
        if (string.IsNullOrWhiteSpace(manifest.Name))
            throw new InvalidDataException("plugin.json requires a non-empty name.");
        if (manifest.Version is null || !VersionPattern().IsMatch(manifest.Version))
            throw new InvalidDataException("plugin.json requires a SemVer version such as 1.0.0.");
        if (manifest.ContractVersion is null or <= 0)
            throw new InvalidDataException("plugin.json requires a positive integer contractVersion.");
        if (manifest.EntryAssembly is null || !EntryAssemblyPattern().IsMatch(manifest.EntryAssembly))
            throw new InvalidDataException("entryAssembly must be a DLL filename in the package root.");

        Compatibility = manifest.ContractVersion == CurrentContractVersion
            ? StudioPluginCompatibility.Compatible
            : StudioPluginCompatibility.Incompatible;

        EntryPath = CheckContainedPath(DirectoryPath, Path.Combine(DirectoryPath, manifest.EntryAssembly));
        if (!File.Exists(EntryPath))
            throw new InvalidDataException($"Entry assembly '{manifest.EntryAssembly}' is missing. Restore the package files.");

        // Walk explicitly so directory links are rejected before they can be traversed.
        var directories = new Stack<string>();
        directories.Push(DirectoryPath);
        while (directories.TryPop(out var directory))
        {
            foreach (var item in new DirectoryInfo(directory).EnumerateFileSystemInfos())
            {
                RejectLink(item);
                if (item is DirectoryInfo)
                    directories.Push(item.FullName);
                else if (item.Extension.Equals(".dll", StringComparison.OrdinalIgnoreCase))
                    ValidatePrivateAssembly(item.FullName, isProtectedAssembly);
            }
        }
    }

    internal static string CheckContainedPath(string directory, string path)
    {
        path = Path.GetFullPath(path);
        var relativePath = Path.GetRelativePath(directory, path);
        if (Path.IsPathRooted(relativePath) || relativePath is "." or ".."
            || relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidDataException($"'{path}' is outside the plugin package.");

        var current = new DirectoryInfo(directory);
        RejectLink(current);
        var parts = relativePath.Split(Path.DirectorySeparatorChar);
        foreach (var part in parts[..^1])
        {
            current = new DirectoryInfo(Path.Combine(current.FullName, part));
            RejectLink(current);
        }

        RejectLink(new FileInfo(path));
        return path;
    }

    internal static void RejectLink(FileSystemInfo item)
    {
        if (item.LinkTarget is not null || (item.Exists && item.Attributes.HasFlag(FileAttributes.ReparsePoint)))
            throw new InvalidDataException($"'{item.FullName}' is a link or reparse point. Use regular files and directories in plugin packages.");
    }

    internal static void ValidatePrivateAssembly(string path, Func<string, bool> isProtectedAssembly)
    {
        if (isProtectedAssembly(Path.GetFileNameWithoutExtension(path)))
            throw new InvalidDataException($"Remove host assembly '{Path.GetFileName(path)}' from the package.");

        AssemblyName identity;
        try
        {
            identity = AssemblyName.GetAssemblyName(path);
        }
        catch (BadImageFormatException)
        {
            // Native DLLs have no managed assembly identity.
            return;
        }

        if (identity.Name is string name && isProtectedAssembly(name))
            throw new InvalidDataException($"Remove '{Path.GetFileName(path)}' from the package. Its assembly identity '{name}' belongs to the host.");
    }

    internal StudioPluginDiscovery Result(StudioPluginStatus status, string? diagnostic = null) => new(
        Metadata?.Id, Metadata?.Name, Metadata?.Version, DirectoryPath, Metadata?.EntryAssembly,
        StudioPluginSource.ManifestPackage, Compatibility, status, diagnostic,
        Metadata?.Description, Metadata?.Author, Metadata?.ContractVersion);

    [GeneratedRegex(@"\A[a-z0-9][a-z0-9._-]*\z", RegexOptions.CultureInvariant)]
    private static partial Regex PluginIdPattern();

    [GeneratedRegex(@"\A(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)(?:-(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*)(?:\.(?:0|[1-9][0-9]*|[0-9]*[A-Za-z-][0-9A-Za-z-]*))*)?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?\z", RegexOptions.CultureInvariant)]
    private static partial Regex VersionPattern();

    [GeneratedRegex(@"\A[^<>:""/\\|?*\x00-\x1f]+\.dll\z", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex EntryAssemblyPattern();

    internal sealed record Manifest(
        [property: JsonPropertyName("schemaVersion")] int? SchemaVersion,
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("version")] string? Version,
        [property: JsonPropertyName("entryAssembly")] string? EntryAssembly,
        [property: JsonPropertyName("contractVersion")] int? ContractVersion,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("author")] string? Author);
}
