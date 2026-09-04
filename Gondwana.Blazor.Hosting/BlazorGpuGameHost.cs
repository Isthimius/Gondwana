using System.Runtime.Versioning;
using Gondwana.Blazor.Rendering;
using Gondwana.Rendering;
using Gondwana.Scenes;
using Microsoft.JSInterop;

namespace Gondwana.Blazor.Hosting;

/// <summary>
/// Provides a base class for Gondwana Blazor games rendered through WebGL and
/// <see cref="Gondwana.Rendering.Backbuffers.GpuBackbuffer"/>.
/// </summary>
[SupportedOSPlatform("browser")]
public abstract class BlazorGpuGameHost : BlazorGameHostBase
{
    /// <summary>Gets the WebGL render surface used for displaying game content.</summary>
    public BlazorGpuRenderSurfaceComponent RenderSurface { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="BlazorGpuGameHost"/>.
    /// </summary>
    /// <param name="renderSurface">The WebGL render surface component.</param>
    /// <param name="jsRuntime">The JavaScript runtime used to drive browser animation frames.</param>
    protected BlazorGpuGameHost(
        BlazorGpuRenderSurfaceComponent renderSurface,
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
