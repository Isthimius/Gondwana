using Gondwana.Assets;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Tooling.Assets;
using Gondwana.Tooling.Assets.WinForms;

namespace Gondwana.Tooling.Studio.WinForms.Tests;

public sealed partial class CompositionTests
{
    /// <summary>Canceling and reusing identical leaf bytes leave a clean document and its notifications unchanged.</summary>
    [Fact]
    public void AssetImportNoOpPreservesCleanState() => RunSta(directory =>
    {
        var path = Path.Combine(directory, "game.gaf");
        var source = Path.Combine(directory, "existing.txt");
        File.WriteAllText(source, "same bytes");
        using var archive = AssetsFile.LoadOrCreate(path, null, false, register: false);
        archive.Add(AssetTypes.Misc, source, "existing.txt");
        using var editor = new AssetEditorControl(archive);
        int changes = 0;
        editor.Changed += (_, _) => changes++;
        var plan = AssetImportPlan.Create(archive, [new(source, AssetTypes.Misc, "existing.txt")], true);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => editor.ApplyImport(plan, cancelled.Token));
        Assert.Equal(0, editor.ApplyImport(plan));
        Assert.Equal(0, changes);
        Assert.False(editor.IsDirty);
        Assert.Single(archive.GetAllEntries());
    });

    /// <summary>The shared control applies imports identically standalone and inside Studio, preserving dirty edits and save routing.</summary>
    /// <param name="hosted">Whether Studio hosts the editor.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AssetImportSharedHostDirtyAndSaveBehavior(bool hosted) => RunSta(directory =>
    {
        var path = Path.Combine(directory, "game.gaf");
        using (var initial = AssetsFile.LoadOrCreate(path, "secret", true, register: false)) initial.Save();
        var diskBefore = File.ReadAllBytes(path);
        using var studio = Host(directory);
        studio.AssetPassword = _ => "secret";
        using var borrowed = AssetsFile.LoadOrCreate(path, "secret", true, register: false);
        var document = hosted ? studio.OpenDocument(path) : null;
        using var standalone = hosted ? null : new AssetEditorControl(borrowed);
        var editor = hosted ? Assert.IsType<AssetEditorControl>(document!.Document.Editor) : standalone!;
        var archive = editor.AssetsFile;
        using (var bytes = new MemoryStream([9])) archive.Add(AssetTypes.Misc, "unsaved", bytes);
        editor.MarkChanged();
        int changes = 0;
        editor.Changed += (_, _) => changes++;
        var source = Path.Combine(directory, "new.txt");
        File.WriteAllText(source, "new resource");
        var imagePath = Path.Combine(directory, "art.png");
        using (var image = new Bitmap(1, 1)) image.Save(imagePath, System.Drawing.Imaging.ImageFormat.Png);
        var sheetPath = Path.Combine(directory, "tiles.gts");
        TilesheetDefinitionSerializer.Save(sheetPath, new TilesheetDefinition
        {
            Name = "terrain",
            Image = new() { FilePath = imagePath },
            Regions = [new() { Name = "default", Area = new Rectangle(0, 0, 1, 1), TileSize = new Size(1, 1) }]
        });
        var plan = AssetImportPlan.Create(archive, [new(source, AssetTypes.Misc, "new.txt"), new(sheetPath, AssetTypes.TilesheetDefinition, "tiles.gts")], true);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => editor.ApplyImport(plan, cancelled.Token));
        Assert.Equal(0, changes);
        Assert.True(editor.IsDirty);
        Assert.Single(archive.GetAllEntries());
        Assert.Equal(3, editor.ApplyImport(plan));
        Assert.Same(archive, editor.AssetsFile);
        Assert.Equal(1, changes);
        Assert.True(editor.IsDirty);
        Assert.Equal(diskBefore, File.ReadAllBytes(path));
        Assert.Equal("new resource", File.ReadAllText(source));
        Assert.Equal(4, archive.GetAllEntries().Count());
        if (hosted)
        {
            editor.SaveRequested!(false);
            Assert.True(AwaitUi(studio.SaveDocument(document!)));
        }
        else editor.SaveTo(path);
        Assert.False(editor.IsDirty);
        using var saved = AssetsFile.LoadOrCreate(path, "secret", true, register: false);
        Assert.Equal(4, saved.GetAllEntries().Count());
        Assert.Equal("art.png", TilesheetDefinitionSerializer.Load(saved, "tiles.gts").Image.AssetEntryName);
        Assert.True(saved.UseEncryption);
        document?.Close();
    });

    /// <summary>Export shares host behavior and preserves dirty leaf edits while refusing unsaved native exports.</summary>
    /// <param name="hosted">Whether Studio hosts the shared control.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AssetExportPreservesOwnershipAndDirtyState(bool hosted) => RunSta(directory =>
    {
        var path = Path.Combine(directory, "game.gaf");
        using (var initial = AssetsFile.LoadOrCreate(path, null, false, register: false)) initial.Save();
        using var studio = Host(directory);
        using var borrowed = AssetsFile.LoadOrCreate(path, null, false, register: false);
        var document = hosted ? studio.OpenDocument(path) : null;
        using var standalone = hosted ? null : new AssetEditorControl(borrowed);
        var editor = hosted ? Assert.IsType<AssetEditorControl>(document!.Document.Editor) : standalone!;
        using (var bytes = new MemoryStream([9])) editor.AssetsFile.Add(AssetTypes.Misc, "unsaved", bytes);
        editor.MarkChanged();
        int changes = 0;
        editor.Changed += (_, _) => changes++;
        var destination = Path.Combine(directory, "export.bin");
        var leaf = new AssetsFileEntry { AssetType = AssetTypes.Misc, AssetName = "unsaved" };
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Throws<OperationCanceledException>(() => editor.ExportAsset(leaf, destination, false, canceled.Token));
        Assert.False(File.Exists(destination));
        Assert.Throws<InvalidOperationException>(() => editor.ExportAsset(
            new AssetsFileEntry { AssetType = AssetTypes.TilesheetDefinition, AssetName = "draft.gts" }, destination, false));
        editor.ExportAsset(leaf, destination, false);
        Assert.Equal(new byte[] { 9 }, File.ReadAllBytes(destination));
        Assert.True(editor.IsDirty);
        Assert.Equal(0, changes);
        Assert.Single(editor.AssetsFile.GetAllEntries());
        if (hosted) Assert.True(AwaitUi(studio.SaveDocument(document!)));
        else editor.SaveTo(path);
        document?.Close();
    });

    /// <summary>Mixed selections infer each native and leaf type independently while allowing unknown-type corrections.</summary>
    [Fact]
    public void AssetImportPickerSupportsMixedSelections() => RunSta(directory =>
    {
        var extensions = new[] { "gts", "gani", "gscn", "gspr", "gsnd", "png", "ogg", "svg", "ttf", "cur", "mp4", "unknown" };
        var expected = new[] { AssetTypes.TilesheetDefinition, AssetTypes.AnimationDefinition, AssetTypes.SceneDefinition, AssetTypes.SpriteDefinition,
            AssetTypes.AudioDefinition, AssetTypes.Image, AssetTypes.Audio, AssetTypes.Svg, AssetTypes.Font, AssetTypes.Cursor, AssetTypes.Video, AssetTypes.Misc };
        using var picker = new AssetTypePickerForm(extensions.Select(extension => Path.Combine(directory, "asset." + extension)));
        Assert.Equal(expected, picker.Sources.Select(source => source.AssetType));
        var grid = picker.Controls.OfType<DataGridView>().Single();
        Assert.All(grid.Rows.Cast<DataGridViewRow>().Take(5), row => Assert.True(row.Cells[1].ReadOnly));
        grid.Rows[^1].Cells[1].Value = AssetTypes.Font;
        Assert.Equal(AssetTypes.Font, picker.Sources[^1].AssetType);
    });
}
