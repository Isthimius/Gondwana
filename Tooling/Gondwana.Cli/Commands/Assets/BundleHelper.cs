using Gondwana.Assets;
using Gondwana.Cli.Commands.Tilesheets;
using ICSharpCode.SharpZipLib.Zip;

namespace Gondwana.Cli.Commands.Assets;

internal static class BundleHelper
{
    public static bool NeedsPassword(string path)
    {
        using var archive = new ZipFile(File.OpenRead(path));
        return archive.Cast<ZipEntry>().Any(e => e.IsCrypted);
    }

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

    public static IReadOnlyList<string> ValidateContents(string path, string? password)
    {
        using var bundle = Open(path, password);
        var errors = new List<string>();
        foreach (var entry in bundle.GetAllEntries().Where(e => e.AssetType == AssetTypes.TilesheetDefinition).OrderBy(e => e.AssetName, StringComparer.Ordinal))
        {
            using var stream = bundle[entry.AssetType, entry.AssetName]!;
            errors.AddRange(TilesheetInspection.FromStream(stream, Path.GetDirectoryName(Path.GetFullPath(path))!, bundle).Errors.Select(e => $"{entry.AssetName}: {e}"));
        }
        return errors;
    }

    public static int Extract(string path, string output, string? password, AssetTypes? type, bool overwrite)
    {
        using var bundle = Open(path, password);
        var entries = bundle.GetAllEntries().Where(e => type is null || e.AssetType == type).OrderBy(e => e.AssetName, StringComparer.Ordinal).ToArray();
        var targets = entries.Select(e => SafeFilePath.Resolve(output, e.AssetName)).ToArray();
        if (targets.Distinct(StringComparer.OrdinalIgnoreCase).Count() != targets.Length)
            throw new InvalidDataException("Multiple asset types map to the same output filename. Extract one --type at a time.");
        foreach (var target in targets)
        {
            if (Directory.Exists(target)) throw new IOException($"Destination is a directory: {target}");
            if (File.Exists(target) && !overwrite) throw new IOException($"File exists: {target}. Use --overwrite to replace it.");
            for (var parent = Path.GetDirectoryName(target); parent is not null; parent = Path.GetDirectoryName(parent))
            {
                if (File.Exists(parent)) throw new IOException($"Destination parent is a file: {parent}");
                if (string.Equals(parent, Path.GetFullPath(output), StringComparison.OrdinalIgnoreCase)) break;
            }
            if (targets.Any(other => other.StartsWith(target + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException($"Bundle contains a file/directory name collision: {target}");
        }
        // Preflight every destination before creating any file. Recheck link ancestry
        // immediately before each write; existing directory links are never followed.
        for (var i = 0; i < entries.Length; i++)
        {
            var destination = SafeFilePath.Resolve(output, entries[i].AssetName);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            using var source = bundle[entries[i].AssetType, entries[i].AssetName] ?? throw new InvalidDataException($"Unreadable entry: {entries[i].AssetName}");
            using var target = new FileStream(destination, overwrite ? FileMode.Create : FileMode.CreateNew, FileAccess.Write);
            source.CopyTo(target);
        }
        return entries.Length;
    }
}

internal static class SafeFilePath
{
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
            if (string.Equals(current, fullRoot, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) break;
        }
        return destination;
    }
}
