namespace Gondwana.Tooling.SceneViewer.WinForms;

internal static class SceneViewerArguments
{
    internal const string Usage = "Usage: Gondwana.Tooling.SceneViewer.WinForms.exe --scene <file.gscn>";

    // The OS has already removed command-line quoting from args.
    internal static string? Parse(string[] args, string baseDirectory)
    {
        if (args.Length == 0)
            return null;
        if (args.Length != 2 || args[0] != "--scene" || string.IsNullOrWhiteSpace(args[1]))
            throw new ArgumentException(Usage);
        return Normalize(args[1], baseDirectory);
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
