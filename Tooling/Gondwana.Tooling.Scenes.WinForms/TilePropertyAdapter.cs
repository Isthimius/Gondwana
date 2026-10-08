using System.ComponentModel;
using Gondwana.Drawing;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Physics.Collisions;
using Gondwana.Scenes.GSCN;
using Gondwana.Tooling.Scenes.Editing;

namespace Gondwana.Tooling.Scenes.WinForms;

/// <summary>
/// PropertyGrid adapter that preserves sparse GSCN tiles: selecting a cell does
/// not create a definition until the user changes a value or assigns content.
/// </summary>
internal sealed class TilePropertyAdapter
{
    private readonly SceneDocument _document;
    private readonly SceneLayerDefinition _layer;
    private readonly int _x;
    private readonly int _y;
    private readonly Action _changed;
    private readonly Func<(TilesheetRegionDefinition? Region, SceneFrameDefinition? Frame)>? _region;

    /// <summary>
    /// Initializes a new instance of the <c>TilePropertyAdapter</c> class.
    /// </summary>
    /// <param name="document">The document displayed or edited by the control.</param>
    /// <param name="layer">The layer.</param>
    /// <param name="x">The horizontal coordinate.</param>
    /// <param name="y">The vertical coordinate.</param>
    /// <param name="changed">The callback invoked when a property changes.</param>
    /// <param name="region">The region.</param>
    public TilePropertyAdapter(
        SceneDocument document,
        SceneLayerDefinition layer,
        int x,
        int y,
        Action changed, Func<(TilesheetRegionDefinition? Region, SceneFrameDefinition? Frame)>? region = null)
    {
        _document = document;
        _layer = layer;
        _x = x;
        _y = y;
        _changed = changed;
        _region = region;
    }

    private SceneLayerTileDefinition? Tile =>
        _document.FindTile(_layer, _x, _y);

    private SceneLayerTileDefinition Mutable()
    {
        var tile = _document.GetOrCreateTile(_layer, _x, _y);
        return tile;
    }

    private void Change(Action<SceneLayerTileDefinition> change)
    {
        bool existed = Tile is not null;
        var tile = Mutable();
        change(tile);

        // GetOrCreateTile marks a newly materialized sparse cell dirty.
        // Existing cells still need an explicit notification after mutation.
        if (existed)
            _document.MarkChanged();

        _changed();
    }

    /// <summary>
    /// Gets the x.
    /// </summary>
    [Category("Cell"), ReadOnly(true)]
    public int X => _x;

    /// <summary>
    /// Gets the y.
    /// </summary>
    [Category("Cell"), ReadOnly(true)]
    public int Y => _y;

    /// <summary>
    /// Gets whether the object has explicit definition.
    /// </summary>
    [Category("Cell"), ReadOnly(true)]
    public bool HasExplicitDefinition => Tile is not null;

    /// <summary>
    /// Gets or sets the unique identifier.
    /// </summary>
    [Category("Identity")]
    public Guid Id
    {
        get => Tile?.Id ?? Guid.Empty;
        set => Change(tile => tile.Id = value);
    }

    /// <summary>
    /// Gets or sets the optional lookup name.
    /// </summary>
    [Category("Identity")]
    public string? Nickname
    {
        get => Tile?.Nickname;
        set => Change(tile => tile.Nickname = value);
    }

    /// <summary>
    /// Gets or sets whether this drawable is visible.
    /// </summary>
    [Category("Appearance")]
    public bool Visible
    {
        get => Tile?.Visible ?? true;
        set => Change(tile => tile.Visible = value);
    }

    /// <summary>
    /// Gets the frame.
    /// </summary>
    [Category("Appearance"), ReadOnly(true)]
    public string Frame =>
        Tile?.Frame is { } frame
            ? $"{frame.Tilesheet}:{frame.RegionName} ({frame.XTile},{frame.YTile})"
            : "(none)";

    /// <summary>
    /// Gets or sets the transform.
    /// </summary>
    [Category("Appearance"), TypeConverter(typeof(TileTransformConverter))]
    [Description("Orientation of this placement. Source atlas metadata remains unchanged.")]
    public TileTransform Transform
    {
        get => Tile?.Transform ?? TileTransform.Identity;
        set => Change(tile => tile.Transform = value);
    }

    internal void ApplyTransform(TileTransform operation) => Transform = TileTransformGeometry.Compose(Transform, operation);

