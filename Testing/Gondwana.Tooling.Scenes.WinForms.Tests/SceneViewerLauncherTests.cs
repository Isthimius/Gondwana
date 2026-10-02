using Gondwana.Tooling.Scenes.Editing;

namespace Gondwana.Tooling.Scenes.WinForms.Tests;

public sealed class SceneViewerLauncherTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "ViewerLaunchTests", Guid.NewGuid().ToString("N"));
    public SceneViewerLauncherTests() => Directory.CreateDirectory(_directory);

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

    [Fact]
    public void ArgumentsPreserveSpacesWithoutShellQuoting()
    {
        string scene = Path.Combine(_directory, "a scene & name.gscn");
        var start = SceneViewerLauncher.CreateStartInfo("viewer.exe", scene);
        Assert.False(start.UseShellExecute);
        Assert.Equal(new[] { "--scene", scene }, start.ArgumentList);
        Assert.Equal(string.Empty, start.Arguments);
    }

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

    public void Dispose() => Directory.Delete(_directory, true);
}
