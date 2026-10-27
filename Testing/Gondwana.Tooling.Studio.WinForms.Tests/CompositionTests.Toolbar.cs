using Gondwana.Tooling.Audio.WinForms;

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
        Assert.Equal(3, bar.Items.OfType<ToolStripSeparator>().Count());
        Assert.Equal(
            new[] { "Open", "Save", "SaveAll", "WorkingDirectory", "Refresh", "Preview", "Validate" },
            bar.Items.OfType<ToolStripButton>().Select(item => item.Name));

        var create = Assert.Single(bar.Items.OfType<ToolStripDropDownButton>());
        Assert.Equal("New", create.Name);
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
    public void SaveAllPersistsAllDirtyDocumentsAndDisablesItsCommandWhenClean() => RunSta(directory =>
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
        Assert.False(saveAll.Enabled);
        Assert.True(AwaitUi(studio.SaveAllDocuments()));

        var editor = Assert.IsType<AudioEditorControl>(sound.Document.Editor);
        editor.Document.MarkChanged();
        Assert.True(saveAll.Enabled);
        Assert.True(AwaitUi(studio.SaveAllDocuments()));
        Assert.False(saveAll.Enabled);
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
}
