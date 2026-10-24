using Gondwana.Assets;
using Gondwana.Scenes.GSCN;
using Gondwana.Tooling.Scenes.Editing;

namespace Gondwana.Tooling.Scenes.WinForms.Tests;

/// <summary>
/// Contains regression tests for scene viewer launcher.
/// </summary>
public sealed class SceneViewerLauncherTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ViewerLaunchTests", Guid.NewGuid().ToString("N"));
    /// <summary>Passes physical document and logical member as separate argument tokens.</summary>
    [Fact]
    public void LaunchArgumentsIncludeSceneIdentity()
    {
        var path = Path.Combine(_directory, "shared scenes.gscn");
        var start = SceneViewerLauncher.CreateStartInfo("viewer.exe", path, "dungeon one");
        Assert.Equal(new[] { "--scene", path, "--scene-id", "dungeon one" }, start.ArgumentList);
    }

    /// <summary>Temporary launches retain the selected logical member and original discovery root.</summary>
    [Fact]
    public void TemporaryLaunchArgumentsPreserveContentRoot()
    {
        var path = Path.Combine(_directory, "snapshot.gscn");
        var start = SceneViewerLauncher.CreateStartInfo("viewer.exe", path, "island", _directory, temporaryScene: true);
        Assert.Equal(new[] { "--scene", path, "--scene-id", "island", "--content-root", _directory, "--temporary-scene" }, start.ArgumentList);
    }

    /// <summary>Snapshotting a packed scene keeps multi-member identities and rebase references without changing document state.</summary>
    [Fact]
    public void PackedSceneSnapshotDoesNotAdoptOrSaveOriginal()
    {
        var scenePath = Path.Combine(_directory, "world.gscn");
        SceneDefinitionSerializer.SaveAll(scenePath, [
            new SceneDefinition { ID = "other" },
            new SceneDefinition { ID = "island", TilesheetSources = [SceneTilesheetSourceDefinition.Packed("terrain", "assets.gaf", "sheets/terrain.gts")] }
        ]);
        var archivePath = Path.Combine(_directory, "world.gaf");
        using var archive = AssetsFile.LoadOrCreate(archivePath, null, false, register: false);
        archive.Add(AssetTypes.SceneDefinition, scenePath, "scenes/world.gscn");
        archive.Save();
        var originalBytes = File.ReadAllBytes(archivePath);
        var document = SceneDocument.Open(archive, "scenes/world.gscn");
        document.SelectDefinition(document.Definitions[1]);
        document.RenameDefinition("island-edited");
        var snapshot = SceneViewerLauncher.CreateTemporaryScene(document);
        try
        {
            Assert.EndsWith(".gscn", snapshot);
            Assert.Contains(Path.Combine("Gondwana", "SceneViewer"), snapshot);
            var saved = SceneDefinitionSerializer.LoadAll(snapshot);
            Assert.Equal(new[] { "other", "island-edited" }, saved.Select(scene => scene.ID));
            var reference = Assert.Single(saved[1].TilesheetSources);
            Assert.Equal(Path.Combine(_directory, "assets.gaf"), Path.GetFullPath(reference.AssetsFilePath!, Path.GetDirectoryName(snapshot)!));
            Assert.True(document.IsDirty);
            Assert.Null(document.FilePath);
            Assert.Equal(archivePath, document.PackedLocation!.Path);
            Assert.Equal(originalBytes, File.ReadAllBytes(archivePath));
        }
        finally { File.Delete(snapshot); }
    }

    /// <summary>
    /// Initializes a new instance of the <c>SceneViewerLauncherTests</c> class.
    /// </summary>
    public SceneViewerLauncherTests() => Directory.CreateDirectory(_directory);

    /// <summary>
    /// Verifies clean saved document launches without save prompt.
    /// </summary>
    [Fact]
    public void CleanSavedDocumentLaunchesWithoutSavePrompt()
    {
        var document = SceneDocument.Create(_directory);
        document.Save(Path.Combine(_directory, "saved scene.gscn"));
        string? launched = null;
        Assert.True(SceneViewerLauncher.ViewSavedScene(document, () => throw new InvalidOperationException(),
            () => throw new InvalidOperationException(), path => launched = path));
        Assert.Equal(document.FilePath, launched);
    }

    /// <summary>
    /// Verifies dirty document launches only after successful save.
    /// </summary>
    /// <param name="confirm">The confirm value for this test case.</param>
    /// <param name="save">The save value for this test case.</param>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void DirtyDocumentLaunchesOnlyAfterSuccessfulSave(bool confirm, bool save)
    {
        var document = SceneDocument.Create(_directory);
        document.Save(Path.Combine(_directory, "saved.gscn"));
        document.MarkChanged();
        bool launched = false;
        bool result = SceneViewerLauncher.ViewSavedScene(document, () => confirm, () =>
        {
            if (save) document.Save(document.FilePath!);
            return save;
        }, _ => launched = true);
        Assert.Equal(confirm && save, result);
        Assert.Equal(confirm && save, launched);
    }

    /// <summary>
    /// Verifies new document requires actual saved path.
    /// </summary>
    [Fact]
    public void NewDocumentRequiresActualSavedPath()
    {
        var document = SceneDocument.Create(_directory);
        Assert.False(SceneViewerLauncher.ViewSavedScene(document, () => true, () => true,
            _ => throw new InvalidOperationException()));
        Assert.True(SceneViewerLauncher.ViewSavedScene(document, () => true, () =>
        {
            document.Save(Path.Combine(_directory, "new.gscn"));
            return true;
        }, _ => { }));
    }

    /// <summary>
    /// Verifies arguments preserve spaces without shell quoting.
    /// </summary>
    [Fact]
    public void ArgumentsPreserveSpacesWithoutShellQuoting()
    {
        string scene = Path.Combine(_directory, "a scene & name.gscn");
        var start = SceneViewerLauncher.CreateStartInfo("viewer.exe", scene);
        Assert.False(start.UseShellExecute);
        Assert.Equal(new[] { "--scene", scene }, start.ArgumentList);
        Assert.Equal(string.Empty, start.Arguments);
    }

    /// <summary>
    /// Verifies resolves matching repository build and published subfolder.
    /// </summary>
    [Fact]
    public void ResolvesMatchingRepositoryBuildAndPublishedSubfolder()
    {
        string editor = Path.Combine(_directory, "Tooling", "Gondwana.Tooling.Scenes.WinForms", "bin", "Release", "net8.0-windows");
        Directory.CreateDirectory(editor);
        string viewer = Path.Combine(_directory, "Tooling", SceneViewerLauncher.Component, "bin", "Release", "net8.0-windows", SceneViewerLauncher.Component + ".exe");
        Directory.CreateDirectory(Path.GetDirectoryName(viewer)!);
        File.WriteAllText(viewer, "");
        Assert.Equal(viewer, SceneViewerLauncher.Locate(editor));
        string published = Path.Combine(editor, "SceneViewer", SceneViewerLauncher.Component + ".exe");
        Directory.CreateDirectory(Path.GetDirectoryName(published)!);
        File.WriteAllText(published, "");
        Assert.Equal(published, SceneViewerLauncher.Locate(editor));
    }

    /// <inheritdoc/>
    public void Dispose() => Directory.Delete(_directory, true);
}
