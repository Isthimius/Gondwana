using Gondwana.Common.Enums;
using Gondwana.EventArgs;
using System.Collections;
using System.Collections.ObjectModel;
using System.Runtime.Serialization;

namespace Gondwana.Grid;

/// <summary>
/// 
/// </summary>
[DataContract(IsReference = true)]
public class GridPointMatrixes : IEnumerable, IDisposable
{
    #region static fields
    internal static List<GridPointMatrixes> _allGridPointMatrixes = new List<GridPointMatrixes>();
    #endregion

    #region private / internal field declarations
    [DataMember]
    private List<GridPointMatrix> _matrixes;    // array of GridPointMatrix objects; each element is one "layer"
    
    internal List<GridPointMatrix> _visibleLayers = new List<GridPointMatrix>();
    internal MatrixesRefreshType refreshNeeded = MatrixesRefreshType.All;

    private string _id = Guid.NewGuid().ToString();
    #endregion

    #region public fields
    [IgnoreDataMember]
    public object Tag;
    #endregion

    #region events
    public event GridPointMatrixAddRemoveHandler GridPointMatrixAdded;
    public event GridPointMatrixAddRemoveHandler GridPointMatrixRemoved;
    public event GridPointMatrixesDisposingEventHandler Disposing;
    #endregion

    #region delegates
    private SourceGridPointChangedEventHandler firstCRDel;
    private VisibleChangedEventHandler visChgDel;
    private GridPointSizeChangedEventHandler gridPtSzDel;
    private RefreshQueueAreaAddedEventHandler refQueueDel;
    private GridPointMatrixWrappingChangedEventHandler wrappingDel;
    private GridPointMatrixDisposingEventHandler matrixDisposingDel;
    #endregion

    #region constructors / finalizer
    public GridPointMatrixes()
    {
        _matrixes = new List<GridPointMatrix>();
        Init();
    }

    public GridPointMatrixes(GridPointMatrix matrix)
    {
        _matrixes = new List<GridPointMatrix>();
        _matrixes.Add(matrix);
        Init();
    }

    public GridPointMatrixes(List<GridPointMatrix> matrixes)
    {
        _matrixes = matrixes;
        Init();
    }

    ~GridPointMatrixes()
    {
        Dispose();
    }

    [OnDeserialized]
    private void OnDeserialized(StreamingContext context)
    {
        Init();
    }
    #endregion

    #region properties
    [DataMember]
    public string ID
    {
        get { return _id; }
        protected internal set { _id = value; }
    }

    [IgnoreDataMember]
    public int Count
    {
        get { return _matrixes.Count; }
    }

    [IgnoreDataMember]
    public int CountOfVisibleLayers
    {
        get { return _visibleLayers.Count; }
    }

    [IgnoreDataMember]
    public GridPointMatrix ForemostVisibleLayer
    {
        get
        {
            if (_visibleLayers.Count == 0) { return null; }
            else { return (GridPointMatrix)_visibleLayers[0]; }
        }
    }

    [IgnoreDataMember]
    public GridPointMatrix BackmostVisibleLayer
    {
        get
        {
            if (_visibleLayers.Count == 0) { return null; }
            else { return (GridPointMatrix)_visibleLayers[_visibleLayers.Count - 1]; }
        }
    }

    [IgnoreDataMember]
    public MatrixesRefreshType RefreshNeeded
    {
        get { return refreshNeeded; }
        set { refreshNeeded = value; }
    }

    [IgnoreDataMember]
    public ReadOnlyCollection<GridPointMatrix> GridPointMatrixList
    {
        get { return _matrixes.AsReadOnly(); }
    }

    [IgnoreDataMember]
    public List<GridPointMatrix> VisibleGridPointMatrixList
    {
        get { return _visibleLayers; }
    }
    #endregion
    
    #region public methods
    public GridPointMatrix AddLayer(GridPointMatrix matrix)
    {
        _matrixes.Add(matrix);
        int newIdx = _matrixes.Count - 1;
        OnGridPointMatrixAdded(this[newIdx]);

        // rediscover the list of visible arrays
        _SetVisibleLayersArray();

        refreshNeeded = MatrixesRefreshType.All;

        return this[newIdx];
    }

