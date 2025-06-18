using Gondwana.Drawing.Sprites;
using Gondwana.Common.Enums;
using System.Runtime.Serialization;

namespace Gondwana.Drawing.Animation;

/// <summary>
/// References a FrameSequence object, along with a particular
/// Throttle value for animating through Frame objects
/// </summary>
[DataContract(IsReference = true)]
public class Cycle : ICloneable, IDisposable
{
    #region fields
    [DataMember]
    public FrameSequence Sequence;

    [DataMember]
    public readonly string CycleKey;

    internal long _throttle = 0;
    #endregion fields

    #region constructors / destructor
    public Cycle(FrameSequence sequence, double throttleTime, string cycleKey)
    {
        Sequence = sequence;
        ThrottleTime = throttleTime;
        NextCycle = this;
        CycleKey = cycleKey;

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

    ~Cycle()
    {
        Dispose();
    }

    [OnDeserialized()]
    private void OnDeserialized(StreamingContext context)
    {
        if (Cycle._cycles.ContainsKey(CycleKey))
            Cycle._cycles[CycleKey] = this;
        else
            Cycle._cycles.Add(CycleKey, this);
    }
    #endregion

    #region public properties
    private double _throttleTime;

    [DataMember]
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
    [IgnoreDataMember]
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

    [DataMember]
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

        foreach (Sprite sprite in Sprites.Sprites._spriteList)
        {
            if (sprite.TileAnimator.CurrentCycle == this)
                sprite.TileAnimator.CurrentCycle = null;
        }

        Cycle._cycles.Remove(CycleKey);
    }
    #endregion IDisposable Members

    #region static members
    internal static Dictionary<string, Cycle> _cycles = new Dictionary<string, Cycle>();

    public static int Count
    {
        get { return _cycles.Count; }
    }

    public static List<string> GetAnimationCycleKeys()
    {
        return new List<string>(_cycles.Keys);
    }

    public static List<Cycle> GetAnimationCycles()
    {
        return new List<Cycle>(_cycles.Values);
    }

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
    #endregion
}