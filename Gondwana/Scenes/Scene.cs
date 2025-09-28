using System.Collections;
using System.Collections.ObjectModel;
using System.Runtime.Serialization;
using Gondwana.Rendering;
using Newtonsoft.Json;

namespace Gondwana.Scenes;

/// <summary>
///
/// </summary>
[JsonObject(IsReference = true)]
public class Scene : IEnumerable, IDisposable
{
    #region static fields

    internal static List<Scene> _allSceneLayeres = new List<Scene>();

    #endregion static fields

    #region private / internal field declarations

    [JsonProperty]
    private List<SceneLayer> _matrixes;    // array of SceneLayer objects; each element is one "layer"

    internal List<SceneLayer> _visibleLayers = new List<SceneLayer>();
    internal SceneRefreshType refreshNeeded = SceneRefreshType.All;

    private string _id = Guid.NewGuid().ToString();

    #endregion private / internal field declarations

    #region public fields

    [JsonIgnore]
    public object Tag;

    #endregion public fields

    #region events

    public event SceneLayerAddRemoveHandler SceneLayerAdded;

    public event SceneLayerAddRemoveHandler SceneLayerRemoved;

    public event SceneLayeresDisposingEventHandler Disposing;

    #endregion events

    #region delegates

    private SourceGridPointChangedEventHandler firstCRDel;
    private VisibleChangedEventHandler visChgDel;
    private GridPointSizeChangedEventHandler gridPtSzDel;
    private EventHandler<RefreshQueueAreaAddedEventArgs> refQueueDel;
    private SceneLayerWrappingChangedEventHandler wrappingDel;
    private SceneLayerDisposingEventHandler matrixDisposingDel;

    #endregion delegates

    #region constructors / finalizer

    public Scene()
    {
        _matrixes = new List<SceneLayer>();
        Init();
    }

    public Scene(SceneLayer matrix)
    {
        _matrixes = new List<SceneLayer>();
        _matrixes.Add(matrix);
        Init();
    }

    public Scene(List<SceneLayer> matrixes)
    {
        _matrixes = matrixes;
        Init();
    }

    ~Scene()
    {
        Dispose();
    }

    [OnDeserialized]
    private void OnDeserialized(StreamingContext context)
    {
        Init();
    }

    #endregion constructors / finalizer

    #region properties

    [JsonProperty]
    public string ID
    {
        get { return _id; }
        protected internal set { _id = value; }
    }

    [JsonIgnore]
    public int Count
    {
        get { return _matrixes.Count; }
    }

    [JsonIgnore]
    public int CountOfVisibleLayers
    {
        get { return _visibleLayers.Count; }
    }

    [JsonIgnore]
    public SceneLayer ForemostVisibleLayer
    {
        get
        {
            if (_visibleLayers.Count == 0) { return null; }
            else { return (SceneLayer)_visibleLayers[0]; }
        }
    }

    [JsonIgnore]
    public SceneLayer BackmostVisibleLayer
    {
        get
        {
            if (_visibleLayers.Count == 0) { return null; }
            else { return (SceneLayer)_visibleLayers[_visibleLayers.Count - 1]; }
        }
    }

    [JsonIgnore]
    public SceneRefreshType RefreshNeeded
    {
        get { return refreshNeeded; }
        set { refreshNeeded = value; }
    }

    [JsonIgnore]
    public ReadOnlyCollection<SceneLayer> SceneLayerList
    {
        get { return _matrixes.AsReadOnly(); }
    }

    [JsonIgnore]
    public List<SceneLayer> VisibleSceneLayerList
    {
        get { return _visibleLayers; }
    }

    #endregion properties

    #region public methods

    public SceneLayer AddLayer(SceneLayer matrix)
    {
        _matrixes.Add(matrix);
        int newIdx = _matrixes.Count - 1;
        OnSceneLayerAdded(this[newIdx]);

        // rediscover the list of visible arrays
        _SetVisibleLayersArray();

        refreshNeeded = SceneRefreshType.All;

        return this[newIdx];
    }

