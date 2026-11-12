using System.Reflection;
using System.Text;
using Gondwana.Assets;
using Gondwana.Drawing.Sprites.GSPR;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Scenes;
using Gondwana.Scenes.GSCN;
using Gondwana.Tooling.Assets;
using Gondwana.Tooling.Scenes.WinForms;
using Gondwana.Tooling.Sprites.WinForms;
using Gondwana.Tooling.WinForms;

namespace Gondwana.Tooling.Studio.WinForms.Tests;

public sealed partial class CompositionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ScenePlacementEditsTheLiveSpriteAndReplaysThroughItsOwner(bool packed) => RunSta(root =>
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        const string spriteEntry = "actors/nested/heroes.gspr";
        const string sceneEntry = "maps/nested/city.gscn";
        string gspr = Path.Combine(root, "actors.gspr");
        string gscn = Path.Combine(root, "city.gscn");
        string archivePath = Path.Combine(root, "world.gaf");

        using (var bitmap = new Bitmap(16, 16)) bitmap.Save(Path.Combine(root, "atlas.png"));
        TilesheetDefinitionSerializer.Save(Path.Combine(root, "atlas.gts"), new()
        {
            Name = "atlas",
            Image = new() { FilePath = "atlas.png" },
            Regions = [new() { Name = "actors", Area = new(0, 0, 16, 16), TileSize = new(16, 16) }]
        });
        var actor = new SpriteInstanceDefinition
        {
            Id = Guid.NewGuid(),
            Nickname = "hero",
            SceneId = "old-scene",
            SceneLayerId = "old-layer",
            Position = new(-1.5f, .5f),
            Frame = new() { Tilesheet = "atlas", RegionName = "actors" },
            CollisionsEnabled = true
        };
        SpriteDefinition[] definitions =
        [
            new() { Key = "decoy", Sprites = [new() { Id = Guid.NewGuid(), Nickname = "decoy", SceneId = "old-scene", SceneLayerId = "old-layer" }] },
            new() { Key = "heroes", Sprites = [actor], TilesheetSources = [SpriteTilesheetSourceDefinition.Loose("atlas", "atlas.gts")] }
        ];
        SceneDefinition[] scenes =
        [
            new() { ID = "city", Layers = [new() { ID = "back", Columns = 12, Rows = 12, TileWidth = 40, TileHeight = 24 },
                new() { ID = "front", ZOrder = 3, Columns = 12, Rows = 12, TileWidth = 40, TileHeight = 24, OriginPx = new(13, -9) }] }
        ];
        if (packed)
        {
            using var archive = AssetsFile.LoadOrCreate(archivePath, password: null, encrypt: false, register: false);
            using var sprites = new MemoryStream(Encoding.UTF8.GetBytes(SpriteDefinitionSerializer.ToDocumentJson(new() { Definitions = definitions.ToList() })));
            using var scene = new MemoryStream(Encoding.UTF8.GetBytes(SceneDefinitionSerializer.ToDocumentJson(new() { Definitions = scenes.ToList() })));
            archive.Add(AssetTypes.SpriteDefinition, spriteEntry, sprites);
            archive.Add(AssetTypes.SceneDefinition, sceneEntry, scene);
            archive.Save();
        }
        else
        {
            SpriteDefinitionSerializer.SaveAll(gspr, definitions);
            SceneDefinitionSerializer.SaveAll(gscn, scenes);
        }

        using var studio = Host(root);
        studio.Size = new(1500, 950);
        var spriteTab = packed ? studio.OpenDocument(new AssetLocation(AssetTypes.SpriteDefinition, archivePath, spriteEntry)) : studio.OpenDocument(gspr);
        Assert.True(spriteTab.Document.SelectMember("heroes"));
        var spriteEditor = Assert.IsType<SpriteEditorControl>(spriteTab.Document.Editor);
        var sprite = Assert.IsType<SpriteInstanceDefinition>(spriteEditor.SelectedSprite);
        var sceneTab = packed ? studio.OpenDocument(new AssetLocation(AssetTypes.SceneDefinition, archivePath, sceneEntry)) : studio.OpenDocument(gscn);
        var editor = Assert.IsType<SceneEditorControl>(sceneTab.Document.Editor);
        Application.DoEvents();

        var preview = Descendants(editor).Single(control => control.GetType().Name == "ScenePreviewControl");
        var structure = Descendants(editor).OfType<TreeView>().Single(tree => tree.Nodes.Count > 0 && tree.Nodes[0].Tag is SceneDefinition);
        var layer = editor.Document.Definition.Layers[1];
        structure.SelectedNode = structure.Nodes[0].Nodes.Cast<TreeNode>().Single(node => ReferenceEquals(node.Tag, layer));
        var controls = Descendants(editor).OfType<ToolStrip>().SelectMany(bar => bar.Items.Cast<ToolStripItem>()).ToArray();
        controls.Single(item => item.Text == "Fill").PerformClick();
        Application.DoEvents();

        var previewType = preview.GetType();
        previewType.GetMethod("SetZoom")!.Invoke(preview, [1f]);
        var projection = (SceneLayer)previewType.GetMethod("GetProjection", Private)!.Invoke(preview, [layer])!;
        Point Client(PointF world)
        {
            previewType.GetMethod("UpdateOffset", Private)!.Invoke(preview, []);
            var offset = (PointF)previewType.GetField("_offset", Private)!.GetValue(preview)!;
            return Point.Round(new PointF(world.X + offset.X, world.Y + offset.Y));
        }
        DragEventArgs Drag(SpriteInstanceDrag payload, PointF grid)
        {
            var point = Client(projection.GridToWorldPx(grid));
            Assert.True(preview.ClientRectangle.Contains(point));
            var screen = preview.PointToScreen(point);
            return new(new ResourceDragData<SpriteInstanceDrag>(payload), 32, screen.X, screen.Y,
                DragDropEffects.Move, DragDropEffects.None);
        }
        void RaiseDrag(string phase, EventArgs e) => typeof(Control).GetMethod("OnDrag" + phase, Private)!.Invoke(preview, [e]);
        void Mouse(string phase, Point point) => previewType.GetMethod("OnMouse" + phase, Private)!
            .Invoke(preview, [new MouseEventArgs(MouseButtons.Left, 1, point.X, point.Y, 0)]);
        bool Shortcut(Keys key) => (bool)typeof(SceneEditorControl).GetMethod("ProcessCmdKey", Private)!
            .Invoke(editor, [new Message(), key])!;
        object? Hit(Point point) => previewType.GetMethod("HitSprite", Private)!.Invoke(preview, [point]);

        var stale = Drag(spriteEditor.CreateInstanceDrag(sprite)!, new(1.25f, 1.5f));
        RaiseDrag("Enter", stale);
        Assert.Equal(DragDropEffects.Move, stale.Effect);
        spriteEditor.Document.Edit(sprite, "Change sprite visibility offset", () => sprite.NudgeY = 2);
        RaiseDrag("Drop", stale);
        Assert.Equal(DragDropEffects.None, stale.Effect);
        Assert.Equal("old-scene", sprite.SceneId);

        var drop = Drag(spriteEditor.CreateInstanceDrag(sprite)!, new(1.25f, 1.5f));
        RaiseDrag("Enter", drop);
        RaiseDrag("Over", drop);
        Assert.Equal(DragDropEffects.Move, drop.Effect);
        Assert.Equal("old-scene", sprite.SceneId);
        RaiseDrag("Leave", EventArgs.Empty);
        RaiseDrag("Drop", drop);
        Assert.Equal(DragDropEffects.None, drop.Effect);
        RaiseDrag("Enter", drop);
        RaiseDrag("Drop", drop);
        Application.DoEvents();
        Assert.Equal(DragDropEffects.Move, drop.Effect);
        Assert.Equal(("city", "front", new PointF(1.25f, 1.5f)), (sprite.SceneId, sprite.SceneLayerId, sprite.Position));
        Assert.Equal(actor.Id, sprite.Id);
        Assert.True(sprite.CollisionsEnabled);
        Assert.Equal(2, sprite.NudgeY);
        Assert.Equal("atlas", sprite.Frame!.Tilesheet);
        Assert.Equal("old-scene", spriteEditor.Document.Definitions[0].Sprites[0].SceneId);
        var source = Assert.Single(spriteEditor.Document.Definition.SceneSources);
        Assert.Equal(packed ? sceneEntry : null, source.AssetEntryName);
        Assert.False(editor.Document.IsDirty);
        Assert.False(editor.Document.History.CanUndo);
        Assert.Same(sprite, spriteEditor.SelectedSprite);
        Assert.Same(spriteEditor.Document.History, editor.SelectedSpriteHistory);

        var anchor = projection.GridToWorldPx(sprite.Position);
        var center = Client(new(anchor.X + 20, anchor.Y + 14));
        var hit = Hit(center);
        Assert.NotNull(hit);
        Assert.NotNull(hit.GetType().GetProperty("Artwork")!.GetValue(hit));
        var toggle = Assert.IsType<ToolStripButton>(controls.Single(item => item.Text == "Show Sprites"));
        toggle.PerformClick();
        Assert.Null(Hit(center));
        Assert.False(editor.Document.IsDirty);
        toggle.PerformClick();
        Assert.NotNull(Hit(center));

        Mouse("Down", center);
        Mouse("Up", center);
        Assert.Equal(new PointF(1.25f, 1.5f), sprite.Position);
        Mouse("Down", center);
        Mouse("Move", new(center.X + 65, center.Y + 26));
        Assert.Equal(new PointF(1.25f, 1.5f), sprite.Position);
        Assert.True(Shortcut(Keys.Escape));
        Mouse("Up", new(center.X + 65, center.Y + 26));
        Assert.Equal(new PointF(1.25f, 1.5f), sprite.Position);

        Mouse("Down", center);
        Mouse("Move", new(center.X + 65, center.Y + 26));
        Mouse("Up", new(center.X + 65, center.Y + 26));
        Assert.Equal(new PointF(3, 3), sprite.Position);
        Assert.Empty(layer.Tiles);
        Assert.True(Assert.IsType<ToolStripButton>(controls.Single(item => item.Text == "Fill")).Checked);
        Assert.True(Shortcut(Keys.Control | Keys.Z));
        Assert.Equal(new PointF(1.25f, 1.5f), sprite.Position);
        Assert.True(Shortcut(Keys.Control | Keys.Z));
        Application.DoEvents();
        Assert.Equal(("old-scene", "old-layer", new PointF(-1.5f, .5f)), (sprite.SceneId, sprite.SceneLayerId, sprite.Position));
        Assert.Empty(spriteEditor.Document.Definition.SceneSources);
        Assert.True(Shortcut(Keys.Control | Keys.Y));
        Assert.True(Shortcut(Keys.Control | Keys.Y));
        Application.DoEvents();
        Assert.Equal(new PointF(3, 3), sprite.Position);

        spriteEditor.Document.Edit(sprite, "Change position in GSPR", () => sprite.Position = new(2, 2));
        Application.DoEvents();
        var moved = projection.GridToWorldPx(sprite.Position);
        Assert.NotNull(Hit(Client(new(moved.X + 20, moved.Y + 14))));
        Assert.False(editor.Document.IsDirty);
        Assert.True(AwaitUi(studio.SaveDocument(spriteTab)));
        Assert.False(spriteEditor.Document.IsDirty);
        var saved = packed ? ReadPacked() : SpriteDefinitionSerializer.Load(gspr, "heroes");
        Assert.Equal(("city", "front", new PointF(2, 2)), (saved.Sprites[0].SceneId, saved.Sprites[0].SceneLayerId, saved.Sprites[0].Position));

        SpriteDefinition ReadPacked()
        {
            using var archive = AssetsFile.LoadOrCreate(archivePath, password: null, encrypt: false, register: false);
            return SpriteDefinitionSerializer.Load(archive, spriteEntry, "heroes");
        }
    });
}
