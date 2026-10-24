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

    /// <summary>Creates a property-grid view of a sparse cell; setters materialize the cell, mark the document dirty, and notify the editor.</summary>
    /// <param name="document">The scene document containing the cell.</param>
    /// <param name="layer">The layer whose sparse tile collection is edited.</param>
    /// <param name="x">Zero-based cell column.</param>
    /// <param name="y">Zero-based cell row.</param>
    /// <param name="changed">Callback refreshing the editor after a mutation.</param>
    /// <param name="region">Optional resolver for the cell's source region and frame, used to display effective geometry.</param>
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

    /// <summary>Gets the zero-based column of the edited scene-layer cell.</summary>
    [Category("Cell"), ReadOnly(true)]
    public int X => _x;

    /// <summary>Gets the zero-based row of the edited scene-layer cell.</summary>
    [Category("Cell"), ReadOnly(true)]
    public int Y => _y;

    /// <summary>Gets whether the sparse layer contains a persisted tile entry for this cell.</summary>
    [Category("Cell"), ReadOnly(true)]
    public bool HasExplicitDefinition => Tile is not null;

    /// <summary>Gets or sets the tile identifier; an unmaterialized cell reads as an empty GUID.</summary>
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

    /// <summary>Gets the assigned tilesheet, region, and grid coordinates as display text, or <c>(none)</c>.</summary>
    [Category("Appearance"), ReadOnly(true)]
    public string Frame =>
        Tile?.Frame is { } frame
            ? $"{frame.Tilesheet}:{frame.RegionName} ({frame.XTile},{frame.YTile})"
            : "(none)";

    /// <summary>Gets or sets the placement orientation without modifying source atlas metadata.</summary>
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
    /// <summary>Gets the source tile dimensions after applying the placement orientation.</summary>
    [Category("Geometry"), ReadOnly(true)]
    public Size EffectiveTileSize => TileTransformGeometry.TransformSize(TileSize, Transform);
    /// <summary>
    /// Gets the current frame's visual overhang beyond its tile bounds.
    /// </summary>
    [Category("Geometry"), ReadOnly(true)]
    public Spacing Overhang => _region?.Invoke().Region?.Overhang ?? Spacing.None;
    /// <summary>Gets source overhang edges remapped by the placement orientation.</summary>
    [Category("Geometry"), ReadOnly(true)]
    public Spacing EffectiveOverhang => TileTransformGeometry.TransformSpacing(Overhang, Transform);
    /// <summary>Gets the source region padding in pixels, or no padding when the region cannot be resolved.</summary>
    [Category("Geometry"), ReadOnly(true)]
    public Spacing SourceTilePadding => _region?.Invoke().Region?.TilePadding ?? Spacing.None;
    /// <summary>Gets source padding edges remapped by the placement orientation.</summary>
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

    /// <summary>Gets frame or instance collision adjustments with edges remapped by the placement orientation.</summary>
    [Category("Collision"), ReadOnly(true)]
    public CollisionAdjust EffectiveCollisionAdjust => TileTransformGeometry.TransformCollisionAdjust(
        AdjustCollisionAreaByFrame ? SourceFrameCollisionAdjust : AdjustCollisionArea, Transform);

    /// <summary>Gets or sets whether the materialized tile has animation enabled.</summary>
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

    /// <summary>Gets or sets whether the assigned animation starts when the tile is materialized.</summary>
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

    /// <summary>Gets or sets whether collision-area adjustments come from the current frame.</summary>
    [Category("Collision")]
    public bool AdjustCollisionAreaByFrame
    {
        get => Tile?.AdjustCollisionAreaByFrame ?? false;
        set => Change(tile => tile.AdjustCollisionAreaByFrame = value);
    }

    /// <summary>Gets or sets instance collision insets in pixels; negative values expand the area.</summary>
    [Category("Collision")]
    public CollisionAdjust AdjustCollisionArea
    {
        get => Tile?.AdjustCollisionArea ?? CollisionAdjust.None;
        set => Change(tile => tile.AdjustCollisionArea = value);
    }

    /// <summary>Gets or sets the instance collision behavior used when frame collision types are disabled.</summary>
    [Category("Collision")]
    public TileCollisionType CollisionType
    {
        get => Tile?.CollisionType ?? TileCollisionType.None;
        set => Change(tile => tile.CollisionType = value);
    }

    /// <summary>Gets or sets whether collision behavior comes from the current frame.</summary>
    [Category("Collision")]
    public bool CollisionTypeByFrame
    {
        get => Tile?.CollisionTypeByFrame ?? false;
        set => Change(tile => tile.CollisionTypeByFrame = value);
    }

    /// <summary>Gets or sets the collision-profile lookup name; blank input clears the name.</summary>
    [Category("Collision")]
    public string? CollisionProfileName
    {
        get => Tile?.CollisionProfileName;
        set => Change(tile => tile.CollisionProfileName = string.IsNullOrWhiteSpace(value) ? null : value);
    }
}
