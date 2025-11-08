using System.Collections;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Runtime.Serialization;
using Gondwana.Drawing;
using Gondwana.Drawing.Coordinates;
using Gondwana.Rendering;
using Newtonsoft.Json;

namespace Gondwana.Scenes;

/// <summary>
///
/// </summary>
[JsonObject(IsReference = true)]
public class SceneLayer : IEnumerable<SceneLayerTile>, IDisposable
{
    #region events

    private EventHandler<RefreshQueueAreaAddedEventArgs> refQueueDel;
    internal event Action<RefreshQueueAreaAddedEventArgs>? RefreshQueueAreaAdded;

    public event Action<SceneLayer>? SceneLayerTileSizeChanged;

    public event Action<SceneLayer>? VisibleChanged;

    public event Action<SceneLayer>? WrappingChanged;

    public event Action<SceneLayer>? ShowGridLinesChanged;

    public event Action<SceneLayer>? ZOrderChanged;

    public event Action<SceneLayer>? ParallaxChanged;

    public event Action<SceneLayer>? Disposing;

    #endregion events

    #region private / internal fields

    private int _tileWidth;                             // rendered width
    private int _tileHeight;                            // rendered height
    private bool _visible;                              // is SceneLayer to be rendered; useful with multiple layers

    [JsonProperty]
    private SceneLayerTile[][] _sceneLayerTileArray;    // array of points; 2 dimensions (X, Y)

    internal bool _wrapHoriz = false;
    internal bool _wrapVerti = false;

    // first pixel visible (i.e., source pixel for rendering calculations)
    private Point _gridPtZeroPxl;

    private PointF _firstGridPt = new PointF();

    #endregion private / internal fields

    #region public fields

    [JsonIgnore]
    public object Tag;

    #endregion public fields

    #region SceneLayer wrapping delegates / variables

    private delegate SceneLayerTile? GetIndexer(int x, int y);

    private GetIndexer FindIndexedSceneLayerTile;

    #endregion SceneLayer wrapping delegates / variables

    #region constructors / finalizer

    public SceneLayer(int columnCount, int rowCount) :
        this(columnCount, rowCount, 0, 0, 1)
    { }

    public SceneLayer(int columnCount, int rowCount, int width, int height) :
        this(columnCount, rowCount, width, height, 1)
    { }

    public SceneLayer(int columnCount, int rowCount, int width, int height, float layerSyncModifier)
    {
        var pt = new SceneLayerTile[columnCount][];

        for (int i = 0; i < pt.Length; i++)
            pt[i] = new SceneLayerTile[rowCount];

        InitValues(pt, width, height, layerSyncModifier);
    }

    public SceneLayer(SceneLayerTile[][] pt) :
        this(pt, 0, 0, 1)
    { }

    public SceneLayer(SceneLayerTile[][] pt, int width, int height) :
        this(pt, width, height, 1)
    { }

    public SceneLayer(SceneLayerTile[][] pt, int width, int height, float layerSyncModifier)
    {
        InitValues(pt, width, height, layerSyncModifier);
    }

    ~SceneLayer()
    {
        Dispose();
    }

    [OnDeserialized]
    private void OnDeserialized(StreamingContext context)
    {
        InitValues(_sceneLayerTileArray, _tileWidth, _tileHeight, _parallax);
    }

    #endregion constructors / finalizer

    #region properties

    [JsonIgnore]
    public ISceneLayerCoordinates CoordinateSystem { get; set; } = new SquareIsoCoordinates();

    [DataMember(Name = "CoordinateSystem")]
    private string CoordinateSystemType
    {
        get
        {
            if (CoordinateSystem == null)
                return string.Empty;
            else
            {
                Type type = CoordinateSystem.GetType();
                return type.Assembly.FullName + ";" + type.ToString();
            }
        }
        set
        {
            if (string.IsNullOrEmpty(value))
                throw new ArgumentException("CoordinateSystemType must have a value", nameof(value));
            else
            {
                var values = value.Split(';');
                var handle = Activator.CreateInstance(values[0], values[1]);
                CoordinateSystem = (ISceneLayerCoordinates)handle.Unwrap();
            }
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
            SceneLayerTileSizeChanged?.Invoke(this);
            _tileHeight = value;
        }
    }