    /// <summary>
    /// Gets the tile dimensions in pixels.
    /// </summary>
    [Category("Geometry"), ReadOnly(true)]
    public Size TileSize => _region?.Invoke().Region?.TileSize ?? Size.Empty;
    /// <summary>
    /// Gets the effective tile size.
    /// </summary>
    [Category("Geometry"), ReadOnly(true)]
    public Size EffectiveTileSize => TileTransformGeometry.TransformSize(TileSize, Transform);
    /// <summary>
    /// Gets the current frame's visual overhang beyond its tile bounds.
    /// </summary>
    [Category("Geometry"), ReadOnly(true)]
    public Spacing Overhang => _region?.Invoke().Region?.Overhang ?? Spacing.None;
    /// <summary>
    /// Gets the effective overhang.
    /// </summary>
    [Category("Geometry"), ReadOnly(true)]
    public Spacing EffectiveOverhang => TileTransformGeometry.TransformSpacing(Overhang, Transform);
    /// <summary>
    /// Gets the source tile padding.
    /// </summary>
    [Category("Geometry"), ReadOnly(true)]
    public Spacing SourceTilePadding => _region?.Invoke().Region?.TilePadding ?? Spacing.None;
    /// <summary>
    /// Gets the effective tile padding.
    /// </summary>
    [Category("Geometry"), ReadOnly(true)]
    public Spacing EffectiveTilePadding => TileTransformGeometry.TransformSpacing(SourceTilePadding, Transform);
    private CollisionAdjust SourceFrameCollisionAdjust
    {
        get
        {
            var source = _region?.Invoke();
            var region = source?.Region;
            var frame = source?.Frame;
            return region?.Frames.FirstOrDefault(candidate => candidate.XTile == frame?.XTile && candidate.YTile == frame?.YTile)?.CollisionAdjust
                ?? region?.CollisionAdjust ?? AdjustCollisionArea;
        }
    }

    /// <summary>
    /// Gets the effective collision adjust.
    /// </summary>
    [Category("Collision"), ReadOnly(true)]
    public CollisionAdjust EffectiveCollisionAdjust => TileTransformGeometry.TransformCollisionAdjust(
        AdjustCollisionAreaByFrame ? SourceFrameCollisionAdjust : AdjustCollisionArea, Transform);

    /// <summary>
    /// Gets or sets whether enable animator is enabled.
    /// </summary>
    [Category("Animation")]
    public bool EnableAnimator
    {
        get => Tile?.EnableAnimator ?? false;
        set => Change(tile => tile.EnableAnimator = value);
    }

    /// <summary>
    /// Gets or sets the lookup key of the referenced animation.
    /// </summary>
    [Category("Animation")]
    public string? AnimationKey
    {
        get => Tile?.AnimationKey;
        set => Change(tile => tile.AnimationKey = string.IsNullOrWhiteSpace(value) ? null : value);
    }

    /// <summary>
    /// Gets or sets whether start animation is enabled.
    /// </summary>
    [Category("Animation")]
    public bool StartAnimation
    {
        get => Tile?.StartAnimation ?? false;
        set => Change(tile => tile.StartAnimation = value);
    }

    /// <summary>
    /// Gets or sets whether scene-layer fog applies to this tile.
    /// </summary>
    [Category("Appearance")]
    public bool EnableFog
    {
        get => Tile?.EnableFog ?? false;
        set => Change(tile => tile.EnableFog = value);
    }

    /// <summary>
    /// Gets or sets whether adjust collision area by frame is enabled.
    /// </summary>
    [Category("Collision")]
    public bool AdjustCollisionAreaByFrame
    {
        get => Tile?.AdjustCollisionAreaByFrame ?? false;
        set => Change(tile => tile.AdjustCollisionAreaByFrame = value);
    }

    /// <summary>
    /// Gets or sets the adjust collision area.
    /// </summary>
    [Category("Collision")]
    public CollisionAdjust AdjustCollisionArea
    {
        get => Tile?.AdjustCollisionArea ?? CollisionAdjust.None;
        set => Change(tile => tile.AdjustCollisionArea = value);
    }

    /// <summary>
    /// Gets or sets the collision type.
    /// </summary>
    [Category("Collision")]
    public TileCollisionType CollisionType
    {
        get => Tile?.CollisionType ?? TileCollisionType.None;
        set => Change(tile => tile.CollisionType = value);
    }

    /// <summary>
    /// Gets or sets whether collision type by frame is enabled.
    /// </summary>
    [Category("Collision")]
    public bool CollisionTypeByFrame
    {
        get => Tile?.CollisionTypeByFrame ?? false;
        set => Change(tile => tile.CollisionTypeByFrame = value);
    }

    /// <summary>
    /// Gets or sets the collision profile name.
    /// </summary>
    [Category("Collision")]
    public string? CollisionProfileName
    {
        get => Tile?.CollisionProfileName;
        set => Change(tile => tile.CollisionProfileName = string.IsNullOrWhiteSpace(value) ? null : value);
    }
}
