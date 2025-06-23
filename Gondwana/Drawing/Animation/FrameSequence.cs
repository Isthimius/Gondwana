using Gondwana.Common.Enums;
using System.Collections;
using System.Runtime.Serialization;

namespace Gondwana.Drawing.Animation;

/// <summary>
///
/// </summary>
[DataContract]
public struct FrameSequence : IEnumerable<Frame>
{
    #region fields
    [DataMember]
    public CycleType SequenceCycleType;

    [DataMember]
    private List<Frame> frameList;

    private int currentFrameIdx;
    private int curFrameIncrement;
    private bool cycleFinished;
    #endregion fields

    #region constructors / finalizer
    public FrameSequence(Frame frame)
    {
        frameList = new List<Frame>();
        frameList.Add(frame);
        SequenceCycleType = CycleType.Simple;
        currentFrameIdx = 0;
        curFrameIncrement = 1;
        cycleFinished = true;
    }

    public FrameSequence(List<Frame> frames)
    {
        frameList = frames;
        SequenceCycleType = CycleType.Simple;
        currentFrameIdx = 0;
        curFrameIncrement = 1;
        cycleFinished = true;
    }

    [OnDeserialized()]
    private void OnDeserialized(StreamingContext context)
    {
        currentFrameIdx = 0;
        curFrameIncrement = 1;
        cycleFinished = true;
    }
    #endregion constructors / finalizer

    #region properties
    [IgnoreDataMember]
    public bool CycleFinished
    {
        get { return cycleFinished; }
    }

    [IgnoreDataMember]
    public int FrameCount
    {
        get
        {
            if (frameList == null)
                SetDefaults();

            return frameList.Count;
        }
    }

    [IgnoreDataMember]
    public Frame CurrentFrame
    {
        get
        {
            if (frameList == null)
                SetDefaults();

            return frameList[currentFrameIdx];
        }
    }

    [IgnoreDataMember]
    public int CurrentFrameIdx
    {
        get { return currentFrameIdx; }
    }

    [IgnoreDataMember]
    public IList<Frame> FrameList
    {
        get { return frameList.AsReadOnly(); }
    }
    #endregion properties

    #region public methods
    public Frame AddFrame(Tilesheet bmp, int xTile, int yTile)
    {
        return AddFrame(new Frame(bmp, xTile, yTile));
    }

    public Frame AddFrame(Frame frame)
    {
        if (frameList == null)
            SetDefaults();

        frameList.Add(frame);
        return frame;
    }

    public void RemoveFrame(int idx)
    {
        if (idx < frameList.Count)
            frameList.RemoveAt(idx);
    }

    public void Reset()
    {
        currentFrameIdx = 0;
        curFrameIncrement = 1;
    }
    #endregion public methods

    #region internal methods
    internal void StopCycle()
    {
        cycleFinished = true;
    }

    internal Frame AdvanceFrame()
    {
        switch (SequenceCycleType)
        {
            case CycleType.PingPong:
                currentFrameIdx += curFrameIncrement;
                if ((currentFrameIdx <= 0) || (currentFrameIdx >= frameList.Count - 1))
                    curFrameIncrement *= -1;

                if (currentFrameIdx < 0)
                    currentFrameIdx = 0;

                if (currentFrameIdx > frameList.Count - 1)
                    currentFrameIdx = frameList.Count - 1;

                cycleFinished = false;
                break;

            case CycleType.Repeating:
                if (++currentFrameIdx >= frameList.Count)
                    currentFrameIdx = 0;

                cycleFinished = false;
                break;

            case CycleType.Simple:
                if (++currentFrameIdx > frameList.Count - 1)
                {
                    currentFrameIdx = frameList.Count - 1;
                    cycleFinished = true;
                }
                else
                    cycleFinished = false;
                break;

            default:
                throw new Exception("Invalid CycleType: " + SequenceCycleType.ToString());
        }

        return frameList[currentFrameIdx];
    }
    #endregion

    #region private methods
    private void SetDefaults()
    {
        frameList = new List<Frame>();
        SequenceCycleType = CycleType.Simple;
        currentFrameIdx = 0;
        curFrameIncrement = 1;
        cycleFinished = true;
    }
    #endregion

    #region indexers
    public Frame this[int frameIdx]
    {
        get { return frameList[frameIdx]; }
    }
    #endregion indexers

    #region IEnumerable Members
    public IEnumerator GetEnumerator()
    {
        for (int i = 0; i < frameList.Count; i++)
            yield return frameList[i];
    }

    IEnumerator<Frame> IEnumerable<Frame>.GetEnumerator()
    {
        return frameList.GetEnumerator();
    }
    #endregion IEnumerable Members
}