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
    /// <param name="commands">The commands.</param>
    /// <param name="sequence">The sequence.</param>
    /// <param name="producedTick">The produced tick.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    internal RenderFrameSnapshot(SKPicture commands, long sequence, long producedTick, int width, int height)
    {
        _commands = commands;
        Sequence = sequence;
        ProducedTick = producedTick;
        Width = width;
        Height = height;
    }

    /// <summary>
    /// Gets the sequence.
    /// </summary>
    internal long Sequence { get; }
    /// <summary>
    /// Gets the produced tick.
    /// </summary>
    internal long ProducedTick { get; }
    /// <summary>
    /// Gets the width.
    /// </summary>
    internal int Width { get; }
    /// <summary>
    /// Gets the height.
    /// </summary>
    internal int Height { get; }
    /// <summary>
    /// Gets the command count.
    /// </summary>
    internal int CommandCount => _commands.ApproximateOperationCount;

    /// <summary>
    /// Performs the replay operation.
    /// </summary>
    /// <param name="canvas">The canvas.</param>
    internal void Replay(SKCanvas canvas) => canvas.DrawPicture(_commands);

    /// <summary>
    /// Releases resources used by this instance.
    /// </summary>
    public void Dispose() => _commands.Dispose();
}
