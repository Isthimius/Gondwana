using System.Drawing;
using Gondwana.Drawing;
using Gondwana.Drawing.Animation;
using Gondwana.Drawing.Collisions;
using Gondwana.Physics.Collisions;
using Newtonsoft.Json;

namespace Gondwana.Scenes;

/// <summary>
/// Represents the values stored at a single location on a SceneLayer
/// </summary>
[JsonObject(IsReference = true)]
public class SceneLayerTile : Tile
{
    #region private / internal fields

    [JsonIgnore]
    internal SceneLayer parentSceneLayer;

    /// <summary>
    /// each SceneLayerTile in array know its location in the array in parentSceneLayer;
    /// this is the position in the SceneLayer array, not pixel coordinates
    /// </summary>
    [JsonProperty]
    internal Point sceneLayerCoordinates;

    #endregion private / internal fields

    #region constructors / finalizer

    [JsonConstructor]
    private SceneLayerTile()
    {
        zOrder = 0;
        visible = true;

        // Parent ownership is reconstructed by SceneLayer.OnDeserialized().
        // Use the null-object layer while Json.NET populates inherited Tile
        // properties whose setters may invalidate the parent layer.
        parentSceneLayer = SceneLayer.Empty;
    }

    internal SceneLayerTile(SceneLayer sceneLayer)
    {
        zOrder = 0;
        visible = true;
        parentSceneLayer = sceneLayer;
    }

    /// <summary>
    /// Ensures this fixed layer tile has the collider used by the public
    /// <see cref="Tile.Collider"/> and <see cref="Tile.CollisionsEnabled"/> APIs.
    /// </summary>
    internal void EnsureCollider()
    {
        if (_collider is null)
        {
            AttachCollider(new TileCollider(
                this,
                collisionGroup: CollisionMasks.None,
                collidesWith: CollisionMasks.None));
        }
    }

    /// <summary>
    /// Finalizes an instance of the <see cref="SceneLayerTile"/> class, releasing resources if the tile
    /// was not explicitly disposed.
    /// </summary>
    /// <remarks>
    /// This finalizer ensures that tile resources are cleaned up even if <see cref="Dispose"/>
    /// is not called explicitly. However, it is recommended to always call <see cref="Dispose"/>
    /// or rely on parent layer disposal to ensure deterministic cleanup.
    /// </remarks>
    ~SceneLayerTile()
    {
        Dispose();
    }

    #endregion constructors / finalizer

    /// <inheritdoc/>
    public override int ZOrder
    {
        get => base.ZOrder;
        set
        {
            bool changed = zOrder != value;
            base.ZOrder = value;

            if (changed && !ReferenceEquals(parentSceneLayer, SceneLayer.Empty))
                parentSceneLayer.InvalidateFixedGridRenderEligibility();
        }
    }

    /// <inheritdoc/>
    public override bool EnableFog
    {
        get => base.EnableFog;
        set
        {
            bool changed = enableFog != value;
            base.EnableFog = value;

            if (changed && !ReferenceEquals(parentSceneLayer, SceneLayer.Empty))
                parentSceneLayer.InvalidateFixedGridRenderEligibility();
        }
    }

    private TileTransform _transform;

    /// <summary>Gets or sets this placement's orthogonal orientation without changing its source frame.</summary>
    [JsonProperty]
    public TileTransform Transform
    {
        get => _transform;
        set
        {
            if (!Enum.IsDefined(value)) throw new ArgumentOutOfRangeException(nameof(value));
            if (_transform == value) return;
            SceneLayer.RefreshQueue.AddWorldRect(DrawLocationWorld);
            _transform = value;
            if (!ReferenceEquals(SceneLayer, SceneLayer.Empty))
            {
                if (value == TileTransform.Identity) SceneLayer.TransformedTiles.Remove(this);
                else SceneLayer.TransformedTiles.Add(this);
            }
            SceneLayer.RefreshQueue.AddWorldRect(DrawLocationWorld);
        }
    }

    /// <summary>Gets source frame dimensions after placement orientation.</summary>
    [JsonIgnore]
    public Size EffectiveTileSize => TileTransformGeometry.TransformSize(CurrentFrame.TileSize, Transform);

