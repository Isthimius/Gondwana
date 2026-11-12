using System.Reflection;
using System.Text;
using Gondwana.Assets;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Sprites.GSPR;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Scenes.GSCN;
using Gondwana.Tooling.Animations.WinForms;
using Gondwana.Tooling.Assets;
using Gondwana.Tooling.Scenes.WinForms;
using Gondwana.Tooling.Sprites.Editing;
using Gondwana.Tooling.Sprites.WinForms;
using Gondwana.Tooling.Studio.WinForms.Panels;
using Gondwana.Tooling.WinForms;

namespace Gondwana.Tooling.Studio.WinForms.Tests;

public sealed partial class CompositionTests
{
    [Fact]
    public void StudioNodesCompleteExactSelectionsOnReleaseIntoASprite() => RunSta(root =>
    {
        const string sceneEntry = "scenes/nested/level.gscn";
        string gts = Path.Combine(root, "atlas.gts");
        string gani = Path.Combine(root, "torch.gani");
        string gaf = Path.Combine(root, "scenes.gaf");
        string gspr = Path.Combine(root, "sprites.gspr");

        using (var bitmap = new Bitmap(32, 32))
            bitmap.Save(Path.Combine(root, "atlas.png"), System.Drawing.Imaging.ImageFormat.Png);
        TilesheetDefinitionSerializer.SaveAll(gts,
        [
            new() { Name = "other" },
            new()
            {
                Name = "terrain",
                Image = new() { FilePath = "atlas.png" },
                Regions = [new() { Name = "ground", Area = new(0, 0, 32, 32), TileSize = new(16, 16) }]
            }
        ]);
        AnimationDefinitionSerializer.SaveAll(gani,
        [
            new() { Key = "other" },
            new()
            {
                Key = "torch.burn",
                ThrottleTime = .1,
                TilesheetSources = [AnimationTilesheetSourceDefinition.Loose("terrain", "atlas.gts")],
                Frames = [new() { Tilesheet = "terrain", RegionName = "ground", XTile = 1, YTile = 1 }]
            }
        ]);
        using (var archive = AssetsFile.LoadOrCreate(gaf, password: null, encrypt: false, register: false))
        {
            var scene = new SceneDefinition
            {
                ID = "level",
                Layers = [new() { ID = "background", Columns = 2, Rows = 2, TileWidth = 16, TileHeight = 16 },
                    new() { ID = "actors", Columns = 2, Rows = 2, TileWidth = 16, TileHeight = 16 }]
            };
            using var contents = new MemoryStream(Encoding.UTF8.GetBytes(SceneDefinitionSerializer.ToDocumentJson(
                new() { Definitions = [new() { ID = "other" }, scene] })));
            archive.Add(AssetTypes.SceneDefinition, sceneEntry, contents);
            archive.Save();
        }
        using (var model = SpriteDocument.Create(root))
        {
            model.AddSprite();
            model.Save(gspr, allowInvalid: true);
        }

        using var studio = Host(root);
        var packedNode = new TreeNode("Friendly name")
        {
            Tag = new PackedAssetNodeIdentity(gaf, AssetTypes.SceneDefinition, sceneEntry)
        };
        var packedAddress = Assert.IsType<ResourceAddress>(MainForm.DirectoryDragAddress(packedNode));
        Assert.Equal(sceneEntry, packedAddress.Location.AssetEntryName);
        Assert.Null(MainForm.DirectoryDragAddress(new TreeNode("Archive") { Tag = gaf }));

        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        var sourceDocument = studio.OpenDocument(packedAddress.Location);
        Assert.True(sourceDocument.Document.SelectMember("level"));
        var scenes = Assert.IsType<SceneEditorControl>(sourceDocument.Document.Editor);
        var layer = scenes.Definition.Layers[1];
        var scenePayload = Assert.IsType<SceneLayerDrag>(typeof(SceneEditorControl).GetMethod("CreateSceneLayerDrag", Private)!
            .Invoke(scenes, [layer]));
        Assert.Equal(packedAddress with { Member = "level" }, scenePayload.Address);
        Assert.Equal("actors", scenePayload.LayerId);

        var spriteDocument = studio.OpenDocument(gspr);
        Application.DoEvents();
        var editor = Assert.IsType<SpriteEditorControl>(spriteDocument.Document.Editor);
        var list = (ListBox)typeof(SpriteEditorControl).GetField("_sprites", Private)!.GetValue(editor)!;
        var sprite = Assert.IsType<SpriteInstanceDefinition>(editor.SelectedSprite);
        int selections = 0;
        studio.ChooseDragItem = (_, _, choices) =>
        {
            selections++;
            Assert.Equal(2, choices.Count);
            return choices[1];
        };
        studio.ChooseDragCoordinates = (_, columns, rows) =>
        {
            selections++;
            Assert.Equal((2L, 2L), (columns, rows));
            return new(1, 1);
        };

        void Drop(ResourceAddress address, int expectedSelections)
        {
            var data = Assert.IsAssignableFrom<ResourceDragData>(studio.CreateResourceDrag(address));
            var bounds = list.GetItemRectangle(0);
            var point = list.PointToScreen(new Point(10, bounds.Top + bounds.Height / 2));
            var args = new DragEventArgs(data, 0, point.X, point.Y, DragDropEffects.Copy, DragDropEffects.None);
            int before = selections;
            typeof(Control).GetMethod("OnDragEnter", Private)!.Invoke(list, [args]);
            Assert.Equal(DragDropEffects.Copy, args.Effect);
            Assert.Equal(before, selections);
            typeof(Control).GetMethod("OnDragDrop", Private)!.Invoke(list, [args]);
            Assert.Equal(DragDropEffects.Copy, args.Effect);
            Assert.Equal(before + expectedSelections, selections);
            Application.DoEvents();
        }

        var frameAddress = Assert.IsType<ResourceAddress>(MainForm.DirectoryDragAddress(new TreeNode("Atlas") { Tag = gts }));
        Drop(frameAddress, 2);
        Assert.Equal(("terrain", "ground", 1, 1), (sprite.Frame!.Tilesheet, sprite.Frame.RegionName, sprite.Frame.XTile, sprite.Frame.YTile));

        using var explorer = new DependencyExplorerPanel();
        var tree = explorer.Controls.OfType<TreeView>().Single();
        var animationAddress = new ResourceAddress(new AssetLocation(AssetTypes.AnimationDefinition, gani), "torch.burn");
        var animationNode = new TreeNode("Friendly animation label") { Tag = animationAddress };
        tree.Nodes.Add(animationNode);
        ResourceAddress? requested = null;
        explorer.ResourceDragRequested += (_, address) => requested = address;
        typeof(TreeView).GetMethod("OnItemDrag", Private)!.Invoke(tree, [new ItemDragEventArgs(MouseButtons.Left, animationNode)]);
        Assert.Equal(animationAddress, requested);
        Drop(requested!, 0);
        Assert.Equal("torch.burn", sprite.AnimationKey);
        Assert.False(sprite.StartAnimation);

        Drop(packedAddress, 2);
        Assert.Equal(("level", "actors"), (sprite.SceneId, sprite.SceneLayerId));
        Assert.Equal(sceneEntry, Assert.Single(editor.Document.Definition.SceneSources).AssetEntryName);
        Assert.False(sourceDocument.Document.Dirty());
        Assert.True(spriteDocument.Document.Dirty());

        var animationDocument = studio.OpenDocument(gani);
        Assert.True(animationDocument.Document.SelectMember("torch.burn"));
        Application.DoEvents();
        var animationEditor = Assert.IsType<AnimationEditorControl>(animationDocument.Document.Editor);
        var sequence = (ListView)typeof(AnimationEditorControl).GetField("_frames", Private)!.GetValue(animationEditor)!;
        var row = sequence.GetItemRect(0);
        var destination = sequence.PointToScreen(new Point(10, row.Bottom + 2));
        var deferred = new DragEventArgs(studio.CreateResourceDrag(frameAddress), 0, destination.X, destination.Y,
            DragDropEffects.Copy, DragDropEffects.None);
        typeof(Control).GetMethod("OnDragEnter", Private)!.Invoke(sequence, [deferred]);
        Assert.Equal(DragDropEffects.Copy, deferred.Effect);
        typeof(Control).GetMethod("OnDragDrop", Private)!.Invoke(sequence, [deferred]);
        Assert.Equal(DragDropEffects.Copy, deferred.Effect);
        Assert.Equal(2, animationEditor.Document.Definition.Frames.Count);
        Assert.Single(animationEditor.Document.Definition.TilesheetSources);
    });