    [JsonProperty]
    public int SceneLayerTileWidth
    {
        get { return _tileWidth; }
        set
        {
            SceneLayerTileSizeChanged?.Invoke(this);
            _tileWidth = value;
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

    [JsonProperty]
    public PointF SourceSceneLayerTile
    {
        get { return _firstGridPt; }
        set { this.SetSourceSceneLayerTile(value); }
    }

    [JsonIgnore]
    public SceneLayerTile[][] SceneLayerTileArray
    {
        get { return _sceneLayerTileArray; }
    }

    [JsonIgnore]
    public int GridColumnCount
    {
        get { return _sceneLayerTileArray.GetUpperBound(0) + 1; }
    }

    [JsonIgnore]
    public int GridRowCount
    {
        get { return _sceneLayerTileArray[0].GetUpperBound(0) + 1; }
    }

    [JsonProperty]
    public bool WrapHorizontally
    {
        get { return _wrapHoriz; }
        set
        {
            _wrapHoriz = value;
            WrappingChanged?.Invoke(this);
        }
    }

    [JsonProperty]
    public bool WrapVertically
    {
        get { return _wrapVerti; }
        set
        {
            _wrapVerti = value;
            WrappingChanged?.Invoke(this);
        }
    }

    [JsonProperty]
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

    [JsonIgnore]
    public Point SceneLayerTileZeroPixel
    {
        get { return _gridPtZeroPxl; }
    }

    #endregion properties

    #region raise events

    internal virtual void OnRefreshQueueAreaAdded(RefreshQueueAreaAddedEventArgs e)
    {
        // just pass the event up
        RefreshQueueAreaAdded?.Invoke(e);
    }

    private void RefreshQueueNewTile(object? sender, RefreshQueueAreaAddedEventArgs e)
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

    public void SetSourceSceneLayerTile(PointF srcGridPt)
    {
        // capture the existing / old source pixel before changes made
        PointF oldSrcPt = SourceSceneLayerTile;
        _firstGridPt = srcGridPt;

        // update the first pixel position; the final SourceSceneLayerTile might be slightly
        // different to srcGridPt due to rounding if srcSceneLayerTile is not a whole number
        _gridPtZeroPxl = CoordinateSystem.GetAnchorPixelAtSceneLayerCoordinates(this, new PointF(0, 0));
    }

    #endregion public methods

    #region private / internal methods

    private void SaveGridCoordinatesToSceneLayerTiles()
    {
        // let each SceneLayerTile in array know its position in the array
        for (int X = 0; X <= _sceneLayerTileArray.GetUpperBound(0); X++)
        {
            for (int Y = 0; Y <= _sceneLayerTileArray[X].GetUpperBound(0); Y++)
            {
                _sceneLayerTileArray[X][Y] = new SceneLayerTile(this);
                _sceneLayerTileArray[X][Y].sceneLayerCoordinates = new Point(X, Y);
            }
        }
    }
    
    protected void InitValues(SceneLayerTile[][] pt, int width, int height, float layerSyncModifier)
    {
        _sceneLayerTileArray = pt;
        _parallax = layerSyncModifier;
        _tileWidth = width;
        _tileHeight = height;
        _visible = true;
        _gridPtZeroPxl = new Point(0, 0);
        // let each SceneLayerTile in array know its position in the array
        SaveGridCoordinatesToSceneLayerTiles();
        RefreshQueue = new RefreshQueue(this);
        refQueueDel = RefreshQueueNewTile;
        RefreshQueue.RefreshQueueAreaAdded += refQueueDel;
        FindIndexedSceneLayerTile = new GetIndexer(GetIndexer_NoWrap);
    }

    #endregion private / internal methods

    #region indexers

    public SceneLayerTile? this[int x, int y]
    {
        get { return FindIndexedSceneLayerTile(x, y); }
        set
        {
            PointF actualSceneLayerTile =
                CoordinateSystem.FindEquivalentSceneLayerCoordinates(new PointF((float)x, (float)y), _sceneLayerTileArray.GetUpperBound(0), _sceneLayerTileArray[x].GetUpperBound(0));

            _sceneLayerTileArray[(int)actualSceneLayerTile.X][(int)actualSceneLayerTile.Y] = value;
        }
    }

    public SceneLayerTile? this[Point pt]
    {
        get { return this[pt.X, pt.Y]; }
        set { this[pt.X, pt.Y] = value; }
    }

    public SceneLayerTile? this[PointF ptF]
    {
        get { return this[(int)ptF.X, (int)ptF.Y]; }
        set { this[(int)ptF.X, (int)ptF.Y] = value; }
    }

    private SceneLayerTile? GetIndexer_NoWrap(int x, int y)
    {
        if (x > _sceneLayerTileArray.GetUpperBound(0)
            || y > _sceneLayerTileArray[0].GetUpperBound(0)
            || x < 0
            || y < 0)
            return null;
        else
            return _sceneLayerTileArray[x][y];
    }

    private SceneLayerTile? GetIndexer_Wrap(int x, int y)
    {
        // if not wrapping horizontally and outside of x bound range, return null
        if ((!_wrapHoriz) && ((x > _sceneLayerTileArray.GetUpperBound(0)) || (x < 0)))
            return null;

        // if not wrapping vertically and outside of y bound range, return null
        if ((!_wrapVerti) && ((y > _sceneLayerTileArray[x].GetUpperBound(0)) || (y < 0)))
            return null;

        // check "non-wrapping" coordinates
        SceneLayerTile? newSceneLayerTile = GetIndexer_NoWrap(x, y);

        // if outside of "non-wrapping" coordinates, find the equivalent point
        if (newSceneLayerTile == null)
        {
            // find the coordinated of the SceneLayerTile being "wrapped"
            PointF actualSceneLayerTile =
                CoordinateSystem.FindEquivalentSceneLayerCoordinates(new PointF((float)x, (float)y), _sceneLayerTileArray.GetUpperBound(0), _sceneLayerTileArray[x].GetUpperBound(0));

            // if not already found, create and add to wrappedGridPts, and associate with "parent"
            if (newSceneLayerTile == null)
            {
                newSceneLayerTile = new SceneLayerTile(_sceneLayerTileArray[(int)actualSceneLayerTile.X][(int)actualSceneLayerTile.Y],
                    new Point(x, y));
            }
        }

        return newSceneLayerTile;
    }

    #endregion indexers

    #region IEnumerable Members

    public IEnumerator GetEnumerator() => ((IEnumerable<SceneLayerTile>)this).GetEnumerator();

    IEnumerator<SceneLayerTile> IEnumerable<SceneLayerTile>.GetEnumerator()
    {
        for (int x = 0; x <= _sceneLayerTileArray.GetUpperBound(0); x++)
        {
            for (int y = 0; y <= _sceneLayerTileArray[x].GetUpperBound(0); y++)
            {
                yield return _sceneLayerTileArray[x][y];
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
