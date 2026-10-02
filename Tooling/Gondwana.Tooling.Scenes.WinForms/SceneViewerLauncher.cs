using System.Diagnostics;

namespace Gondwana.Tooling.Scenes.WinForms;

internal static class SceneViewerLauncher
{
    internal const string Component = "Gondwana.Tooling.SceneViewer.WinForms";

    internal static void Launch(string scenePath)
    {
        using var process = Process.Start(CreateStartInfo(Locate(AppContext.BaseDirectory), scenePath))
            ?? throw new InvalidOperationException("The Scene Viewer process could not be started.");
    }

    internal static ProcessStartInfo CreateStartInfo(string executable, string scenePath)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false };
        start.ArgumentList.Add("--scene");
        start.ArgumentList.Add(Path.GetFullPath(scenePath));
        return start;
    }

    internal static string Locate(string baseDirectory)
    {
        var root = Path.GetFullPath(baseDirectory);
        // Published tooling: one shared folder, a viewer subfolder, or sibling tool folders.
        var candidates = new List<string>
        {
            Path.Combine(root, Component + ".exe"),
            Path.Combine(root, "SceneViewer", Component + ".exe"),
            Path.Combine(root, Component, Component + ".exe"),
            Path.GetFullPath(Path.Combine(root, "..", Component, Component + ".exe"))
        };

        // Repository builds: retain the exact bin/<configuration>/<framework>[/rid]
        // suffix of the running editor. Do not guess between stale build outputs.
        for (var directory = new DirectoryInfo(root); directory is not null; directory = directory.Parent)
        {
            if (directory.Name != "bin" || directory.Parent?.Parent?.Name != "Tooling")
                continue;
            var suffix = Path.GetRelativePath(directory.FullName, root);
            candidates.Add(Path.Combine(directory.Parent.Parent.FullName, Component, "bin", suffix, Component + ".exe"));
            break;
        }

        return candidates.FirstOrDefault(File.Exists)
            ?? throw new FileNotFoundException(
                $"{Component} was not found. Build the Scene Viewer in the same configuration as this editor, " +
                "or deploy its complete output in the SceneViewer subfolder beside this tool.");
    }

    internal static bool ViewSavedScene(
        Editing.SceneDocument document,
        Func<bool> confirmSave,
        Func<bool> save,
        Action<string> launch)
    {
        if (document.IsDirty || document.FilePath is null)
        {
            if (!confirmSave() || !save())
                return false;
        }
        if (document.IsDirty || document.FilePath is null)
            return false;
        launch(document.FilePath);
        return true;
    }
}