    public void ClearAllLayers()
    {
        // raise "remove" event for each grid
        foreach (GridPointMatrix grid in this)
            OnGridPointMatrixRemoved(grid);

        _matrixes.Clear();

        // rediscover the list of visible arrays
        _SetVisibleLayersArray();

        refreshNeeded = MatrixesRefreshType.All;
    }

    public void ClearLayer(int matrix)
    {
        GridPointMatrix grid = this[matrix];
        _matrixes.Remove(grid);
        OnGridPointMatrixRemoved(grid);
        grid = null;

        // rediscover the list of visible arrays
        _SetVisibleLayersArray();

        refreshNeeded = MatrixesRefreshType.All;
    }

    public void ClearLayer(GridPointMatrix matrix)
    {
        _matrixes.Remove(matrix);
        OnGridPointMatrixRemoved(matrix);

        // rediscover the list of visible arrays
        _SetVisibleLayersArray();

        refreshNeeded = MatrixesRefreshType.All;
    }

    public GridPointMatrix GetMatrixByID(string id)
    {
        foreach (GridPointMatrix matrix in _matrixes)
        {
            if (matrix.ID == id)
                return matrix;
        }

        return null;
    }

    public int GetMatrixPosition(GridPointMatrix matrix)
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
    #endregion

    #region raise events
    protected virtual void OnGridPointMatrixAdded(GridPointMatrix grid)
    {
        grid._parent = this;

        grid.FirstColRowChanged += firstCRDel;
        grid.VisibleChanged += visChgDel;
        grid.GridPointSizeChanged += gridPtSzDel;
        grid.RefreshQueueAreaAdded += refQueueDel;
        grid.WrappingChanged += wrappingDel;

        if (GridPointMatrixAdded != null)
            GridPointMatrixAdded(new GridPointMatrixAddRemoveEventArgs(this, grid));
    }

    protected virtual void OnGridPointMatrixRemoved(GridPointMatrix grid)
    {
        grid._parent = null;

        grid.FirstColRowChanged -= firstCRDel;
        grid.VisibleChanged -= visChgDel;
        grid.GridPointSizeChanged -= gridPtSzDel;
        grid.RefreshQueueAreaAdded -= refQueueDel;
        grid.WrappingChanged -= wrappingDel;

        if (GridPointMatrixRemoved != null)
            GridPointMatrixRemoved(new GridPointMatrixAddRemoveEventArgs(this, grid));
    }
    #endregion

    #region private methods
    private void _MatrixColRowChanged(SourceGridPointChangedEventArgs e)
    {
        // shifting at least one Layer, so redraw entire Backbuffer
        refreshNeeded = MatrixesRefreshType.All;
    }

    private void _MatrixVisibleChanged(VisibleChangedEventArgs e)
    {
        // redraw entire Backbuffer
        refreshNeeded = MatrixesRefreshType.All;
        _SetVisibleLayersArray();
    }

    private void _SetVisibleLayersArray()
    {
        if (_visibleLayers == null)
            _visibleLayers = new List<GridPointMatrix>();

        _visibleLayers.Clear();
        foreach (GridPointMatrix grid in this)
        {
            if (grid.Visible == true)
                _visibleLayers.Add(grid);
        }
    }

    private void _GridPointSizeChanged(GridPointSizeChangedEventArgs e)
    {
        refreshNeeded = MatrixesRefreshType.All;
    }

    private void _RefreshQueueNewArea(RefreshQueueAreaAddedEventArgs e)
    {
        // set refresh to Queue if no refresh required
        if (refreshNeeded == MatrixesRefreshType.None)
            refreshNeeded = MatrixesRefreshType.Queue;

        // if matrix that added Tile to queue is visible...
        if (e.layer.Visible == true)
        {
            // refresh all other visible matrixes
            for (int i = _visibleLayers.Count - 1; i >= 0; i--)
            {
                GridPointMatrix otherMatrix = _visibleLayers[i];

                // refresh other matrixes; no need to do the calling one again
                if (e.layer != otherMatrix)
                    // only refresh e.tileAdded.DrawLocationRefresh rectangle; do not raise cascading events
                    otherMatrix.RefreshQueue.AddPixelRangeToRefreshQueue(e.area, false);
            }
        }
    }

