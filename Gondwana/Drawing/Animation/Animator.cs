using Gondwana.Timers;

namespace Gondwana.Drawing.Animation;

public class Animator : IDisposable
{
    #region events

    public event Action<AnimatorEventArgs> Started;

    public event Action<AnimatorEventArgs> Stopped;

    public event Action<AnimatorEventArgs> Cycled;

    #endregion events

    #region private/internal fields

    private Tile parent;
    private bool cycling = false;
    private long LastTick = HighResTimer.GetCurrentTick();

    #endregion private/internal fields

    #region constructors / finalizer

    protected internal Animator(Tile tile)
    {
        parent = tile;
    }

    ~Animator()
    {
        Dispose();
    }

    #endregion constructors / finalizer

    #region properties

    public Tile Parent
    {
        get { return parent; }
    }

    public Cycle CurrentCycle { get; set; }

    public bool IsCycling
    {
        get { return cycling; }
        set
        {
            if (cycling != value)
            {
                if (value == true)
                    StartAnimation();
                else
                    StopAnimation();
            }
        }
    }

    #endregion properties

    #region public methods

    public Cycle SetCurrentCycle(string cycleKey)
    {
        CurrentCycle = Cycle.GetAnimationCycle(cycleKey);
        return CurrentCycle;
    }

    public void StartAnimation()
    {
        if (CurrentCycle != null)
        {
            cycling = true;

            if (Tile.TilesAnimating.IndexOf(parent) == -1)
                Tile.TilesAnimating.Add(parent);

            if (Started != null)
                Started(new AnimatorEventArgs(parent, this));
        }
    }

    public void StartAnimation(string cycleKey)
    {
        CurrentCycle = Cycle.GetAnimationCycle(cycleKey);
        StartAnimation();
    }

    public void StopAnimation()
    {
        // only perform action if actually cycling
        if (cycling)
        {
            cycling = false;
            CurrentCycle.Sequence.StopCycle();

            Tile.TilesAnimating.Remove(parent);

            if (Stopped != null)
                Stopped(new AnimatorEventArgs(parent, this));

            if (CurrentCycle?.NextCycle != null)
                CurrentCycle = CurrentCycle.NextCycle;
            else if (CurrentCycle?.NextCycle.HideTileOnCycleEnd == true)
                parent.Visible = false;
        }
    }

    internal void CycleAnimation(long currentTick)
    {
        if (CurrentCycle is null)
            return;

        // if throttle is 0, stop the animation so loop doesn't hang
        if (CurrentCycle._throttle == 0)
        {
            StopAnimation();
            return;
        }

        //if "throttle" time has passed
        while (currentTick >= LastTick + CurrentCycle._throttle)
        {
            // capture the LastTick value
            LastTick += CurrentCycle._throttle;

            // do not change image if animation is paused
            if (parent.PauseAnimation)
                return;

            // cycle the frame and set the sprite image; this will add it to RefreshQueue
            parent.CurrentFrame = CurrentCycle.Sequence.AdvanceFrame();

            // raise the event
            Cycled?.Invoke(new AnimatorEventArgs(parent, this));

            // if terminating cycle is done, stop the animation
            if (CurrentCycle.Sequence.CycleFinished)
                StopAnimation();
        }
    }

    #endregion public methods

    #region IDisposable Members

    /// <summary>
    /// *** DO NOT CALL DIRECTLY! ***
    /// </summary>
    public void Dispose()
    {
        Started = null;
        Stopped = null;
        Cycled = null;
        GC.SuppressFinalize(this);
    }

    #endregion IDisposable Members
}