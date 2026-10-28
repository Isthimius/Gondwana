using System.ComponentModel;
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
    [Category("Identity")]
    [Description("Unique identifier for this layer within the scene; leave blank to generate one when materialized.")]
    public string ID { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the coordinate system used by the layer.
    /// </summary>
    [Category("Grid")]
    [Description("How layer grid coordinates map to the scene: orthogonal uses rectangular rows and columns; other choices change the tile layout.")]
    public CoordinateSystemTypes CoordinateSystemType { get; set; } = CoordinateSystemTypes.Orthogonal;

    /// <summary>
    /// Gets or sets the number of tile columns.
    /// </summary>
    [Category("Grid")]
    [Description("Number of grid cells across the layer. Together with TileWidth, determines its horizontal extent.")]
    public int Columns { get; set; }

    /// <summary>
    /// Gets or sets the number of tile rows.
    /// </summary>
    [Category("Grid")]
    [Description("Number of grid cells down the layer. Together with TileHeight, determines its vertical extent.")]
    public int Rows { get; set; }

    /// <summary>
    /// Gets or sets the rendered tile width in pixels.
    /// </summary>
    [Category("Grid")]
    [Description("Width in world pixels of one logical grid cell; does not change the source artwork.")]
    public int TileWidth { get; set; } = 32;

    /// <summary>
    /// Gets or sets the rendered tile height in pixels.
    /// </summary>
    [Category("Grid")]
    [Description("Height in world pixels of one logical grid cell; does not change the source artwork.")]
    public int TileHeight { get; set; } = 32;

    /// <summary>
    /// Gets or sets the layer render order.
    /// </summary>
    [Category("Rendering")]
    [Description("Draw order relative to other scene layers; higher values draw in front of lower values.")]
    public int ZOrder { get; set; }

    /// <summary>
    /// Gets or sets the parallax factor.
    /// </summary>
    [Category("Rendering")]
    [Description("Camera-scrolling multiplier for this layer. 1 follows normal world movement; 0.5 scrolls at half speed (distant background); above 1 scrolls faster; 0 does not shift with the camera.")]
    public float Parallax { get; set; } = 1f;

    /// <summary>
    /// Gets or sets whether the layer is visible.
    /// </summary>
    [Category("Rendering")]
    [Description("Whether this entire layer is drawn. False hides its contents without deleting the layer or its tiles.")]
    public bool Visible { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the layer wraps horizontally.
    /// </summary>
    [Category("Rendering")]
    [Description("Repeat the layer horizontally as the camera moves beyond its left or right edge.")]
    public bool WrapHorizontally { get; set; }

    /// <summary>
    /// Gets or sets whether the layer wraps vertically.
    /// </summary>
    [Category("Rendering")]
    [Description("Repeat the layer vertically as the camera moves beyond its top or bottom edge.")]
    public bool WrapVertically { get; set; }

    /// <summary>
    /// Gets or sets whether grid lines are shown.
    /// </summary>
    [Category("Debugging")]
    [Description("Draw the layer's grid boundaries as an authoring/debug overlay; not part of the tile artwork.")]
    public bool ShowGridLines { get; set; }

    /// <summary>
    /// Gets or sets whether collision boxes are shown.
    /// </summary>
    [Category("Debugging")]
    [Description("Draw collision outlines for this layer as a debug overlay; does not turn collision detection on or off.")]
    public bool ShowCollisionBoxes { get; set; }

    /// <summary>
    /// Gets or sets the world-space origin of tile (0,0).
    /// </summary>
    [Category("Grid")]
    [Description("World-pixel position of the layer's grid origin (cell 0,0); shifts the whole layer without changing tile coordinates.")]
    public Point OriginPx { get; set; } = Point.Empty;

    /// <summary>
    /// Gets or sets the collision profile assigned to fixed tiles by default.
    /// </summary>
    [Category("Collision")]
    [Description("Collision profile name used by fixed tiles unless a tile specifies an override.")]
    public string DefaultTileCollisionProfile { get; set; } = CollisionProfileNames.World;

    /// <summary>
    /// Gets or sets tile entries. Coordinates identify the target cell; omitted cells remain default tiles.
    /// </summary>
    [Category("Content")]
    [Description("Saved placements and overrides for cells in this layer; missing cells use their implicit defaults.")]
    public List<SceneLayerTileDefinition> Tiles { get; set; } = [];
}
