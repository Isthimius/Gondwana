using Gondwana.Assets;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Tooling.Assets;
using Gondwana.Tooling.Assets.WinForms;

namespace Gondwana.Tooling.Studio.WinForms.Tests;

public sealed partial class CompositionTests
{
    /// <summary>ZIP packages use the same format detection for opening and whole-archive relocation.</summary>
    [Fact]
    public void AssetRelocationRecognizesStudioZipPackages() => RunSta(directory =>
    {
        var source = Path.Combine(directory, "package.ZIP");
        var target = Path.Combine(directory, "renamed.zip");
        using (var archive = AssetsFile.LoadOrCreate(source, null, false, register: false))
        {
            using var bytes = new MemoryStream([1, 2]);
            archive.Add(AssetTypes.Misc, "data", bytes);
            archive.Save();
        }
        Assert.Equal("gaf", Documents.StudioDocument.FormatFor(source));
        Assert.True(AssetArchiveFormat.IsArchive(source));
        AssetRelocationPlan.MoveArchive(source, target, directory).Commit();
        using var studio = Host(directory);
        Assert.IsType<AssetEditorControl>(studio.OpenDocument(target).Document.Editor);
        Assert.False(File.Exists(source));
    });

    /// <summary>Both hosts protect dirty state and refresh the same borrowed instance after a semantic rename.</summary>
    /// <param name="hosted">Whether Studio hosts the shared editor.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AssetRelocationProtectsDirtyEditorsAndRefreshes(bool hosted) => RunSta(directory =>
    {
        var path = Path.Combine(directory, "game.gaf");
        using (var archive = AssetsFile.LoadOrCreate(path, null, false, register: false))
        {
            using var bytes = new MemoryStream([1, 2, 3]);
            archive.Add(AssetTypes.Misc, "old", bytes);
            archive.Save();
        }
        using var studio = Host(directory);
        using var borrowed = AssetsFile.LoadOrCreate(path, null, false, register: false);
        var document = hosted ? studio.OpenDocument(path) : null;
        using var standalone = hosted ? null : new AssetEditorControl(borrowed);
        var editor = hosted ? Assert.IsType<AssetEditorControl>(document!.Document.Editor) : standalone!;
        var instance = editor.AssetsFile;
        var plan = AssetRelocationPlan.Create(path, [new(new(AssetTypes.Misc, path, "old"), new(AssetTypes.Misc, path, "folder/new"))]);
        var original = File.ReadAllBytes(path);
        editor.MarkChanged();
        Assert.Throws<InvalidOperationException>(() => editor.ApplyRelocation(plan));
        Assert.Equal(original, File.ReadAllBytes(path));
        editor.SaveTo(path);
        plan = AssetRelocationPlan.Create(path, [new(new(AssetTypes.Misc, path, "old"), new(AssetTypes.Misc, path, "folder/new"))]);
        int changes = 0;
        editor.Changed += (_, _) => changes++;
        editor.ApplyRelocation(plan);
        Assert.Same(instance, editor.AssetsFile);
        Assert.False(editor.IsDirty);
        Assert.Equal(1, changes);
        Assert.Null(instance.Get(AssetTypes.Misc, "old"));
        using var moved = instance.Get(AssetTypes.Misc, "folder/new");
        Assert.Equal(1, moved!.ReadByte());
        document?.Close();
    });

    /// <summary>Studio prevents moves while another authoring model is open, even when its disk file is clean.</summary>
    [Fact]
    public void AssetRelocationStudioRequiresOtherModelsClosed() => RunSta(directory =>
    {
        var path = Path.Combine(directory, "source.bin");
        File.WriteAllBytes(path, [1]);
        var plan = AssetRelocationPlan.Create(directory, [new(new(AssetTypes.Misc, path), new(AssetTypes.Misc, Path.Combine(directory, "moved.bin")))]);
        using var studio = Host(directory);
        var other = studio.NewDocument("gts");
        Assert.Throws<InvalidOperationException>(() => studio.ValidateRelocation(plan, null));
        studio.AskSave = _ => DialogResult.No;
        other.Close();
        studio.ValidateRelocation(plan, null);
        plan.Commit();
        Assert.False(File.Exists(path));
    });

    /// <summary>Save As adopts the copied GAF while keeping its loose dependencies portable and preserving the original.</summary>
    [Fact]
    public void AssetRelocationSaveAsRebasesBeforePathAdoption() => RunSta(directory =>
    {
        var source = Path.Combine(directory, "source.gaf");
        var subdirectory = Path.Combine(directory, "copies");
        Directory.CreateDirectory(subdirectory);
        var image = Path.Combine(directory, "art.png");
        using (var bitmap = new Bitmap(1, 1)) bitmap.Save(image, System.Drawing.Imaging.ImageFormat.Png);
        using var archive = AssetsFile.LoadOrCreate(source, null, false, register: false);
        var sheet = new TilesheetDefinition
        {
            Name = "terrain",
            Image = new() { FilePath = "art.png" },
            Regions = [new() { Name = "default", Area = new Rectangle(0, 0, 1, 1), TileSize = new Size(1, 1) }]
        };
        using (var bytes = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(TilesheetDefinitionSerializer.ToJson(sheet)))) archive.Add(AssetTypes.TilesheetDefinition, "tiles.gts", bytes);
        archive.Save();
        var original = File.ReadAllBytes(source);
        using var editor = new AssetEditorControl(archive);
        var target = Path.Combine(subdirectory, "copy.gaf");
        editor.SaveTo(target);
        Assert.Equal(target, editor.FilePath);
        Assert.Equal(original, File.ReadAllBytes(source));
        var copied = TilesheetDefinitionSerializer.Load(editor.AssetsFile, "tiles.gts");
        Assert.Equal(image, Path.GetFullPath(copied.Image.FilePath!, subdirectory));
        Assert.DoesNotContain(".gondwana-", copied.Image.FilePath);
    });
}
