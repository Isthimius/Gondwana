using Gondwana.Blazor.Rendering;
using Gondwana.Rendering;
using Gondwana.Scenes;
using Microsoft.JSInterop;

namespace Gondwana.Blazor.Hosting;

/// <summary>
/// Provides a base class for Gondwana Blazor games rendered through the CPU-backed bitmap path.
/// </summary>
/// <remarks>
/// This type preserves the original Blazor hosting API. Use <see cref="BlazorGpuGameHost"/> with
/// <see cref="BlazorGpuRenderSurfaceComponent"/> for WebGL-backed rendering.
/// </remarks>
public abstract class BlazorGameHost : BlazorGameHostBase
{
    /// <summary>Gets the bitmap render surface used for displaying game content.</summary>
    public BlazorBitmapRenderSurfaceComponent RenderSurface { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="BlazorGameHost"/>.
    /// </summary>
    /// <param name="renderSurface">The bitmap render surface component.</param>
    /// <param name="jsRuntime">The JavaScript runtime used to drive browser animation frames.</param>
    protected BlazorGameHost(
        BlazorBitmapRenderSurfaceComponent renderSurface,
        IJSRuntime jsRuntime)
        : base(jsRuntime)
    {
        RenderSurface = renderSurface ?? throw new ArgumentNullException(nameof(renderSurface));
    }

    /// <inheritdoc/>
    protected sealed override BlazorRenderSurfaceComponentBase RenderSurfaceComponent => RenderSurface;

    /// <inheritdoc/>
    protected sealed override RenderSurfaceHostBase RenderSurfaceHost => RenderSurface.Host;

    /// <inheritdoc/>
    protected sealed override void BindSceneCore(Scene scene)
    {
        RenderSurface.Host.Bind(scene, false);
    }
}
