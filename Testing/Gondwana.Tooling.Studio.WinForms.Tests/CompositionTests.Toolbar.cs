using System.Text;
using Gondwana.Assets;
using Gondwana.Tooling.Assets.WinForms;
using Gondwana.Tooling.Audio.WinForms;
using Gondwana.Tooling.Studio.WinForms.Documents;

namespace Gondwana.Tooling.Studio.WinForms.Tests;

/// <summary>Regression coverage for the Studio-level toolbar and its document commands.</summary>
public sealed partial class CompositionTests
{
    [Fact]
    public void GlobalToolbarContainsShellCommandsAndTracksActiveDocument() => RunSta(directory =>
    {
        using var studio = Host(directory);
        var bar = studio.StudioToolbar;
        Assert.Equal(DockStyle.Top, bar.Dock);
        Assert.Equal(new Size(24, 24), bar.ImageScalingSize);
        Assert.All(bar.Items.OfType<ToolStripButton>(), item =>
        {
            Assert.False(item.AutoSize);
            Assert.Equal(new Size(32, 32), item.Size);
        });
        Assert.Equal(3, bar.Items.OfType<ToolStripSeparator>().Count());
        Assert.Equal(
            new[] { "Open", "Save", "SaveAll", "WorkingDirectory", "Refresh", "Preview", "Validate" },
            bar.Items.OfType<ToolStripButton>().Select(item => item.Name));

        var create = Assert.Single(bar.Items.OfType<ToolStripDropDownButton>());
        Assert.Equal("New", create.Name);
        Assert.False(create.AutoSize);
        Assert.Equal(new Size(32, 32), create.Size);
        Assert.Equal(MainForm.NewFileFormats.Select(format => format.Label),
            create.DropDownItems.OfType<ToolStripMenuItem>().Select(item => item.Text));

        ToolStripButton Get(string name) => bar.Items.OfType<ToolStripButton>().Single(item => item.Name == name);
        Assert.False(Get("Save").Enabled);
        Assert.False(Get("SaveAll").Enabled);
        Assert.False(Get("Preview").Enabled);
        Assert.False(Get("Validate").Enabled);

        var animation = studio.NewDocument("gani");
        Assert.True(Get("Save").Enabled);
        Assert.True(Get("SaveAll").Enabled);
        Assert.False(Get("Preview").Enabled);
        Assert.True(Get("Validate").Enabled);

        var scene = studio.NewDocument("gscn");
        Assert.True(Get("Preview").Enabled);
        animation.Activate();
        Application.DoEvents();
        Assert.False(Get("Preview").Enabled);
        scene.Activate();
        Application.DoEvents();
        Assert.True(Get("Preview").Enabled);
    });

    [Fact]
    public void SaveAllPersistsDirtyDocumentsAndRemainsAvailableForPendingEdits() => RunSta(directory =>
    {
        using var studio = Host(directory);
        var animation = studio.NewDocument("gani");
        var sound = studio.NewDocument("gsnd");
        studio.SavePath = document => Path.Combine(directory, "all." + document.Extension);

        Assert.True(AwaitUi(studio.SaveAllDocuments()));
        Assert.True(File.Exists(Path.Combine(directory, "all.gani")));
        Assert.True(File.Exists(Path.Combine(directory, "all.gsnd")));
        Assert.False(animation.Document.Dirty());
        Assert.False(sound.Document.Dirty());

        var saveAll = studio.StudioToolbar.Items.OfType<ToolStripButton>()
            .Single(button => button.Name == "SaveAll");
        // Remains enabled with clean documents so pending PropertyGrid edits can be committed.
        Assert.True(saveAll.Enabled);
        Assert.True(AwaitUi(studio.SaveAllDocuments()));

        var editor = Assert.IsType<AudioEditorControl>(sound.Document.Editor);
        editor.Document.MarkChanged();
        Assert.True(saveAll.Enabled);
        Assert.True(AwaitUi(studio.SaveAllDocuments()));
        Assert.True(saveAll.Enabled);
    });

