using Gondwana.Drawing.Coordinates;

namespace Gondwana.Tooling.SceneViewer.WinForms;

/// <summary>
/// Represents scene viewer stress options.
/// </summary>
/// <param name="TileCount">The tile count.</param>
/// <param name="Projection">The projection.</param>
internal sealed record SceneViewerStressOptions(
    int TileCount,
    CoordinateSystemTypes Projection);

/// <summary>
/// Represents scene viewer arguments.
/// </summary>
internal static class SceneViewerArguments
{
    /// <summary>
    /// Stores the usage.
    /// </summary>
    internal const string Usage =
        "Usage: Gondwana.Tooling.SceneViewer.WinForms.exe --scene <file.gscn> " +
        "or --stress <tile-count> [--projection <coordinate-system>]";

    // The OS has already removed command-line quoting from args.
    /// <summary>
    /// Performs the parse operation.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <param name="baseDirectory">The base directory used to resolve relative paths.</param>
    /// <returns>The requested value, or <see langword="null"/> when it is unavailable.</returns>
    internal static string? Parse(string[] args, string baseDirectory)
    {
        if (args.Length == 0)
            return null;
        if (args.Length != 2 || args[0] != "--scene" || string.IsNullOrWhiteSpace(args[1]))
            throw new ArgumentException(Usage);
        return Normalize(args[1], baseDirectory);
    }

    /// <summary>
    /// Parses stress.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The requested value, or <see langword="null"/> when it is unavailable.</returns>
    internal static SceneViewerStressOptions? ParseStress(string[] args)
    {
        if (args.Length == 0 || args[0] != "--stress")
            return null;

        if (args.Length is not 2 and not 4 ||
            !int.TryParse(args[1], out int tileCount) ||
            tileCount < 1 ||
            tileCount > 100_000)
        {
            throw new ArgumentException(Usage);
        }

        var projection = CoordinateSystemTypes.Orthogonal;
        if (args.Length == 4)
        {
            if (args[2] != "--projection" ||
                !Enum.TryParse(args[3], ignoreCase: true, out projection) ||
                !Enum.IsDefined(projection))
            {
                throw new ArgumentException(Usage);
            }
        }

        return new(tileCount, projection);
    }

    /// <summary>
    /// Performs the normalize operation.
    /// </summary>
    /// <param name="path">The path to normalize.</param>
    /// <param name="baseDirectory">The base directory used to resolve relative paths.</param>
    /// <returns>The resulting value.</returns>
    internal static string Normalize(string path, string baseDirectory)
    {
        var fullPath = Path.GetFullPath(path, baseDirectory);
        if (!string.Equals(Path.GetExtension(fullPath), ".gscn", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Choose a .gscn scene file.");
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Scene file not found: {fullPath}", fullPath);
        return fullPath;
    }
}
