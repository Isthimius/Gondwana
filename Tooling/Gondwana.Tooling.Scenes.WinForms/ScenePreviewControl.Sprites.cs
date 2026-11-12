using System.Drawing.Drawing2D;
using Gondwana.Drawing.Sprites;
using Gondwana.Drawing.Sprites.GSPR;
using Gondwana.Scenes.GSCN;
using Gondwana.Tooling.Assets;

namespace Gondwana.Tooling.Scenes.WinForms;

internal sealed record SceneSpriteVisual(ResourceAddress Address, SpriteInstanceDefinition Sprite,
    SceneLayerDefinition Layer, SceneSpriteArtwork? Artwork, ISceneSpriteOwner? Owner, string? Restriction)
{
    internal bool SameInstance(SceneSpriteVisual other) => Address == other.Address &&
        (Sprite.Id == Guid.Empty ? ReferenceEquals(Sprite, other.Sprite) : Sprite.Id == other.Sprite.Id);
}

internal sealed partial class ScenePreviewControl
{
    private sealed record SpriteGesture(SceneSpriteVisual Visual, Point Start, PointF GrabOffset);
    private IReadOnlyList<SceneSpriteVisual> _sprites = [];
    private SceneSpriteVisual? _selectedSprite;
    private SpriteGesture? _spriteGesture;
    private (SceneSpriteVisual Visual, PointF Position)? _spriteGhost;
    private bool _showSprites = true;

    internal event Func<SceneSpriteVisual?, bool>? SpriteSelected;
    internal event Action<SceneSpriteVisual, PointF>? SpriteMoved;
    internal event Action? SpriteMoveCanceled;
    internal bool HasSpriteGesture => _spriteGesture is not null;
    internal string? SpriteStatus { get; set; }

    internal void SetSprites(IReadOnlyList<SceneSpriteVisual> sprites)
    {
        _sprites = sprites;
        if (_selectedSprite is { } selected)
            _selectedSprite = sprites.FirstOrDefault(selected.SameInstance);
        UpdateSceneBounds();
        UpdateExtent();
        Invalidate();
    }

    internal void ShowSprites(bool shown)
    {
        _showSprites = shown;
        if (!shown)
        {
            CancelSpriteGesture();
            SelectSprite(null);
            SpriteSelected?.Invoke(null);
        }
        UpdateSceneBounds();
        UpdateExtent();
        Invalidate();
    }

    internal void SelectSprite(SceneSpriteVisual? sprite)
    {
        _selectedSprite = sprite;
        Invalidate();
    }

    internal void SetSpriteGhost(SceneSpriteVisual? sprite, PointF position = default)
    {
        _spriteGhost = sprite is null ? null : (sprite, position);
        Invalidate();
    }

    internal bool TryGetSpriteDropPosition(SceneLayerDefinition layer, Point screen, bool snap, out PointF position)
    {
        position = default;
        var client = PointToClient(screen);
        if (!ClientRectangle.Contains(client) || _definition?.Layers.Contains(layer) != true ||
            !ReferenceEquals(layer, _selectedLayer) || !IsLayerDisplayed(layer))
            return false;
        UpdateOffset();
        position = PositionAt(layer, ScreenToWorld(client), snap);
        return true;
    }

    internal PointF PositionAt(SceneLayerDefinition layer, PointF world, bool snap)
    {
        var position = GetProjection(layer).WorldPxToContinuousGrid(world);
        return snap ? new(MathF.Round(position.X, MidpointRounding.AwayFromZero),
            MathF.Round(position.Y, MidpointRounding.AwayFromZero)) : position;
    }

    private Rectangle SpriteBounds(SceneSpriteVisual visual, PointF position)
    {
        var projection = GetProjection(visual.Layer);
        var sprite = visual.Sprite;
        var size = sprite.RenderSize.IsEmpty ? new Size(projection.TileWidth, projection.TileHeight) : sprite.RenderSize;
        return Sprite.GetPlacementBounds(projection, position, size, sprite.HorizAlign, sprite.VertAlign, new(sprite.NudgeX, sprite.NudgeY));
    }

    private IEnumerable<SceneSpriteVisual> SpritesInDrawOrder(SceneLayerDefinition layer) => _showSprites
        ? _sprites.Where(visual => ReferenceEquals(visual.Layer, layer))
            .OrderBy(visual => visual.Sprite.ZOrder)
            .ThenBy(visual => SpriteBounds(visual, visual.Sprite.Position).Bottom - (visual.Artwork?.BottomOverhang ?? 0) - 1)
            .ThenBy(visual => visual.Sprite.Position.X)
            .ThenBy(visual => visual.Sprite.Id)
        : [];

    internal SceneSpriteVisual? HitSprite(Point client)
    {
        if (!_showSprites || _definition is null) return null;
        UpdateOffset();
        var world = ScreenToWorld(client);
        var frontToBack = _definition.Layers.Where(IsLayerDisplayed).OrderBy(layer => layer.ZOrder)
            .SelectMany(SpritesInDrawOrder).Reverse();
        foreach (var visual in frontToBack)
        {
            var bounds = SpriteBounds(visual, visual.Sprite.Position);
            using var rotation = new Matrix();
            rotation.RotateAt(-visual.Sprite.Rotation, Center(bounds));
            PointF[] points = [world];
            rotation.TransformPoints(points);
            if (((RectangleF)bounds).Contains(points[0]))
                return visual;
        }
        return null;
    }

