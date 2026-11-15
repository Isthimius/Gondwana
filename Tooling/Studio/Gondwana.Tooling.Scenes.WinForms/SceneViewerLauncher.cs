using System.Diagnostics;
using Gondwana.Scenes.GSCN;
using Gondwana.Tooling.Scenes.Editing;

namespace Gondwana.Tooling.Scenes.WinForms;

internal static class SceneViewerLauncher
{
    internal const string Component = "Gondwana.Tooling.SceneViewer.WinForms";

    /// <summary>Opens a saved scene document in the viewer.</summary>
    /// <param name="scenePath">The physical document path.</param>
    /// <param name="sceneId">The logical member identity, or null for a singleton.</param>
    /// <param name="contentRoot">Original content directory when viewing a snapshot.</param>
    /// <param name="temporaryScene">Whether the viewer should delete the snapshot on exit.</param>
    internal static void Launch(string scenePath, string? sceneId = null, string? contentRoot = null, bool temporaryScene = false)
    {
        using var process = Process.Start(CreateStartInfo(Locate(AppContext.BaseDirectory), scenePath, sceneId, contentRoot, temporaryScene))
            ?? throw new InvalidOperationException("The Scene Viewer process could not be started.");
    }

    /// <summary>Writes a packed scene's current editor contents to a unique temporary file without adopting or saving them.</summary>
    /// <param name="document">The packed scene document, including unsaved changes.</param>
    /// <returns>The temporary filename, whose lifetime belongs to the viewer.</returns>
    internal static string CreateTemporaryScene(SceneDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var packed = document.PackedLocation ?? throw new InvalidOperationException("Only packed scenes need a temporary viewer snapshot.");
        var directory = Path.Combine(Path.GetTempPath(), "Gondwana", "SceneViewer");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"scene-{Guid.NewGuid():N}.gscn");

        // SaveAll rebases references from each member's original packed directory.
        // Do not mutate the editor document or its packed provenance.
        var members = document.Definitions.Select(member =>
        {
            var copy = SceneDefinitionSerializer.FromJson(SceneDefinitionSerializer.ToJson(member));
            copy.Source = SceneDefinitionSource.PackedDefinitionFile(packed.Path, packed.AssetEntryName!);
            return copy;
        });
        try
        {
            SceneDefinitionSerializer.SaveAll(path, members);
            return path;
        }
        catch
        {
            if (File.Exists(path)) File.Delete(path);
            throw;
        }
    }

    /// <summary>Builds a viewer command with separate document and member arguments.</summary>
    /// <param name="executable">The viewer executable.</param>
    /// <param name="scenePath">The physical document path.</param>
    /// <param name="sceneId">The logical member identity, or null for a singleton.</param>
    /// <param name="contentRoot">Original content directory when viewing a snapshot.</param>
    /// <param name="temporaryScene">Whether the viewer should delete the snapshot on exit.</param>
    /// <returns>The process start information.</returns>
    internal static ProcessStartInfo CreateStartInfo(string executable, string scenePath, string? sceneId = null, string? contentRoot = null, bool temporaryScene = false)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false };
        start.ArgumentList.Add("--scene");
        start.ArgumentList.Add(Path.GetFullPath(scenePath));
        if (sceneId is not null)
        {
            start.ArgumentList.Add("--scene-id");
            start.ArgumentList.Add(sceneId);
        }
        if (contentRoot is not null)
        {
            start.ArgumentList.Add("--content-root");
            start.ArgumentList.Add(Path.GetFullPath(contentRoot));
        }
        if (temporaryScene)
            start.ArgumentList.Add("--temporary-scene");
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
