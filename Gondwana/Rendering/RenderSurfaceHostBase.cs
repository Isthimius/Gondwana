using Gondwana.Effects;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;
using Gondwana.Timers;
using SkiaSharp;

namespace Gondwana.Rendering;

/// <summary>
/// Represents a base class for hosting a render surface, providing functionality for managing rendering operations,
/// backbuffer access, and integration with platform-specific adapters.
/// </summary>
public abstract class RenderSurfaceHostBase : IDisposable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RenderSurfaceHostBase"/> class and registers it
    /// with the <see cref="RenderSurfaceHostRegistry"/>.
    /// </summary>
    /// <remarks>
    /// Registration ensures the render surface host is tracked for lifecycle management and can be
    /// enumerated by other system components.
    /// </remarks>
    protected RenderSurfaceHostBase()
    {
        Effects = new EffectsManager(this);
        RenderSurfaceHostRegistry.Register(this);
    }

    /// <summary>
    /// Finalizes an instance of the <see cref="RenderSurfaceHostBase"/> class, ensuring resources
    /// are released when the object is garbage collected.
    /// </summary>
    ~RenderSurfaceHostBase() => Dispose(false);

    /// <summary>
    /// Gets the in-memory <see cref="BackbufferBase"/> associated with the current rendering context.
    /// </summary>
    public abstract BackbufferBase Backbuffer { get; }

    /// <summary>
    /// Gets the source <see cref="Scenes.Scene"/> used for rendering operations.
    /// </summary>
    public abstract Scene Scene { get; }

    /// <summary>
    /// Gets the platform-specific <see cref="RenderSurfaceAdapterBase"/> responsible
    /// for rendering the image from the <see cref="Backbuffer"/>.
    /// </summary>
    public abstract RenderSurfaceAdapterBase? RenderSurfaceAdapter { get; }

    /// <summary>
    /// Gets the view manager that controls camera positions, viewports, and multi-view rendering
    /// for this render surface host.
    /// </summary>
    /// <value>
    /// The <see cref="ViewManager"/> instance managing all views associated with this render surface host.
    /// </value>
    /// <remarks>
    /// The view manager enables split-screen, picture-in-picture, and minimap rendering by managing
    /// multiple views with independent cameras and viewports.
    /// </remarks>
    public abstract ViewManager ViewManager { get; }

    /// <summary>
    /// Gets the manager that owns presentation effects for this render surface.
    /// </summary>
    public EffectsManager Effects { get; }

    /// <summary>
    /// Renders the current scene frame on the active GL/WebGL thread and returns a snapshot of the
    /// GPU backbuffer ready to be drawn to the platform surface.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method is intended to be called from the platform GPU paint callback while its
    /// <see cref="GRContext"/> is current. It is the GPU equivalent of the engine loop's
    /// <c>RenderToBackbuffer</c> + <c>PresentBackbufferToAdapter</c> pair; rendering and snapshot
    /// creation happen synchronously on the GPU-owning thread, with no CPU pixel transfer.
    /// </para>
    /// <para>
    /// The returned <see cref="SKImage"/> is a lightweight GPU-backed view of the backbuffer
    /// texture. The caller <strong>must dispose</strong> it after drawing (typically with a
    /// <c>using</c> statement), and must do so within the same GPU paint callback to avoid
    /// aliasing with the next frame's rendering pass.
    /// </para>
    /// <para>
    /// Returns <see langword="null"/> when <see cref="BackbufferBase.IsGlThreadRendered"/> is
    /// <see langword="false"/> (i.e. this is not a GPU-rendered surface).
    /// </para>
    /// </remarks>
    /// <returns>
    /// A GPU-backed <see cref="SKImage"/> snapshot of the rendered frame, or
    /// <see langword="null"/> if this surface does not use GL-thread rendering.
    /// </returns>
    public SKImage? GlRenderAndSnapshot()
    {
        if (!Backbuffer.IsGlThreadRendered)
            return null;

        var tick = HighResTimer.GetCurrentTick();

        RenderToBackbuffer(tick);
        Backbuffer.EndFrame();

        var img = Backbuffer.Snapshot();

        Backbuffer.BeginFrame();

        return img;
    }

    /// <summary>
    /// Returns a snapshot of the current GPU backbuffer without rendering a new scene frame.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is intended for GPU presentation loops that run more frequently than Gondwana's
    /// configured foreground/render cadence. It allows the platform surface to re-blit the most
    /// recently rendered backbuffer while preserving <see cref="Gondwana.Configuration.EngineConfiguration.TargetFPS"/>.
    /// </para>
    /// <para>
    /// Call only from the active GPU paint callback while the backbuffer's <see cref="GRContext"/>
    /// is current. The returned <see cref="SKImage"/> must be disposed before that callback returns.
    /// </para>
    /// </remarks>
    /// <returns>
    /// A GPU-backed snapshot of the current backbuffer, or <see langword="null"/> when the surface
    /// does not use GL-thread rendering.
    /// </returns>
    public SKImage? GlSnapshotCurrentFrame()
    {
        if (!Backbuffer.IsGlThreadRendered)
            return null;

        return Backbuffer.Snapshot();
    }

    /// <summary>
    /// Renders all visible scene layers for every configured view onto the backbuffer.
    /// Called as part of DoForegroundTasks().
    /// </summary>
    internal abstract void RenderToBackbuffer(long tick);

    /// <summary>
    /// Runs as part of DoForegroundTasks(). This renders the DirtyRectangle
    /// area of the backbuffer to the adapter.
    /// </summary>
    internal abstract void PresentBackbufferToAdapter();

    /// <summary>
    /// Releases all resources used by this <see cref="RenderSurfaceHostBase"/> instance.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method unregisters the render surface host from the <see cref="RenderSurfaceHostRegistry"/>
    /// and releases any managed resources. Derived classes should override <see cref="Dispose(bool)"/>
    /// to release additional resources specific to their implementation.
    /// </para>
    /// <para>
    /// After calling <see cref="Dispose()"/>, this instance should not be used. Calling
    /// <see cref="Dispose()"/> multiple times is safe and has no additional effect.
    /// </para>
    /// </remarks>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases resources used by this <see cref="RenderSurfaceHostBase"/> instance and unregisters
    /// it from the <see cref="RenderSurfaceHostRegistry"/>.
    /// </summary>
    /// <param name="disposing">
    /// <see langword="true"/> to release both managed and unmanaged resources;
    /// <see langword="false"/> to release only unmanaged resources (called from finalizer).
    /// </param>
    /// <remarks>
    /// This method always unregisters the instance from the registry. Derived classes should override
    /// this method to release additional resources but must call the base implementation to ensure
    /// proper unregistration.
    /// </remarks>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
            Effects.Dispose();

        RenderSurfaceHostRegistry.Unregister(this);
    }
}
