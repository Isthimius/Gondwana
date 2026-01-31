using System.Collections;
using System.Runtime.Serialization;
using Gondwana.Drawing.Tilesheets;
using Newtonsoft.Json;

namespace Gondwana.Drawing.Animation;

/// <summary>
/// Represents a sequence of animation frames that can be cycled through using different animation patterns
/// </summary>
public struct FrameSequence : IEnumerable<Frame>
{
    #region fields

    /// <summary>
    /// The type of cycle pattern used when animating through the frame sequence
    /// </summary>
    [JsonProperty]
    public CycleType SequenceCycleType;

    [JsonProperty]
    private List<Frame> frameList;

    private int currentFrameIdx;
    private int curFrameIncrement;
    private bool cycleFinished;

    #endregion fields

    #region constructors / finalizer

    /// <summary>
    /// Initializes a new instance of the <see cref="FrameSequence"/> struct with a single frame
    /// </summary>
    /// <param name="frame">The single frame to include in the sequence</param>
    public FrameSequence(Frame frame)
    {
        frameList = new List<Frame>();
        frameList.Add(frame);
        SequenceCycleType = CycleType.Simple;
        currentFrameIdx = 0;
        curFrameIncrement = 1;
        cycleFinished = true;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FrameSequence"/> struct with a collection of frames
    /// </summary>
    /// <param name="frames">The list of frames to include in the sequence</param>
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

    /// <summary>
    /// Gets a value indicating whether the animation cycle has finished playing through the sequence
    /// </summary>
    [JsonIgnore]
    public bool CycleFinished
    {
        get { return cycleFinished; }
    }

    /// <summary>
    /// Gets the total number of frames in the sequence
    /// </summary>
    [JsonIgnore]
    public int FrameCount
    {
        get
        {
            if (frameList == null)
                SetDefaults();

            return frameList.Count;
        }
    }

    /// <summary>
    /// Gets the frame currently active in the animation sequence
    /// </summary>
    [JsonIgnore]
    public Frame CurrentFrame
    {
        get
        {
            if (frameList == null)
                SetDefaults();

            return frameList[currentFrameIdx];
        }
    }

    /// <summary>
    /// Gets the zero-based index of the current frame in the sequence
    /// </summary>
    [JsonIgnore]
    public int CurrentFrameIdx
    {
        get { return currentFrameIdx; }
    }

    /// <summary>
    /// Gets a read-only view of the list of frames in the sequence
    /// </summary>
    [JsonIgnore]
    public IList<Frame> FrameList
    {
        get { return frameList.AsReadOnly(); }
    }

    #endregion properties

    #region public methods

    /// <summary>
    /// Creates and adds a new frame to the sequence using tilesheet coordinates
    /// </summary>
    /// <param name="bmp">The tilesheet containing the frame image</param>
    /// <param name="xTile">The x-coordinate of the tile in the tilesheet</param>
    /// <param name="yTile">The y-coordinate of the tile in the tilesheet</param>
    /// <returns>The newly created and added <see cref="Frame"/></returns>
    public Frame AddFrame(Tilesheet bmp, int xTile, int yTile)
    {
        return AddFrame(new Frame(bmp, xTile, yTile));
    }

    /// <summary>
    /// Adds an existing frame to the sequence
    /// </summary>
    /// <param name="frame">The frame to add to the sequence</param>
    /// <returns>The added <see cref="Frame"/></returns>
    public Frame AddFrame(Frame frame)
    {
        if (frameList == null)
            SetDefaults();

        frameList.Add(frame);
        return frame;
    }

    /// <summary>
    /// Removes the frame at the specified index from the sequence
    /// </summary>
    /// <param name="idx">The zero-based index of the frame to remove</param>
    public void RemoveFrame(int idx)
    {
        if (idx < frameList.Count)
            frameList.RemoveAt(idx);
    }

    /// <summary>
    /// Resets the sequence to its initial state, starting from the first frame
    /// </summary>
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
                throw new InvalidOperationException("Invalid CycleType: " + SequenceCycleType.ToString());
        }

        return frameList[currentFrameIdx];
    }

    #endregion internal methods

    #region private methods

    private void SetDefaults()
    {
        frameList = new List<Frame>();
        SequenceCycleType = CycleType.Simple;
        currentFrameIdx = 0;
        curFrameIncrement = 1;
        cycleFinished = true;
    }

    #endregion private methods

    #region indexers

    /// <summary>
    /// Gets the frame at the specified index in the sequence
    /// </summary>
    /// <param name="frameIdx">The zero-based index of the frame to retrieve</param>
    /// <returns>The <see cref="Frame"/> at the specified index</returns>
    public Frame this[int frameIdx]
    {
        get { return frameList[frameIdx]; }
    }

    #endregion indexers

    #region IEnumerable Members

    /// <summary>
    /// Returns an enumerator that iterates through the frame sequence
    /// </summary>
    /// <returns>An <see cref="IEnumerator"/> for the frame sequence</returns>
    public IEnumerator GetEnumerator()
    {
        for (int i = 0; i < frameList.Count; i++)
            yield return frameList[i];
    }

    /// <summary>
    /// Returns a strongly-typed enumerator that iterates through the frame sequence
    /// </summary>
    /// <returns>An <see cref="IEnumerator{Frame}"/> for the frame sequence</returns>
    IEnumerator<Frame> IEnumerable<Frame>.GetEnumerator()
    {
        return frameList.GetEnumerator();
    }

    #endregion IEnumerable Members
}