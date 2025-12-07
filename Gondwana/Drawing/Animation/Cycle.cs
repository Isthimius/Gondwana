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

    [JsonProperty]
    public FrameSequence Sequence;

    [JsonProperty]
    public readonly string CycleKey;

    [JsonProperty]
    public readonly bool HideTileOnCycleEnd;

    internal long _throttle = 0;

    #endregion fields

    #region constructors / destructor

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

    [JsonProperty]
    public Cycle NextCycle { get; set; }

    #endregion public properties

    #region ICloneable Members

    public object Clone()
    {
        return new Cycle(this);
    }

    #endregion ICloneable Members

    #region IDisposable Members

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        foreach (Sprite sprite in Sprites.SpriteManager._spriteList)
        {
            if (sprite.TileAnimator.CurrentCycle == this)
                sprite.TileAnimator.CurrentCycle = null;
        }

        Cycle._cycles.Remove(CycleKey);
    }

    #endregion IDisposable Members

    #region static members

    internal static readonly Dictionary<string, Cycle> _cycles = new();

    public static int Count => _cycles.Count;

    public static List<string> GetAnimationCycleKeys() => new List<string>(_cycles.Keys);

    public static List<Cycle> GetAnimationCycles() => new List<Cycle>(_cycles.Values);

    public static Cycle GetAnimationCycle(string cycleKey)
    {
        if (_cycles.ContainsKey(cycleKey))
            return (Cycle)_cycles[cycleKey].Clone();
        else
            return null;
    }

    public static void ClearAnimationCycle(string cycleKey)
    {
        if (_cycles.ContainsKey(cycleKey))
            _cycles[cycleKey].Dispose();
    }

    public static void ClearAllAnimationCycles()
    {
        var tempCycles = new List<Cycle>(_cycles.Values);
        foreach (Cycle cyc in tempCycles)
            cyc.Dispose();
    }

    #endregion static members
}