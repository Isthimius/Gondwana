using System.Reflection;
using System.Text;
using Gondwana.Assets;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Tooling.Animations.WinForms;
using Gondwana.Tooling.Assets;
using Gondwana.Tooling.Scenes.Editing;
using Gondwana.Tooling.Scenes.WinForms;
using Gondwana.Tooling.WinForms;

namespace Gondwana.Tooling.Studio.WinForms.Tests;

public sealed partial class CompositionTests
{
    [Fact]
    public void StudioDropsSelectedAnimationFromAnEncryptedPackedDocument() => RunSta(root =>
    {
        const string entry = "animations/nested/torch.gani";
        const string password = "scene-drop";
        var path = Path.Combine(root, "assets.gaf");
        using (var bitmap = new Bitmap(32, 32))
            bitmap.Save(Path.Combine(root, "atlas.png"), System.Drawing.Imaging.ImageFormat.Png);
        TilesheetDefinitionSerializer.Save(Path.Combine(root, "atlas.gts"), new()
        {
            Name = "torch",
            Image = new() { FilePath = "atlas.png" },
            Regions = [new() { Name = "flame", Area = new(0, 0, 32, 32), TileSize = new(16, 16) }]
        });
        using (var archive = AssetsFile.LoadOrCreate(path, password, encrypt: true, register: false))
        {
            var definition = new AnimationDefinition
            {
                Key = "torch.burn",
                ThrottleTime = .2,
                TilesheetSources = [AnimationTilesheetSourceDefinition.Loose("torch", "atlas.gts")],
                Frames = [new() { Tilesheet = "torch", RegionName = "flame", XTile = 1, YTile = 1 }]
            };
            using var gani = new MemoryStream(Encoding.UTF8.GetBytes(AnimationDefinitionSerializer.ToDocumentJson(new()
            {
                Definitions = [new() { Key = "other" }, definition]
            })));
            archive.Add(AssetTypes.AnimationDefinition, entry, gani);
            archive.Save();
        }

        string scenePath = Path.Combine(root, "dungeon.gscn");
        using (var model = SceneDocument.Create(root))
        {
            model.AddLayer();
            model.Save(scenePath);
        }

        using var studio = Host(root);
        studio.AssetPassword = _ => password;
        var source = studio.OpenDocument(new AssetLocation(AssetTypes.AnimationDefinition, path, entry), password);
        Assert.True(source.Document.SelectMember("torch.burn"));
        var animation = Assert.IsType<AnimationEditorControl>(source.Document.Editor);
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        var createDrag = typeof(AnimationEditorControl).GetMethod("CreateAnimationDrag", Private)!;
        var drag = Assert.IsType<AnimationDrag>(createDrag.Invoke(animation, null));
        Assert.Equal(new ResourceAddress(new AssetLocation(AssetTypes.AnimationDefinition, path, entry), "torch.burn"), drag.Address);
        animation.Document.RenameDefinition("unsaved");
        Assert.Null(createDrag.Invoke(animation, null));
        animation.Document.History.Undo();

        var target = studio.OpenDocument(scenePath);
        Application.DoEvents();
        var editor = Assert.IsType<SceneEditorControl>(target.Document.Editor);
        var preview = (Control)typeof(SceneEditorControl).GetField("_preview", Private)!.GetValue(editor)!;
        Assert.True(preview.Visible && preview.AllowDrop);
        using var image = new Bitmap(preview.Width, preview.Height);
        preview.DrawToBitmap(image, preview.ClientRectangle);
        var offset = (PointF)preview.GetType().GetField("_offset", Private)!.GetValue(preview)!;
        var zoom = (float)preview.GetType().GetProperty("Zoom")!.GetValue(preview)!;
        var point = preview.PointToScreen(Point.Round(new(offset.X + 48 * zoom, offset.Y + 48 * zoom)));
        var args = new DragEventArgs(new ResourceDragData<AnimationDrag>(drag), 0,
            point.X, point.Y, DragDropEffects.Copy, DragDropEffects.None);
        typeof(Control).GetMethod("OnDragEnter", Private)!.Invoke(preview, [args]);
        Assert.Equal(DragDropEffects.Copy, args.Effect);
        Assert.False(target.Document.Dirty());
        typeof(Control).GetMethod("OnDragDrop", Private)!.Invoke(preview, [args]);
        Assert.Equal(DragDropEffects.Copy, args.Effect);

        var layer = Assert.Single(editor.Definition.Layers);
        var tile = Assert.Single(layer.Tiles);
        Assert.Equal((1, 1, "torch.burn", true, true), (tile.X, tile.Y, tile.AnimationKey, tile.EnableAnimator, tile.StartAnimation));
        Assert.Equal(("torch", "flame", 1, 1), (tile.Frame!.Tilesheet, tile.Frame.RegionName, tile.Frame.XTile, tile.Frame.YTile));
        var dependency = Assert.Single(editor.Definition.AnimationSources);
        Assert.Equal(entry, dependency.AssetEntryName);
        Assert.Equal(path, editor.Document.ResolveReferencePath(dependency.AssetsFilePath!));
        Assert.True(target.Document.Dirty());
        Assert.False(source.Document.Dirty());
        target.Document.History!.Undo();
        Assert.Empty(layer.Tiles);
        Assert.Empty(editor.Definition.AnimationSources);
        Assert.False(target.Document.Dirty());
    });
}
