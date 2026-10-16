using Gondwana.Assets;
using Gondwana.Cli.Commands.Tilesheets;
using Gondwana.Tooling.Assets;
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
