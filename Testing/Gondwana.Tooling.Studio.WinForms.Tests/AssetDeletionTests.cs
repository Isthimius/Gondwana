using Gondwana.Assets;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Tooling.Assets;
using Gondwana.Tooling.Assets.WinForms;

namespace Gondwana.Tooling.Studio.WinForms.Tests;

public sealed partial class CompositionTests
{
    /// <summary>Both editor hosts preserve dirty state on blocked deletion and refresh the same archive only after explicit force.</summary>
    /// <param name="hosted">Whether Studio hosts the shared GAF control.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AssetDeletionProtectsEditorsAndRefreshes(bool hosted) => RunSta(directory =>
    {
        var path = Path.Combine(directory, "game.gaf");
        using (var archive = AssetsFile.LoadOrCreate(path, null, false, register: false))
        {
            using var image = new MemoryStream([1, 2]);
            archive.Add(AssetTypes.Image, "images/art.png", image);
            var sheet = new TilesheetDefinition
            {
                Name = "terrain",
                Image = new() { AssetEntryName = "images/art.png" },
                Regions = [new() { Name = "default", Area = new Rectangle(0, 0, 1, 1), TileSize = new Size(1, 1) }]
            };
            using var bytes = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(TilesheetDefinitionSerializer.ToJson(sheet)));
            archive.Add(AssetTypes.TilesheetDefinition, "tiles.gts", bytes);
            archive.Save();
        }
        using var studio = Host(directory);
        using var borrowed = AssetsFile.LoadOrCreate(path, null, false, register: false);
        var document = hosted ? studio.OpenDocument(path) : null;
        using var standalone = hosted ? null : new AssetEditorControl(borrowed);
        var editor = hosted ? Assert.IsType<AssetEditorControl>(document!.Document.Editor) : standalone!;
        var instance = editor.AssetsFile;
        var originalEntries = AssetImportPlan.Snapshot(instance);
        var plan = AssetDeletionPlan.Create(path, [new(AssetTypes.Image, path, "images/art.png")]);
        int changes = 0;
        int refreshes = 0;
        editor.Changed += (_, _) => changes++;
        editor.WorkspaceChanged = () => refreshes++;
        var original = File.ReadAllBytes(path);
        Assert.True(plan.ForceRequired);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => editor.ApplyDeletion(plan, true, canceled.Token));
        Assert.Throws<InvalidOperationException>(() => editor.ApplyDeletion(plan));
        Assert.Equal(0, changes);
        Assert.Equal(0, refreshes);
        Assert.False(editor.IsDirty);
        Assert.Equal(original, File.ReadAllBytes(path));
        editor.MarkChanged();
        Assert.Throws<InvalidOperationException>(() => editor.ApplyDeletion(plan, true));
        Assert.True(editor.IsDirty);
        editor.SaveTo(path);
        plan = AssetDeletionPlan.Create(path, [new(AssetTypes.Image, path, "images/art.png")]);
        changes = refreshes = 0;
        editor.ApplyDeletion(plan, true);
        Assert.Same(instance, editor.AssetsFile);
        Assert.False(editor.IsDirty);
        Assert.Equal(1, changes);
        Assert.Equal(1, refreshes);
        var remaining = AssetImportPlan.Snapshot(instance);
        var only = Assert.Single(remaining);
        Assert.Equal(AssetTypes.TilesheetDefinition, only.Key.AssetType);
        Assert.Equal(originalEntries[only.Key], only.Value);
        using var disk = AssetsFile.LoadOrCreate(path, null, false, register: false);
        Assert.Equal(only.Value, AssetImportPlan.Snapshot(disk).Single().Value);
        document?.Close();
    });

    /// <summary>Studio refuses deletion while any other authoring model is open, including unsaved new documents.</summary>
    [Fact]
    public void AssetDeletionStudioRequiresOtherModelsClosed() => RunSta(directory =>
    {
        var path = Path.Combine(directory, "source.bin");
        File.WriteAllBytes(path, [1]);
        var plan = AssetDeletionPlan.Create(directory, [new(AssetTypes.Misc, path)]);
        using var studio = Host(directory);
        var other = studio.NewDocument("gts");
        Assert.Throws<InvalidOperationException>(() => studio.ValidateDeletion(plan, null));
        Assert.True(File.Exists(path));
        studio.AskSave = _ => DialogResult.No;
        other.Close();
        studio.ValidateDeletion(plan, null);
        plan.Commit();
        Assert.False(File.Exists(path));
    });

    /// <summary>A whole-archive delete cannot invalidate the current borrowed editor instance.</summary>
    [Fact]
    public void AssetDeletionRequiresCurrentArchiveClosedForWholeDelete() => RunSta(directory =>
    {
        var path = Path.Combine(directory, "game.zip");
        using var archive = AssetsFile.LoadOrCreate(path, null, false, register: false);
        archive.Save();
        using var editor = new AssetEditorControl(archive);
        var plan = AssetDeletionPlan.Create(path, [], [path]);
        Assert.Throws<InvalidOperationException>(() => editor.ApplyDeletion(plan));
        Assert.False(editor.IsDirty);
        Assert.True(File.Exists(path));
    });
}
