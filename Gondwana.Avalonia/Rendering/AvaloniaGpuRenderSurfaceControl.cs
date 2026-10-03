using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using SkiaSharp;

namespace Gondwana.Avalonia.Rendering;

/// <summary>
/// An Avalonia <see cref="OpenGlControlBase"/> that displays game frames produced by the
/// Gondwana engine using GPU-accelerated rendering.
/// </summary>
/// <remarks>
/// <para>
/// Rendering uses Avalonia's native OpenGL surface.  SkiaSharp's <see cref="GRContext"/>
/// is created from the current OpenGL context on the first render callback and is used to
/// manage both the <see cref="GpuBackbuffer"/> off-screen surface and the window framebuffer
/// blit.  No CPU↔GPU pixel copies occur.
/// </para>
/// <para>
/// The engine's <c>AfterFrameRender</c> event posts <c>RequestNextFrameRendering()</c> to
/// the UI thread to pace frame delivery in lockstep with
/// <see cref="Gondwana.Configuration.EngineConfiguration.TargetFPS"/>.
/// </para>
/// <para>
/// This control targets desktop Avalonia platforms (Windows, macOS, Linux) that support
/// OpenGL.  Use <see cref="AvaloniaBitmapRenderSurfaceControl"/> for cross-platform / WASM
/// scenarios instead.
/// </para>
/// </remarks>
public class AvaloniaGpuRenderSurfaceControl : OpenGlControlBase, IDisposable
{
    // GL_RGBA8 pixel format for the SkiaSharp framebuffer wrapper.
    private const uint GlRgba8 = 0x8058;

    private GRGlInterface? _glInterface;
    private GRContext? _grContext;
    private GpuBackbuffer? _gpuBackbuffer;
    private readonly object _lifecycleGate = new();
    private int _disposeRequested;

    // Last physical dimensions used to detect resize inside OnOpenGlRender.
    private int _lastPhysW;
    private int _lastPhysH;

    /// <summary>
    /// Gets the GPU render surface adapter used to drive this control.
    /// </summary>
    public AvaloniaGpuRenderSurfaceAdapter Adapter { get; }

    /// <summary>
    /// Gets the <see cref="RenderSurfaceHost{T}"/> bound to this control.
    /// </summary>
    public RenderSurfaceHost<GpuBackbuffer> Host { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AvaloniaGpuRenderSurfaceControl"/> class.
    /// </summary>
    public AvaloniaGpuRenderSurfaceControl()
    {
        var scaling = VisualRoot?.RenderScaling ?? 1.0;
        var physW = Math.Max(1, (int)Math.Round(Bounds.Width * scaling));
        var physH = Math.Max(1, (int)Math.Round(Bounds.Height * scaling));

        Adapter = new AvaloniaGpuRenderSurfaceAdapter(physW, physH);
        Host = new RenderSurfaceHost<GpuBackbuffer>(Adapter);
        _gpuBackbuffer = (GpuBackbuffer)Host.Backbuffer;

        // Pacing: post RequestNextFrameRendering() after each engine foreground cycle.
        Adapter.AttachToEngine(RequestNextFrameRendering);
    }

    // ── OpenGL lifecycle callbacks (called on the GL thread by Avalonia) ─────

    /// <inheritdoc/>
    protected override void OnOpenGlInit(GlInterface gl)
    {
        // Create a SkiaSharp GRContext from the current OpenGL context.
        // GRGlInterface.Create() auto-detects the current platform GL/EGL/WGL context
        // (which Avalonia has made current before this callback) and loads function
        // pointers from it without requiring any explicit proc-address lookup delegate.
        lock (_lifecycleGate)
        {
            if (_disposeRequested != 0) return;
            _glInterface = GRGlInterface.Create();
            if (_glInterface == null) return;
            _grContext = GRContext.CreateGl(_glInterface);
            if (_grContext == null)
            {
                _glInterface.Dispose();
                _glInterface = null;
                return;
            }
        }

        var scaling = VisualRoot?.RenderScaling ?? 1.0;
        var physW = (int)Math.Round(Bounds.Width * scaling);
        var physH = (int)Math.Round(Bounds.Height * scaling);

        if (physW <= 0 || physH <= 0) return;

        _lastPhysW = physW;
        _lastPhysH = physH;

        Adapter.UpdateDimensions(physW, physH);
        _gpuBackbuffer?.EnsureInitialized(_grContext);
    }

    /// <inheritdoc/>
    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        if (Volatile.Read(ref _disposeRequested) != 0 || _grContext == null || _gpuBackbuffer == null) return;

        // Notify SkiaSharp that external GL code (Avalonia's compositor) may have
        // modified the GL state since the last Skia draw call.
        _grContext.ResetContext();

        var scaling = VisualRoot?.RenderScaling ?? 1.0;
        var physW = (int)Math.Round(Bounds.Width * scaling);
        var physH = (int)Math.Round(Bounds.Height * scaling);

        if (physW <= 0 || physH <= 0)
        {
            _lastPhysW = _lastPhysH = 0;
            Adapter.UpdateDimensions(0, 0);
            return;
        }

        // Resize presentation resources only; the logical render target remains unchanged.
        if (physW != _lastPhysW || physH != _lastPhysH)
        {
            _lastPhysW = physW;
            _lastPhysH = physH;
            Adapter.UpdateDimensions(physW, physH);

        }

        _gpuBackbuffer.EnsureInitialized(_grContext);

        // Wrap the Avalonia-provided framebuffer in a SkiaSharp surface for compositing.
        // The framebuffer uses OpenGL bottom-left origin, RGBA8 color format.
        var fbInfo = new GRGlFramebufferInfo((uint)fb, GlRgba8);
        using var renderTarget = new GRBackendRenderTarget(physW, physH, sampleCount: 0, stencilBits: 0, fbInfo);
        using var fbSurface = SKSurface.Create(_grContext, renderTarget, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888);

        if (fbSurface == null) return;

        var canvas = fbSurface.Canvas;

        // Replay the newest immutable Engine snapshot on GL and blit the result.
        using var img = Host.GlRenderAndSnapshot();
        if (img != null)
        {
            Adapter.DrawImage(canvas, img, Adapter.ClearColor);
        }
        else
        {
            canvas.Clear(Adapter.ClearColor);
        }

        _grContext.Flush();
        _gpuBackbuffer.RecordFrame();
    }

    /// <inheritdoc/>
    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        lock (_lifecycleGate)
        {
            // Avalonia may deinitialize and recreate GL without ending the game's
            // logical lifecycle. Release surfaces before their owning context.
            _gpuBackbuffer?.ReleaseContext();
            _grContext?.Dispose();
            _grContext = null;
            _glInterface?.Dispose();
            _glInterface = null;
            if (_disposeRequested != 0) _gpuBackbuffer?.Dispose();
        }
    }

    /// <summary>
    /// Ends the logical surface lifecycle. The hosting class calls this on shutdown;
    /// custom hosts should call it when permanently removing the control. Native GPU
    /// teardown remains in Avalonia's current-context deinitialization callback.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposeRequested, 1) != 0) return;
        Host.Dispose();
        Adapter.Dispose();
        lock (_lifecycleGate)
        {
            // If GL is already gone (or never existed), only CPU resources remain.
            if (_grContext is null) _gpuBackbuffer?.Dispose();
        }
        GC.SuppressFinalize(this);
    }
}