    private bool TryStartSpriteGesture(Point point)
    {
        var sprite = HitSprite(point);
        if (sprite is null)
        {
            SelectSprite(null);
            SpriteSelected?.Invoke(null);
            return false;
        }

        CancelGesture();
        Focus();
        SelectSprite(sprite);
        if (SpriteSelected?.Invoke(sprite) == true)
        {
            var mouse = ScreenToWorld(point);
            var anchor = GetProjection(sprite.Layer).GridToWorldPx(sprite.Sprite.Position);
            _spriteGesture = new(sprite, point, new(mouse.X - anchor.X, mouse.Y - anchor.Y));
            Capture = true;
            if (!Capture) CancelSpriteGesture();
        }
        return true;
    }

    private void ContinueSpriteGesture(Point point, bool snap)
    {
        if (_spriteGesture is not { } gesture) return;
        var size = SystemInformation.DragSize;
        var threshold = new Rectangle(gesture.Start.X - size.Width / 2, gesture.Start.Y - size.Height / 2, size.Width, size.Height);
        if (_spriteGhost is null && threshold.Contains(point)) return;
        if (!ClientRectangle.Contains(point) || !IsLayerDisplayed(gesture.Visual.Layer))
        {
            SetSpriteGhost(null);
            return;
        }

        UpdateOffset();
        var mouse = ScreenToWorld(point);
        var world = new PointF(mouse.X - gesture.GrabOffset.X, mouse.Y - gesture.GrabOffset.Y);
        var position = point == gesture.Start ? gesture.Visual.Sprite.Position : PositionAt(gesture.Visual.Layer, world, snap);
        SetSpriteGhost(gesture.Visual, position);
    }

    private void FinishSpriteGesture(Point point, bool snap)
    {
        ContinueSpriteGesture(point, snap);
        var gesture = _spriteGesture;
        var candidate = _spriteGhost;
        _spriteGesture = null;
        SetSpriteGhost(null);
        Capture = false;
        if (gesture is not null && candidate is { } ghost && ghost.Position != gesture.Visual.Sprite.Position)
            SpriteMoved?.Invoke(gesture.Visual, ghost.Position);
        else
            SpriteMoveCanceled?.Invoke();
    }

    internal void CancelSpriteGesture()
    {
        if (_spriteGesture is null) return;
        _spriteGesture = null;
        SetSpriteGhost(null);
        Capture = false;
        SpriteMoveCanceled?.Invoke();
    }

    private static PointF Center(RectangleF bounds) => new(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);

    private void DrawSprite(Graphics graphics, SceneSpriteVisual sprite, PointF position, bool ghost = false)
    {
        if (!ghost && _spriteGesture?.Visual.SameInstance(sprite) == true && _spriteGhost is not null)
            return;

        var bounds = ToScreen(SpriteBounds(sprite, position));
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        var state = graphics.Save();
        try
        {
            using var transform = new Matrix();
            transform.RotateAt(sprite.Sprite.Rotation, Center(bounds));
            graphics.MultiplyTransform(transform);
            if (sprite.Artwork is { } artwork)
                graphics.DrawImage(artwork.Image, bounds, artwork.Bounds, GraphicsUnit.Pixel);
            else
            {
                using var placeholder = new SolidBrush(Color.FromArgb(125, 115, 65, 140));
                graphics.FillRectangle(placeholder, bounds);
                using var cross = new Pen(Color.Plum);
                graphics.DrawLine(cross, bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);
                graphics.DrawLine(cross, bounds.Right, bounds.Top, bounds.Left, bounds.Bottom);
            }

            if (ghost || _selectedSprite?.SameInstance(sprite) == true)
            {
                using var outline = new Pen(Color.MediumSpringGreen, 2f) { DashStyle = ghost ? DashStyle.Dash : DashStyle.Solid };
                graphics.DrawRectangle(outline, bounds.X, bounds.Y, bounds.Width, bounds.Height);
            }
        }
        finally { graphics.Restore(state); }
    }

    private void IncludeSpriteBounds(IEnumerable<SceneLayerDefinition> layers)
    {
        foreach (var sprite in layers.SelectMany(SpritesInDrawOrder))
        {
            var bounds = SpriteBounds(sprite, sprite.Sprite.Position);
            var corners = new PointF[] { bounds.Location, new(bounds.Right, bounds.Top), new(bounds.Right, bounds.Bottom), new(bounds.Left, bounds.Bottom) };
            using var rotation = new Matrix();
            rotation.RotateAt(sprite.Sprite.Rotation, Center(bounds));
            rotation.TransformPoints(corners);
            var visual = RectangleF.FromLTRB(corners.Min(point => point.X), corners.Min(point => point.Y),
                corners.Max(point => point.X), corners.Max(point => point.Y));
            _worldBounds = RectangleF.Union(_worldBounds, visual);
        }
    }
}
