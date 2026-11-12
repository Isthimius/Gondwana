using System.Reflection;
using Gondwana.Drawing.Coordinates;
using Gondwana.Drawing.Sprites.GSPR;
using Gondwana.Scenes;
using Gondwana.Scenes.GSCN;

namespace Gondwana.Tooling.Scenes.WinForms.Tests;

public sealed partial class SceneEditorTests
{
    [Theory]
    [MemberData(nameof(DropProjections))]
    public void SpritePositionsUseContinuousRuntimeAnchorsAtZoomAndScroll(CoordinateSystemTypes coordinateSystem) =>
        WithResourceDropEditor((editor, preview, _) =>
        {
            var layer = editor.Document.Definition.Layers[0];
            layer.CoordinateSystemType = coordinateSystem;
            layer.Columns = 75;
            layer.Rows = 70;
            layer.TileWidth = 48;
            layer.TileHeight = 32;
            layer.OriginPx = new(17, -19);
            editor.Document.MarkChanged();
            Application.DoEvents();
            SelectDropLayer(editor, layer);
            preview.SetZoom(2f);
            preview.AutoScrollPosition = new(100, 75);
            var projection = SpriteProjection(preview, layer);
            var negative = new PointF(-1.25f, 2.5f);
            Assert.Equal(negative, preview.PositionAt(layer, projection.GridToWorldPx(negative), snap: false));
            Assert.Equal(new PointF(-1, 3), preview.PositionAt(layer, projection.GridToWorldPx(negative), snap: true));

            var fractional = new PointF(6.25f, 5.5f);
            var world = projection.GridToWorldPx(fractional);
            var client = SpriteClient(preview, world);
            if (!preview.ClientRectangle.Contains(client))
            {
                var scroll = preview.AutoScrollPosition;
                preview.AutoScrollPosition = new(
                    Math.Max(0, client.X - scroll.X - preview.ClientSize.Width / 2),
                    Math.Max(0, client.Y - scroll.Y - preview.ClientSize.Height / 2));
                client = SpriteClient(preview, world);
            }
            Assert.NotEqual(Point.Empty, preview.AutoScrollPosition);
            Assert.True(preview.TryGetSpriteDropPosition(layer, preview.PointToScreen(client), snap: false, out var position));
            Assert.InRange(position.X, fractional.X - .04f, fractional.X + .04f);
            Assert.InRange(position.Y, fractional.Y - .04f, fractional.Y + .04f);
        });

    [Fact]
    public void StandaloneDrawsSavedSpritesAndSelectsTheFrontRotatedSpriteReadOnly() =>
        WithResourceDropEditor((editor, preview, root) =>
        {
            var document = editor.Document;
            var layer = document.Definition.Layers[0];
            layer.Columns = 12;
            layer.Rows = 12;
            layer.TileWidth = 32;
            layer.TileHeight = 24;
            document.Save(Path.Combine(root, "scene.gscn"));
            var back = new SpriteInstanceDefinition
            {
                Id = Guid.NewGuid(),
                Nickname = "back",
                SceneId = document.Definition.ID,
                SceneLayerId = layer.ID,
                Position = new(3, 3),
                RenderSize = new(50, 50)
            };
            var front = new SpriteInstanceDefinition
            {
                Id = Guid.NewGuid(),
                Nickname = "front",
                SceneId = document.Definition.ID,
                SceneLayerId = layer.ID,
                Position = new(3, 3),
                RenderSize = new(40, 12),
                Rotation = 90f,
                ZOrder = 2
            };
            SpriteDefinitionSerializer.SaveAll(Path.Combine(root, "actors.gspr"),
                [new() { Key = "actors", Sprites = [back, front] }]);
            editor.InvalidateSpriteOwners(reloadSaved: true);
            Application.DoEvents();
            preview.SetZoom(1);

            var projection = SpriteProjection(preview, layer);
            var anchor = projection.GridToWorldPx(front.Position);
            var center = SpriteClient(preview, new(anchor.X + 16, anchor.Y + 18));
            var extension = SpriteClient(preview, new(anchor.X + 16, anchor.Y + 32));
            Assert.Equal(front.Id, preview.HitSprite(center)!.Sprite.Id);
            Assert.Equal(front.Id, preview.HitSprite(extension)!.Sprite.Id);
            ClickPreview(preview, center);
            Assert.Contains("Read-only sprite", preview.SpriteStatus);
            Assert.Contains("Artwork unresolved", preview.SpriteStatus);
            Assert.Null(editor.SelectedSpriteHistory);
            Assert.False(preview.HasSpriteGesture);
            Assert.False(document.IsDirty);

            layer.Visible = false;
            document.MarkChanged();
            Application.DoEvents();
            Assert.Null(preview.HitSprite(center));
        });

    private static SceneLayer SpriteProjection(ScenePreviewControl preview, SceneLayerDefinition layer) =>
        (SceneLayer)typeof(ScenePreviewControl).GetMethod("GetProjection", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(preview, [layer])!;

    private static Point SpriteClient(ScenePreviewControl preview, PointF world)
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(ScenePreviewControl).GetMethod("UpdateOffset", Private)!.Invoke(preview, []);
        var offset = (PointF)typeof(ScenePreviewControl).GetField("_offset", Private)!.GetValue(preview)!;
        return Point.Round(new PointF(world.X * preview.Zoom + offset.X, world.Y * preview.Zoom + offset.Y));
    }
}
