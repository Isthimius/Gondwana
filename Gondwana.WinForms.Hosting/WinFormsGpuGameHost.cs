using Gondwana.WinForms.Rendering;

namespace Gondwana.WinForms.Hosting;

/// <summary>
/// Provides a base class for hosting Gondwana games in Windows Forms applications using
/// GPU-accelerated (OpenGL) rendering via <see cref="WinFormGpuRenderSurfaceControl"/>.
/// </summary>
public abstract class WinFormsGpuGameHost : WinFormsGameHostBase
{
    /// <summary>
    /// Gets the GPU render surface control used for displaying game content.
    /// </summary>
    public WinFormGpuRenderSurfaceControl RenderSurface { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="WinFormsGpuGameHost"/> class.
    /// </summary>
    /// <param name="renderSurface">The GPU render surface control to use for rendering.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="renderSurface"/> is null.</exception>
    protected WinFormsGpuGameHost(WinFormGpuRenderSurfaceControl renderSurface)
        : this(new SurfaceInitialization(renderSurface))
    {
    }

    private WinFormsGpuGameHost(SurfaceInitialization initialization)
        : base(initialization.RenderSurface, initialization.RenderSurface.Host, initialization.RenderSurface.Host.Bind)
    {
        RenderSurface = initialization.RenderSurface;
    }

    private sealed class SurfaceInitialization
    {
        public WinFormGpuRenderSurfaceControl RenderSurface { get; }

        public SurfaceInitialization(WinFormGpuRenderSurfaceControl renderSurface)
        {
            RenderSurface = renderSurface ?? throw new ArgumentNullException(nameof(renderSurface));
        }
    }
}
