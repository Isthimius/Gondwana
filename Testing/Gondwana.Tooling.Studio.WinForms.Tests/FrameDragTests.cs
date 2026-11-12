using System.Reflection;
using System.Text;
using Gondwana.Assets;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Tooling.Animations.WinForms;
using Gondwana.Tooling.Assets;
using Gondwana.Tooling.Tilesheets.WinForms;
using Gondwana.Tooling.WinForms;

namespace Gondwana.Tooling.Studio.WinForms.Tests;

public sealed partial class CompositionTests
{
    [Fact]
    public void StudioSharedControlsDropAnExactPackedFrameIntoAnotherDocument() => RunSta(root =>
    {
        const string entry = "sheets/nested/characters.gts";
        const string imageEntry = "images/characters.png";
        string path = Path.Combine(root, "characters.gaf");
        using (var archive = AssetsFile.LoadOrCreate(path, password: null, encrypt: false, register: false))
        {
            using var image = new Bitmap(32, 32);
            using var png = new MemoryStream();
            image.Save(png, System.Drawing.Imaging.ImageFormat.Png);
            png.Position = 0;
            archive.Add(AssetTypes.Image, imageEntry, png);
            using var gts = new MemoryStream(Encoding.UTF8.GetBytes(TilesheetDefinitionSerializer.ToDocumentJson(new()
            {
                Definitions =
                [
                    new() { Name = "first" },
                    new()
                    {
                        Name = "characters", Image = new() { AssetEntryName = imageEntry },
                        Regions = [new() { Name = "idle", Area = new(0, 0, 32, 32), TileSize = new(16, 16) }]
                    }
                ]
            })));
            archive.Add(AssetTypes.TilesheetDefinition, entry, gts);
            archive.Save();
        }
        string gani = Path.Combine(root, "idle.gani");
        File.WriteAllText(gani, NativeJson("gani"));
        using var studio = Host(root);
        var tilesheet = studio.OpenDocument(new AssetLocation(AssetTypes.TilesheetDefinition, path, entry));
        Assert.True(tilesheet.Document.SelectMember("characters"));
        var tilesheetEditor = Assert.IsType<TilesheetEditorControl>(tilesheet.Document.Editor);
        var region = Assert.Single(tilesheetEditor.Document.Definition.Regions);
        const BindingFlags NonPublicInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        var drag = Assert.IsType<TilesheetFrameDrag>(typeof(TilesheetEditorControl)
            .GetMethod("CreateFrameDrag", NonPublicInstance)!.Invoke(tilesheetEditor, [region, new Point(1, 1)]));
        Assert.Equal(new ResourceAddress(new AssetLocation(AssetTypes.TilesheetDefinition, path, entry), "characters"), drag.Address);

        var target = studio.OpenDocument(gani);
        Assert.True(target.Document.SelectMember("sibling"));
        Application.DoEvents();
        var animation = Assert.IsType<AnimationEditorControl>(target.Document.Editor);
        var frames = (ListView)typeof(AnimationEditorControl).GetField("_frames", NonPublicInstance)!.GetValue(animation)!;
        Assert.True(frames.Visible && frames.AllowDrop);
        var point = frames.PointToScreen(new Point(10, 40));
        var args = new DragEventArgs(new ResourceDragData<TilesheetFrameDrag>(drag),
            0, point.X, point.Y, DragDropEffects.Copy, DragDropEffects.None);
        typeof(Control).GetMethod("OnDragEnter", NonPublicInstance)!.Invoke(frames, [args]);
        Assert.Equal(DragDropEffects.Copy, args.Effect);
        typeof(Control).GetMethod("OnDragDrop", NonPublicInstance)!.Invoke(frames, [args]);
        Assert.Equal(DragDropEffects.Copy, args.Effect);
        var frame = Assert.Single(animation.Document.Definition.Frames);
        Assert.Equal(("characters", "idle", 1, 1), (frame.Tilesheet, frame.RegionName, frame.XTile, frame.YTile));
        var source = Assert.Single(animation.Document.Definition.TilesheetSources);
        Assert.Equal(entry, source.AssetEntryName);
        Assert.Equal(path, animation.Document.ResolveReferencePath(source.AssetsFilePath!));
        Assert.Empty(animation.Document.Definitions[0].Frames);
        Assert.True(target.Document.Dirty());
        Assert.False(tilesheet.Document.Dirty());

        target.Document.History!.Undo();
        Assert.False(target.Document.Dirty());
        Assert.Empty(animation.Document.Definition.Frames);
        Assert.Empty(animation.Document.Definition.TilesheetSources);
        target.Document.History.Redo();
        Assert.Same(frame, Assert.Single(animation.Document.Definition.Frames));
        Assert.Same(source, Assert.Single(animation.Document.Definition.TilesheetSources));
    });
}