    [Fact]
    public void FramePickerRejectsAnImageReplacedDuringSelection() => RunSta(root =>
    {
        string image = Path.Combine(root, "atlas.png");
        string gts = Path.Combine(root, "atlas.gts");
        string gspr = Path.Combine(root, "sprites.gspr");
        using (var bitmap = new Bitmap(16, 16))
            bitmap.Save(image, System.Drawing.Imaging.ImageFormat.Png);
        TilesheetDefinitionSerializer.SaveAll(gts, [new()
        {
            Name = "terrain",
            Image = new() { FilePath = "atlas.png" },
            Regions = [new() { Name = "ground", Area = new(0, 0, 16, 16), TileSize = new(16, 16) }]
        }]);
        using (var model = SpriteDocument.Create(root))
        {
            model.AddSprite();
            model.Save(gspr, allowInvalid: true);
        }

        using var studio = Host(root);
        var editor = Assert.IsType<SpriteEditorControl>(studio.OpenDocument(gspr).Document.Editor);
        Application.DoEvents();
        studio.ChooseDragCoordinates = (_, _, _) =>
        {
            var originalWriteTime = File.GetLastWriteTimeUtc(image);
            using var replacement = new Bitmap(16, 16);
            replacement.SetPixel(0, 0, Color.Red);
            replacement.Save(image, System.Drawing.Imaging.ImageFormat.Png);
            File.SetLastWriteTimeUtc(image, originalWriteTime.AddSeconds(5));
            return Point.Empty;
        };

        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        var list = (ListBox)typeof(SpriteEditorControl).GetField("_sprites", Private)!.GetValue(editor)!;
        var point = list.PointToScreen(new Point(10, list.GetItemRectangle(0).Height / 2));
        var address = new ResourceAddress(new AssetLocation(AssetTypes.TilesheetDefinition, gts));
        var args = new DragEventArgs(studio.CreateResourceDrag(address), 0, point.X, point.Y,
            DragDropEffects.Copy, DragDropEffects.None);
        typeof(Control).GetMethod("OnDragEnter", Private)!.Invoke(list, [args]);
        Assert.Equal(DragDropEffects.Copy, args.Effect);
        typeof(Control).GetMethod("OnDragDrop", Private)!.Invoke(list, [args]);

        Assert.Equal(DragDropEffects.None, args.Effect);
        var validation = (TextBox)typeof(SpriteEditorControl).GetField("_validation", Private)!.GetValue(editor)!;
        Assert.Contains("'atlas.png' changed during the drag", validation.Text);
        Assert.Null(Assert.Single(editor.Document.Definition.Sprites).Frame);
        Assert.Empty(editor.Document.Definition.TilesheetSources);
        Assert.False(editor.Document.IsDirty);
        Assert.False(editor.Document.History.CanUndo);
    });
}