    /// <summary>Gets oriented overhang. The source value remains on CurrentFrame.</summary>
    [JsonIgnore]
    public override Spacing Overhang => TileTransformGeometry.TransformSpacing(CurrentFrame.Overhang, Transform);

    /// <summary>Gets oriented atlas padding for inspection, never for source slicing.</summary>
    [JsonIgnore]
    public Spacing EffectiveTilePadding => TileTransformGeometry.TransformSpacing(CurrentFrame.TilePadding, Transform);

    /// <summary>Gets the oriented collision insets, including the current frame-derived adjustment.</summary>
    [JsonIgnore]
    public CollisionAdjust EffectiveCollisionAdjust => TileTransformGeometry.TransformCollisionAdjust(AdjustCollisionArea, Transform);

    /// <inheritdoc/>
    [JsonIgnore]
    public override Rectangle CollisionArea => EffectiveCollisionAdjust.ApplyTo(DrawLocationWorld);

    /// <inheritdoc/>
    public override void Draw(Gondwana.Rendering.Backbuffers.BackbufferBase backbuffer, RectangleF destRectScreen)
    {
        if (Transform == TileTransform.Identity)
        {
            base.Draw(backbuffer, destRectScreen);
            return;
        }

        var origin = TileTransformGeometry.MapToDestination(new(0, 0), destRectScreen, Transform);
        var x = TileTransformGeometry.MapToDestination(new(1, 0), destRectScreen, Transform);
        var y = TileTransformGeometry.MapToDestination(new(0, 1), destRectScreen, Transform);
        var matrix = new global::SkiaSharp.SKMatrix
        {
            ScaleX = x.X - origin.X,
            SkewX = y.X - origin.X,
            TransX = origin.X,
            SkewY = x.Y - origin.Y,
            ScaleY = y.Y - origin.Y,
            TransY = origin.Y,
            Persp2 = 1
        };
        var canvas = backbuffer.Canvas;
        canvas.Save();
        try
        {
            canvas.Concat(ref matrix);
            base.Draw(backbuffer, new RectangleF(0, 0, 1, 1));
        }
        finally { canvas.Restore(); }
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        parentSceneLayer?.TransformedTiles.Remove(this);
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    #region public properties

    /// <summary>
    /// Gets the z-order (rendering depth) of this tile within its layer.
    /// </summary>
    /// <value>
    /// An integer representing the tile's rendering priority. Lower values render first (behind),
    /// higher values render last (in front). For scene layer tiles, this is typically 0.
    /// </value>
    /// <remarks>
    /// This property overrides the base <see cref="Tile.ZOrder"/> property with a new implementation
    /// that returns the tile's z-order for rendering within the scene layer. Z-order affects the
    /// rendering order when multiple drawable objects occupy overlapping positions.
    /// </remarks>
    [JsonIgnore]
    public virtual new int ZOrder => zOrder;

    /// <summary>
    /// Gets the world-space pixel rectangle where this tile should be drawn.
    /// </summary>
    /// <value>
    /// A <see cref="Rectangle"/> representing the tile's drawing bounds in world pixel coordinates,
    /// calculated by the parent layer's coordinate system and including any tile overhang.
    /// </value>
    /// <remarks>
    /// <para>
    /// This property uses the parent layer's coordinate system to transform the tile's grid
    /// coordinates into world-space pixel coordinates. The returned rectangle includes any
    /// overhang pixels defined by the tile's tilesheet, ensuring that tall or wide tiles
    /// (such as trees or buildings) are rendered with their full graphical extent.
    /// </para>
    /// <para>
    /// The coordinate transformation depends on the layer's active coordinate system type
    /// (orthogonal, isometric, hexagonal, etc.) and accounts for the layer's tile size,
    /// origin offset, and other rendering properties.
    /// </para>
    /// </remarks>
    [JsonIgnore]
    public override Rectangle DrawLocationWorld => parentSceneLayer.CoordinateSystem.GetPixelRangeForTile(this, true);

    /// <summary>
    /// Gets a value indicating whether this tile's position is fixed within its grid.
    /// </summary>
    /// <value>
    /// Always returns <c>true</c> for scene layer tiles, indicating that they do not move
    /// independently from their assigned grid position.
    /// </value>
    /// <remarks>
    /// <para>
    /// Scene layer tiles are fundamentally different from sprites in that they occupy fixed
    /// positions within the layer's tile grid. While the entire layer can be scrolled, individual
    /// tiles cannot move to arbitrary positions. This property distinguishes tiles from sprites
    /// for rendering and collision detection purposes.
    /// </para>
    /// <para>
    /// This property overrides <see cref="Tile.IsPositionFixed"/> to reflect the fixed nature
    /// of tiles within scene layers.
    /// </para>
    /// </remarks>
    [JsonIgnore]
    public override bool IsPositionFixed => true;

    /// <summary>
    /// Gets the grid coordinates of this tile within its parent scene layer as a floating-point value.
    /// </summary>
    /// <value>
    /// A <see cref="PointF"/> representing the tile's column and row position in the layer's grid,
    /// where X is the column index and Y is the row index.
    /// </value>
    /// <remarks>
    /// <para>
    /// This property returns the tile's position in grid space (column, row), not pixel space.
    /// Grid coordinates are zero-based, with (0, 0) representing the top-left tile of the layer.
    /// </para>
    /// <para>
    /// For integer grid coordinates, use <see cref="GridCoordinatesAbs"/> instead. This property
    /// returns a <see cref="PointF"/> for compatibility with base class APIs that work with
    /// fractional coordinates.
    /// </para>
    /// </remarks>
    [JsonIgnore]
    public override PointF SceneLayerCoordinates => (PointF)sceneLayerCoordinates;

    /// <summary>
    /// Gets the absolute grid coordinates of this tile within its parent scene layer as integer values.
    /// </summary>
    /// <value>
    /// A <see cref="Point"/> representing the tile's exact column and row position in the layer's grid,
    /// where X is the column index and Y is the row index.
    /// </value>
    /// <remarks>
    /// <para>
    /// This property provides integer-based access to the tile's grid position, which is more
    /// appropriate for most tile-based operations than the floating-point <see cref="SceneLayerCoordinates"/>
    /// property. Grid coordinates are zero-based, with (0, 0) representing the top-left tile.
    /// </para>
    /// <para>
    /// Use this property when you need exact grid indices for array access, adjacency checks,
    /// or other grid-based calculations that require integer precision.
    /// </para>
    /// </remarks>
    [JsonIgnore]
    public Point GridCoordinatesAbs => sceneLayerCoordinates;

    /// <summary>
    /// Gets the parent scene layer that contains this tile.
    /// </summary>
    /// <value>
    /// The <see cref="Gondwana.Scenes.SceneLayer"/> instance that owns this tile.
    /// </value>
    /// <remarks>
    /// <para>
    /// This property provides access to the tile's parent layer, which is useful for accessing
    /// layer-level properties such as tile size, coordinate system, parallax settings, and
    /// other rendering parameters that affect how the tile is displayed.
    /// </para>
    /// <para>
    /// Every scene layer tile is associated with exactly one parent layer, established when
    /// the layer is created. The relationship is immutable for the tile's lifetime.
    /// </para>
    /// </remarks>
    [JsonIgnore]
    public override SceneLayer SceneLayer => parentSceneLayer;

    /// <summary>
    /// Gets or sets a value indicating whether the tile's animator is enabled.
    /// </summary>
    /// <value>
    /// <c>true</c> if the tile has an active <see cref="Animator"/> instance; otherwise, <c>false</c>.
    /// </value>
    /// <remarks>
    /// <para>
    /// Setting this property to <c>true</c> creates a new <see cref="Animator"/> instance for this tile
    /// if one doesn't already exist, enabling animation cycle playback. Setting it to <c>false</c>
    /// disposes the existing animator and stops any active animations.
    /// </para>
    /// <para>
    /// The animator manages frame cycling for animated tiles, allowing tiles to display animated
    /// sequences defined by <see cref="Cycle"/> definitions. Animations are updated automatically
    /// during the engine's background update phase.
    /// </para>
    /// <para>
    /// Creating animators adds overhead, so this property should only be set to <c>true</c> for
    /// tiles that require animation. Static tiles should keep this property as <c>false</c>.
    /// </para>
    /// </remarks>
    [JsonProperty]
    public bool EnableAnimator
    {
        get { return (animator != null); }
        set
        {
            if (value)
            {
                if (animator == null)
                    animator = new Animator(this);

                return;
            }

            animator?.Dispose();
            animator = null;
        }
    }

    #endregion public properties
}
