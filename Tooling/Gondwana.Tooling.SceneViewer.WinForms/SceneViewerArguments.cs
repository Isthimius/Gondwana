using Gondwana.Drawing.Coordinates;

namespace Gondwana.Tooling.SceneViewer.WinForms;

internal sealed record SceneViewerStressOptions(
    int TileCount,
    CoordinateSystemTypes Projection);

internal sealed record SceneViewerSceneOptions(string ScenePath, string? SceneId, string? ContentRoot, bool TemporaryScene);

internal static class SceneViewerArguments
{
    internal const string Usage =
        "Usage: Gondwana.Tooling.SceneViewer.WinForms.exe --scene <file.gscn> [--scene-id <id>] " +
        "[--content-root <directory>] [--temporary-scene] or --stress <tile-count> [--projection <coordinate-system>]";

    /// <summary>Parses a scene document path and validates an optional member selector.</summary>
    /// <param name="args">Argument tokens with operating-system quoting removed.</param>
    /// <param name="baseDirectory">The base for relative document paths.</param>
    /// <returns>The normalized document path, or null when no arguments were supplied.</returns>
    internal static string? Parse(string[] args, string baseDirectory) => ParseScene(args, baseDirectory)?.ScenePath;

    /// <summary>Parses a scene launch and its optional original content root and temporary ownership.</summary>
    internal static SceneViewerSceneOptions? ParseScene(string[] args, string baseDirectory)
    {
        if (args.Length == 0) return null;
        if (args.Length < 2 || args[0] != "--scene" || string.IsNullOrWhiteSpace(args[1]))
            throw new ArgumentException(Usage);

        string? sceneId = null;
        string? contentRoot = null;
        bool temporary = false;
        int index = 2;
        if (index < args.Length && args[index] == "--scene-id")
        {
            if (++index >= args.Length || string.IsNullOrWhiteSpace(args[index])) throw new ArgumentException(Usage);
            sceneId = args[index++];
        }
        if (index < args.Length && args[index] == "--content-root")
        {
            if (++index >= args.Length || string.IsNullOrWhiteSpace(args[index])) throw new ArgumentException(Usage);
            contentRoot = Path.GetFullPath(args[index++], baseDirectory);
            if (!Directory.Exists(contentRoot)) throw new DirectoryNotFoundException($"Scene content directory not found: {contentRoot}");
        }
        if (index < args.Length && args[index] == "--temporary-scene")
        {
            temporary = true;
            index++;
        }
        if (index != args.Length || (temporary && contentRoot is null)) throw new ArgumentException(Usage);
        var path = Normalize(args[1], baseDirectory);
        if (temporary && !IsOwnedTemporaryPath(path))
            throw new ArgumentException("Temporary scene cleanup is only supported for Gondwana Scene Viewer snapshots.");
        return new(path, sceneId, contentRoot, temporary);
    }

    private static bool IsOwnedTemporaryPath(string path)
    {
        var directory = Path.Combine(Path.GetTempPath(), "Gondwana", "SceneViewer");
        var name = Path.GetFileNameWithoutExtension(path);
        return string.Equals(Path.GetDirectoryName(path), Path.GetFullPath(directory), StringComparison.OrdinalIgnoreCase) &&
            name.StartsWith("scene-", StringComparison.Ordinal) &&
            Guid.TryParseExact(name["scene-".Length..], "N", out _);
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
