using System.Drawing;
using Gondwana.Drawing.Coordinates;
using Gondwana.Physics.Collisions;

namespace Gondwana.Scenes.GSCN;

/// <summary>
/// Defines one layer in a GSCN scene.
/// </summary>
public sealed class SceneLayerDefinition
{
    /// <summary>
    /// Gets or sets the stable layer identifier. An empty value generates a new ID when materialized.
    /// </summary>
    public string ID { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the coordinate system used by the layer.
    /// </summary>
    public CoordinateSystemTypes CoordinateSystemType { get; set; } = CoordinateSystemTypes.Orthogonal;

    /// <summary>
    /// Gets or sets the number of tile columns.
    /// </summary>
    public int Columns { get; set; }

    /// <summary>
    /// Gets or sets the number of tile rows.
    /// </summary>
    public int Rows { get; set; }

    /// <summary>
    /// Gets or sets the rendered tile width in pixels.
    /// </summary>
    public int TileWidth { get; set; } = 32;

    /// <summary>
    /// Gets or sets the rendered tile height in pixels.
    /// </summary>
    public int TileHeight { get; set; } = 32;

    /// <summary>
    /// Gets or sets the layer render order.
    /// </summary>
    public int ZOrder { get; set; }

    /// <summary>
    /// Gets or sets the parallax factor.
    /// </summary>
    public float Parallax { get; set; } = 1f;

    /// <summary>
    /// Gets or sets whether the layer is visible.
    /// </summary>
    public bool Visible { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the layer wraps horizontally.
    /// </summary>
    public bool WrapHorizontally { get; set; }

    /// <summary>
    /// Gets or sets whether the layer wraps vertically.
    /// </summary>
    public bool WrapVertically { get; set; }

    /// <summary>
    /// Gets or sets whether grid lines are shown.
    /// </summary>
    public bool ShowGridLines { get; set; }

    /// <summary>
    /// Gets or sets whether collision boxes are shown.
    /// </summary>
    public bool ShowCollisionBoxes { get; set; }

    /// <summary>
    /// Gets or sets the world-space origin of tile (0,0).
    /// </summary>
    public Point OriginPx { get; set; } = Point.Empty;

    /// <summary>
    /// Gets or sets the collision profile assigned to fixed tiles by default.
    /// </summary>
    public string DefaultTileCollisionProfile { get; set; } = CollisionProfileNames.World;

    /// <summary>
    /// Gets or sets tile entries. Coordinates identify the target cell; omitted cells remain default tiles.
    /// </summary>
    public List<SceneLayerTileDefinition> Tiles { get; set; } = [];
}
