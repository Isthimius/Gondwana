using System.Runtime.Versioning;
using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Timers;
using Microsoft.JSInterop;
using SkiaSharp;
using SkiaSharp.Views.Blazor;

namespace Gondwana.Blazor.Rendering;

/// <summary>
/// A Blazor WebGL render surface that draws Gondwana scenes through an
/// <see cref="GpuBackbuffer"/> without transferring frame pixels through JavaScript.
/// </summary>
/// <remarks>
/// <para>
/// The component uses <see cref="SKGLView"/>'s built-in animation loop as the single browser
/// <c>requestAnimationFrame</c> source for the WebGL path. Each paint callback advances the
/// timer-driven engine and, when the engine's foreground cadence requests a new scene frame,
/// renders that frame immediately while the WebGL context is current.
/// </para>
/// <para>
/// Browser animation frames that do not require a new Gondwana scene render simply re-blit the
/// current GPU backbuffer. This preserves <see cref="Gondwana.Configuration.EngineConfiguration.TargetFPS"/>
/// semantics without introducing a second animation-frame scheduler or a CPU pixel transfer.
/// </para>
/// <para>
/// The GPU context is obtained from <see cref="SKPaintGLSurfaceEventArgs.Surface"/> while the
/// WebGL context is current. Scene rendering and the final GPU-to-GPU surface draw both occur
/// synchronously inside that callback without allocating a per-frame <see cref="SKImage"/> snapshot.
/// </para>
/// </remarks>
[SupportedOSPlatform("browser")]
public sealed partial class BlazorGpuRenderSurfaceComponent : BlazorRenderSurfaceComponentBase
{
    private readonly string _generatedCanvasId = $"gondwana-webgl-{Guid.NewGuid():N}";
    private BlazorSkiaGlView? _glView;
    private IJSObjectReference? _module;
    private GRContext? _grContext;
    private IReadOnlyDictionary<string, object>? _gpuCanvasAttributes;
    private string _canvasId = string.Empty;
    private bool _backbufferNeedsRender = true;
    private bool _disposed;

    // Lightweight WebGL phase tracing. Values are aggregated for roughly one second so tracing
    // itself does not add a Console.WriteLine call to every browser animation frame.
    private long _performanceWindowStartTick = HighResTimer.GetCurrentTick();
    private long _sceneRenderStartTick;
    private long _lastSceneRenderTicks;
    private long _tickTicks;
    private long _sceneRenderTicks;
    private long _newFramePresentTicks;
    private long _cachedPresentTicks;
    private long _paintTicks;
    private int _tickSamples;
    private int _sceneRenderSamples;
    private int _newFramePresentSamples;
    private int _cachedPresentSamples;
    private int _paintSamples;

    /// <summary>Gets the adapter that coordinates engine frame requests with this component.</summary>
    public BlazorGpuRenderSurfaceAdapter Adapter { get; private set; } = null!;

    /// <summary>Gets the GPU render-surface host bound to this component.</summary>
    public RenderSurfaceHost<GpuBackbuffer> Host { get; private set; } = null!;

    /// <summary>Gets the HTML attributes applied to the <see cref="SKGLView"/> canvas.</summary>
    private IReadOnlyDictionary<string, object>? GpuCanvasAttributes => _gpuCanvasAttributes;

    /// <inheritdoc/>
    protected override void OnInitialized()
    {
        Adapter = new BlazorGpuRenderSurfaceAdapter();
        Host = new RenderSurfaceHost<GpuBackbuffer>(Adapter);
        Host.RenderBackbufferBegin += HandleRenderBackbufferBegin;
        Host.RenderBackbufferEnd += HandleRenderBackbufferEnd;
        Adapter.AttachToEngine();
    }

    /// <inheritdoc/>
    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        var attributes = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (CanvasAttributes is not null)
        {
            foreach (var attribute in CanvasAttributes)
                attributes[attribute.Key] = attribute.Value;
        }

        if (attributes.TryGetValue("id", out var id) && id is not null)
            _canvasId = id.ToString() ?? _generatedCanvasId;
        else
            attributes["id"] = _canvasId = _generatedCanvasId;

        attributes[nameof(SKGLView.OnPaintSurface)] =
            (Action<SKPaintGLSurfaceEventArgs>)HandlePaintSurface;

