using Gondwana.Rendering;

namespace Gondwana.Blazor.Hosting;

/// <summary>
/// Defers a one-time logical render-scale selection until a Blazor canvas has a usable size.
/// </summary>
internal sealed class BlazorLogicalResolutionBinding : IDisposable
{
    private readonly RenderSurfaceAdapterBase _adapter;
    private readonly Action<float> _applyScale;
    private readonly int _logicalWidth;
    private readonly int _logicalHeight;
    private bool _established;
    private bool _disposed;

    /// <summary>Creates a canvas-to-logical-resolution binding.</summary>
    /// <param name="adapter">The adapter supplying current canvas size.</param>
    /// <param name="logicalWidth">Minimum logical width.</param>
    /// <param name="logicalHeight">Minimum logical height.</param>
    /// <param name="applyScale">Invoked once to apply the calculated render scale.</param>
    internal BlazorLogicalResolutionBinding(
        RenderSurfaceAdapterBase adapter,
        int logicalWidth,
        int logicalHeight,
        Action<float> applyScale)
    {
        ArgumentNullException.ThrowIfNull(adapter);
        ArgumentNullException.ThrowIfNull(applyScale);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(logicalWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(logicalHeight);

        _adapter = adapter;
        _logicalWidth = logicalWidth;
        _logicalHeight = logicalHeight;
        _applyScale = applyScale;

        // Subscribe first so a paint between subscription and the size check is not missed.
        _adapter.Resized += OnResized;
        TryEstablish(_adapter.Width, _adapter.Height);
    }

    /// <summary>Responds to a pending canvas resize.</summary>
    /// <param name="args">The resize event arguments.</param>
    private void OnResized(RenderSurfaceAdapterResizedEventArgs args)
        => TryEstablish(args.NewWidth, args.NewHeight);

    /// <summary>Applies scale for the first usable canvas size.</summary>
    /// <param name="width">Canvas width.</param>
    /// <param name="height">Canvas height.</param>
    private void TryEstablish(int width, int height)
    {
        // WebGL uses a 1x1 placeholder until its first real paint.
        if (_disposed || _established || width <= 1 || height <= 1)
            return;

        // Round physical size outwards: never crop the requested logical game board.
        float scale = Math.Max((float)_logicalWidth / width, (float)_logicalHeight / height);
        _applyScale(scale);
        _established = true;
        _adapter.Resized -= OnResized;
    }

    /// <summary>Detaches from the adapter when no further resizing is needed.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _adapter.Resized -= OnResized;
    }
}
