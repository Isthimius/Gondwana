using System.ComponentModel;
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

    public TilePropertyAdapter(
        SceneDocument document,
        SceneLayerDefinition layer,
        int x,
        int y,
        Action changed)
    {
        _document = document;
        _layer = layer;
        _x = x;
        _y = y;
        _changed = changed;
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
        var tile = Mutable();
        change(tile);
        _document.MarkChanged();
        _changed();
    }

    [Category("Cell"), ReadOnly(true)]
    public int X => _x;

    [Category("Cell"), ReadOnly(true)]
    public int Y => _y;

    [Category("Cell"), ReadOnly(true)]
    public bool HasExplicitDefinition => Tile is not null;

    [Category("Identity")]
    public Guid Id
    {
        get => Tile?.Id ?? Guid.Empty;
        set => Change(tile => tile.Id = value);
    }

    [Category("Identity")]
    public string? Nickname
    {
        get => Tile?.Nickname;
        set => Change(tile => tile.Nickname = value);
    }

    [Category("Appearance")]
    public bool Visible
    {
        get => Tile?.Visible ?? true;
        set => Change(tile => tile.Visible = value);
    }

    [Category("Appearance"), ReadOnly(true)]
    public string Frame =>
        Tile?.Frame is { } frame
            ? $"{frame.Tilesheet}:{frame.RegionName} ({frame.XTile},{frame.YTile})"
            : "(none)";

    [Category("Animation")]
    public bool EnableAnimator
    {
        get => Tile?.EnableAnimator ?? false;
        set => Change(tile => tile.EnableAnimator = value);
    }

    [Category("Animation")]
    public string? AnimationKey
    {
        get => Tile?.AnimationKey;
        set => Change(tile => tile.AnimationKey = string.IsNullOrWhiteSpace(value) ? null : value);
    }

    [Category("Animation")]
    public bool StartAnimation
    {
        get => Tile?.StartAnimation ?? false;
        set => Change(tile => tile.StartAnimation = value);
    }

    [Category("Appearance")]
    public bool EnableFog
    {
        get => Tile?.EnableFog ?? false;
        set => Change(tile => tile.EnableFog = value);
    }

    [Category("Collision")]
    public bool AdjustCollisionAreaByFrame
    {
        get => Tile?.AdjustCollisionAreaByFrame ?? false;
        set => Change(tile => tile.AdjustCollisionAreaByFrame = value);
    }

    [Category("Collision")]
    public CollisionAdjust AdjustCollisionArea
    {
        get => Tile?.AdjustCollisionArea ?? CollisionAdjust.None;
        set => Change(tile => tile.AdjustCollisionArea = value);
    }

    [Category("Collision")]
    public TileCollisionType CollisionType
    {
        get => Tile?.CollisionType ?? TileCollisionType.None;
        set => Change(tile => tile.CollisionType = value);
    }

    [Category("Collision")]
    public bool CollisionTypeByFrame
    {
        get => Tile?.CollisionTypeByFrame ?? false;
        set => Change(tile => tile.CollisionTypeByFrame = value);
    }

    [Category("Collision")]
    public string? CollisionProfileName
    {
        get => Tile?.CollisionProfileName;
        set => Change(tile => tile.CollisionProfileName = string.IsNullOrWhiteSpace(value) ? null : value);
    }
}