    [Fact]
    public void SaveAllStopsOnCancelledSaveWithoutWritingOtherDocuments() => RunSta(directory =>
    {
        using var studio = Host(directory);
        var animation = studio.NewDocument("gani");
        var sound = studio.NewDocument("gsnd");
        studio.SavePath = _ => null;

        Assert.False(AwaitUi(studio.SaveAllDocuments()));
        Assert.True(animation.Document.Dirty());
        Assert.True(sound.Document.Dirty());
        Assert.Empty(Directory.GetFiles(directory, "*.gani"));
        Assert.Empty(Directory.GetFiles(directory, "*.gsnd"));
        Assert.True(studio.StudioToolbar.Items.OfType<ToolStripButton>()
            .Single(button => button.Name == "SaveAll").Enabled);
    });

    [Fact]
    public void ValidationUsesCurrentDocumentAndIsUnavailableWithNoDocument() => RunSta(directory =>
    {
        using var studio = Host(directory);
        Assert.Null(studio.ValidateActiveDocument());
        var document = studio.NewDocument("gscn");
        var errors = studio.ValidateActiveDocument();
        Assert.NotNull(errors);
        Assert.Equal(document.Document.Validate(), errors);
        if (errors.Count > 0)
            Assert.True(document.Document.IsPaneVisible("Validation"));
    });
    [Fact]
    public void ArchiveDocumentsDoNotClaimDefinitionValidation() => RunSta(root =>
    {
        var path = NativeArchive(root);
        using var studio = Host(root);
        studio.OpenDocument(path);
        var validate = studio.StudioToolbar.Items.OfType<ToolStripButton>()
            .Single(button => button.Name == "Validate");
        Assert.False(validate.Enabled);
        Assert.Null(studio.ValidateActiveDocument());
        var child = studio.OpenDocument(NativeLocation(path, "gts"));
        Assert.True(validate.Enabled);
        child.Activate();
        Assert.NotNull(studio.ValidateActiveDocument());
    });

    [Fact]
    public void SaveAllWritesGafBeforePackedChildrenRegardlessOfOpenOrder() => RunSta(root =>
    {
        var path = NativeArchive(root);
        using var studio = Host(root);
        // Deliberately open the child first to catch accidental document-list ordering.
        var child = studio.OpenDocument(NativeLocation(path, "gani"));
        RenameFirst(child.Document, "child-edited");
        var parent = studio.OpenDocument(path);
        var asset = (AssetEditorControl)parent.Document.Editor;
        using (var payload = new MemoryStream([4, 5, 6]))
            asset.AssetsFile.Add(AssetTypes.Misc, "unrelated.bin", payload);
        asset.MarkChanged();

        Assert.True(AwaitUi(studio.SaveAllDocuments()));
        Assert.False(parent.Document.Dirty());
        Assert.False(child.Document.Dirty());
        using var saved = StudioArchiveSnapshot.Open(path, null);
        Assert.Equal(new byte[] { 4, 5, 6 },
            StudioArchiveSnapshot.Entry(saved.Archive, new(AssetTypes.Misc, path, "unrelated.bin")));
        Assert.Contains("child-edited", Encoding.UTF8.GetString(
            StudioArchiveSnapshot.Entry(saved.Archive, NativeLocation(path, "gani"))));
    });

    [Fact]
    public void SaveAllDoesNotOverwriteConflictingPackedEntryFromGaf() => RunSta(root =>
    {
        var path = NativeArchive(root);
        using var studio = Host(root);
        var child = studio.OpenDocument(NativeLocation(path, "gsnd"));
        RenameFirst(child.Document, "child-edited");
        var parent = studio.OpenDocument(path);
        var asset = (AssetEditorControl)parent.Document.Editor;
        using (var bytes = new MemoryStream(Encoding.UTF8.GetBytes(NativeJson("gsnd", "parent-edited"))))
            asset.AssetsFile.Add(AssetTypes.AudioDefinition, "nested/same.gsnd", bytes);
        asset.MarkChanged();

        Exception? failure = null;
        studio.ReportError = error => failure = error;
        Assert.False(AwaitUi(studio.SaveAllDocuments()));
        Assert.NotNull(failure);
        Assert.True(child.Document.Dirty());
        Assert.False(parent.Document.Dirty());
        using var persisted = StudioArchiveSnapshot.Open(path, null);
        Assert.Equal(NativeJson("gsnd", "parent-edited"),
            Encoding.UTF8.GetString(StudioArchiveSnapshot.Entry(persisted.Archive, NativeLocation(path, "gsnd"))));
    });

}
