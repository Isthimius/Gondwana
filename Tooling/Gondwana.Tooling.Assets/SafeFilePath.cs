namespace Gondwana.Tooling.Assets;

/// <summary>Validates portable entry names and contained extraction destinations.</summary>
internal static class SafeFilePath
{
    /// <summary>Rejects conflicting planned filenames and directory spellings on every platform.</summary>
    /// <param name="destinations">Absolute physical paths and whether multiple entries may share that archive.</param>
    internal static void ValidateDestinations(IEnumerable<(string Path, bool Archive)> destinations)
    {
        var paths = new Dictionary<string, (string Spelling, bool File, bool Archive)>(StringComparer.OrdinalIgnoreCase);
        foreach (var destination in destinations)
        {
            bool file = true;
            for (string? path = destination.Path; path is not null; path = Path.GetDirectoryName(path))
            {
                bool archive = file && destination.Archive;
                if (paths.TryGetValue(path, out var prior) &&
                    (prior.Spelling != path || prior.File != file || (file && !(prior.Archive && archive))))
                    throw new InvalidDataException($"Conflicting planned destination path or directory prefix: {path} (already planned as {prior.Spelling}).");
                paths[path] = (path, file, archive);
                file = false;
            }
        }
    }

    /// <summary>Rejects nonportable entry paths, including traversal components and reserved Windows device names.</summary>
    /// <param name="name">Non-null entry path using either slash style; no filesystem access is performed.</param>
    /// <exception cref="InvalidDataException">The entry name is rooted, empty, contains traversal, or has unsafe path components.</exception>
    public static void ValidateRelative(string name)
    {
        var normalized = name.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(normalized) || normalized.StartsWith('/') || normalized.Contains(':')) throw new InvalidDataException($"Unsafe asset path: {name}");
        foreach (var part in normalized.Split('/'))
        {
            var stem = part.Split('.')[0];
            if (part is "" or "." or ".." || part.EndsWith('.') || part.EndsWith(' ') || part.Any(c => c < 32 || "<>\"|?*".Contains(c)) ||
                new[] { "CON", "PRN", "AUX", "NUL" }.Contains(stem, StringComparer.OrdinalIgnoreCase) ||
                System.Text.RegularExpressions.Regex.IsMatch(stem, @"^(COM|LPT)[1-9]$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                throw new InvalidDataException($"Unsafe asset path: {name}");
        }
    }

    /// <summary>Resolves an entry beneath a root and rejects symbolic links or junctions in existing ancestry.</summary>
    /// <param name="root">Extraction directory, normalized to a full path.</param>
    /// <param name="name">Portable relative entry name using either slash style.</param>
    /// <returns>The full destination path, without creating files or directories.</returns>
    /// <exception cref="InvalidDataException">The name is unsafe or resolves outside the root.</exception>
    /// <exception cref="IOException">A symbolic link or junction is encountered.</exception>
    public static string Resolve(string root, string name)
    {
        ValidateRelative(name);
        var fullRoot = Path.GetFullPath(root);
        var destination = Path.GetFullPath(Path.Combine(fullRoot, name.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar)));
        var prefix = Path.EndsInDirectorySeparator(fullRoot) ? fullRoot : fullRoot + Path.DirectorySeparatorChar;
        if (!destination.StartsWith(prefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidDataException($"Path escapes the root: {name}");
        for (var current = destination; current is not null; current = Path.GetDirectoryName(current))
        {
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException($"Refusing to follow symbolic link or junction: {current}");
            if (new FileInfo(current).LinkTarget is not null) throw new IOException($"Refusing symbolic link: {current}");

        }
        return destination;
    }
}
