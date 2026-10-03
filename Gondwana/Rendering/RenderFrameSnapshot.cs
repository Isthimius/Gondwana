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

    internal RenderFrameSnapshot(SKPicture commands, long sequence, long producedTick, int width, int height)
    {
        _commands = commands;
        Sequence = sequence;
        ProducedTick = producedTick;
        Width = width;
        Height = height;
    }

    internal long Sequence { get; }
    internal long ProducedTick { get; }
    internal int Width { get; }
    internal int Height { get; }
    internal int CommandCount => _commands.ApproximateOperationCount;

    internal void Replay(SKCanvas canvas) => canvas.DrawPicture(_commands);

    public void Dispose() => _commands.Dispose();
}
