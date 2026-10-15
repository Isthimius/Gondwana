using Gondwana.Assets;
using Gondwana.Cli.Commands.Tilesheets;
using ICSharpCode.SharpZipLib.Zip;

namespace Gondwana.Cli.Commands.Assets;

internal static class BundleHelper
{
    /// <summary>
    /// Checks whether an assets file requires a password.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns><see langword="true"/> if the assets file requires a password; otherwise, <see langword="false"/>.</returns>
    public static bool NeedsPassword(string path)
    {
        using var archive = new ZipFile(File.OpenRead(path));
        return archive.Cast<ZipEntry>().Any(e => e.IsCrypted);
    }

    /// <summary>
    /// Opens the supplied file for use by the bundle helper.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <param name="password">The password used to open the assets file, if required.</param>
    /// <param name="testData">Whether to verify the archive data while opening it.</param>
    /// <returns>The resulting assets file.</returns>
    public static AssetsFile Open(string path, string? password, bool testData = true)
    {
        // Validate before loading because the runtime intentionally ignores unknown
        // entries and collapses duplicate keys for ordinary application usage.
        AssetsFile.Validate(path, password, testData);
        var bundle = AssetsFile.LoadOrCreate(path, password);
        try
        {
            foreach (var entry in bundle.GetAllEntries()) SafeFilePath.ValidateRelative(entry.AssetName);
            return bundle;
        }
        catch { bundle.Dispose(); throw; }
    }

    /// <summary>
    /// Checks the contents of an assets file and returns validation errors.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <param name="password">The password used to open the assets file, if required.</param>
    /// <returns>The validation errors; an empty collection indicates that validation passed.</returns>
    public static IReadOnlyList<string> ValidateContents(string path, string? password)
    {
        using var bundle = Open(path, password);
        var errors = new List<string>();
        foreach (var entry in bundle.GetAllEntries().Where(e => e.AssetType == AssetTypes.TilesheetDefinition).OrderBy(e => e.AssetName, StringComparer.Ordinal))
        {
            using var stream = bundle[entry.AssetType, entry.AssetName]!;
            errors.AddRange(TilesheetInspection.AllFromStream(stream, Path.GetDirectoryName(Path.GetFullPath(path))!, bundle).SelectMany(result => result.Errors.Select(e => $"{entry.AssetName} :: {result.Definition?.Name}: {e}")));
        }
        return errors;
    }

    /// <summary>
    /// Semantically exports matching entries, with staged writes and rollback of replaced files.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <param name="output">The output.</param>
    /// <param name="password">The password used to open the assets file, if required.</param>
    /// <param name="type">The type.</param>
    /// <param name="overwrite">Whether existing output files may be replaced.</param>
    /// <returns>The number of entries extracted.</returns>
    public static int Extract(string path, string output, string? password, AssetTypes? type, bool overwrite)
    {
        return SemanticUnpackPlan.Create(path, output, password, type, null, overwrite, default).Commit();
    }
}

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
