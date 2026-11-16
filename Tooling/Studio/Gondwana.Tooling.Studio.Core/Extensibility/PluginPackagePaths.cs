namespace Gondwana.Tooling.Studio.Core.Extensibility;

internal static class PluginPackagePaths
{
    internal static StringComparer Comparer { get; } = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    internal static void ValidateTree(string directory)
    {
        EnsureNoLinks(directory);
        foreach (var path in Directory.EnumerateFileSystemEntries(directory))
        {
            EnsureNoLinks(path);
            if (Directory.Exists(path)) ValidateTree(path);
        }
    }

    internal static string RequireContained(string directory, string path)
    {
        path = Path.GetFullPath(path);
        var relative = Path.GetRelativePath(directory, path);
        if (Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidDataException($"Dependency path escapes package: {path}");
        EnsureNoLinks(path);
        return path;
    }

    // Reject all links/junctions, including ancestors. This conservative rule also avoids
    // recursive link cycles and works identically before future folder/ZIP installation.
    internal static void EnsureNoLinks(string path)
    {
        for (var current = Path.GetFullPath(path); current is not null; current = Path.GetDirectoryName(current))
        {
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException($"Symbolic links and junctions are not supported in plugin paths: {current}");
        }
    }
}
