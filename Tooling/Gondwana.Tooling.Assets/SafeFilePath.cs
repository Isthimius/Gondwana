namespace Gondwana.Tooling.Assets;

/// <summary>Validates portable entry names and contained extraction destinations.</summary>
internal static class SafeFilePath
{
    /// <summary>
    /// Rejects asset entry names that are not safe relative paths.
    /// </summary>
    /// <param name="name">The name.</param>
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

    /// <summary>
    /// Resolves an asset entry beneath the root, rejecting symbolic links and junctions anywhere in its ancestry.
    /// </summary>
    /// <param name="root">The root.</param>
    /// <param name="name">The name.</param>
    /// <returns>The resolved path beneath the supplied root.</returns>
    public static string Resolve(string root, string name)
    {
        ValidateRelative(name);
        var fullRoot = Path.GetFullPath(root);
        var destination = Path.GetFullPath(Path.Combine(fullRoot, name.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar)));
        if (!destination.StartsWith(Path.TrimEndingDirectorySeparator(fullRoot) + Path.DirectorySeparatorChar, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
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
