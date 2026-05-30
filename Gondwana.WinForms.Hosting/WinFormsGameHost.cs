using Gondwana.WinForms.Rendering;

namespace Gondwana.WinForms.Hosting;

/// <summary>
/// Provides a base class for hosting Gondwana games in Windows Forms applications.
/// </summary>
public abstract class WinFormsGameHost : WinFormsGameHostBase
{
    /// <summary>
    /// Gets the render surface control used for displaying game content.
    /// </summary>
    public WinFormBitmapRenderSurfaceControl RenderSurface { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="WinFormsGameHost"/> class.
    /// </summary>
    /// <param name="renderSurface">The render surface control to use for rendering.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="renderSurface"/> is null.</exception>
    protected WinFormsGameHost(WinFormBitmapRenderSurfaceControl renderSurface)
        : this(new SurfaceInitialization(renderSurface))
    {
    }

    private WinFormsGameHost(SurfaceInitialization initialization)
        : base(initialization.RenderSurface, initialization.RenderSurface.Host, initialization.RenderSurface.Host.Bind)
    {
        RenderSurface = initialization.RenderSurface;
    }

    private sealed class SurfaceInitialization
    {
        public WinFormBitmapRenderSurfaceControl RenderSurface { get; }

        public SurfaceInitialization(WinFormBitmapRenderSurfaceControl renderSurface)
        {
            RenderSurface = renderSurface ?? throw new ArgumentNullException(nameof(renderSurface));
        }
    }
}
