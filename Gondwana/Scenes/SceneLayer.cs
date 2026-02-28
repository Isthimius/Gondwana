using System.Collections;
using System.Drawing;
using System.Runtime.Serialization;
using Gondwana.Collisions;
using Gondwana.Drawing;
using Gondwana.Drawing.Collisions;
using Gondwana.Drawing.Coordinates;
using Gondwana.Drawing.Direct;
using Gondwana.Drawing.Sprites;
using Gondwana.Rendering;
using Newtonsoft.Json;

namespace Gondwana.Scenes;

/// <summary>
/// Represents a single layer within a <see cref="Scene"/>, containing a grid of tiles and supporting
/// various coordinate systems, parallax scrolling, and rendering properties. Scene layers provide
/// the fundamental structure for organizing and rendering tile-based game content.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="SceneLayer"/> is a 2D grid of <see cref="SceneLayerTile"/> instances that can be
/// rendered with different coordinate systems (orthogonal, isometric, hexagonal), z-ordering for
/// depth sorting, and parallax effects for multi-plane scrolling backgrounds.
/// </para>
/// <para>
/// Layers support both tile-based content and sprite rendering, with automatic management of
/// dirty regions for efficient incremental rendering. They can also contain direct drawing
/// instances for custom graphics overlays.
/// </para>
/// <para>
/// Each layer maintains its own coordinate system transformation, allowing mixing of different
/// projection types within the same scene. Layers can be configured to wrap horizontally or
/// vertically for seamless tiling effects.
/// </para>
/// </remarks>
[JsonObject(IsReference = true)]
public class SceneLayer : IEnumerable<SceneLayerTile>, IDisposable
{
    #region events

    /// <summary>
    /// Occurs when the tile dimensions of this layer change.
    /// </summary>
    /// <remarks>
    /// This event is raised when either <see cref="TileWidth"/> or
    /// <see cref="TileHeight"/> is modified, or when <see cref="SetTileSize"/>
    /// is called. Changes to tile size typically require a full scene refresh to ensure
    /// correct rendering of all tiles and sprites.
    /// </remarks>
    public event Action<SceneLayer>? SceneLayerTileSizeChanged;

    /// <summary>
    /// Occurs when the visibility state of this layer changes.
    /// </summary>
    /// <remarks>
    /// This event is raised when the <see cref="Visible"/> property is modified. Visibility
    /// changes affect which layers are included in rendering and may trigger cache invalidation
    /// for visible layer collections in the parent scene.
    /// </remarks>
    public event Action<SceneLayer>? VisibleChanged;

    /// <summary>
    /// Occurs when the wrapping configuration of this layer changes.
    /// </summary>
    /// <remarks>
    /// This event is raised when either <see cref="WrapHorizontally"/> or <see cref="WrapVertically"/>
    /// is modified. Wrapping changes affect how the layer handles tile coordinates at boundaries
    /// and typically require a full scene refresh.
    /// </remarks>
    public event Action<SceneLayer>? WrappingChanged;

    /// <summary>
    /// Occurs when the grid line display setting changes.
    /// </summary>
    /// <remarks>
    /// This event is raised when the <see cref="ShowGridLines"/> property is modified. Grid line
    /// visibility changes require a full scene refresh to add or remove grid line overlays from
    /// the rendering output.
    /// </remarks>
    public event Action<SceneLayer>? ShowGridLinesChanged;

    /// <summary>
    /// Occurs when the collision box display setting changes.
    /// </summary>
    /// <remarks>
    /// This event is raised when the <see cref="ShowCollisionBoxes"/> property is modified.
    /// Collision box visibility changes require a full scene refresh to add or remove collision
    /// box overlays from the rendering output, useful for debugging collision detection.
    /// </remarks>
    public event Action<SceneLayer>? ShowCollisionBoxesChanged;

    /// <summary>
    /// Occurs when the z-order (rendering depth) of this layer changes.
    /// </summary>
    /// <remarks>
    /// This event is raised when the <see cref="ZOrder"/> property is modified. Z-order changes
    /// affect the rendering order of layers within the parent scene and typically require
    /// re-sorting the visible layer collection and a full scene refresh.
    /// </remarks>
    public event Action<SceneLayer>? ZOrderChanged;

    /// <summary>
    /// Occurs when the parallax scrolling factor of this layer changes.
    /// </summary>
    /// <remarks>
    /// This event is raised when the <see cref="Parallax"/> property is modified. Parallax changes
    /// affect how the layer scrolls relative to the camera and require a full scene refresh to
    /// ensure correct positioning of all layer content.
    /// </remarks>
    public event Action<SceneLayer>? ParallaxChanged;

    /// <summary>
    /// Occurs when the world-space origin point of this layer changes.
    /// </summary>
    /// <remarks>
    /// This event is raised when the <see cref="OriginPx"/> property is modified. Origin changes
    /// shift the entire layer as a block within world space and require a full scene refresh to
    /// ensure all tiles and sprites are positioned correctly.
    /// </remarks>
    public event Action<SceneLayer>? OriginPxChanged;

