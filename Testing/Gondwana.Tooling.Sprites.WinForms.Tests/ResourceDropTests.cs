using System.Reflection;
using System.Text;
using Gondwana.Assets;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Sprites.GSPR;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Physics.Collisions;
using Gondwana.Scenes.GSCN;
using Gondwana.Tooling.Assets;
using Gondwana.Tooling.Sprites.Editing;
using Gondwana.Tooling.Tilesheets.Sources;
using Gondwana.Tooling.WinForms;

namespace Gondwana.Tooling.Sprites.WinForms.Tests;

public sealed partial class InteractionTests
{
    [Theory]
    [InlineData(false, null)]
    [InlineData(true, null)]
    [InlineData(true, "secret")]
    public void ExactResourcesTargetTheHoveredSpriteAndUndoAsIndependentEdits(bool packed, string? password) => Sta(root =>
    {
        var (frame, animation, layer) = MakeSpriteDragResources(root, packed, password);
        using var document = SpriteDocument.Create(root);
        var otherMember = document.Definition;
        var otherMemberSprite = document.AddSprite();
        var member = document.AddDefinition();
        var first = document.AddSprite();
        var second = document.AddSprite();
        second.Nickname = "Torch";
        second.Position = new(2.5f, 4);
        second.Rotation = 30;
        second.CollisionType = TileCollisionType.Blocking;
        second.EnableFog = true;
        second.StartAnimation = true;
        var id = second.Id;
        document.History.ResetSaved();
        using var packages = new AssetPackageCatalog { PasswordProvider = _ => password };
        using var editor = new SpriteEditorControl(document, packages);
        using var host = ShowSpriteDropHost(editor);
        var rows = Descendants(editor).OfType<SpriteDropList>().Single();
        Assert.Same(first, editor.SelectedSprite);

        void Drop(ResourceDragData data)
        {
            var args = AtSpriteRow(rows, data, 1);
            RaiseSpriteDrag(rows, "Enter", args);
            Assert.Equal(DragDropEffects.Copy, args.Effect);
            Assert.Equal(1, rows.DropIndex);
            RaiseSpriteDrag(rows, "Over", args);
            RaiseSpriteDrag(rows, "Drop", args);
            Assert.Equal(DragDropEffects.Copy, args.Effect);
            Assert.Equal(-1, rows.DropIndex);
            Application.DoEvents();
        }

        Drop(new ResourceDragData<TilesheetFrameDrag>(frame));
        Assert.Same(second, editor.SelectedSprite);
        Assert.Equal(("terrain", "ground", 1, 1), (second.Frame!.Tilesheet, second.Frame.RegionName, second.Frame.XTile, second.Frame.YTile));
        Assert.True(second.StartAnimation);
        Assert.Null(second.AnimationKey);
        Drop(new ResourceDragData<AnimationDrag>(animation));
        Assert.Equal("torch.burn", second.AnimationKey);
        Assert.True(second.StartAnimation);
        Drop(new ResourceDragData<SceneLayerDrag>(layer));
        Assert.Equal(("level", "actors"), (second.SceneId, second.SceneLayerId));
        Assert.Equal(id, second.Id);
        Assert.Equal("Torch", second.Nickname);
        Assert.Equal(new PointF(2.5f, 4), second.Position);
        Assert.Equal(30f, second.Rotation);
        Assert.Equal(TileCollisionType.Blocking, second.CollisionType);
        Assert.True(second.EnableFog);
        Assert.Null(first.Frame);
        Assert.Null(first.AnimationKey);
        Assert.Empty(first.SceneId);
        Assert.Null(otherMemberSprite.Frame);
        Assert.Empty(otherMember.TilesheetSources);

        Assert.Equal(frame.Address, SpriteTilesheetAddress(document));
        Assert.Equal(animation.Address, SpriteAnimationAddress(document));
        Assert.Equal(layer.Address, SpriteSceneAddress(document));

        var shortcut = typeof(SpriteEditorControl).GetMethod("ProcessCmdKey", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.True(rows.Focused || rows.Focus());
        Assert.True((bool)shortcut.Invoke(editor, [new Message(), Keys.Control | Keys.Z])!);
        Assert.Empty(member.SceneSources);
        Assert.Equal(("", ""), (second.SceneId, second.SceneLayerId));
        Assert.Equal("torch.burn", second.AnimationKey);
        document.History.Undo();
        Assert.Empty(member.AnimationSources);
        Assert.Null(second.AnimationKey);
        Assert.NotNull(second.Frame);
        document.History.Undo();
        Assert.Empty(member.TilesheetSources);
        Assert.Null(second.Frame);
        Assert.False(document.IsDirty);
        Assert.False(document.History.CanUndo);
        Assert.True(second.StartAnimation);

        var saveAs = Path.Combine(root, "exports", "sprites.gspr");
        document.Save(saveAs, allowInvalid: true);
        Assert.True((bool)shortcut.Invoke(editor, [new Message(), Keys.Control | Keys.Y])!);
        document.History.Redo();
        document.History.Redo();
        Assert.Equal(frame.Address, SpriteTilesheetAddress(document));
        Assert.Equal(animation.Address, SpriteAnimationAddress(document));
        Assert.Equal(layer.Address, SpriteSceneAddress(document));
        Assert.True(document.IsDirty);
        document.Save(saveAs, allowInvalid: true);
        Application.DoEvents();

        var noOp = AtSpriteRow(rows, new ResourceDragData<SceneLayerDrag>(layer), 1);
        RaiseSpriteDrag(rows, "Enter", noOp);
        RaiseSpriteDrag(rows, "Drop", noOp);
        Assert.Equal(DragDropEffects.None, noOp.Effect);
        Assert.False(document.IsDirty);
        document.History.Undo();
        Assert.Empty(member.SceneSources);
        Assert.Equal(("", ""), (second.SceneId, second.SceneLayerId));

        using var reloaded = SpriteDocument.Open(saveAs);
        reloaded.SelectDefinition(reloaded.Definitions[1]);
        Assert.Equal(frame.Address, SpriteTilesheetAddress(reloaded));
        Assert.Equal(animation.Address, SpriteAnimationAddress(reloaded));
        Assert.Equal(layer.Address, SpriteSceneAddress(reloaded));
        var saved = reloaded.Definition.Sprites[1];
        Assert.Equal(id, saved.Id);
        Assert.Equal(("level", "actors", "torch.burn"), (saved.SceneId, saved.SceneLayerId, saved.AnimationKey));
        Assert.Equal(1, saved.Frame!.YTile);
    });

    [Fact]
    public void CanceledStaleAndConflictingDragsLeaveSpritesAndHistoryAlone() => Sta(root =>
    {
        var (frame, animation, layer) = MakeSpriteDragResources(root, packed: false);
        var (conflicting, _, _) = MakeSpriteDragResources(Path.Combine(root, "different"), packed: false);
        using var document = SpriteDocument.Create(root);
        var first = document.AddSprite();
        var second = document.AddSprite();
        document.SetLooseTilesheetSource("terrain", frame.Address.Location.Path);
        document.History.ResetSaved();
        using var editor = new SpriteEditorControl(document);
        using var host = ShowSpriteDropHost(editor);
        var rows = Descendants(editor).OfType<SpriteDropList>().Single();
        var preview = Descendants(editor).OfType<SpritePreviewControl>().Single().DropSurface;

        void Reject(Control target, DragEventArgs args)
        {
            RaiseSpriteDrag(target, "Enter", args);
            RaiseSpriteDrag(target, "Drop", args);
            Assert.Equal(DragDropEffects.None, args.Effect);
            Assert.Equal(-1, rows.DropIndex);
        }

        Reject(rows, AtSpriteRow(rows, new ResourceDragData<TilesheetFrameDrag>(conflicting), 0));
        Reject(rows, AtSpriteRow(rows, new ResourceDragData<TilesheetFrameDrag>(frame with { X = 200 }), 0));
        Reject(rows, AtSpriteRow(rows, new ResourceDragData<SceneLayerDrag>(layer with { LayerId = "other-scene-layer" }), 0));
        Reject(rows, AtSpriteRow(rows, new ResourceDragData<AnimationDrag>(animation with { Address = frame.Address }), 0));
        Reject(rows, AtSpriteRow(rows, new DataObject(DataFormats.FileDrop, new[] { frame.Address.Location.Path }), 0));
        Reject(rows, SpriteDragAt(rows, new ResourceDragData<AnimationDrag>(animation), new Point(4, rows.ClientSize.Height - 2)));

        var stale = new ResourceDragData<AnimationDrag>(animation);
        File.AppendAllText(animation.Address.Location.Path, Environment.NewLine);
        Reject(rows, AtSpriteRow(rows, stale, 0));

        var canceled = AtSpriteRow(rows, new ResourceDragData<SceneLayerDrag>(layer.Address, _ => null), 1);
        RaiseSpriteDrag(rows, "Enter", canceled);
        Assert.Equal(1, rows.DropIndex);
        RaiseSpriteDrag(rows, "Drop", canceled);
        Assert.Equal(DragDropEffects.None, canceled.Effect);
        Assert.Equal(-1, rows.DropIndex);

        var left = AtSpriteRow(rows, new ResourceDragData<SceneLayerDrag>(layer), 1);
        RaiseSpriteDrag(rows, "Enter", left);
        RaiseSpriteDrag(rows, "Leave", EventArgs.Empty);
        Assert.Equal(-1, rows.DropIndex);
        RaiseSpriteDrag(rows, "Drop", left);
        Assert.Equal(DragDropEffects.None, left.Effect);

        var selection = SpriteDragAt(preview, new ResourceDragData<SceneLayerDrag>(layer.Address, _ =>
        {
            editor.SelectSprite(second);
            return layer;
        }), new Point(10, 10));
        RaiseSpriteDrag(preview, "Enter", selection);
        Assert.Equal(DragDropEffects.Copy, selection.Effect);
        RaiseSpriteDrag(preview, "Drop", selection);
        Assert.Equal(DragDropEffects.None, selection.Effect);
        Assert.Same(second, editor.SelectedSprite);

        Assert.Null(first.Frame);
        Assert.Null(second.Frame);
        Assert.Empty(first.SceneId);
        Assert.Empty(second.SceneId);
        Assert.Empty(document.Definition.AnimationSources);
        Assert.Empty(document.Definition.SceneSources);
        Assert.Single(document.Definition.TilesheetSources);
        Assert.False(document.IsDirty);
        Assert.False(document.History.CanUndo);
    });

    [Fact]
    public void HoveredMemberAndDisposedTargetsCannotReceiveADrop() => Sta(root =>
    {
        var (frame, _, _) = MakeSpriteDragResources(root, packed: false);
        using var document = SpriteDocument.Create(root);
        var first = document.AddSprite();
        var firstMember = document.Definition;
        var otherMember = document.AddDefinition();
        var second = document.AddSprite();
        document.SelectDefinition(firstMember);
        document.History.ResetSaved();
        using var editor = new SpriteEditorControl(document);
        using var host = ShowSpriteDropHost(editor);
        var rows = Descendants(editor).OfType<SpriteDropList>().Single();
        var args = AtSpriteRow(rows, new ResourceDragData<TilesheetFrameDrag>(frame), 0);
        RaiseSpriteDrag(rows, "Enter", args);
        Assert.Equal(DragDropEffects.Copy, args.Effect);
        editor.SelectDefinition(otherMember);
        RaiseSpriteDrag(rows, "Drop", args);
        Assert.Equal(DragDropEffects.None, args.Effect);
        RaiseSpriteDrag(rows, "Enter", args);
        Assert.Equal(DragDropEffects.Copy, args.Effect);
        editor.Dispose();
        RaiseSpriteDrag(rows, "Drop", args);
        Assert.Null(first.Frame);
        Assert.Null(second.Frame);
        Assert.False(document.IsDirty);
        Assert.False(document.History.CanUndo);
    });

    private static Form ShowSpriteDropHost(SpriteEditorControl editor)
    {
        var host = new Form { ShowInTaskbar = false, Opacity = 0, Size = new Size(1200, 800) };
        host.Controls.Add(editor);
        host.Show();
        Application.DoEvents();
        return host;
    }

    private static void RaiseSpriteDrag(Control target, string phase, EventArgs args) =>
        typeof(Control).GetMethod("OnDrag" + phase, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, [args]);

    private static DragEventArgs AtSpriteRow(ListBox rows, IDataObject data, int index)
    {
        var bounds = rows.GetItemRectangle(index);
        return SpriteDragAt(rows, data, new Point(bounds.Left + 4, bounds.Top + bounds.Height / 2));
    }

    private static DragEventArgs SpriteDragAt(Control target, IDataObject data, Point client)
    {
        var screen = target.PointToScreen(client);
        return new(data, 0, screen.X, screen.Y, DragDropEffects.Copy, DragDropEffects.None);
    }

    private static ResourceAddress SpriteTilesheetAddress(SpriteDocument document)
    {
        var source = Assert.Single(document.Definition.TilesheetSources);
        return new(new AssetLocation(AssetTypes.TilesheetDefinition, document.ResolveReferencePath(source.GtsPath ?? source.AssetsFilePath!),
            source.AssetEntryName), source.Tilesheet);
    }

    private static ResourceAddress SpriteAnimationAddress(SpriteDocument document)
    {
        var source = Assert.Single(document.Definition.AnimationSources);
        return new(new AssetLocation(AssetTypes.AnimationDefinition, document.ResolveReferencePath(source.GaniPath ?? source.AssetsFilePath!),
            source.AssetEntryName), source.AnimationKey);
    }

    private static ResourceAddress SpriteSceneAddress(SpriteDocument document)
    {
        var source = Assert.Single(document.Definition.SceneSources);
        return new(new AssetLocation(AssetTypes.SceneDefinition, document.ResolveReferencePath(source.GscnPath ?? source.AssetsFilePath!),
            source.AssetEntryName), source.SceneId);
    }

    private static (TilesheetFrameDrag Frame, AnimationDrag Animation, SceneLayerDrag Layer) MakeSpriteDragResources(string root, bool packed, string? password = null)
    {
        Directory.CreateDirectory(root);
        const string pack = "assets.gaf";
        const string sheetEntry = "tiles/nested/atlas.gts";
        const string animationEntry = "animations/nested/torch.gani";
        const string sceneEntry = "scenes/nested/level.gscn";
        const string imageEntry = "images/nested/atlas.png";
        string package = Path.Combine(root, pack);
        string gts = Path.Combine(root, "atlas.gts");
        string gani = Path.Combine(root, "torch.gani");
        string gscn = Path.Combine(root, "level.gscn");

        var sheet = new TilesheetDefinition
        {
            Name = "terrain",
            Image = packed ? new() { AssetEntryName = imageEntry } : new() { FilePath = "atlas.png" },
            Regions = [new() { Name = "ground", Area = new(0, 0, 32, 32), TileSize = new(16, 16) }]
        };
        var animation = new AnimationDefinition
        {
            Key = "torch.burn",
            ThrottleTime = .2,
            Frames = [new() { Tilesheet = "terrain", RegionName = "ground", XTile = 1, YTile = 1 }],
            TilesheetSources = [packed ? AnimationTilesheetSourceDefinition.Packed("terrain", pack, sheetEntry)
                : AnimationTilesheetSourceDefinition.Loose("terrain", "atlas.gts")]
        };
        var scene = new SceneDefinition
        {
            ID = "level",
            Layers = [new() { ID = "background", Columns = 2, Rows = 2, TileWidth = 16, TileHeight = 16 },
                new() { ID = "actors", Columns = 2, Rows = 2, TileWidth = 16, TileHeight = 16 }]
        };

        using var image = new Bitmap(32, 32);
        if (packed)
        {
            using var archive = AssetsFile.LoadOrCreate(package, password, encrypt: password is not null, register: false);
            using var png = new MemoryStream();
            image.Save(png, System.Drawing.Imaging.ImageFormat.Png);
            png.Position = 0;
            archive.Add(AssetTypes.Image, imageEntry, png);
            Add(AssetTypes.TilesheetDefinition, sheetEntry, TilesheetDefinitionSerializer.ToDocumentJson(new() { Definitions = [new() { Name = "other" }, sheet] }));
            Add(AssetTypes.AnimationDefinition, animationEntry, AnimationDefinitionSerializer.ToDocumentJson(new() { Definitions = [new() { Key = "other" }, animation] }));
            Add(AssetTypes.SceneDefinition, sceneEntry, SceneDefinitionSerializer.ToDocumentJson(new() { Definitions = [new() { ID = "other" }, scene] }));
            archive.Save();

            void Add(AssetTypes type, string entry, string json)
            {
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
                archive.Add(type, entry, stream);
            }
        }
        else
        {
            image.Save(Path.Combine(root, "atlas.png"), System.Drawing.Imaging.ImageFormat.Png);
            TilesheetDefinitionSerializer.SaveAll(gts, [new() { Name = "other" }, sheet]);
            AnimationDefinitionSerializer.SaveAll(gani, [new() { Key = "other" }, animation]);
            SceneDefinitionSerializer.SaveAll(gscn, [new() { ID = "other" }, scene]);
        }

        ResourceAddress Address(AssetTypes type, string loose, string entry, string member) =>
            new(new AssetLocation(type, packed ? package : loose, packed ? entry : null), member);
        return (new(Address(AssetTypes.TilesheetDefinition, gts, sheetEntry, "terrain"), "ground", 1, 1),
            new(Address(AssetTypes.AnimationDefinition, gani, animationEntry, "torch.burn")),
            new(Address(AssetTypes.SceneDefinition, gscn, sceneEntry, "level"), "actors"));
    }
}