    private void _GridPointMatrixWrappingChanged(GridPointMatrixWrappingChangedEventArgs e)
    {
        refreshNeeded = MatrixesRefreshType.All;
    }

    private void _GridPointMatrixDisposing(GridPointMatrixDisposingEventArgs e)
    {
        ClearLayer(e.Matrix);
    }

    /// <summary>
    /// set delegates to be used to subscribe to GridPointMatrix events
    /// </summary>
    private void SetEventDelegates()
    {
        firstCRDel = new SourceGridPointChangedEventHandler(_MatrixColRowChanged);
        visChgDel = new VisibleChangedEventHandler(_MatrixVisibleChanged);
        gridPtSzDel = new GridPointSizeChangedEventHandler(_GridPointSizeChanged);
        refQueueDel = new RefreshQueueAreaAddedEventHandler(_RefreshQueueNewArea);
        wrappingDel = new GridPointMatrixWrappingChangedEventHandler(_GridPointMatrixWrappingChanged);
        matrixDisposingDel = new GridPointMatrixDisposingEventHandler(_GridPointMatrixDisposing);
    }

    private void Init()
    {
        SetEventDelegates();

        foreach (GridPointMatrix matrix in _matrixes)
            OnGridPointMatrixAdded(matrix);

        // discover the list of visible arrays
        _SetVisibleLayersArray();

        _allGridPointMatrixes.Add(this);
    }
    #endregion

    #region indexers
    public GridPointMatrix this[int i]
    {
        get
        {
            try { return _matrixes[i]; }
            catch { throw; }
        }
    }

    public GridPointMatrix this[string id]
    {
        get
        {
            try { return GetMatrixByID(id); }
            catch { throw; }
        }
    }
    #endregion

    #region enumerable code
    public IEnumerator GetEnumerator()
    {
        for (int i = 0; i < _matrixes.Count; i++)
        {
            yield return _matrixes[i];
        }
    }
    #endregion

    #region IDisposable Members
    public void Dispose()
    {
        GC.SuppressFinalize(this);

        _allGridPointMatrixes.Remove(this);

        if (Disposing != null)
            Disposing(new GridPointMatrixesDisposingEventArgs(this));

        // unsubscribe from events
        foreach (GridPointMatrix grid in _matrixes)
        {
            grid.FirstColRowChanged -= firstCRDel;
            grid.VisibleChanged -= visChgDel;
            grid.GridPointSizeChanged -= gridPtSzDel;
            grid.RefreshQueueAreaAdded -= refQueueDel;
            grid.WrappingChanged -= wrappingDel;
        }

        // cancel all subscriptions to this object
        GridPointMatrixAdded = null;
        GridPointMatrixRemoved = null;
        Disposing = null;
    }
    #endregion

    #region static methods
    public static GridPointMatrixes GetGridPointMatrixesByID(string id)
    {
        foreach (GridPointMatrixes matrixes in _allGridPointMatrixes)
        {
            if (matrixes.ID == id)
                return matrixes;
        }

        return null;
    }

    public static List<string> GetAllGridPointMatrixesIDs()
    {
        List<string> ret = new List<string>(_allGridPointMatrixes.Count);
        foreach (GridPointMatrixes matrixes in _allGridPointMatrixes)
            ret.Add(matrixes.ID);

        return ret;
    }

    public static ReadOnlyCollection<GridPointMatrixes> GetAllGridPointMatrixes()
    {
        return _allGridPointMatrixes.AsReadOnly();
    }

    public static void ClearAllGridPointMatrixes()
    {
        var tmp = new List<GridPointMatrixes>(_allGridPointMatrixes);
        foreach (var matrixes in tmp)
            matrixes.Dispose();
    }
    #endregion
}
