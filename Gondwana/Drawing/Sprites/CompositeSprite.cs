using System.Collections.ObjectModel;
using System.Drawing;
using System.Numerics;
using System.Runtime.Serialization;
using Gondwana.Physics.Movement;
using Gondwana.Scenes;
using Newtonsoft.Json;

namespace Gondwana.Drawing.Sprites;

/// <summary>
/// Represents a composite sprite that contains multiple child sprites and manages them as a single unit.
/// All child sprites must belong to the same SceneLayer.
/// </summary>
public class CompositeSprite : IMovableOnSceneLayer
{
    [JsonProperty]
    private readonly List<Sprite> _children = new();

    /// <summary>
    /// Gets or sets the anchor mode that determines how the composite's position is calculated relative to its children.
    /// </summary>
    [JsonProperty]
    public CompositeAnchorMode AnchorMode { get; set; } = CompositeAnchorMode.TopLeft;

    #region ctor

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeSprite"/> class.
    /// </summary>
    public CompositeSprite() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeSprite"/> class with the specified list of sprites.
    /// </summary>
    /// <param name="sprites">The list of sprites to add to this composite.</param>
    public CompositeSprite(List<Sprite> sprites)
    {
        foreach (var sprite in sprites)
            Add(sprite);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeSprite"/> class with the specified array of sprites.
    /// </summary>
    /// <param name="sprites">The array of sprites to add to this composite.</param>
    public CompositeSprite(params Sprite[] sprites) : this(sprites.ToList())
    {
    }

    [OnDeserialized]
    private void OnDeserialized(StreamingContext context)
    {
        foreach (var sprite in _children)
            sprite.Disposing += sprite_Disposing;
    }

    #endregion ctor

    #region IMovable / IMovableOnSceneLayer

    /// <summary>
    /// Gets the movement space used for positioning this composite sprite.
    /// </summary>
    [JsonIgnore]
    public MovementSpace PositionSpace => MovementSpace.Grid;

    /// <summary>
    /// Gets the scene layer that all child sprites belong to.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the composite has no children.</exception>
    [JsonIgnore]
    public SceneLayer SceneLayer
    {
        get
        {
            if (_children.Count == 0)
                throw new InvalidOperationException("CompositeSprite has no children; SceneLayer is undefined.");

            // Enforced by Add(...) invariants.
            return _children[0].SceneLayer;
        }
    }

    /// <summary>
    /// Gets the current position of the composite sprite in scene-layer grid coordinates,
    /// based on its anchor mode.
    /// </summary>
    /// <returns>The grid-space position of the composite's anchor point.</returns>
    public Vector2 GetPosition()
    {
        Rectangle r = Range;
        if (r == Rectangle.Empty)
            return Vector2.Zero;

        PointF anchorWorldPx = AnchorMode switch
        {
            CompositeAnchorMode.TopLeft => new PointF(r.Left, r.Top),
            CompositeAnchorMode.Center => new PointF(
                r.Left + (r.Width / 2f),
                r.Top + (r.Height / 2f)),
            _ => new PointF(r.Left, r.Top)
        };

        PointF anchorGrid = SceneLayer.WorldPxToGrid(anchorWorldPx);
        return new Vector2(anchorGrid.X, anchorGrid.Y);
    }

    /// <summary>
    /// Sets the position of the composite sprite by moving all child sprites to maintain their relative positions.
    /// </summary>
    /// <param name="pos">The new position for the composite's anchor point.</param>
    public void SetPosition(Vector2 pos)
    {
        Vector2 oldAnchor = GetPosition();
        Vector2 delta = pos - oldAnchor;

        if (delta == Vector2.Zero || _children.Count == 0)
            return;

        // Translate all children by delta based on their CURRENT positions.
        // This preserves independent child movement naturally.
        foreach (var sprite in _children)
        {
            Vector2 childPos = GetSpritePosition(sprite);
            sprite.SetPosition(childPos + delta);
        }
    }

    /// <summary>
    /// Translates the composite sprite by the specified delta vector.
    /// </summary>
    /// <param name="delta">The translation vector to apply.</param>
    public void Translate(Vector2 delta)
    {
        if (delta == Vector2.Zero)
            return;

        SetPosition(GetPosition() + delta);
    }

    #endregion IMovable / IMovableOnSceneLayer

    #region methods

    /// <summary>
    /// Adds an existing sprite at its current position (no reposition).
    /// </summary>
    /// <param name="sprite">The sprite to add to this composite.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sprite"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the sprite belongs to a different SceneLayer than existing children.</exception>
    public void Add(Sprite sprite)
    {
        if (sprite is null) throw new ArgumentNullException(nameof(sprite));
        if (_children.Contains(sprite)) return;

        EnforceSameSceneLayer(sprite);

        sprite.Disposing += sprite_Disposing;
        _children.Add(sprite);
    }

    /// <summary>
    /// Add a sprite and place it at an absolute position (in the composite's PositionSpace).
    /// </summary>
    /// <param name="sprite">The sprite to add to this composite.</param>
    /// <param name="absolutePosition">The absolute position to place the sprite at.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sprite"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the sprite belongs to a different SceneLayer than existing children.</exception>
    public void AddChildAtPosition(Sprite sprite, Vector2 absolutePosition)
    {
        if (sprite is null) throw new ArgumentNullException(nameof(sprite));
        if (_children.Contains(sprite)) return;

        EnforceSameSceneLayer(sprite);

        sprite.Disposing += sprite_Disposing;
        _children.Add(sprite);

        sprite.SetPosition(absolutePosition);
    }

    /// <summary>
    /// Add a sprite and place it by offset from the composite anchor.
    /// Offset is interpreted in grid coordinates relative to the current composite anchor.
    /// </summary>
    /// <param name="sprite">The sprite to add to this composite.</param>
    /// <param name="offsetFromCompositeAnchor">The offset from the composite's anchor point.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sprite"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the sprite belongs to a different SceneLayer than existing children.</exception>
    public void AddChildWithOffset(Sprite sprite, Vector2 offsetFromCompositeAnchor)
    {
        if (sprite is null) throw new ArgumentNullException(nameof(sprite));
        if (_children.Contains(sprite)) return;

        EnforceSameSceneLayer(sprite);

        // If this is the first child:
        // - Its CURRENT position becomes the anchor.
        // - Offset is interpreted relative to that, so we only reposition if offset != 0.
        if (_children.Count == 0)
        {
            sprite.Disposing += sprite_Disposing;
            _children.Add(sprite);

            Vector2 firstPos = GetSpritePosition(sprite);
            Vector2 target = firstPos + offsetFromCompositeAnchor;

            if (offsetFromCompositeAnchor != Vector2.Zero)
                sprite.SetPosition(target);

            return;
        }

        // Otherwise offset is from the current composite anchor (derived from Range).
        Vector2 anchor = GetPosition();
        AddChildAtPosition(sprite, anchor + offsetFromCompositeAnchor);
    }

    /// <summary>
    /// Removes a sprite from this composite.
    /// </summary>
    /// <param name="sprite">The sprite to remove.</param>
    public void Remove(Sprite sprite)
    {
        if (sprite is null) return;
        if (!_children.Contains(sprite)) return;

        sprite.Disposing -= sprite_Disposing;
        _children.Remove(sprite);
    }

    private void sprite_Disposing(Sprite sprite)
    {
        sprite.Disposing -= sprite_Disposing;
        _children.Remove(sprite);
    }

    private void EnforceSameSceneLayer(Sprite sprite)
    {
        if (_children.Count == 0)
            return;

        if (!ReferenceEquals(sprite.SceneLayer, _children[0].SceneLayer))
            throw new InvalidOperationException("All sprites in a CompositeSprite must belong to the same SceneLayer.");
    }

    private static Vector2 GetSpritePosition(Sprite sprite)
    {
        // Adjust this if your Sprite uses a different property name/type.
        // Assumed: SceneLayerCoordinates is PointF.
        PointF p = sprite.SceneLayerCoordinates;
        return new Vector2(p.X, p.Y);
    }

    #endregion methods

    #region properties

    /// <summary>
    /// Gets a read-only collection of all child sprites in this composite.
    /// </summary>
    [JsonIgnore]
    public ReadOnlyCollection<Sprite> Children => _children.AsReadOnly();

    /// <summary>
    /// Gets the bounding rectangle that encompasses all child sprites.
    /// Returns <see cref="Rectangle.Empty"/> if there are no children.
    /// </summary>
    [JsonIgnore]
    public Rectangle Range
    {
        get
        {
            if (_children.Count == 0)
                return Rectangle.Empty;

            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;

            foreach (var sprite in _children)
            {
                var r = sprite.DrawLocationWorld;

                if (r.Left < minX) minX = r.Left;
                if (r.Top < minY) minY = r.Top;
                if (r.Right > maxX) maxX = r.Right;
                if (r.Bottom > maxY) maxY = r.Bottom;
            }

            return Rectangle.FromLTRB(minX, minY, maxX, maxY);
        }
    }

    #endregion properties
}

/// <summary>
/// Defines the anchor mode for a composite sprite, determining how its position is calculated.
/// </summary>
public enum CompositeAnchorMode
{
    /// <summary>
    /// The anchor is at the top-left corner of the bounding rectangle.
    /// </summary>
    TopLeft = 0,

    /// <summary>
    /// The anchor is at the center of the bounding rectangle.
    /// </summary>
    Center = 1
}
