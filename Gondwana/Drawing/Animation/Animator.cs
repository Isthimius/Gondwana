using Gondwana.Timers;

namespace Gondwana.Drawing.Animation;

/// <summary>
/// Manages animation playback for a <see cref="Tile"/>, controlling animation cycles, frame timing, and raising events during the animation lifecycle.
/// </summary>
public class Animator : IDisposable
{
    #region events

    /// <summary>
    /// Occurs when an animation starts playing.
    /// </summary>
    public event Action<AnimatorEventArgs> Started;

    /// <summary>
    /// Occurs when an animation stops playing.
    /// </summary>
    public event Action<AnimatorEventArgs> Stopped;

    /// <summary>
    /// Occurs each time the animation advances to the next frame in the cycle.
    /// </summary>
    public event Action<AnimatorEventArgs> Cycled;

    #endregion events

    #region private/internal fields

    private readonly Tile parent;
    private bool cycling = false;
    private long LastTick = HighResTimer.GetCurrentTick();

    #endregion private/internal fields

    #region constructors / finalizer

    /// <summary>
    /// Initializes a new instance of the <see cref="Animator"/> class for the specified tile.
    /// </summary>
    /// <param name="tile">The tile that owns this animator.</param>
    protected internal Animator(Tile tile)
    {
        parent = tile;
    }

    /// <summary>
    /// Finalizer that ensures proper cleanup of the animator resources.
    /// </summary>
    ~Animator()
    {
        Dispose();
    }

    #endregion constructors / finalizer

    #region properties

    /// <summary>
    /// Gets the tile that owns this animator.
    /// </summary>
    public Tile Parent
    {
        get { return parent; }
    }

    /// <summary>
    /// Gets or sets the current animation cycle being played.
    /// </summary>
    public Cycle CurrentCycle { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the animation is currently cycling.
    /// Setting this property to <c>true</c> starts the animation; setting it to <c>false</c> stops it.
    /// </summary>
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

    /// <summary>
    /// Sets the current animation cycle using the specified cycle key.
    /// </summary>
    /// <param name="cycleKey">The key identifying the animation cycle to set as current.</param>
    /// <returns>The cycle that was set as the current cycle.</returns>
    public Cycle SetCurrentCycle(string cycleKey)
    {
        CurrentCycle = Cycle.GetAnimationCycle(cycleKey);
        return CurrentCycle;
    }

    /// <summary>
    /// Starts playing the current animation cycle.
    /// If no cycle is set, this method has no effect.
    /// Raises the <see cref="Started"/> event when the animation begins.
    /// </summary>
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

    /// <summary>
    /// Sets the current animation cycle and starts playing it.
    /// </summary>
    /// <param name="cycleKey">The key identifying the animation cycle to play.</param>
    public void StartAnimation(string cycleKey)
    {
        CurrentCycle = Cycle.GetAnimationCycle(cycleKey);
        StartAnimation();
    }

    /// <summary>
    /// Stops the current animation cycle.
    /// Raises the <see cref="Stopped"/> event and advances to the next cycle if one is configured.
    /// If the next cycle is configured to hide the tile on cycle end, the parent tile will be hidden.
    /// </summary>
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

        var throttle = GetThrottleForCurrentFrame();

        // if throttle is 0, stop the animation so loop doesn't hang
        if (throttle == 0)
        {
            StopAnimation();
            return;
        }

        //if "throttle" time has passed
        while (currentTick >= LastTick + throttle)
        {
            // capture the LastTick value
            LastTick += throttle;

            // do not change image if animation is paused
            if (parent.PauseAnimation)
                return;

            // cycle the frame and set the sprite image; this will add it to RefreshQueue
            parent.CurrentFrame = CurrentCycle.Sequence.AdvanceFrame();

            // raise the event
            Cycled?.Invoke(new AnimatorEventArgs(parent, this));

            // if terminating cycle is done, stop the animation
            if (CurrentCycle.Sequence.CycleFinished)
            {
                StopAnimation();
                return;
            }

            // recalculate throttle for the new current frame
            throttle = GetThrottleForCurrentFrame();
            if (throttle == 0)
            {
                StopAnimation();
                return;
            }
        }
    }

    private long GetThrottleForCurrentFrame() => CurrentCycle._throttle;

    #endregion public methods

    #region IDisposable Members

    /// <summary>
    /// Releases all event handlers and suppresses finalization.
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