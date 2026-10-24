using System.Text;

namespace Gondwana.Tooling.Importers;

/// <summary>Portable, deterministic names shared by all format providers.</summary>
public static class ImportNaming
{
    /// <summary>Produces a lowercase portable filename component with unsafe characters replaced by hyphens.</summary>
    /// <param name="name">Source label to normalize to Unicode Form C.</param>
    /// <returns>The sanitized label, asset when empty after trimming, or an asset-prefixed reserved device name.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is null.</exception>
    public static string Sanitize(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var result = new StringBuilder();
        foreach (char c in name.Normalize(NormalizationForm.FormC))
        {
            if (char.IsLetterOrDigit(c) || c is '-' or '_' or '.') result.Append(char.ToLowerInvariant(c));
            else if (result.Length > 0 && result[^1] != '-') result.Append('-');
        }
        var value = result.ToString().Trim('.', '-', ' ');
        if (value.Length == 0) value = "asset";
        var stem = value.Split('.')[0];
        if (stem is "con" or "prn" or "aux" or "nul" ||
            (stem.Length == 4 && (stem.StartsWith("com", StringComparison.Ordinal) || stem.StartsWith("lpt", StringComparison.Ordinal)) && stem[3] is >= '1' and <= '9'))
            value = "asset-" + value;
        return value;
    }

    /// <summary>Expresses a dependency path relative to the output directory without copying or checking the resource.</summary>
    /// <param name="outputDirectory">Destination document directory, normalized against the process current directory.</param>
    /// <param name="dependencyPath">Dependency location, normalized against the process current directory.</param>
    /// <returns>A path using forward slashes; it remains absolute when the locations are on different roots.</returns>
    public static string RelativePath(string outputDirectory, string dependencyPath) =>
        Path.GetRelativePath(Path.GetFullPath(outputDirectory), Path.GetFullPath(dependencyPath)).Replace('\\', '/');
}
