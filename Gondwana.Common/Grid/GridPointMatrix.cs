using Gondwana.Common.Drawing;
using Gondwana.Common.Drawing.Sprites;
using Gondwana.Common.Enums;
using Gondwana.Common.EventArgs;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Runtime.Serialization;

namespace Gondwana.Common.Grid
{
    /// <summary>
    /// 
    /// </summary>
    [DataContract(IsReference = true)]
    public class GridPointMatrix : IEnumerable, ICloneable, IDisposable
    {
        #region events
        internal event RefreshQueueAreaAddedEventHandler RefreshQueueAreaAdded;
        
        public event GridPointSizeChangedEventHandler GridPointSizeChanged;
        public event VisibleChangedEventHandler VisibleChanged;
        public event SourceGridPointChangedEventHandler FirstColRowChanged;
        public event GridPointMatrixWrappingChangedEventHandler WrappingChanged;
        public event ShowGridLinesChangedEventHandler ShowGridLinesChanged;
        public event GridPointMatrixDisposingEventHandler Disposing;
        #endregion

        #region delegates
        private RefreshQueueAreaAddedEventHandler refQueueDel;
        #endregion

        #region static fields
        internal static List<GridPointMatrix> _allGridPointMatrix = new List<GridPointMatrix>();
        #endregion

        #region private / internal fields
        private string _id = Guid.NewGuid().ToString();

        private int _tileWidth;             // rendered width
        private int _tileHeight;            // rendered height
        private bool _visible;              // is matrix to be rendered; useful with multiple layers

        [DataMember]
        private GridPoint[][] _matrix;      // array of points; 2 dimensions (X, Y)
        private float _layerSyncModifier;   // 1 = default; <1 is slower, >1 is faster

        [DataMember]
        protected internal GridPointMatrixes _parent = null;

        internal bool _wrapHoriz = false;
        internal bool _wrapVerti = false;
        internal GridPointMatrixScrollBinding scrollBinding = null;

        // first pixel visible (i.e., source pixel for rendering calculations)
        private Point _gridPtZeroPxl;
        private PointF _firstGridPt = new PointF();
        
        internal RefreshQueue _refreshQueue;
        internal GridPointMatrix.Movement _movement;
        #endregion

        #region public fields
        [IgnoreDataMember]
        public object Tag;
        #endregion

        #region matrix wrapping delegates / variables
        private delegate GridPoint GetIndexer(int x, int y);
        private GetIndexer FindIndexedGridPoint;
        internal List<GridPoint> wrappedGridPts = new List<GridPoint>();
        #endregion

        #region constructors / finalizer
        public GridPointMatrix(int columnCount, int rowCount) :
            this(columnCount, rowCount, 0, 0, 1) { }

        public GridPointMatrix(int columnCount, int rowCount, int width, int height) :
            this(columnCount, rowCount, width, height, 1) { }

        public GridPointMatrix(int columnCount, int rowCount, int width, int height, float layerSyncModifier)
        {
            var pt = new GridPoint[columnCount][];

            for (int i = 0; i < pt.Length; i++)
                pt[i] = new GridPoint[rowCount];

            InitValues(pt, width, height, layerSyncModifier, true);
        }

        public GridPointMatrix(GridPoint[][] pt) :
            this(pt, 0, 0, 1) { }

        public GridPointMatrix(GridPoint[][] pt, int width, int height) :
            this(pt, width, height, 1) { }
        
        public GridPointMatrix(GridPoint[][] pt, int width, int height, float layerSyncModifier)
        {
            InitValues(pt, width, height, layerSyncModifier, true);
        }

        ~GridPointMatrix()
        {
            Dispose();
        }

        [OnDeserialized]
        private void OnDeserialized(StreamingContext context)
        {
            InitValues(_matrix, _tileWidth, _tileHeight, _layerSyncModifier, true);
        }
        #endregion

