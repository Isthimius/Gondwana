using System.Text;

namespace Gondwana.Tooling.Importers;

/// <summary>Portable, deterministic names shared by all format providers.</summary>
public static class ImportNaming
{
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

    public static string RelativePath(string outputDirectory, string dependencyPath) =>
        Path.GetRelativePath(Path.GetFullPath(outputDirectory), Path.GetFullPath(dependencyPath)).Replace('\\', '/');
}
