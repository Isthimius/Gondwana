using Gondwana.Timers;
using Timer = Gondwana.Timers.Timer;

namespace Gondwana.Rendering;

internal sealed class VisibleSurfacesInstance
{
    private Timer? _timer;
    private readonly TimerEventHandler _tickHandler;

    public double RefreshRate { get; private set; }

    public VisibleSurfacesInstance(double refreshRate)
    {
        _tickHandler = Timer_Tick;
        SetVisibleSurfaceRefreshTimer(refreshRate);
    }

    public void SetVisibleSurfaceRefreshTimer(double refreshRate)
    {
        RefreshRate = refreshRate;

        _timer?.Dispose();
        _timer = null;

        if (RefreshRate > 0)
        {
            _timer = Timer.Add(TimerType.PostCycle, TimerCycles.Repeating, RefreshRate);
            _timer.Tick += _tickHandler;
        }
    }

    private static void Timer_Tick(TimerEventArgs e)
    {
        foreach (var surface in VisibleSurfaces.InternalSurfaces)
        {
            surface.RenderBackbuffer(false);
        }
    }
}
