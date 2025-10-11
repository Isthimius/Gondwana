using System.Collections;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Runtime.Serialization;
using Gondwana.Drawing;
using Gondwana.Drawing.Coordinates;
using Gondwana.Rendering;
using Gondwana.Scenes.EventArgs;
using Gondwana.Timers;
using Newtonsoft.Json;

namespace Gondwana.Scenes;

/// <summary>
///
/// </summary>
[JsonObject(IsReference = true)]
public class SceneLayer : IEnumerable<SceneLayerPoint>, IDisposable
{
    #region events

    internal event EventHandler<RefreshQueueAreaAddedEventArgs> RefreshQueueAreaAdded;

    public event GridPointSizeChangedEventHandler GridPointSizeChanged;

    public event VisibleChangedEventHandler VisibleChanged;

    public event SourceGridPointChangedEventHandler FirstColRowChanged;

    public event SceneLayerWrappingChangedEventHandler WrappingChanged;

    public event ShowGridLinesChangedEventHandler ShowGridLinesChanged;

    public event SceneLayerDisposingEventHandler Disposing;

    #endregion events

    #region delegates

    private EventHandler<RefreshQueueAreaAddedEventArgs> refQueueDel;

    #endregion delegates

    #region static fields

    internal static List<SceneLayer> _allSceneLayer = new List<SceneLayer>();

    #endregion static fields

    #region private / internal fields

    private string _id = Guid.NewGuid().ToString();

    private int _tileWidth;             // rendered width
    private int _tileHeight;            // rendered height
    private bool _visible;              // is matrix to be rendered; useful with multiple layers

    [JsonProperty]
    private SceneLayerPoint[][] _matrix;      // array of points; 2 dimensions (X, Y)

    private float _layerSyncModifier;   // 1 = default; <1 is slower, >1 is faster

    internal bool _wrapHoriz = false;
    internal bool _wrapVerti = false;
    internal SceneLayerScrollBinding scrollBinding = null;

    // first pixel visible (i.e., source pixel for rendering calculations)
    private Point _gridPtZeroPxl;

    private PointF _firstGridPt = new PointF();

    internal SceneLayer.Movement _movement;

    #endregion private / internal fields

    #region public fields

    [JsonIgnore]
    public object Tag;

    #endregion public fields

    #region matrix wrapping delegates / variables

    private delegate SceneLayerPoint GetIndexer(int x, int y);

    private GetIndexer FindIndexedGridPoint;

    // TODO: remove this; wrapping should be handled via rendering, not by creating new GridPoints
    internal List<SceneLayerPoint> wrappedGridPts = new List<SceneLayerPoint>();

    #endregion matrix wrapping delegates / variables

    #region constructors / finalizer

    public SceneLayer(int columnCount, int rowCount) :
        this(columnCount, rowCount, 0, 0, 1)
    { }

    public SceneLayer(int columnCount, int rowCount, int width, int height) :
        this(columnCount, rowCount, width, height, 1)
    { }

    public SceneLayer(int columnCount, int rowCount, int width, int height, float layerSyncModifier)
    {
        var pt = new SceneLayerPoint[columnCount][];

        for (int i = 0; i < pt.Length; i++)
            pt[i] = new SceneLayerPoint[rowCount];

        InitValues(pt, width, height, layerSyncModifier, true);
    }

    public SceneLayer(SceneLayerPoint[][] pt) :
        this(pt, 0, 0, 1)
    { }

    public SceneLayer(SceneLayerPoint[][] pt, int width, int height) :
        this(pt, width, height, 1)
    { }

    public SceneLayer(SceneLayerPoint[][] pt, int width, int height, float layerSyncModifier)
    {
        InitValues(pt, width, height, layerSyncModifier, true);
    }

    ~SceneLayer()
    {
        Dispose();
    }

