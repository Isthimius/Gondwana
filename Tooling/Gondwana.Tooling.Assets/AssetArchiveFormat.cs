namespace Gondwana.Tooling.Assets;

/// <summary>Shared recognition of supported GAF package filename extensions.</summary>
public static class AssetArchiveFormat
{
    /// <summary>Recognizes GAF and ZIP package paths, without opening the archive.</summary>
    /// <param name="path">Physical package path.</param>
    /// <returns>Whether the path has a supported archive extension.</returns>
    public static bool IsArchive(string path) => Path.GetExtension(path).ToLowerInvariant() is ".gaf" or ".zip";
}
