using Gondwana.Common.Enums;
using Gondwana.EventArgs;
using System.Collections.ObjectModel;

namespace Gondwana;

public static class VisibleSurfaces
{
    #region fields
    internal static List<VisibleSurfaceBase> _surfaces = new List<VisibleSurfaceBase>();
    private static Rectangle maxSurfaceSize = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
    internal static VisibleSurfacesInstance instance = new VisibleSurfacesInstance(0);
    #endregion

    #region public members
    public static int Count
    {
        get { return _surfaces.Count; }
    }

    public static ReadOnlyCollection<VisibleSurfaceBase> AllVisibleSurfaces
    {
        get { return _surfaces.AsReadOnly(); }
    }

    public static Rectangle MaxSurfaceSize
    {
        get { return maxSurfaceSize; }
    }

    public static double ForcedRefreshRate
    {
        get { return instance._refreshTimer; }
        set { instance.SetVisibleSurfaceRefreshTimer(value); }
    }
    #endregion

    #region public members
    public static void Add(VisibleSurfaceBase surface)
    {
        if (!_surfaces.Contains(surface))
        {
            _surfaces.Add(surface);
            CalcMaxSurfaceSize();
        }
    }

    public static void Remove(VisibleSurfaceBase surface)
    {
        if (_surfaces.Contains(surface))
        {
            _surfaces.Remove(surface);
            CalcMaxSurfaceSize();
        }
    }
    #endregion

    #region private methods
    private static void CalcMaxSurfaceSize()
    {
        maxSurfaceSize = new Rectangle();

        foreach (VisibleSurfaceBase surface in _surfaces)
        {
            maxSurfaceSize = Rectangle.Union(maxSurfaceSize,
                new Rectangle(0, 0, surface.Width, surface.Height));
        }

        // child Sprite creation dependent on VisibleSurface size, so recreate child Sprites
        Gondwana.Drawing.Sprites.Sprites.CreateChildSprites();
    }
    #endregion

    internal class VisibleSurfacesInstance
    {
        private Timers.Timer _timer;
        private TimerEventHandler _timerDel;

        internal double _refreshTimer;          // in seconds

        // constructor
        internal VisibleSurfacesInstance(double refreshTimer)
        {
            _timerDel = new TimerEventHandler(this.Timer_Tick);
            SetVisibleSurfaceRefreshTimer(refreshTimer);
        }

        // finalizer for clean up
        ~VisibleSurfacesInstance()
        {
            _timerDel = null;

            if (_refreshTimer > 0)
                _timer.Dispose();
        }

        internal void SetVisibleSurfaceRefreshTimer(double refreshTimer)
        {
            _refreshTimer = refreshTimer;

            if (_timer != null)
            {
                _timer.Dispose();
                _timer = null;
            }

            if (_refreshTimer > 0)
            {
                _timer = Timers.Timers.Add(TimerType.PostCycle, TimerCycles.Repeating, _refreshTimer);
                _timer.engineTimer = true;

                _timer.Tick += _timerDel;
            }
        }

        private void Timer_Tick(TimerEventArgs e)
        {
            foreach (VisibleSurfaceBase surface in VisibleSurfaces._surfaces)
                surface.RenderBackbuffer(false);
        }
    }
}