    public void ClearAllLayers()
    {
        // raise "remove" event for each grid
        foreach (SceneLayer grid in this)
            OnSceneLayerRemoved(grid);

        _matrixes.Clear();

        // rediscover the list of visible arrays
        _SetVisibleLayersArray();

        refreshNeeded = SceneRefreshType.All;
    }

    public void ClearLayer(int matrix)
    {
        SceneLayer grid = this[matrix];
        _matrixes.Remove(grid);
        OnSceneLayerRemoved(grid);
        grid = null;

        // rediscover the list of visible arrays
        _SetVisibleLayersArray();

        refreshNeeded = SceneRefreshType.All;
    }

    public void ClearLayer(SceneLayer matrix)
    {
        _matrixes.Remove(matrix);
        OnSceneLayerRemoved(matrix);

        // rediscover the list of visible arrays
        _SetVisibleLayersArray();

        refreshNeeded = SceneRefreshType.All;
    }

    public SceneLayer GetMatrixByID(string id)
    {
        foreach (SceneLayer matrix in _matrixes)
        {
            if (matrix.ID == id)
                return matrix;
        }

        return null;
    }

    public int GetMatrixPosition(SceneLayer matrix)
    {
        int ret = -1;

        for (int i = this.Count - 1; i >= 0; i--)
        {
            if (this[i] == matrix)
            {
                ret = i;
                break;
            }
        }

        return ret;
    }

    #endregion public methods

    #region raise events

    protected virtual void OnSceneLayerAdded(SceneLayer grid)
    {
        grid.Parent = this;

        grid.FirstColRowChanged += firstCRDel;
        grid.VisibleChanged += visChgDel;
        grid.GridPointSizeChanged += gridPtSzDel;
        grid.RefreshQueueAreaAdded += refQueueDel;
        grid.WrappingChanged += wrappingDel;

        if (SceneLayerAdded != null)
            SceneLayerAdded(new SceneLayerAddRemoveEventArgs(this, grid));
    }

    protected virtual void OnSceneLayerRemoved(SceneLayer grid)
    {
        grid.Parent = null;

        grid.FirstColRowChanged -= firstCRDel;
        grid.VisibleChanged -= visChgDel;
        grid.GridPointSizeChanged -= gridPtSzDel;
        grid.RefreshQueueAreaAdded -= refQueueDel;
        grid.WrappingChanged -= wrappingDel;

        if (SceneLayerRemoved != null)
            SceneLayerRemoved(new SceneLayerAddRemoveEventArgs(this, grid));
    }

    #endregion raise events

    #region private methods

    private void _MatrixColRowChanged(SourceGridPointChangedEventArgs e)
    {
        // shifting at least one Layer, so redraw entire Backbuffer
        refreshNeeded = SceneRefreshType.All;
    }

    private void _MatrixVisibleChanged(VisibleChangedEventArgs e)
    {
        // redraw entire Backbuffer
        refreshNeeded = SceneRefreshType.All;
        _SetVisibleLayersArray();
    }

    private void _SetVisibleLayersArray()
    {
        if (_visibleLayers == null)
            _visibleLayers = new List<SceneLayer>();

        _visibleLayers.Clear();
        foreach (SceneLayer grid in this)
        {
            if (grid.Visible == true)
                _visibleLayers.Add(grid);
        }
    }

    private void _GridPointSizeChanged(SceneLayerPointSizeChangedEventArgs e)
    {
        refreshNeeded = SceneRefreshType.All;
    }

