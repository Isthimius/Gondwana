using System.Runtime.Versioning;
using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
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
/// The component uses <see cref="SKGLView"/> with its built-in continuous loop disabled. The
/// engine requests frames at its foreground cadence, and duplicate invalidations are coalesced
/// until the browser delivers the corresponding WebGL paint callback.
/// </para>
/// <para>
/// The GPU context is obtained from <see cref="SKPaintGLSurfaceEventArgs.Surface"/> while the
/// WebGL context is current. Scene rendering and the final GPU-to-GPU blit both occur
/// synchronously inside that callback.
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
    private bool _disposed;

    /// <summary>Gets the adapter that coordinates engine frame requests with this component.</summary>
    public BlazorGpuRenderSurfaceAdapter Adapter { get; private set; } = null!;

    /// <summary>Gets the GPU render-surface host bound to this component.</summary>
    public RenderSurfaceHost<GpuBackbuffer> Host { get; private set; } = null!;

    /// <summary>Gets the HTML attributes applied to the <see cref="SKGLView"/> canvas.</summary>
    private IReadOnlyDictionary<string, object>? GpuCanvasAttributes => _gpuCanvasAttributes;

    /// <inheritdoc/>
    protected override void OnInitialized()
    {
        Adapter = new BlazorGpuRenderSurfaceAdapter(RequestRender);
        Host = new RenderSurfaceHost<GpuBackbuffer>(Adapter);
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

    private void RequestRender()
    {
        if (!_disposed)
            _glView?.Invalidate();
    }

    private void HandlePaintSurface(SKPaintGLSurfaceEventArgs e)
    {
        if (_disposed)
            return;

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
        }

        using var image = Host.GlRenderAndSnapshot();
        if (image is null)
        {
            e.Surface.Canvas.Clear(backbuffer.ClearColor);
            return;
        }

        e.Surface.Canvas.DrawImage(image, SKRect.Create(0, 0, width, height));
        backbuffer.RecordFrame();
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

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
