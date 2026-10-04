using Gondwana.Drawing.Coordinates;

namespace Gondwana.Tooling.SceneViewer.WinForms;

internal sealed record SceneViewerStressOptions(
    int TileCount,
    CoordinateSystemTypes Projection);

internal static class SceneViewerArguments
{
    internal const string Usage =
        "Usage: Gondwana.Tooling.SceneViewer.WinForms.exe --scene <file.gscn> " +
        "or --stress <tile-count> [--projection <coordinate-system>]";

    // The OS has already removed command-line quoting from args.
    internal static string? Parse(string[] args, string baseDirectory)
    {
        if (args.Length == 0)
            return null;
        if (args.Length != 2 || args[0] != "--scene" || string.IsNullOrWhiteSpace(args[1]))
            throw new ArgumentException(Usage);
        return Normalize(args[1], baseDirectory);
    }

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
