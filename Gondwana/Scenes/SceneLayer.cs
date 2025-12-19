using System.Collections;
using System.Drawing;
using System.Runtime.Serialization;
using Gondwana.Drawing;
using Gondwana.Drawing.Coordinates;
using Gondwana.Drawing.Sprites;
using Gondwana.Rendering;
using Newtonsoft.Json;

namespace Gondwana.Scenes;

[JsonObject(IsReference = true)]
public class SceneLayer : IEnumerable<SceneLayerTile>, IDisposable
{
    #region events

    public event Action<SceneLayer>? SceneLayerTileSizeChanged;

    public event Action<SceneLayer>? VisibleChanged;

    public event Action<SceneLayer>? WrappingChanged;

    public event Action<SceneLayer>? ShowGridLinesChanged;

    public event Action<SceneLayer>? ShowCollisionBoxesChanged;

    public event Action<SceneLayer>? ZOrderChanged;

    public event Action<SceneLayer>? ParallaxChanged;

    public event Action<SceneLayer>? OriginPxChanged;

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
                        CoordinateSystemTypes coordinateSystem = CoordinateSystemTypes.SqaureIso)
    {
        var tileArray = new SceneLayerTile[columnCount, rowCount];

        InitValues(tileArray, width, height, parallax, coordinateSystem);
    }

    [OnDeserialized]
    private void OnDeserialized(StreamingContext context) =>
        InitValues(_sceneLayerTileArray, _tileWidth, _tileHeight, _parallax, CoordinateSystemType);

    ~SceneLayer()
    {
        Dispose();
    }

    #endregion constructors / finalizer

    #region properties

    [JsonIgnore]
    public object? Tag { get; set; }

    [JsonIgnore]
    internal ISceneLayerCoordinates CoordinateSystem { get; private set; } = new SquareIsoCoordinates();

    [JsonProperty]
    public CoordinateSystemTypes CoordinateSystemType
    {
        get
        {
            return CoordinateSystem switch
            {
                SquareIsoCoordinates => CoordinateSystemTypes.SqaureIso,
                DiagIsoDiagMatrixCoordinates => CoordinateSystemTypes.DiagIso_DiagMatrix,
                DiagIsoSquareMatrixCoordinates => CoordinateSystemTypes.DiagIso_SquareMatrix,
                HexagonalFlatTopCoordinates => CoordinateSystemTypes.HexFlatTop,
                HexagonalPointedTopCoordinates => CoordinateSystemTypes.HexPointedTop,
                _ => throw new InvalidOperationException($"Unknown coordinate system type: {CoordinateSystem.GetType().Name}")
            };
        }
        set
        {
            CoordinateSystem = value switch
            {
                CoordinateSystemTypes.SqaureIso => new SquareIsoCoordinates(),
                CoordinateSystemTypes.DiagIso_DiagMatrix => new DiagIsoDiagMatrixCoordinates(),
                CoordinateSystemTypes.DiagIso_SquareMatrix => new DiagIsoSquareMatrixCoordinates(),
                CoordinateSystemTypes.HexFlatTop => new HexagonalFlatTopCoordinates(),
                CoordinateSystemTypes.HexPointedTop => new HexagonalPointedTopCoordinates(),
                _ => throw new ArgumentOutOfRangeException(nameof(value), $"Unknown coordinate system type: {value}")
            };
        }
    }

    [JsonProperty]
    public string ID { get; protected internal set; } = Guid.NewGuid().ToString();

    [JsonProperty]
    public Scene Scene { get; internal set; }

    [JsonIgnore]
    internal RefreshQueue RefreshQueue { get; set; }

    private int _zOrder = 0;

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

    [JsonProperty]
    public int SceneLayerTileHeight
    {
        get { return _tileHeight; }
        set
        {
            _tileHeight = value;
            SceneLayerTileSizeChanged?.Invoke(this);
        }
    }

    [JsonProperty]
    public int SceneLayerTileWidth
    {
        get { return _tileWidth; }
        set
        {
            _tileWidth = value;
            SceneLayerTileSizeChanged?.Invoke(this);
        }
    }

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

    [JsonIgnore]
    public SceneLayerTile[,] SceneLayerTileArray => _sceneLayerTileArray;

    [JsonIgnore]
    public int GridColumnCount => _sceneLayerTileArray.GetUpperBound(0) + 1;

    [JsonIgnore]
    public int GridRowCount => _sceneLayerTileArray.GetUpperBound(1) + 1;

    [JsonProperty("WrapHorizontally")]
    private bool _wrapHoriz = false;

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

    // World-space origin (in pixels) of this layer’s (0,0) tile.
    // Usually (0,0); can be shifted to move the entire layer as a block.
    [JsonProperty("OriginPx")]
    private Point _originPx = Point.Empty;

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

    #endregion properties

    #region public methods

    public void SetTileSize(int newWidth, int newHeight)
    {
        _tileWidth = newWidth;
        _tileHeight = newHeight;
        SceneLayerTileSizeChanged?.Invoke(this);
    }

    /// <summary>
    /// Converts a grid coordinate (col,row) into the world-space pixel anchor
    /// where that tile begins. This returns the tile’s top-left anchor in world
    /// pixels, not the tile center.
    /// </summary>
    public PointF GridToWorldPx(PointF grid) => CoordinateSystem.GetAnchorPixelAtSceneLayerCoordinates(this, grid);

    /// <summary>
    /// Converts a world-space pixel position into this layer’s grid coordinates.
    /// This uses the layer’s active coordinate system (square, iso, hex, etc.)
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
    /// Wraps a grid coordinate around the layer’s valid grid bounds using 
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
    public RectangleF GetLayerBoundsPx()
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

        // let each SceneLayerTile in array know its position in the array
        SaveGridCoordinatesToSceneLayerTiles();
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

    internal IEnumerable<Tile> GetTilesInWorldRect(Rectangle worldRect, bool includeOverhang = true)
    {
        // Gather into a list so we can sort it.
        var list = new List<Tile>(64);
        var seen = new HashSet<Tile>();

        // 1) Grid tiles
        var sceneLayerTiles = CoordinateSystem.GetSceneLayerTilesInPixelRange(
            this,
            worldRect,
            includeOverhang: includeOverhang);

        if (sceneLayerTiles != null)
        {
            for (int i = 0; i < sceneLayerTiles.Count; i++)
            {
                var t = sceneLayerTiles[i];
                if (t is null) continue;

                // Defensive overlap check (same idea as sprites)
                if (!t.DrawLocation.IntersectsWith(worldRect))
                    continue;

                if (seen.Add(t))
                    list.Add(t);
            }
        }

        // 2) Sprites
        var sprites = SpriteManager.GetSpritesInRange(worldRect, this, fullEnclosures: false);

        for (int i = 0; i < sprites.Count; i++)
        {
            var s = sprites[i];
            if (s is null) continue;

            // Defensive overlap check (cheap)
            if (!s.DrawLocation.IntersectsWith(worldRect))
                continue;

            if (seen.Add(s))
                list.Add(s);
        }

        // 3) Sort using Tile.CompareTo
        list.Sort(); // ← this calls Tile.CompareTo internally

        // 4) Yield in sorted order
        for (int i = 0; i < list.Count; i++)
            yield return list[i];
    }

    #endregion private methods

    #region indexers

    public SceneLayerTile? this[int x, int y] => GetIndexer_NoWrap(x, y);

    public SceneLayerTile? this[Point pt] => this[pt.X, pt.Y];

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

    public IEnumerator GetEnumerator() => ((IEnumerable<SceneLayerTile>)this).GetEnumerator();

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

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        Disposing?.Invoke(this);

        // dispose child objects
        RefreshQueue.Dispose();

        foreach (SceneLayerTile gridPt in this)
            gridPt.Dispose();

        // cancel all subscriptions to this object
        SceneLayerTileSizeChanged = null;
        VisibleChanged = null;
        WrappingChanged = null;
        Disposing = null;
    }

    #endregion IDisposable Members
}