        #region properties
        [IgnoreDataMember]
        public IGridCoordinates CoordinateSystem { get; set; }

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
                    CoordinateSystem = (IGridCoordinates)handle.Unwrap();
                }
            }
        }

        [DataMember]
        public string ID
        {
            get { return _id; }
            protected internal set { _id = value; }
        }

        [IgnoreDataMember]
        public GridPointMatrixes Parent
        {
            get { return _parent; }
        }

        [IgnoreDataMember]
        public RefreshQueue RefreshQueue
        {
            get { return _refreshQueue; }
            protected internal set { _refreshQueue = value; }
        }

        [DataMember]
        public float LayerSyncModifier
        {
            get { return _layerSyncModifier; }
            set { _layerSyncModifier = value; }
        }

        [DataMember]
        public GridPointMatrixScrollBinding ScrollBinding
        {
            get { return scrollBinding; }
            private set { scrollBinding = value; }
        }

        [DataMember]
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

        [DataMember]
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

        [DataMember]
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

        [DataMember]
        public PointF SourceGridPoint
        {
            get { return _firstGridPt; }
            set { this.SetSourceGridPoint(value); }
        }

        [IgnoreDataMember]
        public GridPoint[][] GridPointArray
        {
            get { return _matrix; }
        }

        [IgnoreDataMember]
        public int GridColumnCount
        {
            get { return _matrix.GetUpperBound(0) + 1; }
        }

        [IgnoreDataMember]
        public int GridRowCount
        {
            get { return _matrix[0].GetUpperBound(0) + 1; }
        }

        [DataMember]
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

        [DataMember]
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

        [DataMember]
        private bool showGridLines;

        [IgnoreDataMember]
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

        [IgnoreDataMember]
        public Point GridPointZeroPixel
        {
            get { return _gridPtZeroPxl; }
        }

        [IgnoreDataMember]
        public bool IsScrolling
        {
            get { return _movement.IsScrolling; }
        }

        [IgnoreDataMember]
        public float VelocityX
        {
            get { return _movement.VelocityX; }
            set { _movement.VelocityX = value; }
        }

        [IgnoreDataMember]
        public float VelocityY
        {
            get { return _movement.VelocityY; }
            set { _movement.VelocityY = value; }
        }

        [IgnoreDataMember]
        public float AccelerationX
        {
            get { return _movement.AccelerationX; }
            set { _movement.AccelerationX = value; }
        }

        [IgnoreDataMember]
        public float AccelerationY
        {
            get { return _movement.AccelerationY; }
            set { _movement.AccelerationY = value; }
        }
        #endregion

        #region raise events
        protected virtual void OnGridPointSizeChanged(int oldWidth, int oldHeight, int newWidth, int newHeight)
        {
            if (GridPointSizeChanged != null)
            {
                GridPointSizeChangedEventArgs e;
                e = new GridPointSizeChangedEventArgs(this, oldWidth, oldHeight, newWidth, newHeight);
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

            // create new child Sprites for all Sprite instances on this grid
            Sprites.CreateChildSprites(this);
        }

        protected virtual void OnFirstColRowChanged(PointF oldPt, PointF newPt)
        {
            foreach (GridPointMatrixScrollBinding scrollBind in GridPointMatrixScrollBinding._allScrollBindings)
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

        internal virtual void OnRefreshQueueAreaAdded(RefreshQueueAreaAddedEventArgs e)
        {
            // just pass the event up
            if (RefreshQueueAreaAdded != null)
                RefreshQueueAreaAdded(e);
        }

        protected virtual void OnWrappingChanged(bool oldHoriz, bool newHoriz, bool oldVerti, bool newVerti)
        {
            if (WrappingChanged != null)
            {
                GridPointMatrixWrappingChangedEventArgs e =
                    new GridPointMatrixWrappingChangedEventArgs(this, oldHoriz, newHoriz, oldVerti, newVerti);
                WrappingChanged(e);
            }

            // set indexer delegate
            if (newHoriz || newVerti)
                FindIndexedGridPoint = new GetIndexer(GetIndexer_Wrap);
            else
                FindIndexedGridPoint = new GetIndexer(GetIndexer_NoWrap);

            // create new child Sprites for all Sprite instances on this grid
            Sprites.CreateChildSprites(this);
        }

        protected virtual void OnShowGridLinesChanged(bool oldVal, bool newVal)
        {
            this.Parent.RefreshNeeded = MatrixesRefreshType.All;

            if (ShowGridLinesChanged != null)
                ShowGridLinesChanged(new ShowGridLinesChangedEventArgs(this, oldVal, newVal));
        }
        #endregion

        #region public methods
        public void SetGridPointSize(int newWidth, int newHeight)
        {
            // capture before and after values and raise event here
            this.OnGridPointSizeChanged(_tileWidth, _tileHeight, newWidth, newHeight);

            _tileWidth = newWidth;
            _tileHeight = newHeight;
        }

        public GridPoint SetGridPoint(GridPoint gridPt, int x, int y)
        {
            this[x, y] = gridPt;
            return this[x, y];
        }

        public GridPoint SetGridPoint(int x, int y, Frame frame)
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
            _gridPtZeroPxl = CoordinateSystem.GetSrcPxlAtGridPt(this, new PointF(0, 0));

            // capture the before and after values and raise event
            OnFirstColRowChanged(oldSrcPt, SourceGridPoint);
        }

        public void BindScrollingToParentGrid(GridPointMatrix parent)
        {
            BindScrollingToParentGrid(parent, parent.SourceGridPoint);
        }

        public void BindScrollingToParentGrid(GridPointMatrix parent, PointF parentAnchor)
        {
            BindScrollingToParentGrid(parent, parentAnchor, this.SourceGridPoint);
        }

        public void BindScrollingToParentGrid(GridPointMatrix parent, PointF parentAnchor, PointF thisAnchor)
        {
            // remove any previous binding
            UnbindScrolling();

            // create new binding instance
            scrollBinding = new GridPointMatrixScrollBinding();
            scrollBinding.ParentGrid = parent;
            scrollBinding.ChildGrid = this;
            scrollBinding.ParentAnchorGridPoint = parentAnchor;
            scrollBinding.ChildAnchorGridPoint = thisAnchor;
        }

        public void UnbindScrolling()
        {
            if (scrollBinding != null)
            {
                GridPointMatrixScrollBinding._allScrollBindings.Remove(scrollBinding);

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
        #endregion

        #region private / internal methods
        private void SaveGridCoordinatesToGridPoints()
        {
            // let each GridPoint in array know its position in the array
            for (int X = 0; X <= _matrix.GetUpperBound(0); X++)
            {
                for (int Y = 0; Y <= _matrix[X].GetUpperBound(0); Y++)
                {
                    _matrix[X][Y] = new GridPoint(this);
                    _matrix[X][Y].gridCoordinates = new Point(X, Y);
                    _matrix[X][Y].DoNotRedrawChanges = false;
                }
            }
        }

        private void RefreshQueueNewTile(RefreshQueueAreaAddedEventArgs e)
        {
            // pass the event up to any containing GridPointMatrixes
            OnRefreshQueueAreaAdded(e);
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

        protected void InitValues(GridPoint[][] pt, int width, int height, float layerSyncModifier, bool addToInstances)
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
            refQueueDel = new RefreshQueueAreaAddedEventHandler(RefreshQueueNewTile);
            RefreshQueue.RefreshQueueAreaAdded += refQueueDel;
            FindIndexedGridPoint = new GetIndexer(GetIndexer_NoWrap);
            _movement = new Movement(this);

            if (addToInstances)
                _allGridPointMatrix.Add(this);
        }
        #endregion

        #region indexers
        public GridPoint this[int x, int y]
        {
            get { return FindIndexedGridPoint(x, y); }
            set
            {
                PointF actualGridPoint =
                    CoordinateSystem.FindEquivGridCoord(new PointF((float)x, (float)y), _matrix.GetUpperBound(0), _matrix[x].GetUpperBound(0));

                _matrix[(int)actualGridPoint.X][(int)actualGridPoint.Y] = value;
            }
        }

        public GridPoint this[Point pt]
        {
            get { return this[pt.X, pt.Y]; }
            set { this[pt.X, pt.Y] = value; }
        }

        public GridPoint this[PointF ptF]
        {
            get { return this[(int)ptF.X, (int)ptF.Y]; }
            set { this[(int)ptF.X, (int)ptF.Y] = value; }
        }

        private GridPoint GetIndexer_NoWrap(int x, int y)
        {
            if (x > _matrix.GetUpperBound(0)
                || y > _matrix[0].GetUpperBound(0)
                || x < 0
                || y < 0)
                return null;
            else
                return _matrix[x][y];
        }

        private GridPoint GetIndexer_Wrap(int x, int y)
        {
            // if not wrapping horizontally and outside of x bound range, return null
            if ((!_wrapHoriz) && ((x > _matrix.GetUpperBound(0)) || (x < 0)))
                return null;

            // if not wrapping vertically and outside of y bound range, return null
            if ((!_wrapVerti) && ((y > _matrix[x].GetUpperBound(0)) || (y < 0)))
                return null;

            // check "non-wrapping" coordinates
            GridPoint newGridPoint = GetIndexer_NoWrap(x, y);

            // if outside of "non-wrapping" coordinates, find the equivalent point
            if (newGridPoint == null)
            {
                // find the coordinated of the GridPoint being "wrapped"
                PointF actualGridPoint =
                    CoordinateSystem.FindEquivGridCoord(new PointF((float)x, (float)y), _matrix.GetUpperBound(0), _matrix[x].GetUpperBound(0));

                // capture GridPoint if x-y coord already exists in wrappedGridPts
                foreach (GridPoint pt in wrappedGridPts)
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
                    newGridPoint = new GridPoint(_matrix[(int)actualGridPoint.X][(int)actualGridPoint.Y],
                        new Point(x, y));
                    wrappedGridPts.Add(newGridPoint);
                }
            }

            return newGridPoint;
        }
        #endregion

        #region IEnumerable Members
        public IEnumerator GetEnumerator()
        {
            for (int x = 0; x <= _matrix.GetUpperBound(0); x++)
            {
                for (int y = 0; y <= _matrix[x].GetUpperBound(0); y++)
                {
                    yield return _matrix[x][y];
                }
            }
        }
        #endregion

        #region ICloneable Members
        public object Clone()
        {
            return new GridPointMatrix(_matrix, _tileWidth, _tileHeight, _layerSyncModifier);
        }
        #endregion

        #region IDisposable Members
        public void Dispose()
        {
            GC.SuppressFinalize(this);

            _allGridPointMatrix.Remove(this);

            if (Disposing != null)
                Disposing(new GridPointMatrixDisposingEventArgs(this));

            // remove any scroll bindings
            UnbindScrolling();

            // unsubscribe from events
            RefreshQueue.RefreshQueueAreaAdded -= refQueueDel;

            // dispose child objects
            RefreshQueue.Dispose();

            foreach (GridPoint gridPt in this)
                gridPt.Dispose();

            // cancel all subscriptions to this object
            GridPointSizeChanged = null;
            VisibleChanged = null;
            FirstColRowChanged = null;
            RefreshQueueAreaAdded = null;
            WrappingChanged = null;
            Disposing = null;
        }
        #endregion

        #region static methods
        public static GridPointMatrix GetGridPointMatrixByID(string id)
        {
            foreach (GridPointMatrix matrix in _allGridPointMatrix)
            {
                if (matrix.ID == id)
                    return matrix;
            }

            // if made it this far, Guid was not found
            return null;
        }

        public static List<string> GetAllGridPointMatrixIDs()
        {
            List<string> ret = new List<string>(_allGridPointMatrix.Count);
            foreach (GridPointMatrix matrix in _allGridPointMatrix)
                ret.Add(matrix.ID);

            return ret;
        }

        public static ReadOnlyCollection<GridPointMatrix> GetAllGridPointMatrix()
        {
            return _allGridPointMatrix.AsReadOnly();
        }

        public static void ClearAllGridPointMatrix()
        {
            var tmp = new List<GridPointMatrix>(_allGridPointMatrix);
            foreach (var matrix in tmp)
                matrix.Dispose();
        }
        #endregion

        internal class Movement
        {
            internal GridPointMatrix parent;

            internal long startTick;
            internal long lastTick;
            internal long totalTicks;
            internal PointF startCoord;
            internal PointF destCoord;

            #region ctor
            internal Movement(GridPointMatrix matrix)
            {
                parent = matrix;
                IsScrolling = false;
            }
            #endregion

            #region properties
            private float _velocityX;
            internal float VelocityX
            {
                get { return _velocityX; }
                set
                {
                    var oldVelocityY = _velocityY;
                    Stop();
                    lastTick = HighResTimer.GetCurrentTickCount();
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
                    lastTick = HighResTimer.GetCurrentTickCount();
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
                    lastTick = HighResTimer.GetCurrentTickCount();
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
                    lastTick = HighResTimer.GetCurrentTickCount();
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
            #endregion

            #region methods
            internal void Start(double totalTime, PointF dest)
            {
                Stop();

                startTick = HighResTimer.GetCurrentTickCount();
                //lastTick = startTick;
                totalTicks = (long)(totalTime * HighResTimer.TicksPerSecond);
                startCoord = parent.SourceGridPoint;
                destCoord = dest;

                IsScrolling = true;
            }

            internal void Next(long tick)
            {
                foreach (GridPointMatrixes matrixes in GridPointMatrixes._allGridPointMatrixes)
                {
                    if (matrixes.GetMatrixByID(parent._id) != null)
                        matrixes.refreshNeeded = MatrixesRefreshType.All;
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
            #endregion
        }
    }
}