    [OnDeserialized]
    private void OnDeserialized(StreamingContext context)
    {
        InitValues(_matrix, _tileWidth, _tileHeight, _layerSyncModifier, true);
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
                CoordinateSystem = null;
            else
            {
                var values = value.Split(';');
                var handle = Activator.CreateInstance(values[0], values[1]);
                CoordinateSystem = (ISceneLayerCoordinates)handle.Unwrap();
            }
        }
    }

    [JsonProperty]
    public string ID
    {
        get { return _id; }
        protected internal set { _id = value; }
    }

    [JsonProperty]
    public Scene? Parent { get; internal set; }

    [JsonIgnore]
    internal RefreshQueue RefreshQueue { get; set; }

    [JsonProperty]
    public float LayerSyncModifier
    {
        get { return _layerSyncModifier; }
        set { _layerSyncModifier = value; }
    }

    [JsonProperty]
    public SceneLayerScrollBinding ScrollBinding
    {
        get { return scrollBinding; }
        private set { scrollBinding = value; }
    }

    [JsonProperty]
    public int GridPointHeight
    {
        get { return _tileHeight; }
        set
        {
            // capture before and after values and raise event here
            this.OnGridPointSizeChanged(_tileWidth, _tileHeight, _tileWidth, value);

            _tileHeight = value;
        }
    }

    [JsonProperty]
    public int GridPointWidth
    {
        get { return _tileWidth; }
        set
        {
            // capture before and after values and raise event here
            this.OnGridPointSizeChanged(_tileWidth, _tileHeight, value, _tileHeight);

            _tileWidth = value;
        }
    }

    [JsonProperty]
    public bool Visible
    {
        get { return _visible; }
        set
        {
            // capture before and after values and raise event here
            bool oldVal = _visible;
            bool newVal = value;
            _visible = value;
            this.OnVisibleChanged(oldVal, newVal);
        }
    }

    [JsonProperty]
    public PointF SourceGridPoint
    {
        get { return _firstGridPt; }
        set { this.SetSourceGridPoint(value); }
    }

    [JsonIgnore]
    public SceneLayerPoint[][] GridPointArray
    {
        get { return _matrix; }
    }

    [JsonIgnore]
    public int GridColumnCount
    {
        get { return _matrix.GetUpperBound(0) + 1; }
    }

    [JsonIgnore]
    public int GridRowCount
    {
        get { return _matrix[0].GetUpperBound(0) + 1; }
    }

    [JsonProperty]
    public bool WrapHorizontally
    {
        get { return _wrapHoriz; }
        set
        {
            bool oldH = _wrapHoriz;
            bool newH = value;
            bool oldV = _wrapVerti;
            bool newV = _wrapVerti;

            _wrapHoriz = value;
            OnWrappingChanged(oldH, newH, oldV, newV);
        }
    }

    [JsonProperty]
    public bool WrapVertically
    {
        get { return _wrapVerti; }
        set
        {
            bool oldH = _wrapHoriz;
            bool newH = _wrapHoriz;
            bool oldV = _wrapVerti;
            bool newV = value;

            _wrapVerti = value;
            OnWrappingChanged(oldH, newH, oldV, newV);
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
            bool oldVal = showGridLines;
            showGridLines = value;
            OnShowGridLinesChanged(oldVal, value);
        }
    }

    [JsonIgnore]
    public Point GridPointZeroPixel
    {
        get { return _gridPtZeroPxl; }
    }

    [JsonIgnore]
    public bool IsScrolling
    {
        get { return _movement.IsScrolling; }
    }

    [JsonIgnore]
    public float VelocityX
    {
        get { return _movement.VelocityX; }
        set { _movement.VelocityX = value; }
    }

    [JsonIgnore]
    public float VelocityY
    {
        get { return _movement.VelocityY; }
        set { _movement.VelocityY = value; }
    }

    [JsonIgnore]
    public float AccelerationX
    {
        get { return _movement.AccelerationX; }
        set { _movement.AccelerationX = value; }
    }

    [JsonIgnore]
    public float AccelerationY
    {
        get { return _movement.AccelerationY; }
        set { _movement.AccelerationY = value; }
    }

    #endregion properties

    #region raise events

    protected virtual void OnGridPointSizeChanged(int oldWidth, int oldHeight, int newWidth, int newHeight)
    {
        if (GridPointSizeChanged != null)
        {
            SceneLayerPointSizeChangedEventArgs e;
            e = new SceneLayerPointSizeChangedEventArgs(this, oldWidth, oldHeight, newWidth, newHeight);
            GridPointSizeChanged(e);
        }
    }

    protected virtual void OnVisibleChanged(bool oldValue, bool newValue)
    {
        if (VisibleChanged != null)
        {
            VisibleChangedEventArgs e = new VisibleChangedEventArgs(this, oldValue, newValue);
            VisibleChanged(e);
        }
    }

    protected virtual void OnFirstColRowChanged(PointF oldPt, PointF newPt)
    {
        foreach (SceneLayerScrollBinding scrollBind in SceneLayerScrollBinding._allScrollBindings)
        {
            if (scrollBind.ParentGrid == this)
            {
                scrollBind.ChildGrid.ScrollWithParent();
            }
        }

        if (FirstColRowChanged != null)
        {
            SourceGridPointChangedEventArgs e = new SourceGridPointChangedEventArgs(this, oldPt, newPt);
            FirstColRowChanged(e);
        }
    }

    internal virtual void OnRefreshQueueAreaAdded(object? sender, RefreshQueueAreaAddedEventArgs e)
    {
        // just pass the event up
        RefreshQueueAreaAdded?.Invoke(this, e);
    }

    private void RefreshQueueNewTile(object? sender, RefreshQueueAreaAddedEventArgs e)
    {
        // pass the event up to any containing SceneLayeres
        OnRefreshQueueAreaAdded(sender, e);
    }

    protected virtual void OnWrappingChanged(bool oldHoriz, bool newHoriz, bool oldVerti, bool newVerti)
    {
        if (WrappingChanged != null)
        {
            SceneLayerWrappingChangedEventArgs e =
                new SceneLayerWrappingChangedEventArgs(this, oldHoriz, newHoriz, oldVerti, newVerti);
            WrappingChanged(e);
        }

        // set indexer delegate
        if (newHoriz || newVerti)
            FindIndexedGridPoint = new GetIndexer(GetIndexer_Wrap);
        else
            FindIndexedGridPoint = new GetIndexer(GetIndexer_NoWrap);
    }

    protected virtual void OnShowGridLinesChanged(bool oldVal, bool newVal)
    {
        this.Parent.RefreshNeeded = SceneRefreshType.All;

        if (ShowGridLinesChanged != null)
            ShowGridLinesChanged(new ShowGridLinesChangedEventArgs(this, oldVal, newVal));
    }

    #endregion raise events

    #region public methods

    public void SetGridPointSize(int newWidth, int newHeight)
    {
        // capture before and after values and raise event here
        this.OnGridPointSizeChanged(_tileWidth, _tileHeight, newWidth, newHeight);

        _tileWidth = newWidth;
        _tileHeight = newHeight;
    }

    public SceneLayerPoint SetGridPoint(SceneLayerPoint gridPt, int x, int y)
    {
        this[x, y] = gridPt;
        return this[x, y];
    }

    public SceneLayerPoint SetGridPoint(int x, int y, Frame frame)
    {
        this[x, y].CurrentFrame = frame;
        return this[x, y];
    }

    public void SetSourceGridPoint(float firstCol, float firstRow)
    {
        PointF newPt = new PointF(firstCol, firstRow);
        SetSourceGridPoint(newPt);
    }

    public void SetSourceGridPoint(PointF srcGridPt)
    {
        // capture the existing / old source pixel before changes made
        PointF oldSrcPt = SourceGridPoint;
        _firstGridPt = srcGridPt;

        // update the first pixel position; the final SourceGridPoint might be slightly
        // different to srcGridPt due to rounding if srcGridPoint is not a whole number
        _gridPtZeroPxl = CoordinateSystem.GetSrcPixelAtLayerPoint(this, new PointF(0, 0));

        // capture the before and after values and raise event
        OnFirstColRowChanged(oldSrcPt, SourceGridPoint);
    }

    public void BindScrollingToParentGrid(SceneLayer parent)
    {
        BindScrollingToParentGrid(parent, parent.SourceGridPoint);
    }

    public void BindScrollingToParentGrid(SceneLayer parent, PointF parentAnchor)
    {
        BindScrollingToParentGrid(parent, parentAnchor, this.SourceGridPoint);
    }

    public void BindScrollingToParentGrid(SceneLayer parent, PointF parentAnchor, PointF thisAnchor)
    {
        // remove any previous binding
        UnbindScrolling();

        // create new binding instance
        scrollBinding = new SceneLayerScrollBinding();
        scrollBinding.ParentGrid = parent;
        scrollBinding.ChildGrid = this;
        scrollBinding.ParentAnchorGridPoint = parentAnchor;
        scrollBinding.ChildAnchorGridPoint = thisAnchor;
    }

    public void UnbindScrolling()
    {
        if (scrollBinding != null)
        {
            SceneLayerScrollBinding._allScrollBindings.Remove(scrollBinding);

            if (scrollBinding.ParentGrid != null)
                this.scrollBinding.ParentGrid = null;

            scrollBinding = null;
        }
    }

    public void ScrollSourceGridPoint(double totalTime, PointF destCoord)
    {
        _movement.Start(totalTime, destCoord);
    }

    public void StopScrolling()
    {
        _movement.Stop();
    }

    public void MoveNext(long tick)
    {
        if (_movement.IsScrolling)
            _movement.Next(tick);

        _movement.lastTick = tick;
    }

    #endregion public methods

    #region private / internal methods

    private void SaveGridCoordinatesToGridPoints()
    {
        // let each GridPoint in array know its position in the array
        for (int X = 0; X <= _matrix.GetUpperBound(0); X++)
        {
            for (int Y = 0; Y <= _matrix[X].GetUpperBound(0); Y++)
            {
                _matrix[X][Y] = new SceneLayerPoint(this);
                _matrix[X][Y].gridCoordinates = new Point(X, Y);
            }
        }
    }

    private void ScrollWithParent()
    {
        PointF parentSrc = scrollBinding.ParentGrid.SourceGridPoint;

        // find difference between anchor and current point with Parent
        float parentDifX = parentSrc.X - scrollBinding.ParentAnchorGridPoint.X;
        float parentDifY = parentSrc.Y - scrollBinding.ParentAnchorGridPoint.Y;

        // apply SynchLayerModifiers to the parent offset from anchor
        float netModifier = scrollBinding.ChildGrid._layerSyncModifier /
            scrollBinding.ParentGrid._layerSyncModifier;

        parentDifX *= netModifier;
        parentDifY *= netModifier;

        // apply the parent offset with modifier to the child
        float childDifX = scrollBinding.ChildAnchorGridPoint.X + parentDifX;
        float childDifY = scrollBinding.ChildAnchorGridPoint.Y + parentDifY;

        //scrollBinding.ChildGrid._gridPtZeroPxl = new Point((int)childDifX, (int)childDifY);
        scrollBinding.ChildGrid.SetSourceGridPoint(childDifX, childDifY);
    }

    protected void InitValues(SceneLayerPoint[][] pt, int width, int height, float layerSyncModifier, bool addToInstances)
    {
        _matrix = pt;
        _layerSyncModifier = layerSyncModifier;
        _tileWidth = width;
        _tileHeight = height;
        _visible = true;
        _gridPtZeroPxl = new Point(0, 0);
        // let each GridPoint in array know its position in the array
        SaveGridCoordinatesToGridPoints();
        RefreshQueue = new RefreshQueue(this);
        refQueueDel = RefreshQueueNewTile;
        RefreshQueue.RefreshQueueAreaAdded += refQueueDel;
        FindIndexedGridPoint = new GetIndexer(GetIndexer_NoWrap);
        _movement = new Movement(this);

        if (addToInstances)
            _allSceneLayer.Add(this);
    }

    #endregion private / internal methods

    #region indexers

    public SceneLayerPoint this[int x, int y]
    {
        get { return FindIndexedGridPoint(x, y); }
        set
        {
            PointF actualGridPoint =
                CoordinateSystem.FindEquivalentLayerPoint(new PointF((float)x, (float)y), _matrix.GetUpperBound(0), _matrix[x].GetUpperBound(0));

            _matrix[(int)actualGridPoint.X][(int)actualGridPoint.Y] = value;
        }
    }

    public SceneLayerPoint this[Point pt]
    {
        get { return this[pt.X, pt.Y]; }
        set { this[pt.X, pt.Y] = value; }
    }

    public SceneLayerPoint this[PointF ptF]
    {
        get { return this[(int)ptF.X, (int)ptF.Y]; }
        set { this[(int)ptF.X, (int)ptF.Y] = value; }
    }

    private SceneLayerPoint GetIndexer_NoWrap(int x, int y)
    {
        if (x > _matrix.GetUpperBound(0)
            || y > _matrix[0].GetUpperBound(0)
            || x < 0
            || y < 0)
            return null;
        else
            return _matrix[x][y];
    }

    private SceneLayerPoint GetIndexer_Wrap(int x, int y)
    {
        // if not wrapping horizontally and outside of x bound range, return null
        if ((!_wrapHoriz) && ((x > _matrix.GetUpperBound(0)) || (x < 0)))
            return null;

        // if not wrapping vertically and outside of y bound range, return null
        if ((!_wrapVerti) && ((y > _matrix[x].GetUpperBound(0)) || (y < 0)))
            return null;

        // check "non-wrapping" coordinates
        SceneLayerPoint newGridPoint = GetIndexer_NoWrap(x, y);

        // if outside of "non-wrapping" coordinates, find the equivalent point
        if (newGridPoint == null)
        {
            // find the coordinated of the GridPoint being "wrapped"
            PointF actualGridPoint =
                CoordinateSystem.FindEquivalentLayerPoint(new PointF((float)x, (float)y), _matrix.GetUpperBound(0), _matrix[x].GetUpperBound(0));

            // capture GridPoint if x-y coord already exists in wrappedGridPts
            foreach (SceneLayerPoint pt in wrappedGridPts)
            {
                if ((pt.gridCoordinates.X == x) && (pt.gridCoordinates.Y == y))
                {
                    newGridPoint = pt;
                    break;
                }
            }

            // if not already found, create and add to wrappedGridPts, and associate with "parent"
            if (newGridPoint == null)
            {
                newGridPoint = new SceneLayerPoint(_matrix[(int)actualGridPoint.X][(int)actualGridPoint.Y],
                    new Point(x, y));

                wrappedGridPts.Add(newGridPoint);
            }
        }

        return newGridPoint;
    }

    #endregion indexers

    #region IEnumerable Members

    IEnumerator<SceneLayerPoint> IEnumerable<SceneLayerPoint>.GetEnumerator()
    {
        for (int x = 0; x <= _matrix.GetUpperBound(0); x++)
        {
            for (int y = 0; y <= _matrix[x].GetUpperBound(0); y++)
            {
                yield return _matrix[x][y];
            }
        }
    }

    #endregion IEnumerable Members

    #region IDisposable Members

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        _allSceneLayer.Remove(this);

        if (Disposing != null)
            Disposing(new SceneLayerDisposingEventArgs(this));

        // remove any scroll bindings
        UnbindScrolling();

        // unsubscribe from events
        RefreshQueue.RefreshQueueAreaAdded -= refQueueDel;

        // dispose child objects
        RefreshQueue.Dispose();

        foreach (SceneLayerPoint gridPt in this)
            gridPt.Dispose();

        // cancel all subscriptions to this object
        GridPointSizeChanged = null;
        VisibleChanged = null;
        FirstColRowChanged = null;
        RefreshQueueAreaAdded = null;
        WrappingChanged = null;
        Disposing = null;
    }

    #endregion IDisposable Members

    #region static methods

    public static ReadOnlyCollection<SceneLayer> GetAllSceneLayers()
    {
        return _allSceneLayer.AsReadOnly();
    }

    public static void ClearAllSceneLayer()
    {
        var tmp = new List<SceneLayer>(_allSceneLayer);
        foreach (var matrix in tmp)
            matrix.Dispose();
    }

    public IEnumerator GetEnumerator()
    {
        throw new NotImplementedException();
    }

    #endregion static methods

    internal class Movement
    {
        internal SceneLayer parent;

        internal long startTick;
        internal long lastTick;
        internal long totalTicks;
        internal PointF startCoord;
        internal PointF destCoord;

        #region ctor

        internal Movement(SceneLayer matrix)
        {
            parent = matrix;
            IsScrolling = false;
        }

        #endregion ctor

        #region properties

        private float _velocityX;

        internal float VelocityX
        {
            get { return _velocityX; }
            set
            {
                var oldVelocityY = _velocityY;
                Stop();
                lastTick = HighResTimer.GetCurrentTick();
                _velocityX = value;
                _velocityY = oldVelocityY;
                IsScrolling = (_velocityX != 0 || _velocityY != 0 || _accelerationX != 0 || _accelerationY != 0);
            }
        }

        private float _velocityY;

        internal float VelocityY
        {
            get { return _velocityY; }
            set
            {
                var oldVelocityX = _velocityX;
                Stop();
                lastTick = HighResTimer.GetCurrentTick();
                _velocityX = oldVelocityX;
                _velocityY = value;
                IsScrolling = (_velocityX != 0 || _velocityY != 0 || _accelerationX != 0 || _accelerationY != 0);
            }
        }

        internal bool IsScrolling { get; set; }

        private float _accelerationX;

        internal float AccelerationX
        {
            get { return _accelerationX; }
            set
            {
                lastTick = HighResTimer.GetCurrentTick();
                _accelerationX = value;
                IsScrolling = (_velocityX != 0 || _velocityY != 0 || _accelerationX != 0 || _accelerationY != 0);
            }
        }

        private float _accelerationY;

        internal float AccelerationY
        {
            get { return _accelerationY; }
            set
            {
                lastTick = HighResTimer.GetCurrentTick();
                _accelerationY = value;
                IsScrolling = (_velocityX != 0 || _velocityY != 0 || _accelerationX != 0 || _accelerationY != 0);
            }
        }

        private float _terminalVelocityXMin = float.MinValue;

        public float TerminalVelocityXMin
        {
            get { return _terminalVelocityXMin; }
            set
            {
                _terminalVelocityXMin = value;
                LimitVelocityXByTerminal();
            }
        }

        private float _terminalVelocityXMax = float.MaxValue;

        public float TerminalVelocityXMax
        {
            get { return _terminalVelocityXMax; }
            set
            {
                _terminalVelocityXMax = value;
                LimitVelocityXByTerminal();
            }
        }

        private float _terminalVelocityYMin = float.MinValue;

        public float TerminalVelocityYMin
        {
            get { return _terminalVelocityYMin; }
            set
            {
                _terminalVelocityYMin = value;
                LimitVelocityYByTerminal();
            }
        }

        private float _terminalVelocityYMax = float.MaxValue;

        public float TerminalVelocityYMax
        {
            get { return _terminalVelocityYMax; }
            set
            {
                _terminalVelocityYMax = value;
                LimitVelocityYByTerminal();
            }
        }

        #endregion properties

        #region methods

        internal void Start(double totalTime, PointF dest)
        {
            Stop();

            startTick = HighResTimer.GetCurrentTick();
            //lastTick = startTick;
            totalTicks = (long)(totalTime * HighResTimer.TicksPerSecond);
            startCoord = parent.SourceGridPoint;
            destCoord = dest;

            IsScrolling = true;
        }

        internal void Next(long tick)
        {
            foreach (Scene matrixes in Scene._allSceneLayeres)
            {
                if (matrixes.GetMatrixByID(parent._id) != null)
                    matrixes.refreshNeeded = SceneRefreshType.All;
            }

            if (VelocityX != 0 || VelocityY != 0)
                NextVelocity(tick);
            else
                NextDestination(tick);
        }

        private void NextDestination(long tick)
        {
            if (tick >= startTick + totalTicks)
            {
                parent.SetSourceGridPoint(destCoord);
                Stop();
            }
            else
            {
                float percentComplete = (float)(tick - startTick) / (float)totalTicks;
                float newX = startCoord.X + ((float)(destCoord.X - startCoord.X) * percentComplete);
                float newY = startCoord.Y + ((float)(destCoord.Y - startCoord.Y) * percentComplete);

                parent.SetSourceGridPoint(new PointF(newX, newY));
            }

            return;
        }

        private void NextVelocity(long tick)
        {
            double secondsElapsed = (double)(tick - lastTick) / (double)HighResTimer.TicksPerSecond;

            // adjust velocity if acceleration is not 0
            if (AccelerationX != 0)
            {
                _velocityX += (float)(AccelerationX * secondsElapsed);
                LimitVelocityXByTerminal();
            }

            if (AccelerationY != 0)
            {
                _velocityY += (float)(AccelerationY * secondsElapsed);
                LimitVelocityYByTerminal();
            }

            float newX = parent.SourceGridPoint.X + (float)((double)VelocityX * secondsElapsed);
            float newY = parent.SourceGridPoint.Y + (float)((double)VelocityY * secondsElapsed);

            parent.SetSourceGridPoint(new PointF(newX, newY));
            //lastTick = tick;

            return;
        }

        internal void Stop()
        {
            _velocityX = 0;
            _velocityY = 0;
            _accelerationX = 0;
            _accelerationY = 0;
            startTick = 0;
            lastTick = 0;
            totalTicks = 0;

            IsScrolling = false;
        }

        private void LimitVelocityXByTerminal()
        {
            if (_velocityX < TerminalVelocityXMin)
                _velocityX = TerminalVelocityXMin;

            if (_velocityX > TerminalVelocityXMax)
                _velocityX = TerminalVelocityXMax;
        }

        private void LimitVelocityYByTerminal()
        {
            if (_velocityY < TerminalVelocityYMin)
                _velocityY = TerminalVelocityYMin;

            if (_velocityY > TerminalVelocityYMax)
                _velocityY = TerminalVelocityYMax;
        }

        #endregion methods
    }
}