    /// <summary>
    /// Occurs when this layer is being disposed.
    /// </summary>
    /// <remarks>
    /// This event is raised at the beginning of the <see cref="Dispose"/> method, before any
    /// tiles are disposed or resources are released. Subscribers can use this event to perform
    /// cleanup operations or save state before the layer is destroyed.
    /// </remarks>
    public event Action<SceneLayer>? Disposing;

    #endregion events

    #region private fields

    private int _tileWidth;     // rendered width
    private int _tileHeight;    // rendered height
    private bool _visible;      // is SceneLayer to be rendered; useful with multiple layers

    #endregion private fields

    #region constructors / finalizer

    internal SceneLayer(int columnCount,
                        int rowCount,
                        int width = 32,
                        int height = 32,
                        float parallax = 1,
                        CoordinateSystemTypes coordinateSystem = CoordinateSystemTypes.Orthogonal)
    {
        var tileArray = new SceneLayerTile[columnCount, rowCount];

        InitValues(tileArray, width, height, parallax, coordinateSystem);
    }

    [OnDeserialized]
    private void OnDeserialized(StreamingContext context) =>
        InitValues(_sceneLayerTileArray, _tileWidth, _tileHeight, _parallax, CoordinateSystemType);

    /// <summary>
    /// Finalizes an instance of the <see cref="SceneLayer"/> class, releasing resources if the layer
    /// was not explicitly disposed.
    /// </summary>
    /// <remarks>
    /// This finalizer ensures that layer resources are cleaned up even if <see cref="Dispose"/>
    /// is not called explicitly. However, it is recommended to always call <see cref="Dispose"/>
    /// or rely on parent scene disposal to ensure deterministic cleanup.
    /// </remarks>
    ~SceneLayer()
    {
        Dispose();
    }

    #endregion constructors / finalizer

    #region properties

    /// <summary>
    /// Gets the extensible value bag for storing arbitrary layer-specific metadata.
    /// </summary>
    /// <value>A <see cref="TypedValueBag"/> instance for storing custom key-value data.</value>
    /// <remarks>
    /// The value bag allows games or engine extensions to attach arbitrary structured data
    /// to layers (such as layer-specific properties, AI navigation data, weather effects, or
    /// custom attributes) without modifying the core <see cref="SceneLayer"/> class. Values
    /// are accessed using strongly-typed <see cref="ValueKey{T}"/> instances and are included
    /// in layer serialization.
    /// </remarks>
    [JsonProperty]
    public TypedValueBag ValueBag { get; } = new();

    [JsonIgnore]
    internal ISceneLayerCoordinates CoordinateSystem { get; private set; } = new OrthogonalCoordinates();

    /// <summary>
    /// Gets or sets the coordinate system type used by this layer for transforming between
    /// grid coordinates and pixel positions.
    /// </summary>
    /// <value>A <see cref="CoordinateSystemTypes"/> value specifying the projection type.</value>
    /// <remarks>
    /// <para>
    /// The coordinate system determines how grid positions are mapped to screen pixels and affects
    /// rendering, collision detection, and movement calculations. Common types include:
    /// </para>
    /// <list type="bullet">
    /// <item><description><see cref="CoordinateSystemTypes.Orthogonal"/> - Standard square grid</description></item>
    /// <item><description><see cref="CoordinateSystemTypes.IsometricRhombic"/> - Diamond-shaped isometric projection</description></item>
    /// <item><description><see cref="CoordinateSystemTypes.IsometricAxial"/> - Axial isometric projection</description></item>
    /// <item><description><see cref="CoordinateSystemTypes.HexAxialFlatTop"/> - Hexagonal grid with flat-top orientation</description></item>
    /// <item><description><see cref="CoordinateSystemTypes.HexAxialPointedTop"/> - Hexagonal grid with pointed-top orientation</description></item>
    /// </list>
    /// <para>
    /// Changing the coordinate system after layer creation is supported but may produce unexpected
    /// visual results if tile content was designed for a different projection.
    /// </para>
    /// </remarks>
    [JsonProperty]
    public CoordinateSystemTypes CoordinateSystemType
    {
        get
        {
            return CoordinateSystem switch
            {
                OrthogonalCoordinates => CoordinateSystemTypes.Orthogonal,
                IsometricRhombicCoordinates => CoordinateSystemTypes.IsometricRhombic,
                IsometricAxialCoordinates => CoordinateSystemTypes.IsometricAxial,
                HexAxialFlatTopCoordinates => CoordinateSystemTypes.HexAxialFlatTop,
                HexAxialPointedTop => CoordinateSystemTypes.HexAxialPointedTop,
                _ => throw new InvalidOperationException($"Unknown coordinate system type: {CoordinateSystem.GetType().Name}")
            };
        }
        set
        {
            CoordinateSystem = value switch
            {
                CoordinateSystemTypes.Orthogonal => new OrthogonalCoordinates(),
                CoordinateSystemTypes.IsometricRhombic => new IsometricRhombicCoordinates(),
                CoordinateSystemTypes.IsometricAxial => new IsometricAxialCoordinates(),
                CoordinateSystemTypes.HexAxialFlatTop => new HexAxialFlatTopCoordinates(),
                CoordinateSystemTypes.HexAxialPointedTop => new HexAxialPointedTop(),
                _ => throw new ArgumentOutOfRangeException(nameof(value), $"Unknown coordinate system type: {value}")
            };
        }
    }

