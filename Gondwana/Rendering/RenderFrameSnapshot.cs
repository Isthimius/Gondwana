using SkiaSharp;

namespace Gondwana.Rendering;

/// <summary>
/// Completed visual commands for one surface. Contains no live scene objects.
/// Ownership transfers to the mailbox; only its slot owner may dispose the picture.
/// Skia pictures copy paint/path state and retain native image and font references,
/// so disposing the original managed drawing resources cannot invalidate replay.
/// Recordings must contain CPU resources, never context-bound textures or surfaces.
/// </summary>
internal sealed class RenderFrameSnapshot : IDisposable
{
    private readonly SKPicture _commands;

    /// <summary>
    /// Initializes a new instance of <see cref="RenderFrameSnapshot"/>.
    /// </summary>
    /// <param name="commands">The immutable recorded Skia commands for the frame.</param>
    /// <param name="sequence">The monotonically increasing publication sequence.</param>
    /// <param name="producedTick">The engine tick at which the frame was produced.</param>
    /// <param name="width">The recorded backbuffer width in pixels.</param>
    /// <param name="height">The recorded backbuffer height in pixels.</param>
    internal RenderFrameSnapshot(SKPicture commands, long sequence, long producedTick, int width, int height)
    {
        _commands = commands;
        Sequence = sequence;
        ProducedTick = producedTick;
        Width = width;
        Height = height;
    }

    /// <summary>
    /// Gets the monotonically increasing sequence assigned to this snapshot.
    /// </summary>
    internal long Sequence { get; }

    /// <summary>
    /// Gets the engine tick at which this snapshot was produced.
    /// </summary>
    internal long ProducedTick { get; }

    /// <summary>
    /// Gets the recorded backbuffer width in pixels.
    /// </summary>
    internal int Width { get; }

    /// <summary>
    /// Gets the recorded backbuffer height in pixels.
    /// </summary>
    internal int Height { get; }

    /// <summary>
    /// Gets Skia's approximate number of operations recorded in the snapshot.
    /// </summary>
    internal int CommandCount => _commands.ApproximateOperationCount;

    /// <summary>
    /// Replays the immutable recorded commands onto the supplied canvas.
    /// </summary>
    /// <param name="canvas">The destination canvas.</param>
    internal void Replay(SKCanvas canvas) => canvas.DrawPicture(_commands);

    /// <summary>
    /// Releases resources used by this instance.
    /// </summary>
    public void Dispose() => _commands.Dispose();
}
