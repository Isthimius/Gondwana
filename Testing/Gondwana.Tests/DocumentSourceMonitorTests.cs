using System.Text;
using Gondwana.Assets;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Scenes.GSCN;
using Gondwana.Tooling.Assets;

namespace Gondwana.Tests;

public sealed class DocumentSourceMonitorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "SourceMonitor-" + Guid.NewGuid());
    public DocumentSourceMonitorTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);
    private AssetLocation Loose(string name = "sheet.gts", AssetTypes type = AssetTypes.TilesheetDefinition) => new(type, Path.Combine(_root, name));
    private static void Write(AssetLocation location, string text) => File.WriteAllText(location.Path, text);
    private static DocumentSourceMonitor Manual() => new(TimeSpan.FromHours(1), enableWatchers: false);

    [Theory]
    [InlineData("gts")]
    [InlineData("gani")]
    [InlineData("gscn")]
    public void ContentHashDetectsSameLengthAndTimestamp(string extension)
    {
        var location = Loose("source." + extension);
        Write(location, "first");
        var timestamp = File.GetLastWriteTimeUtc(location.Path);
        using var monitor = Manual();
        var changes = new List<DocumentSourceChange>();
        using var source = monitor.Register(location, DocumentSourceVersion.Read(location), DocumentSourceRole.Primary, changes.Add);
        Write(location, "other");
        File.SetLastWriteTimeUtc(location.Path, timestamp);
        Assert.Equal(DocumentSourceChangeKind.ContentChanged, source.Verify().Kind);
        source.Verify();
        Assert.Single(changes);
        source.Accept(changes[0].Current);
        Assert.Equal(DocumentSourceChangeKind.Unchanged, source.Verify().Kind);
    }

    [Fact]
    public void DeleteRenameRecreateAndCleanup()
    {
        var location = Loose();
        Write(location, "old");
        using var monitor = Manual();
        var changes = new List<DocumentSourceChange>();
        var source = monitor.Register(location, DocumentSourceVersion.Read(location), DocumentSourceRole.Dependency, changes.Add);
        using var second = monitor.Register(location, source.Accepted, DocumentSourceRole.Primary, _ => { });
        Assert.Equal(1, monitor.DirectoryCount);
        File.Move(location.Path, location.Path + ".moved");
        monitor.Signal(location.Path, location.Path + ".moved");
        var renamed = source.Verify();
        Assert.Equal(DocumentSourceChangeKind.Renamed, renamed.Kind);
        Assert.Equal(location.Path + ".moved", renamed.RenamedTo);
        Write(location, "new");
        Assert.Equal(DocumentSourceChangeKind.ContentChanged, source.Verify().Kind);
        File.Delete(location.Path);
        Assert.Equal(DocumentSourceChangeKind.Deleted, source.Verify().Kind);
        source.Dispose();
        Assert.Equal(1, monitor.DirectoryCount);
        second.Dispose();
        Assert.Equal(0, monitor.DirectoryCount);
        Assert.Throws<ObjectDisposedException>(() => source.Verify());
        monitor.Dispose();
        Assert.Throws<ObjectDisposedException>(() => monitor.Register(location, source.Accepted, DocumentSourceRole.Primary, _ => { }));
    }

    [Fact]
    public void SharingFailureRecoversWithoutAcceptingIt()
    {
        var location = Loose();
        Write(location, "old");
        using var monitor = Manual();
        var changes = new List<DocumentSourceChange>();
        using var source = monitor.Register(location, DocumentSourceVersion.Read(location), DocumentSourceRole.Primary, changes.Add);
        using (var locked = new FileStream(location.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.Equal(DocumentSourceChangeKind.Inaccessible, source.Verify().Kind);
            source.Verify();
            Assert.Single(changes);
        }
        Assert.Equal(DocumentSourceChangeKind.Unchanged, source.Verify().Kind);
        Assert.Equal(2, changes.Count);
    }

    [Fact]
    public void OwnSaveUsesStagedHashAndDoesNotHideAdjacentExternalWrite()
    {
        var location = Loose();
        TilesheetDefinitionSerializer.SaveAll(location.Path, [new() { Name = "old" }]);
        using var monitor = Manual();
        var changes = new List<DocumentSourceChange>();
        using var source = monitor.Register(location, DocumentSourceVersion.Read(location), DocumentSourceRole.Primary, changes.Add);
        source.Save(check => TilesheetDefinitionSerializer.SaveAll(location.Path, [new() { Name = "saved" }], check));
        Assert.Equal(DocumentSourceChangeKind.Unchanged, source.Verify().Kind);
        Assert.Empty(changes);
        Write(location, "external");
        Assert.Equal(DocumentSourceChangeKind.ContentChanged, source.Verify().Kind);
        Assert.Single(changes);
        Assert.Throws<DocumentSourceConflictException>(() => source.Save(check =>
            TilesheetDefinitionSerializer.SaveAll(location.Path, [new() { Name = "overwrite" }], check)));
        Assert.Equal("external", File.ReadAllText(location.Path));
    }

    [Theory]
    [InlineData("gts")]
    [InlineData("gani")]
    [InlineData("gscn")]
    public void ReplacementDuringPreparationIsRejectedAndStagingRemoved(string format)
    {
        var location = Loose("source." + format);
        Write(location, "original");
        var version = DocumentSourceVersion.Read(location);
        void Replace()
        {
            var replacement = location.Path + ".replacement";
            File.WriteAllText(replacement, "external");
            File.Replace(replacement, location.Path, destinationBackupFileName: null);
        }
        IEnumerable<T> Prepare<T>(T member) { Replace(); yield return member; }
        Assert.Throws<DocumentSourceConflictException>(() => DocumentSourceSave.Save(location, version, check =>
        {
            switch (format)
            {
                case "gts": TilesheetDefinitionSerializer.SaveAll(location.Path, Prepare(new TilesheetDefinition { Name = "new" }), check); break;
                case "gani": AnimationDefinitionSerializer.SaveAll(location.Path, Prepare(new AnimationDefinition { Key = "new" }), check); break;
                case "gscn": SceneDefinitionSerializer.SaveAll(location.Path, Prepare(new SceneDefinition { ID = "new" }), check); break;
            }
        }));
        Assert.Equal("external", File.ReadAllText(location.Path));
        Assert.Single(Directory.GetFiles(_root));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ArchivePhysicalAndEntryVersionsRemainDistinct(bool encrypted)
    {
        var path = Path.Combine(_root, "assets.gaf");
        var password = encrypted ? "test-password" : null;
        void Save(string a, string b)
        {
            using var archive = AssetsFile.LoadOrCreate(path, password, encrypted, register: false);
            using var first = new MemoryStream(Encoding.UTF8.GetBytes(a));
            using var second = new MemoryStream(Encoding.UTF8.GetBytes(b));
            archive.Add(AssetTypes.Image, "a", first);
            archive.Add(AssetTypes.Image, "b", second);
            archive.Save();
        }
        Save("a", "b");
        var a = new AssetLocation(AssetTypes.Image, path, "a");
        var b = new AssetLocation(AssetTypes.Image, path, "b");
        using var monitor = Manual();
        using var first = monitor.Register(a, DocumentSourceVersion.Read(a, password), DocumentSourceRole.Primary, _ => { }, password);
        using var second = monitor.Register(b, DocumentSourceVersion.Read(b, password), DocumentSourceRole.Dependency, _ => { }, password);
        Save("a", "changed");
        Assert.Equal(DocumentSourceChangeKind.ContainerChanged, first.Verify().Kind);
        Assert.Equal(DocumentSourceChangeKind.ContentChanged, second.Verify().Kind);
        Assert.Equal(DocumentSourceStatus.Missing, DocumentSourceVersion.Read(new(AssetTypes.Image, path, "absent"), password).Status);
        if (encrypted) Assert.Equal(DocumentSourceStatus.Inaccessible, DocumentSourceVersion.Read(a, "wrong").Status);
        Write(new(AssetTypes.Misc, path), "partial zip");
        Assert.Equal(DocumentSourceChangeKind.Inaccessible, first.Verify().Kind);
    }

    [Fact]
    public void NewDestinationCreatedAfterFinalCheckIsNotOverwritten()
    {
        var location = Loose();
        var absent = DocumentSourceVersion.Read(location);
        Assert.ThrowsAny<IOException>(() => DocumentSourceSave.Save(location, absent, check =>
            TilesheetDefinitionSerializer.SaveAll(location.Path, [new() { Name = "ours" }], staging =>
            {
                check(staging);
                Write(location, "theirs");
            })));
        Assert.Equal("theirs", File.ReadAllText(location.Path));
        Assert.Single(Directory.GetFiles(_root));
    }

    [Fact]
    public void FailedSavePreservesDirtyHistoryAndAcceptedVersion()
    {
        var location = Loose();
        TilesheetDefinitionSerializer.SaveAll(location.Path, [new() { Name = "one" }]);
        var document = Tooling.Studio.Tilesheets.WinForms.Editing.TilesheetDocument.Open(location.Path);
        var accepted = document.SourceVersion;
        document.Definition.Name = "edited";
        document.MarkChanged();
        Write(location, "external");
        Assert.Throws<DocumentSourceConflictException>(() => document.Save(location.Path, allowInvalid: true));
        Assert.True(document.IsDirty);
        Assert.Equal(accepted, document.SourceVersion);
        Assert.Equal("edited", document.Definition.Name);
    }

    [Fact]
    public void ConsecutiveSignalsRequireStableContentAndCoalesce()
    {
        var location = Loose();
        Write(location, "initial");
        using var monitor = Manual();
        var changes = new List<DocumentSourceChange>();
        using var source = monitor.Register(location, DocumentSourceVersion.Read(location), DocumentSourceRole.Primary, changes.Add);
        for (int i = 0; i < 20; i++)
        {
            Write(location, "version " + i);
            monitor.Signal(location.Path);
            monitor.VerifyPending();
        }
        Assert.Empty(changes);
        monitor.VerifyPending();
        monitor.VerifyPending();
        Assert.Single(changes);
        Assert.Equal(DocumentSourceVersion.Read(location), changes[0].Current);
    }

    [Theory]
    [InlineData("gts")]
    [InlineData("gani")]
    [InlineData("gscn")]
    [InlineData("gspr")]
    [InlineData("gsnd")]
    public void ExistingDocumentModelsRejectStaleLooseSaves(string format)
    {
        var location = Loose("source." + format);
        Write(location, format == "gts" ? "{\"Name\":\"one\"}" : format == "gscn" ? "{\"ID\":\"one\"}" : "{\"Key\":\"one\"}");
        Action save = format switch
        {
            "gts" => OpenTilesheet(),
            "gani" => OpenAnimation(),
            "gscn" => OpenScene(),
            "gspr" => OpenSprite(),
            _ => OpenAudio()
        };
        Action OpenTilesheet() { var d = Tooling.Studio.Tilesheets.WinForms.Editing.TilesheetDocument.Open(location.Path); return () => d.Save(location.Path, allowInvalid: true); }
        Action OpenAnimation() { var d = Tooling.Animations.Editing.AnimationDocument.Open(location.Path); return () => d.Save(location.Path, allowInvalid: true); }
        Action OpenScene() { var d = Tooling.Scenes.Editing.SceneDocument.Open(location.Path); return () => d.Save(location.Path, allowInvalid: true); }
        Action OpenSprite() { var d = Tooling.Sprites.Editing.SpriteDocument.Open(location.Path); return () => d.Save(location.Path, allowInvalid: true); }
        Action OpenAudio() { var d = Tooling.Audio.Editing.AudioDocument.Open(location.Path); return () => d.Save(location.Path, allowInvalid: true); }
        save(); // Successful save updates the accepted version, so another save is valid.
        save();
        Write(location, "external");
        Assert.Throws<DocumentSourceConflictException>(save);
        Assert.Equal("external", File.ReadAllText(location.Path));
    }

    [Fact]
    public async Task RealWatcherCoalescesBurstAndAtomicReplacement()
    {
        var location = Loose();
        Write(location, "initial");
        using var monitor = new DocumentSourceMonitor(TimeSpan.FromMilliseconds(30));
        var completion = new TaskCompletionSource<DocumentSourceChange>(TaskCreationOptions.RunContinuationsAsynchronously);
        var count = 0;
        using var source = monitor.Register(location, DocumentSourceVersion.Read(location), DocumentSourceRole.Primary,
            change => { Interlocked.Increment(ref count); completion.TrySetResult(change); });
        var replacement = location.Path + ".tmp";
        File.WriteAllText(replacement, "final");
        File.Replace(replacement, location.Path, destinationBackupFileName: null);
        var change = await completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(DocumentSourceChangeKind.ContentChanged, change.Kind);
        Assert.Equal(DocumentSourceVersion.Read(location), change.Current);
        source.Verify();
        Assert.Equal(1, Volatile.Read(ref count));
    }

    [Fact]
    public void ExplicitInaccessibleVerificationSchedulesRecoveryWithoutAFileEvent()
    {
        var location = Loose();
        Write(location, "original");
        using var monitor = Manual();
        var changes = new List<DocumentSourceChange>();
        using var source = monitor.Register(location, DocumentSourceVersion.Read(location), DocumentSourceRole.Primary, changes.Add);
        monitor.VerifyPending();
        monitor.VerifyPending();
        Assert.False(source.Pending);
        using (var locked = new FileStream(location.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.Equal(DocumentSourceChangeKind.Inaccessible, source.Verify().Kind);
            Assert.True(source.Pending);
        }
        monitor.VerifyPending();
        monitor.VerifyPending();
        Assert.False(source.Pending);
        Assert.Equal(2, changes.Count);
        Assert.Equal(DocumentSourceChangeKind.Inaccessible, changes[0].Kind);
        Assert.Equal(DocumentSourceChangeKind.Unchanged, changes[1].Kind);
    }

    [Fact]
    public void RemovedArchiveEntryCannotFallBackToDifferentExtension()
    {
        var path = Path.Combine(_root, "assets.gaf");
        using (var archive = AssetsFile.LoadOrCreate(path, password: null, encrypt: false, register: false))
        {
            using var png = new MemoryStream(Encoding.UTF8.GetBytes("png-data"));
            using var jpg = new MemoryStream(Encoding.UTF8.GetBytes("jpg-data"));
            archive.Add(AssetTypes.Image, "image.png", png);
            archive.Add(AssetTypes.Image, "image.jpg", jpg);
            archive.Save();
        }
        var location = new AssetLocation(AssetTypes.Image, path, "image.png");
        Assert.Equal(DocumentSourceStatus.Available, DocumentSourceVersion.Read(location).Status);
        using (var archive = AssetsFile.LoadOrCreate(path, password: null, encrypt: false, register: false))
        {
            archive.Remove(AssetTypes.Image, "image.png");
            archive.Save();
        }
        var current = DocumentSourceVersion.Read(location);
        Assert.Equal(DocumentSourceStatus.Missing, current.Status);
        Assert.NotNull(current.PhysicalHash);
        Assert.Null(current.ContentHash);
        Assert.Equal(DocumentSourceStatus.Available,
            DocumentSourceVersion.Read(new AssetLocation(AssetTypes.Image, path, "image.jpg")).Status);
    }

    [Fact]
    public void VerificationPassSharesArchiveSnapshotBetweenRegistrations()
    {
        var path = Path.Combine(_root, "shared.gaf");
        void WriteArchive(string content)
        {
            using var archive = AssetsFile.LoadOrCreate(path, password: null, encrypt: false, register: false);
            using var png = new MemoryStream(Encoding.UTF8.GetBytes(content));
            using var jpg = new MemoryStream(Encoding.UTF8.GetBytes(content));
            archive.Add(AssetTypes.Image, "image.png", png);
            archive.Add(AssetTypes.Image, "image.jpg", jpg);
            archive.Save();
        }
        WriteArchive("first");
        var pngLocation = new AssetLocation(AssetTypes.Image, path, "image.png");
        var jpgLocation = new AssetLocation(AssetTypes.Image, path, "image.jpg");
        using var monitor = Manual();
        var pngChanges = new List<DocumentSourceChange>();
        var jpgChanges = new List<DocumentSourceChange>();
        using var pngSource = monitor.Register(pngLocation, DocumentSourceVersion.Read(pngLocation), DocumentSourceRole.Primary,
            change =>
            {
                pngChanges.Add(change);
                // Replaces the archive during callback delivery, but this pass must remain consistent.
                WriteArchive("third");
            });
        using var jpgSource = monitor.Register(jpgLocation, DocumentSourceVersion.Read(jpgLocation), DocumentSourceRole.Dependency, jpgChanges.Add);
        WriteArchive("second");
        monitor.VerifyPending();
        monitor.VerifyPending();
        Assert.Single(pngChanges);
        Assert.Single(jpgChanges);
        Assert.Equal(pngChanges[0].Current.PhysicalHash, jpgChanges[0].Current.PhysicalHash);
        Assert.Equal(pngChanges[0].Current.ContentHash, jpgChanges[0].Current.ContentHash);
    }

}

