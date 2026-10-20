using Gondwana.Tooling.WinForms;

namespace Gondwana.Tests;

public sealed class WorkspaceSessionTests : IDisposable
{
    private readonly string _directory = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "GondwanaSessions-" + Guid.NewGuid())).FullName;
    private readonly List<string> _messages = [];
    private WorkspaceSessionStore Store(string application = "studio") => new(_messages.Add, application, _directory);
    private WorkspaceSessionDocument FileDocument(string name)
    {
        string path = Path.Combine(_directory, name);
        File.WriteAllText(path, "original content");
        return WorkspaceSessionDocument.FromPath(path)!;
    }

    [Fact]
    public void RoundTripRestoresDirectoryOrderActiveDocumentAndCurrentDiskContents()
    {
        var first = FileDocument("first.gts");
        var second = FileDocument("second.gscn");
        var session = new WorkspaceSession { WorkingDirectory = _directory, Documents = [second, first], ActiveDocument = first.Path };
        Assert.True(Store().Write(session));
        File.WriteAllText(first.Path, "changed after shutdown");
        var opened = new List<string>();
        string? directory = null;
        string? active = null;
        WorkspaceSessionRestore.Restore(Store().Read(), ["gts", "gscn"], path => directory = path,
            document => opened.Add(File.ReadAllText(document.Path)), path => active = path, _messages.Add);
        Assert.Equal(_directory, directory);
        Assert.Equal(new[] { "original content", "changed after shutdown" }, opened);
        Assert.Equal(first.Path, active);
        Assert.Empty(_messages);
        string metadata = File.ReadAllText(Store().FilePath);
        Assert.DoesNotContain("original content", metadata);
        Assert.DoesNotContain("changed after shutdown", metadata);
    }

    [Fact]
    public void StaleDuplicateMalformedUnsupportedAndFailedDocumentsDoNotBlockOtherFiles()
    {
        var good = FileDocument("good.gts");
        var failed = FileDocument("failed.gts");
        var unavailable = FileDocument("unsupported.gscn");
        var session = new WorkspaceSession
        {
            WorkingDirectory = Path.Combine(_directory, "removed-directory"),
            Documents = [new(Path.Combine(_directory, "removed.gts"), "gts"), new("\0invalid", "gts"),
                unavailable, failed, failed, good, good with { Path = Path.Combine(_directory, ".", "good.gts") }],
            ActiveDocument = failed.Path
        };
        var opened = new List<string>();
        int failures = 0;
        WorkspaceSessionRestore.Restore(session, ["gts"], _ => Assert.Fail("Missing directory restored"),
            document => { if (document == failed) { failures++; throw new InvalidDataException("Changed file is corrupt"); } opened.Add(document.Path); },
            _ => Assert.Fail("Failed active document activated"), _messages.Add);
        Assert.Equal(new[] { good.Path }, opened);
        Assert.Equal(1, failures);
        Assert.Equal(5, _messages.Count);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void OptOutAndEmptyStartupDoNotTouchWorkspace(bool restore, bool empty)
    {
        var session = new WorkspaceSession { RestoreOnStartup = restore, WorkingDirectory = _directory, Documents = [FileDocument("file.gts")] };
        Assert.True(Store().Write(session));
        WorkspaceSessionRestore.Restore(Store().Read(), ["gts"], _ => Assert.Fail(), _ => Assert.Fail(), _ => Assert.Fail(), _messages.Add, empty);
        Assert.Equal(restore, Store().Read().RestoreOnStartup);
        Assert.Empty(_messages);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{\"Version\":999}")]
    [InlineData("{\"Documents\":42}")]
    public void CorruptSessionFallsBackWithoutPreventingStartup(string content)
    {
        var store = Store();
        Directory.CreateDirectory(Path.GetDirectoryName(store.FilePath)!);
        File.WriteAllText(store.FilePath, content);
        Assert.Empty(store.Read().Documents);
        Assert.Single(_messages);
        Assert.Contains("--empty-workspace", _messages[0]);
    }

    [Fact]
    public void NullDocumentArrayAndNullMembersAreRecoverable()
    {
        var store = Store();
        Assert.True(store.Write(new()));
        File.WriteAllText(store.FilePath, "{\"Documents\":null}");
        WorkspaceSessionRestore.Restore(store.Read(), ["gts"], _ => Assert.Fail(), _ => Assert.Fail(), _ => Assert.Fail(), _messages.Add);
        File.WriteAllText(store.FilePath, "{\"Documents\":[null]}");
        WorkspaceSessionRestore.Restore(store.Read(), ["gts"], _ => Assert.Fail(), _ => Assert.Fail(), _ => Assert.Fail(), _messages.Add);
        Assert.Empty(_messages);
    }

    [Fact]
    public void ApplicationsKeepIndependentPreferencesAndWritesLeaveNoTemporaryFiles()
    {
        Assert.True(Store("studio").Write(new() { RestoreOnStartup = false }));
        Assert.True(Store("standalone").Write(new() { StartEmptyOnce = true }));
        Assert.False(Store("studio").Read().RestoreOnStartup);
        Assert.True(Store("standalone").Read().RestoreOnStartup);
        Assert.True(Store("standalone").Read().StartEmptyOnce);
        Assert.Empty(Directory.GetFiles(_directory, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public void UnwritablePreferencesReportFailureAndDoNotThrow()
    {
        string blocker = Path.Combine(_directory, "blocked");
        File.WriteAllText(blocker, "not a directory");
        var store = new WorkspaceSessionStore(_messages.Add, "studio", blocker);
        Assert.False(store.Write(new()));
        Assert.NotEmpty(_messages);
    }

    [Theory]
    [InlineData("archive.ZIP", "gaf")]
    [InlineData("archive.gaf", "gaf")]
    [InlineData("tiles.GTS", "gts")]
    [InlineData("animation.gani", "gani")]
    [InlineData("sound.gsnd", "gsnd")]
    [InlineData("scene.gscn", "gscn")]
    [InlineData("sprites.gspr", "gspr")]
    public void SavedIdentityContainsOnlyPathAndSupportedFormat(string path, string format)
    {
        var document = WorkspaceSessionDocument.FromPath(Path.Combine(_directory, path));
        Assert.Equal(format, document!.Format);
        Assert.True(Path.IsPathFullyQualified(document.Path));
        Assert.Null(WorkspaceSessionDocument.FromPath(null));
        Assert.Null(WorkspaceSessionDocument.FromPath("temporary.bin"));
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
