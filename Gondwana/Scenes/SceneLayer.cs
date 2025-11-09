using System.Collections;
using System.Drawing;
using System.Runtime.Serialization;
using Gondwana.Drawing.Coordinates;
using Gondwana.Rendering;
using Newtonsoft.Json;

namespace Gondwana.Scenes;

[JsonObject(IsReference = true)]
public class SceneLayer : IEnumerable<SceneLayerTile>, IDisposable
{
    #region events

    private Action<RefreshQueueAreaAddedEventArgs> refQueueDel;     // for responding to *other* SceneLayers' RefreshQueue events
    internal event Action<RefreshQueueAreaAddedEventArgs>? RefreshQueueAreaAdded;   // raised by *this* SceneLayer's RefreshQueue

    public event Action<SceneLayer>? SceneLayerTileSizeChanged;

    public event Action<SceneLayer>? VisibleChanged;

    public event Action<SceneLayer>? WrappingChanged;

    public event Action<SceneLayer>? ShowGridLinesChanged;

    public event Action<SceneLayer>? ZOrderChanged;

    public event Action<SceneLayer>? ParallaxChanged;

    public event Action<SceneLayer>? RenderSurfaceOriginPxChanged;

    public event Action<SceneLayer>? Disposing;

    #endregion events

    #region private fields

    private int _tileWidth;                             // rendered width
    private int _tileHeight;                            // rendered height
    private bool _visible;                              // is SceneLayer to be rendered; useful with multiple layers

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
    public ISceneLayerCoordinates CoordinateSystem { get; private set; } = new SquareIsoCoordinates();

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
    private bool showGridLines;

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

    // first pixel visible (i.e., source pixel for rendering calculations)
    [JsonProperty("RenderSurfaceOriginPx")]
    private Point _renderSurfaceOriginPx;

    [JsonIgnore]
    public Point RenderSurfaceOriginPx
    {
        get => _renderSurfaceOriginPx;
        set
        {
            _renderSurfaceOriginPx = value;
            RenderSurfaceOriginPxChanged?.Invoke(this);
        }
    }

    [JsonIgnore]
    public PointF RenderSurfaceOriginCoordinates => CoordinateSystem.GetSceneLayerCoordinatesAtPixel(this, RenderSurfaceOriginPx);

    #endregion properties

    #region raise events

    internal virtual void OnRefreshQueueAreaAdded(RefreshQueueAreaAddedEventArgs e)
    {
        // just pass the event up
        RefreshQueueAreaAdded?.Invoke(e);
    }

    private void RefreshQueueNewTile(RefreshQueueAreaAddedEventArgs e)
    {
        // pass the event up to any containing SceneLayers
        OnRefreshQueueAreaAdded(e);
    }

    #endregion raise events

    #region public methods

    public void SetSceneLayerTileSize(int newWidth, int newHeight)
    {
        _tileWidth = newWidth;
        _tileHeight = newHeight;
        SceneLayerTileSizeChanged?.Invoke(this);
    }

    #endregion public methods

    #region private / internal methods

    private void InitValues(SceneLayerTile[,] tileArray, int width, int height, float parallax, CoordinateSystemTypes coordinateSystem)
    {
        _sceneLayerTileArray = tileArray;
        _parallax = parallax;
        _tileWidth = width;
        _tileHeight = height;
        _visible = true;
        _renderSurfaceOriginPx = new Point(0, 0);
        CoordinateSystemType = coordinateSystem;

        // let each SceneLayerTile in array know its position in the array
        SaveGridCoordinatesToSceneLayerTiles();
        RefreshQueue = new RefreshQueue(this);
        refQueueDel = RefreshQueueNewTile;
        RefreshQueue.RefreshQueueAreaAdded += refQueueDel;
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

    #endregion private / internal methods

    #region indexers

    public SceneLayerTile? this[int x, int y]
    {
        get { return GetIndexer_NoWrap(x, y); }
    }

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

        // unsubscribe from events
        RefreshQueue.RefreshQueueAreaAdded -= refQueueDel;

        // dispose child objects
        RefreshQueue.Dispose();

        foreach (SceneLayerTile gridPt in this)
            gridPt.Dispose();

        // cancel all subscriptions to this object
        SceneLayerTileSizeChanged = null;
        VisibleChanged = null;
        RefreshQueueAreaAdded = null;
        WrappingChanged = null;
        Disposing = null;
    }

    #endregion IDisposable Members
}
