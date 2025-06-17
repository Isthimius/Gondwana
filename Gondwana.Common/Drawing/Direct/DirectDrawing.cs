using Gondwana.Grid;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;

namespace Gondwana.Common.Drawing.Direct
{
    public abstract class DirectDrawing : IComparable<DirectDrawing>, IDisposable
    {
        #region static members
        internal static List<DirectDrawing> _instances = new List<DirectDrawing>();

        public static ReadOnlyCollection<DirectDrawing> Instances
        {
            get { return _instances.AsReadOnly(); }
        }

        public static void RenderAll()
        {
            _instances.Sort();

            foreach (DirectDrawing drawing in _instances)
            {
                if (drawing.Bounds.IntersectsWith(drawing.Surface.Buffer.DirtyRectangle))
                {
                    if (drawing._dirty)
                    {
                        drawing.Render();
                        drawing._dirty = false;
                    }
                }
            }
        }

        public static void Clear()
        {
            for (int i = 0; i < _instances.Count; i++)
                _instances[i].Dispose();
        }

        public static void Clear(string name)
        {
            DirectDrawing tmpDraw = GetDirectDrawing(name);

            if (tmpDraw != null)
                tmpDraw.Dispose();
        }

        public static List<DirectDrawing> AllDirectDrawings
        {
            get { return _instances; }
        }

        public static int Count
        {
            get { return _instances.Count; }
        }

        public static DirectDrawing GetDirectDrawing(string name)
        {
            foreach (DirectDrawing drawing in _instances)
            {
                if (drawing.Name == name)
                    return drawing;
            }

            return null;
        }
        #endregion

        #region private / protected fields
        protected VisibleSurfaceBase _surface;
        protected Rectangle _bounds;
        protected int _zOrder;
        private GridPointMatrixes _drawSource = null;
        private string _Name;
        internal DirectDrawing.Movement _movement;
        internal bool _dirty = true;
        #endregion

        #region abstract methods
        protected internal abstract void Render();
        #endregion

        #region constructors / finalizer
        public DirectDrawing(VisibleSurfaceBase surface, Rectangle bounds)
        {
            _instances.Add(this);

            _surface = surface;
            _bounds = bounds;
            _zOrder = 0;
            _Name = Guid.NewGuid().ToString();

            _drawSource = _surface.Buffer.DrawSource;

            ForceRefresh();
        }

        ~DirectDrawing()
        {
            Dispose();
        }
        #endregion

        #region public properties
        public VisibleSurfaceBase Surface
        {
            get { return _surface; }
        }

        public Rectangle Bounds
        {
            get { return _bounds; }
            set
            {
                ForceRefresh();
                _bounds = value;
                ForceRefresh();
            }
        }

        public int ZOrder
        {
            get { return _zOrder; }
            set
            {
                _zOrder = value;
                ForceRefresh();
            }
        }

        public string Name
        {
            get { return _Name; }
            set { _Name = value; }
        }

        public bool IsScrolling
        {
            get
            {
                if (_movement == null)
                    return false;
                else
                    return true;
            }
        }
        #endregion

        #region public / internal methods
        public void ScrollSourceGridPoint(double totalTime, Rectangle destBounds)
        {
            if (_movement != null)
                _movement.parent = null;

            _movement = new DirectDrawing.Movement(this, totalTime, destBounds);
        }

        public void StopScrolling()
        {
            if (_movement != null)
            {
                _movement.parent = null;
                _movement = null;
            }
        }

        public void MoveNext(long tick)
        {
            if (_movement != null)
            {
                if (_movement.MoveNext(tick))
                    _movement = null;
            }
        }

        public void ForceRefresh()
        {
            GridPointMatrixes matrixes = _surface.Buffer.DrawSource;

            if (matrixes != null && matrixes.Count != 0)
                matrixes[0].RefreshQueue.AddPixelRangeToRefreshQueue(_bounds, true);

            _dirty = true;
        }
        #endregion

        #region IComparable<DirectDrawing> Members
        public int CompareTo(DirectDrawing other)
        {
            if (this._zOrder < other._zOrder)
                return -1;
            else if (this._zOrder > other._zOrder)
                return 1;
            else
                return 0;
        }
        #endregion

        #region IDisposable Members
        public void Dispose()
        {
            GC.SuppressFinalize(this);
            _instances.Remove(this);
            ForceRefresh();
        }
        #endregion

        internal class Movement
        {
            internal DirectDrawing parent;
            internal long startTick;
            internal long lastTick;
            internal long totalTicks;
            internal Rectangle startBounds;
            internal Rectangle destBounds;

            internal Movement(DirectDrawing drawing, double totalTime, Rectangle dest)
            {
                parent = drawing;
                startTick = HighResTimer.GetCurrentTickCount();
                lastTick = startTick;
                totalTicks = (long)(totalTime * (double)HighResTimer.TicksPerSecond);
                startBounds = parent.Bounds;
                destBounds = dest;
            }

            internal bool IsFinished(long tick)
            {
                if (tick >= startTick + totalTicks)
                    return true;
                else
                    return false;
            }

            internal bool MoveNext(long tick)
            {
                bool finished = IsFinished(tick);

                if (finished)
                {
                    parent.Bounds = destBounds;
                    parent = null;
                }
                else
                {
                    double percentComplete = (double)(tick - startTick) / (double)totalTicks;

                    int newX = startBounds.X +
                        (int)((float)(destBounds.X - startBounds.X) * percentComplete);
                    int newY = startBounds.Y +
                        (int)((float)(destBounds.Y - startBounds.Y) * percentComplete);
                    int newWidth = startBounds.Width +
                        (int)((float)(destBounds.Width - startBounds.Width) * percentComplete);
                    int newHeight = startBounds.Height +
                        (int)((float)(destBounds.Height - startBounds.Height) * percentComplete);

                    parent.Bounds = new Rectangle(newX, newY, newWidth, newHeight);
                }

                return finished;
            }
        }
    }
}