        _gpuCanvasAttributes = attributes;
    }

    /// <inheritdoc/>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
            return;

        _module = await JS.InvokeAsync<IJSObjectReference>(
            "import",
            "./_content/Gondwana.Blazor/gondwana-blazor.js");

        try
        {
            await _module.InvokeVoidAsync("focusElementById", _canvasId);
            await _module.InvokeVoidAsync("suppressBrowserInputDefaultsById", _canvasId);
        }
        catch
        {
            // Focus is best-effort; browser policy may reject programmatic focus.
        }
    }

    private void HandlePaintSurface(SKPaintGLSurfaceEventArgs e)
    {
        if (_disposed)
            return;

        long paintStartTick = HighResTimer.GetCurrentTick();

        var width = e.Info.Width;
        var height = e.Info.Height;
        Adapter.BeginPaint(width, height);

        if (width <= 0 || height <= 0 || e.Surface.Context is not GRContext grContext)
        {
            e.Surface.Canvas.Clear(SKColors.Black);
            return;
        }

        var backbuffer = (GpuBackbuffer)Host.Backbuffer;
        if (!ReferenceEquals(_grContext, grContext)
            || backbuffer.Width != width
            || backbuffer.Height != height)
        {
            _grContext = grContext;
            backbuffer.Initialize(grContext, width, height);
            _backbufferNeedsRender = true;
        }

        var engine = Engine.Instance;
        if (!engine.IsRunning)
        {
            e.Surface.Canvas.Clear(backbuffer.ClearColor);
            return;
        }

        // SKGLView owns the browser rAF for the GPU path. Advance simulation first so a
        // foreground frame request, when due, can be rendered in this same paint callback.
        long tickStartTick = HighResTimer.GetCurrentTick();
        engine.Tick();
        long tickEndTick = HighResTimer.GetCurrentTick();
        _tickTicks += Math.Max(0, tickEndTick - tickStartTick);
        _tickSamples++;

        bool engineFrameRequested = Adapter.ConsumeFrameRequest();
        bool renderScene = _backbufferNeedsRender || engineFrameRequested;

        if (renderScene)
            _lastSceneRenderTicks = 0;

        long presentStartTick = HighResTimer.GetCurrentTick();
        bool presented = renderScene
            ? Host.GlRenderToCanvas(e.Surface.Canvas)
            : Host.GlDrawCurrentFrameToCanvas(e.Surface.Canvas);
        long presentEndTick = HighResTimer.GetCurrentTick();

        long presentCallTicks = Math.Max(0, presentEndTick - presentStartTick);
        if (renderScene)
        {
            // RenderBackbufferBegin/End measures RenderToBackbuffer itself. Subtracting that
            // leaves EndFrame/flush + direct DrawSurface + BeginFrame presentation overhead.
            _newFramePresentTicks += Math.Max(0, presentCallTicks - _lastSceneRenderTicks);
            _newFramePresentSamples++;
        }
        else
        {
            _cachedPresentTicks += presentCallTicks;
            _cachedPresentSamples++;
        }

        if (!presented)
        {
            e.Surface.Canvas.Clear(backbuffer.ClearColor);
            return;
        }

        if (renderScene)
            _backbufferNeedsRender = false;

        // Count successful WebGL paint/presentation callbacks. This intentionally measures the
        // browser GPU presentation cadence, which may differ from the engine's TargetFPS.
        backbuffer.RecordFrame();

        long paintEndTick = HighResTimer.GetCurrentTick();
        _paintTicks += Math.Max(0, paintEndTick - paintStartTick);
        _paintSamples++;
        TracePerformanceIfDue(paintEndTick);
    }

    private void HandleRenderBackbufferBegin()
    {
        _sceneRenderStartTick = HighResTimer.GetCurrentTick();
    }

    private void HandleRenderBackbufferEnd()
    {
        if (_sceneRenderStartTick == 0)
            return;

        long endTick = HighResTimer.GetCurrentTick();
        _lastSceneRenderTicks = Math.Max(0, endTick - _sceneRenderStartTick);
        _sceneRenderTicks += _lastSceneRenderTicks;
        _sceneRenderSamples++;
        _sceneRenderStartTick = 0;
    }

    private void TracePerformanceIfDue(long now)
    {
        long elapsedTicks = now - _performanceWindowStartTick;
        if (elapsedTicks < HighResTimer.TicksPerSecond)
            return;

        Console.WriteLine(
            $"Gondwana WebGL perf: Tick {FormatAverageMs(_tickTicks, _tickSamples)} | " +
            $"Scene {FormatAverageMs(_sceneRenderTicks, _sceneRenderSamples)} | " +
            $"Present {FormatAverageMs(_newFramePresentTicks, _newFramePresentSamples)} | " +
            $"Cached {FormatAverageMs(_cachedPresentTicks, _cachedPresentSamples)} | " +
            $"Paint {FormatAverageMs(_paintTicks, _paintSamples)} | " +
            $"samples tick={_tickSamples} scene={_sceneRenderSamples} cached={_cachedPresentSamples}");

        _performanceWindowStartTick = now;
        _tickTicks = 0;
        _sceneRenderTicks = 0;
        _newFramePresentTicks = 0;
        _cachedPresentTicks = 0;
        _paintTicks = 0;
        _tickSamples = 0;
        _sceneRenderSamples = 0;
        _newFramePresentSamples = 0;
        _cachedPresentSamples = 0;
        _paintSamples = 0;
    }

    private static string FormatAverageMs(long ticks, int samples)
    {
        if (samples <= 0)
            return "n/a";

        double milliseconds = ticks * 1000.0 / HighResTimer.TicksPerSecond / samples;
        return $"{milliseconds:0.00} ms";
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (Host is not null)
        {
            Host.RenderBackbufferBegin -= HandleRenderBackbufferBegin;
            Host.RenderBackbufferEnd -= HandleRenderBackbufferEnd;
        }

        Adapter?.Dispose();
        Host?.Dispose();

        _grContext = null;
        _glView = null;

        if (_module is not null)
        {
            _ = _module.DisposeAsync();
            _module = null;
        }
    }
}
