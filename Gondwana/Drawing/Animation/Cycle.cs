using System.Runtime.Serialization;
using Gondwana.Drawing.Sprites;
using Gondwana.Timers;
using Newtonsoft.Json;

namespace Gondwana.Drawing.Animation;

/// <summary>
/// References a FrameSequence object, along with a particular
/// Throttle value for animating through Frame objects
/// </summary>
[JsonObject(IsReference = true)]
public class Cycle : ICloneable, IDisposable
{
    #region fields

    /// <summary>
    /// The frame sequence used by this animation cycle
    /// </summary>
    [JsonProperty]
    public FrameSequence Sequence;

    /// <summary>
    /// The unique identifier key for this cycle
    /// </summary>
    [JsonProperty]
    public readonly string CycleKey;

    /// <summary>
    /// Indicates whether the tile should be hidden when the cycle completes
    /// </summary>
    [JsonProperty]
    public readonly bool HideTileOnCycleEnd;

    internal long _throttle = 0;

    #endregion fields

    #region constructors / destructor

    /// <summary>
    /// Initializes a new instance of the <see cref="Cycle"/> class
    /// </summary>
    /// <param name="sequence">The frame sequence to animate through</param>
    /// <param name="throttleTime">The time in seconds between frame transitions</param>
    /// <param name="cycleKey">The unique identifier for this cycle</param>
    /// <param name="hideTileOnCycleEnd">If true, hides the tile when the cycle completes. Default is false.</param>
    public Cycle(FrameSequence sequence, double throttleTime, string cycleKey, bool hideTileOnCycleEnd = false)
    {
        Sequence = sequence;
        ThrottleTime = throttleTime;
        NextCycle = this;
        CycleKey = cycleKey;
        HideTileOnCycleEnd = hideTileOnCycleEnd;

        if (Cycle._cycles.ContainsKey(cycleKey))
            Cycle._cycles[cycleKey] = this;
        else
            Cycle._cycles.Add(cycleKey, this);
    }

    private Cycle(Cycle fromCycle)
    {
        Sequence = fromCycle.Sequence;
        _throttle = fromCycle._throttle;
        NextCycle = this;
        CycleKey = fromCycle.CycleKey;
    }

    [OnDeserialized()]
    private void OnDeserialized(StreamingContext context)
    {
        if (Cycle._cycles.ContainsKey(CycleKey))
            Cycle._cycles[CycleKey] = this;
        else
            Cycle._cycles.Add(CycleKey, this);
    }

    #endregion constructors / destructor

    #region public properties

    private double _throttleTime;

    /// <summary>
    /// Gets or sets the time in seconds between frame transitions in the animation cycle
    /// </summary>
    [JsonProperty]
    public double ThrottleTime
    {
        get { return _throttleTime; }
        set
        {
            _throttle = (long)(value * (double)HighResTimer.TicksPerSecond);
            _throttleTime = value;
        }
    }

    /// <summary>
    /// Returns the total time in seconds for the Cycle
    /// </summary>
    [JsonIgnore]
    public double TotalCycleTime
    {
        get
        {
            switch (Sequence.SequenceCycleType)
            {
                case CycleType.Simple:
                    // -1 since first frame is played right away
                    return ThrottleTime * (double)(Sequence.FrameCount - 1);

                case CycleType.Repeating:
                    return ThrottleTime * (double)Sequence.FrameCount;

                case CycleType.PingPong:
                    // -2, since:
                    // "C" is only shown once, and...
                    // second "A" is actually part of next cycle repetition
                    return ThrottleTime * (double)((Sequence.FrameCount * 2) - 2);

                default:
                    return 0;
            }
        }
    }

    /// <summary>
    /// Gets or sets the next cycle to transition to when this cycle completes
    /// </summary>
    [JsonProperty]
    public Cycle NextCycle { get; set; }

    #endregion public properties

    #region ICloneable Members

    /// <summary>
    /// Creates a shallow copy of the current <see cref="Cycle"/> instance
    /// </summary>
    /// <returns>A new <see cref="Cycle"/> object that is a copy of this instance</returns>
    public object Clone()
    {
        return new Cycle(this);
    }

    #endregion ICloneable Members

    #region IDisposable Members

    /// <summary>
    /// Releases all resources used by the <see cref="Cycle"/> and removes it from the static cycle collection
    /// </summary>
    public void Dispose()
    {
        GC.SuppressFinalize(this);

        foreach (Sprite sprite in SpriteManager.Instance._spriteList)
        {
            if (sprite.TileAnimator.CurrentCycle == this)
                sprite.TileAnimator.CurrentCycle = null;
        }

        Cycle._cycles.Remove(CycleKey);
    }

    #endregion IDisposable Members

    #region static members

    internal static readonly Dictionary<string, Cycle> _cycles = new();

    /// <summary>
    /// Gets the total number of animation cycles currently registered
    /// </summary>
    public static int Count => _cycles.Count;

    /// <summary>
    /// Retrieves a list of all registered animation cycle keys
    /// </summary>
    /// <returns>A list containing all cycle keys</returns>
    public static List<string> GetAnimationCycleKeys() => new List<string>(_cycles.Keys);

    /// <summary>
    /// Retrieves a list of all registered animation cycles
    /// </summary>
    /// <returns>A list containing all registered <see cref="Cycle"/> instances</returns>
    public static List<Cycle> GetAnimationCycles() => new List<Cycle>(_cycles.Values);

    /// <summary>
    /// Retrieves a clone of the animation cycle with the specified key
    /// </summary>
    /// <param name="cycleKey">The unique identifier of the cycle to retrieve</param>
    /// <returns>A cloned <see cref="Cycle"/> instance if found; otherwise, null</returns>
    public static Cycle GetAnimationCycle(string cycleKey)
    {
        if (_cycles.ContainsKey(cycleKey))
            return (Cycle)_cycles[cycleKey].Clone();
        else
            return null;
    }

    /// <summary>
    /// Removes and disposes the animation cycle with the specified key
    /// </summary>
    /// <param name="cycleKey">The unique identifier of the cycle to clear</param>
    public static void ClearAnimationCycle(string cycleKey)
    {
        if (_cycles.ContainsKey(cycleKey))
            _cycles[cycleKey].Dispose();
    }

    /// <summary>
    /// Removes and disposes all registered animation cycles
    /// </summary>
    public static void ClearAllAnimationCycles()
    {
        var tempCycles = new List<Cycle>(_cycles.Values);
        foreach (Cycle cyc in tempCycles)
            cyc.Dispose();
    }

    #endregion static members
}