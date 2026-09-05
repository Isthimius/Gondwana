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
/// <remarks>
/// In browser/WASM builds, the <see cref="BlazorGpuRenderSurfaceComponent"/> owns the browser
/// animation loop through <c>SKGLView</c>. Its paint callback advances the timer-driven engine and
/// presents the GPU frame in the same browser animation-frame callback.
/// </remarks>
[SupportedOSPlatform("browser")]
public abstract class BlazorGpuGameHost : BlazorGameHostBase
{
    /// <summary>Gets the WebGL render surface used for displaying game content.</summary>
    public BlazorGpuRenderSurfaceComponent RenderSurface { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="BlazorGpuGameHost"/>.
    /// </summary>
    /// <param name="renderSurface">The WebGL render surface component.</param>
    /// <param name="jsRuntime">The JavaScript runtime used for shared Blazor browser interop.</param>
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
    protected sealed override bool RenderSurfaceDrivesBrowserFrames => true;

    /// <inheritdoc/>
    protected sealed override void BindSceneCore(Scene scene)
    {
        RenderSurface.Host.Bind(scene, false);
    }
}