    private void _RefreshQueueNewArea(object? sender, RefreshQueueAreaAddedEventArgs e)
    {
        // set refresh to Queue if no refresh required
        if (refreshNeeded == SceneRefreshType.None)
            refreshNeeded = SceneRefreshType.Queue;

        // if matrix that added Tile to queue is visible...
        if (e.layer.Visible)
        {
            // refresh all other visible matrixes
            for (int i = _visibleLayers.Count - 1; i >= 0; i--)
            {
                SceneLayer otherMatrix = _visibleLayers[i];

                // refresh other matrixes; no need to do the calling one again
                if (e.layer != otherMatrix)
                    // only refresh e.tileAdded.DrawLocationRefresh rectangle; do not raise cascading events
                    otherMatrix.RefreshQueue.AddPixelRangeToRefreshQueue(e.area, false);
            }
        }
    }

    private void _SceneLayerWrappingChanged(SceneLayerWrappingChangedEventArgs e)
    {
        refreshNeeded = SceneRefreshType.All;
    }

    private void _SceneLayerDisposing(SceneLayerDisposingEventArgs e)
    {
        ClearLayer(e.Matrix);
    }

    /// <summary>
    /// set delegates to be used to subscribe to SceneLayer events
    /// </summary>
    private void SetEventDelegates()
    {
        firstCRDel = new SourceGridPointChangedEventHandler(_MatrixColRowChanged);
        visChgDel = new VisibleChangedEventHandler(_MatrixVisibleChanged);
        gridPtSzDel = new GridPointSizeChangedEventHandler(_GridPointSizeChanged);
        refQueueDel = new EventHandler<RefreshQueueAreaAddedEventArgs>(_RefreshQueueNewArea);
        wrappingDel = new SceneLayerWrappingChangedEventHandler(_SceneLayerWrappingChanged);
        matrixDisposingDel = new SceneLayerDisposingEventHandler(_SceneLayerDisposing);
    }

    private void Init()
    {
        SetEventDelegates();

        foreach (SceneLayer matrix in _matrixes)
            OnSceneLayerAdded(matrix);

        // discover the list of visible arrays
        _SetVisibleLayersArray();

        _allSceneLayeres.Add(this);
    }

    #endregion private methods

    #region indexers

    public SceneLayer this[int i]
    {
        get
        {
            try { return _matrixes[i]; }
            catch { throw; }
        }
    }

    public SceneLayer this[string id]
    {
        get
        {
            try { return GetMatrixByID(id); }
            catch { throw; }
        }
    }

    #endregion indexers

    #region enumerable code

    public IEnumerator GetEnumerator()
    {
        for (int i = 0; i < _matrixes.Count; i++)
        {
            yield return _matrixes[i];
        }
    }

    #endregion enumerable code

    #region IDisposable Members

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        _allSceneLayeres.Remove(this);

        if (Disposing != null)
            Disposing(new SceneLayeresDisposingEventArgs(this));

        // unsubscribe from events
        foreach (SceneLayer grid in _matrixes)
        {
            grid.FirstColRowChanged -= firstCRDel;
            grid.VisibleChanged -= visChgDel;
            grid.GridPointSizeChanged -= gridPtSzDel;
            grid.RefreshQueueAreaAdded -= refQueueDel;
            grid.WrappingChanged -= wrappingDel;
        }

        // cancel all subscriptions to this object
        SceneLayerAdded = null;
        SceneLayerRemoved = null;
        Disposing = null;
    }

    #endregion IDisposable Members

    #region static methods

    public static Scene GetSceneLayeresByID(string id)
    {
        foreach (Scene matrixes in _allSceneLayeres)
        {
            if (matrixes.ID == id)
                return matrixes;
        }

        return null;
    }

    public static List<string> GetAllSceneLayeresIDs()
    {
        List<string> ret = new List<string>(_allSceneLayeres.Count);
        foreach (Scene matrixes in _allSceneLayeres)
            ret.Add(matrixes.ID);

        return ret;
    }

    public static ReadOnlyCollection<Scene> GetAllSceneLayeres()
    {
        return _allSceneLayeres.AsReadOnly();
    }

    public static void ClearAllSceneLayers()
    {
        var tmp = new List<Scene>(_allSceneLayeres);
        foreach (var matrixes in tmp)
            matrixes.Dispose();
    }

    #endregion static methods
}