    /// <summary>
    /// Gets or sets the unique identifier for this layer.
    /// </summary>
    /// <value>A string representing the layer's unique ID, typically a GUID.</value>
    /// <remarks>
    /// The ID is automatically generated when a layer is created and is used to identify
    /// the layer within its parent scene. It can also be used to look up layers via
    /// <see cref="Scene.GetSceneLayerByID"/> or the scene's string indexer.
    /// </remarks>
    [JsonProperty]
    public string ID { get; protected internal set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the parent scene that contains this layer.
    /// </summary>
    /// <value>The <see cref="Gondwana.Scenes.Scene"/> that owns this layer, or <c>null</c> if the layer is not attached to a scene.</value>
    /// <remarks>
    /// This property is set automatically when the layer is added to a scene via
    /// <see cref="Scene.AddLayer"/> and cleared when the layer is removed. The scene reference
    /// provides access to scene-level resources such as the collision world.
    /// </remarks>
    [JsonProperty]
    public Scene Scene { get; internal set; }

    [JsonIgnore]
    internal RefreshQueue RefreshQueue { get; set; }

    private int _zOrder = 0;

    /// <summary>
    /// Gets or sets the z-order (rendering depth) of this layer within its parent scene.
    /// </summary>
    /// <value>An integer representing the layer's rendering priority. Lower values render first (behind), higher values render last (in front).</value>
    /// <remarks>
    /// <para>
    /// Z-order determines the rendering order of layers when multiple layers are present in a scene.
    /// Layers with lower z-order values are rendered first and appear behind layers with higher values.
    /// </para>
    /// <para>
    /// Setting this property raises the <see cref="ZOrderChanged"/> event and typically triggers
    /// a full scene refresh to ensure correct layer ordering.
    /// </para>
    /// </remarks>
    [JsonProperty]
    public int ZOrder
    {
        get => _zOrder;
        set
        {
            _zOrder = value;
            ZOrderChanged?.Invoke(this);
        }
    }

    private float _parallax = 1f;       // 1 = default; <1 is slower, >1 is faster

    /// <summary>
    /// Gets or sets the parallax scrolling factor for this layer.
    /// </summary>
    /// <value>
    /// A floating-point value controlling the layer's scroll rate relative to the camera.
    /// Default is 1.0 (no parallax effect).
    /// </value>
    /// <remarks>
    /// <para>
    /// Parallax scrolling creates depth illusion by moving layers at different rates:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Values less than 1.0 (e.g., 0.5) create a background effect, moving slower than the camera</description></item>
    /// <item><description>Value of 1.0 moves with the camera (standard layer movement)</description></item>
    /// <item><description>Values greater than 1.0 (e.g., 1.5) create a foreground effect, moving faster than the camera</description></item>
    /// </list>
    /// <para>
    /// Setting this property raises the <see cref="ParallaxChanged"/> event and triggers a full scene refresh.
    /// </para>
    /// </remarks>
    [JsonProperty]
    public float Parallax
    {
        get { return _parallax; }
        set
        {
            _parallax = value;
            ParallaxChanged?.Invoke(this);
        }
    }

    /// <summary>
    /// Gets or sets the height of each tile in this layer, measured in pixels.
    /// </summary>
    /// <value>The tile height in pixels. Default is typically 32.</value>
    /// <remarks>
    /// <para>
    /// This property defines the rendered height of tiles in this layer. Changes to tile height
    /// affect how tiles are positioned and rendered within the layer's coordinate system.
    /// </para>
    /// <para>
    /// Setting this property raises the <see cref="SceneLayerTileSizeChanged"/> event and triggers
    /// a full scene refresh. Consider using <see cref="SetTileSize"/> to update both width and
    /// height atomically with a single event.
    /// </para>
    /// </remarks>
    [JsonProperty]
    public int TileHeight
    {
        get { return _tileHeight; }
        set
        {
            _tileHeight = value;
            SceneLayerTileSizeChanged?.Invoke(this);
        }
    }

    /// <summary>
    /// Gets or sets the width of each tile in this layer, measured in pixels.
    /// </summary>
    /// <value>The tile width in pixels. Default is typically 32.</value>
    /// <remarks>
    /// <para>
    /// This property defines the rendered width of tiles in this layer. Changes to tile width
    /// affect how tiles are positioned and rendered within the layer's coordinate system.
    /// </para>
    /// <para>
    /// Setting this property raises the <see cref="SceneLayerTileSizeChanged"/> event and triggers
    /// a full scene refresh. Consider using <see cref="SetTileSize"/> to update both width and
    /// height atomically with a single event.
    /// </para>
    /// </remarks>
    [JsonProperty]
    public int TileWidth
    {
        get { return _tileWidth; }
        set
        {
            _tileWidth = value;
            SceneLayerTileSizeChanged?.Invoke(this);
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether this layer should be rendered.
    /// </summary>
    /// <value><c>true</c> if the layer is visible and should be rendered; otherwise, <c>false</c>.</value>
    /// <remarks>
    /// <para>
    /// Invisible layers are excluded from rendering but remain part of the scene structure.
    /// This is useful for temporarily hiding layers (such as background layers in cutscenes)
    /// without removing them from the scene.
    /// </para>
    /// <para>
    /// Setting this property raises the <see cref="VisibleChanged"/> event, invalidates the
    /// parent scene's visible layer cache, and triggers a full scene refresh.
    /// </para>
    /// </remarks>
    [JsonProperty]
    public bool Visible
    {
        get { return _visible; }
        set
        {
            _visible = value;
            VisibleChanged?.Invoke(this);
        }
    }

    [JsonProperty("SceneLayerTileArray")]
    private SceneLayerTile[,] _sceneLayerTileArray;    // array of points; 2 dimensions (X, Y)

    /// <summary>
    /// Gets the two-dimensional array of tiles that comprise this layer's grid.
    /// </summary>
    /// <value>A 2D array of <see cref="SceneLayerTile"/> instances indexed by [column, row].</value>
    /// <remarks>
    /// <para>
    /// This property provides direct access to the underlying tile array. The array is indexed
    /// by [x, y] or [column, row], where [0, 0] represents the top-left tile.
    /// </para>
    /// <para>
    /// For safer access with bounds checking and wrapping support, prefer using the layer's
    /// indexer properties (<c>layer[x, y]</c>, <c>layer[point]</c>, or <c>layer[pointf]</c>).
    /// </para>
    /// </remarks>
    [JsonIgnore]
    public SceneLayerTile[,] SceneLayerTileArray => _sceneLayerTileArray;

    /// <summary>
    /// Gets the number of columns (tiles wide) in this layer's grid.
    /// </summary>
    /// <value>The width of the tile grid in tiles.</value>
    /// <remarks>
    /// This value is determined at layer creation time and represents the horizontal extent
    /// of the tile array. Valid column indices range from 0 to <c>GridColumnCount - 1</c>.
    /// </remarks>
    [JsonIgnore]
    public int GridColumnCount => _sceneLayerTileArray.GetUpperBound(0) + 1;

    /// <summary>
    /// Gets the number of rows (tiles high) in this layer's grid.
    /// </summary>
    /// <value>The height of the tile grid in tiles.</value>
    /// <remarks>
    /// This value is determined at layer creation time and represents the vertical extent
    /// of the tile array. Valid row indices range from 0 to <c>GridRowCount - 1</c>.
    /// </remarks>
    [JsonIgnore]
    public int GridRowCount => _sceneLayerTileArray.GetUpperBound(1) + 1;

    [JsonProperty("WrapHorizontally")]
    private bool _wrapHoriz = false;

    /// <summary>
    /// Gets or sets a value indicating whether this layer wraps horizontally at the grid boundaries.
    /// </summary>
    /// <value><c>true</c> if horizontal wrapping is enabled; otherwise, <c>false</c>.</value>
    /// <remarks>
    /// <para>
    /// When enabled, horizontal coordinates that exceed the grid boundaries wrap around to the
    /// opposite side, creating a seamless toroidal topology. This is useful for scrolling backgrounds,
    /// wrap-around game worlds, and continuous map designs.
    /// </para>
    /// <para>
    /// Setting this property raises the <see cref="WrappingChanged"/> event and triggers a full scene refresh.
    /// </para>
    /// </remarks>
    [JsonIgnore]
    public bool WrapHorizontally
    {
        get { return _wrapHoriz; }
        set
        {
            _wrapHoriz = value;
            WrappingChanged?.Invoke(this);
        }
    }

    [JsonProperty("WrapVertically")]
    private bool _wrapVerti = false;

    /// <summary>
    /// Gets or sets a value indicating whether this layer wraps vertically at the grid boundaries.
    /// </summary>
    /// <value><c>true</c> if vertical wrapping is enabled; otherwise, <c>false</c>.</value>
    /// <remarks>
    /// <para>
    /// When enabled, vertical coordinates that exceed the grid boundaries wrap around to the
    /// opposite side, creating a seamless toroidal topology. This is useful for scrolling backgrounds,
    /// wrap-around game worlds, and continuous map designs.
    /// </para>
    /// <para>
    /// Setting this property raises the <see cref="WrappingChanged"/> event and triggers a full scene refresh.
    /// </para>
    /// </remarks>
    [JsonIgnore]
    public bool WrapVertically
    {
        get { return _wrapVerti; }
        set
        {
            _wrapVerti = value;
            WrappingChanged?.Invoke(this);
        }
    }

    [JsonProperty("ShowGridLines")]
    private bool showGridLines = false;

    /// <summary>
    /// Gets or sets a value indicating whether grid lines should be rendered over this layer for debugging.
    /// </summary>
    /// <value><c>true</c> if grid lines should be displayed; otherwise, <c>false</c>.</value>
    /// <remarks>
    /// <para>
    /// Grid lines provide a visual overlay showing tile boundaries, which is useful during development
    /// for aligning content, debugging tile positions, and verifying coordinate system behavior.
    /// </para>
    /// <para>
    /// Setting this property raises the <see cref="ShowGridLinesChanged"/> event and triggers a full scene refresh.
    /// </para>
    /// </remarks>
    [JsonIgnore]
    public bool ShowGridLines
    {
        get { return showGridLines; }
        set
        {
            showGridLines = value;
            ShowGridLinesChanged?.Invoke(this);
        }
    }

    [JsonProperty("ShowCollisionBoxes")]
    private bool showCollisionBoxes = false;

    /// <summary>
    /// Gets or sets a value indicating whether collision boxes should be rendered over this layer for debugging.
    /// </summary>
    /// <value><c>true</c> if collision boxes should be displayed; otherwise, <c>false</c>.</value>
    /// <remarks>
    /// <para>
    /// Collision boxes provide a visual overlay showing the collision geometry of tiles and sprites,
    /// which is useful during development for debugging physics interactions, verifying collision
    /// boundaries, and tuning collision detection.
    /// </para>
    /// <para>
    /// Setting this property raises the <see cref="ShowCollisionBoxesChanged"/> event and triggers a full scene refresh.
    /// </para>
    /// </remarks>
    [JsonIgnore]
    public bool ShowCollisionBoxes
    {
        get { return showCollisionBoxes; }
        set
        {
            showCollisionBoxes = value;
            ShowCollisionBoxesChanged?.Invoke(this);
        }
    }

    // World-space origin (in pixels) of this layer's (0,0) tile.
    // Usually (0,0); can be shifted to move the entire layer as a block.
    [JsonProperty("OriginPx")]
    private Point _originPx = Point.Empty;

    /// <summary>
    /// Gets or sets the world-space pixel origin of this layer's (0,0) tile position.
    /// </summary>
    /// <value>A <see cref="Point"/> specifying the pixel offset in world space. Default is (0, 0).</value>
    /// <remarks>
    /// <para>
    /// The origin point allows shifting the entire layer as a block within world space, which is
    /// useful for creating offset parallax effects, layered UI elements, or special positioning
    /// requirements. Normally this is (0, 0), meaning the layer's grid coordinate (0, 0) maps to
    /// world pixel (0, 0).
    /// </para>
    /// <para>
    /// Setting this property raises the <see cref="OriginPxChanged"/> event and triggers a full scene refresh.
    /// </para>
    /// </remarks>
    [JsonIgnore]
    public Point OriginPx
    {
        get => _originPx;
        set
        {
            if (_originPx == value) return;
            _originPx = value;
            OriginPxChanged?.Invoke(this);
        }
    }

    /// <summary>
    /// Gets the collision world associated with this scene, used for physics and collision detection.
    /// </summary>
    /// <value>A <see cref="Gondwana.Collision.CollisionWorld"/> instance managing collision data for this scene.</value>
    /// <remarks>
    /// The collision world maintains collision geometry, spatial partitioning structures, and
    /// collision detection state for all collidable entities within the scene. It is automatically
    /// created when the scene is initialized and is used by the engine's collision resolution system.
    /// </remarks>
    [JsonIgnore]
    public CollisionWorld CollisionWorld { get; private set; } = new();

    [JsonIgnore]
    internal CollisionResolver CollisionResolver { get; private set; } = null!;

    #endregion properties

    #region public methods

    /// <summary>
    /// Sets both the width and height of tiles in this layer atomically, raising only a single
    /// <see cref="SceneLayerTileSizeChanged"/> event.
    /// </summary>
    /// <param name="newWidth">The new tile width in pixels.</param>
    /// <param name="newHeight">The new tile height in pixels.</param>
    /// <remarks>
    /// <para>
    /// This method is preferred over setting <see cref="TileWidth"/> and
    /// <see cref="TileHeight"/> separately, as it avoids triggering two change events
    /// and two scene refreshes.
    /// </para>
    /// <para>
    /// The method raises the <see cref="SceneLayerTileSizeChanged"/> event after updating both dimensions.
    /// </para>
    /// </remarks>
    public void SetTileSize(int newWidth, int newHeight)
    {
        _tileWidth = newWidth;
        _tileHeight = newHeight;
        SceneLayerTileSizeChanged?.Invoke(this);
    }

    /// <summary>
    /// Converts a grid coordinate (col,row) into the world-space pixel anchor
    /// where that tile begins. This returns the tile's top-left anchor in world
    /// pixels, not the tile center.
    /// </summary>
    public PointF GridToWorldPx(PointF grid) => CoordinateSystem.GetAnchorPixelAtSceneLayerCoordinates(this, grid);

    /// <summary>
    /// Converts a world-space pixel position into this layer's grid coordinates.
    /// This uses the layer's active coordinate system (square, iso, hex, etc.)
    /// and returns fractional grid values when the point lies between tiles.
    /// </summary>
    public PointF WorldPxToGrid(PointF worldPx) => CoordinateSystem.GetSceneLayerCoordinatesAtPixel(this, worldPx);

    /// <summary>
    /// Returns the neighboring tile in the given direction, or null if the
    /// direction would move off the layer and wrapping is not enabled. The
    /// meaning of N,S,E,W depends on the active coordinate system.
    /// </summary>
    public SceneLayerTile? GetAdjacentTile(SceneLayerTile tile, CardinalDirections direction) => CoordinateSystem.GetAdjacentSceneLayerTile(tile, direction);

    /// <summary>
    /// Wraps a grid coordinate around the layer's valid grid bounds using 
    /// toroidal wrapping (0..max). Used by movement and map designs that 
    /// loop at edges.
    /// </summary>
    public PointF WrapGrid(PointF grid)
    {
        // grid indices wrap based on tile array width/height
        return CoordinateSystem.FindEquivalentSceneLayerCoordinates(
            grid,
            GridColumnCount - 1,
            GridRowCount - 1);
    }

    /// <summary>
    /// Computes the world-space pixel bounding rectangle for this SceneLayer.
    /// This uses the coordinate system to evaluate the pixel extents of the
    /// extreme grid tiles. Works for square, iso, hex, and any other supported
    /// projection.
    /// </summary>
    public virtual RectangleF GetLayerBoundsPx()
    {
        if (GridColumnCount == 0 || GridRowCount == 0)
            return RectangleF.Empty;

        int maxCol = GridColumnCount - 1;
        int maxRow = GridRowCount - 1;

        // Corner tiles
        var corners = new[]
        {
            this[0, 0],
            this[maxCol, 0],
            this[0, maxRow],
            this[maxCol, maxRow]
        };

        RectangleF result = RectangleF.Empty;
        bool hasBounds = false;

        foreach (var tile in corners)
        {
            var px = CoordinateSystem.GetPixelRangeForTile(tile, includeOverhang: true);
            var rf = RectangleF.FromLTRB(px.Left, px.Top, px.Right, px.Bottom);

            if (!hasBounds)
            {
                result = rf;
                hasBounds = true;
            }
            else
            {
                result = RectangleF.Union(result, rf);
            }
        }

        return result;
    }

    #endregion public methods

    #region private methods

    private void InitValues(SceneLayerTile[,] tileArray, int width, int height, float parallax, CoordinateSystemTypes coordinateSystem)
    {
        _sceneLayerTileArray = tileArray;
        _parallax = parallax;
        _tileWidth = width;
        _tileHeight = height;
        _visible = true;

        _originPx = Point.Empty;                  // layer world origin

        CoordinateSystemType = coordinateSystem;

        CollisionWorld = new CollisionWorld();
        CollisionResolver = new CollisionResolver(CollisionWorld);

        // let each SceneLayerTile in array know its position in the array
        SaveGridCoordinatesToSceneLayerTiles();
        BuildTileColliders();
        RefreshQueue = new RefreshQueue();
    }

    private void SaveGridCoordinatesToSceneLayerTiles()
    {
        // let each SceneLayerTile in array know its position in the array
        for (int X = 0; X <= _sceneLayerTileArray.GetUpperBound(0); X++)
        {
            for (int Y = 0; Y <= _sceneLayerTileArray.GetUpperBound(1); Y++)
            {
                _sceneLayerTileArray[X, Y] = new SceneLayerTile(this);
                _sceneLayerTileArray[X, Y].sceneLayerCoordinates = new Point(X, Y);
            }
        }
    }

    private void BuildTileColliders()
    {
        foreach (var tile in _sceneLayerTileArray)
        {
            if (tile is null)
                continue;

            tile.Collider ??= new TileCollider(tile, layerMask: ~0, collidesWithMask: ~0, isStatic: true);
            CollisionWorld.Register(tile.Collider);
        }
    }

    internal virtual List<IDrawable> GetDrawablesInWorldRect(Rectangle worldRect, bool includeOverhang = true)
    {
        // Make selection rect covering so we never miss the edge tile.
        // Drawing is still clipped later, so over-selecting is safe.
        var queryRect = worldRect;
        queryRect.Inflate(TileWidth, TileHeight); // <- KEY (tile-sized)
        queryRect.Inflate(1, 1); // optional boundary insurance

        // Gather into a list so we can sort it.
        var list = new List<IDrawable>(64);

        // 1) Grid tiles
        var sceneLayerTiles = CoordinateSystem.GetSceneLayerTilesInPixelRange(
            this,
            worldRect,
            includeOverhang: includeOverhang);

        if (sceneLayerTiles != null)
        {
            for (int i = 0; i < sceneLayerTiles.Count; i++)
            {
                var tile = sceneLayerTiles[i];

                if (tile is null)
                    continue;

                if (!tile.Visible)
                    continue;

                // Defensive overlap check (same idea as sprites)
                if (!tile.DrawLocationWorld.IntersectsWith(queryRect))
                    continue;

                list.Add(tile);
            }
        }

        // 2) Sprites
        var sprites = SpriteManager.GetSpritesInWorldRectRange(queryRect, this, fullEnclosures: false);

        for (int i = 0; i < sprites.Count; i++)
        {
            var sprite = sprites[i];

            if (sprite is null)
                continue;

            // Defensive overlap check (cheap)
            if (!sprite.DrawLocationWorld.IntersectsWith(queryRect))
                continue;

            list.Add(sprite);
        }

        // 3) DirectDrawing instances
        var drawings = DirectDrawingManager.Instance.GetDrawingsForLayer(this);

        for (int i = 0; i < drawings.Count; i++)
        {
            var drawing = drawings[i];

            if (!drawing.Visible)
                continue;

            // Must be SceneLayer-mode by definition if it's "for layer", but be defensive:
            if (drawing.Mode != DirectDrawingMode.SceneLayer)
                continue;

            // Only include if it intersects this dirty rect
            if (!drawing.WorldBounds.IntersectsWith(worldRect))
                continue;

            list.Add(drawing);
        }

        // 4) Sort using Tile.CompareTo
        list.Sort(CompareDrawables); // ← this calls Tile.CompareTo internally
        return list;
    }

    private static int CompareDrawables(IDrawable a, IDrawable b)
    {
        int z = a.ZOrder.CompareTo(b.ZOrder);
        if (z != 0)
            return z;

        // Preserve legacy ordering for tiles/sprites when Z ties
        if (a is Tile ta && b is Tile tb)
            return ta.CompareTo(tb);

        // Stable tie-breaker (avoid flicker)
        return a.Id.CompareTo(b.Id);
    }

    #endregion private methods

    #region indexers

    /// <summary>
    /// Gets the tile at the specified grid coordinates.
    /// </summary>
    /// <param name="x">The zero-based column index (X coordinate).</param>
    /// <param name="y">The zero-based row index (Y coordinate).</param>
    /// <returns>
    /// The <see cref="SceneLayerTile"/> at the specified position, or <c>null</c> if the coordinates
    /// are out of bounds.
    /// </returns>
    /// <remarks>
    /// This indexer provides bounds-checked access to tiles in the layer's grid. Coordinates outside
    /// the valid range (0 to GridColumnCount-1, 0 to GridRowCount-1) return <c>null</c>. Wrapping
    /// is not applied; use <see cref="WrapGrid"/> first if wrapping behavior is desired.
    /// </remarks>
    public SceneLayerTile? this[int x, int y] => GetIndexer_NoWrap(x, y);

    /// <summary>
    /// Gets the tile at the specified grid coordinates.
    /// </summary>
    /// <param name="pt">A <see cref="Point"/> specifying the grid coordinates (X = column, Y = row).</param>
    /// <returns>
    /// The <see cref="SceneLayerTile"/> at the specified position, or <c>null</c> if the coordinates
    /// are out of bounds.
    /// </returns>
    /// <remarks>
    /// This indexer provides convenient point-based access to tiles and internally uses the
    /// [x, y] indexer. Coordinates outside the valid range return <c>null</c>.
    /// </remarks>
    public SceneLayerTile? this[Point pt] => this[pt.X, pt.Y];

    /// <summary>
    /// Gets the tile at the specified grid coordinates, truncating fractional values.
    /// </summary>
    /// <param name="ptF">
    /// A <see cref="PointF"/> specifying the grid coordinates (X = column, Y = row).
    /// Fractional values are truncated to integers.
    /// </param>
    /// <returns>
    /// The <see cref="SceneLayerTile"/> at the truncated position, or <c>null</c> if the coordinates
    /// are out of bounds.
    /// </returns>
    /// <remarks>
    /// This indexer provides convenient access when working with floating-point grid coordinates,
    /// such as results from <see cref="WorldPxToGrid"/>. The coordinates are cast to integers,
    /// truncating toward zero. Coordinates outside the valid range return <c>null</c>.
    /// </remarks>
    public SceneLayerTile? this[PointF ptF] => this[(int)ptF.X, (int)ptF.Y];

    private SceneLayerTile? GetIndexer_NoWrap(int x, int y)
    {
        if (x > _sceneLayerTileArray.GetUpperBound(0)
            || y > _sceneLayerTileArray.GetUpperBound(1)
            || x < 0
            || y < 0)
            return null;
        else
            return _sceneLayerTileArray[x, y];
    }

    #endregion indexers

    #region IEnumerable Members

    /// <summary>
    /// Returns an enumerator that iterates through the layer's tiles.
    /// </summary>
    /// <returns>An <see cref="IEnumerator"/> for iterating through <see cref="SceneLayerTile"/> instances.</returns>
    /// <remarks>
    /// This method enables enumeration of layer tiles using non-generic enumerator interfaces.
    /// Tiles are enumerated in column-major order (all rows in column 0, then all rows in column 1, etc.).
    /// For type-safe enumeration, the generic <see cref="IEnumerable{T}.GetEnumerator"/> is preferred.
    /// </remarks>
    public IEnumerator GetEnumerator() => ((IEnumerable<SceneLayerTile>)this).GetEnumerator();

    /// <summary>
    /// Returns a strongly-typed enumerator that iterates through the layer's tiles.
    /// </summary>
    /// <returns>
    /// An <see cref="IEnumerator{T}"/> for iterating through <see cref="SceneLayerTile"/> instances.
    /// </returns>
    /// <remarks>
    /// This method enables enumeration of layer tiles using <c>foreach</c> loops and LINQ queries.
    /// Tiles are enumerated in column-major order: all rows in column 0, then all rows in column 1, and so on.
    /// </remarks>
    IEnumerator<SceneLayerTile> IEnumerable<SceneLayerTile>.GetEnumerator()
    {
        for (int x = 0; x <= _sceneLayerTileArray.GetUpperBound(0); x++)
        {
            for (int y = 0; y <= _sceneLayerTileArray.GetUpperBound(1); y++)
            {
                yield return _sceneLayerTileArray[x, y];
            }
        }
    }

    #endregion IEnumerable Members

    #region IDisposable Members

    /// <summary>
    /// Releases all resources used by the <see cref="SceneLayer"/> and disposes all tiles.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method performs orderly cleanup of the layer, including:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Raising the <see cref="Disposing"/> event</description></item>
    /// <item><description>Disposing all <see cref="SceneLayerTile"/> instances in the grid</description></item>
    /// <item><description>Clearing all event subscriptions</description></item>
    /// </list>
    /// <para>
    /// After disposal, the layer should not be used. This method can be overridden in derived classes
    /// to add custom cleanup logic, but the base implementation should be called to ensure proper
    /// resource release.
    /// </para>
    /// <para>
    /// This method suppresses finalization to prevent the finalizer from running, as cleanup has
    /// already been performed.
    /// </para>
    /// </remarks>
    public virtual void Dispose()
    {
        GC.SuppressFinalize(this);

        Disposing?.Invoke(this);

        foreach (SceneLayerTile gridPt in this)
            gridPt.Dispose();

        // cancel all subscriptions to this object
        SceneLayerTileSizeChanged = null;
        VisibleChanged = null;
        WrappingChanged = null;
        Disposing = null;
    }

    #endregion IDisposable Members

    #region empty SceneLayer

    /// <summary>
    /// Gets the singleton empty scene layer instance.
    /// </summary>
    /// <value>
    /// A <see cref="SceneLayer"/> instance with zero dimensions that serves as a null object pattern.
    /// </value>
    /// <remarks>
    /// <para>
    /// The empty scene layer is a special singleton instance that contains no tiles (0x0 grid),
    /// is invisible, has minimum z-order, and serves as a placeholder when a valid layer is not available.
    /// It is used internally by <see cref="Scene.Empty"/> and helps avoid null reference checks.
    /// </para>
    /// <para>
    /// This layer has the following characteristics:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Grid dimensions: 0 columns × 0 rows</description></item>
    /// <item><description>Tile size: 1×1 pixels</description></item>
    /// <item><description>Visible: false</description></item>
    /// <item><description>ZOrder: int.MinValue</description></item>
    /// <item><description>Parallax: 1.0</description></item>
    /// </list>
    /// </remarks>
    public static SceneLayer Empty { get; } = new EmptySceneLayer();

    private sealed class EmptySceneLayer : SceneLayer
    {
        internal EmptySceneLayer()
            : base(columnCount: 0, rowCount: 0, width: 1, height: 1)
        {
            Visible = false;
            ZOrder = int.MinValue;
            Parallax = 1f;
        }
    }

    #endregion empty SceneLayer
}
