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
    /// <summary>Neutral session-local measurements for this host; null when the collector source limit was reached.</summary>
    public Diagnostics.TelemetrySource? Telemetry { get; internal set; }
    internal RenderFrameMailbox FrameMailbox { get; } = new();
    internal bool UsesRenderFrameSnapshots => !OperatingSystem.IsBrowser() && Backbuffer is GpuBackbuffer;
    internal virtual void ProduceRenderFrameSnapshot(long tick) { }

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

    /// <summary>Actual scale used to fit this Backbuffer into its current adapter.</summary>
    public float PresentationScale => RenderSurfaceAdapter?.PresentationScale ?? 0f;

    internal virtual void RequestRenderScale(float scale) { }
    internal virtual void InvalidatePresentation() { }

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
    /// Occurs after a GPU render/snapshot call when a subscriber requests timing for the shared
    /// render-state synchronization gate.
    /// </summary>
    public event Action<GpuRenderSynchronizationDiagnostics>? GpuRenderSynchronizationDiagnosticsCalculated;

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

        if (UsesRenderFrameSnapshots)
            return ReplayRenderFrameSnapshot();

        bool collectSynchronizationDiagnostics =
            GpuRenderSynchronizationDiagnosticsCalculated is not null;
        long waitStarted = collectSynchronizationDiagnostics
            ? HighResTimer.GetCurrentTick()
            : 0;
        long lockAcquired = 0;
        long lockReleased = 0;
        SKImage? image;

        RenderStateSynchronization.EnterGpuRender();
        try
        {
            if (collectSynchronizationDiagnostics)
                lockAcquired = HighResTimer.GetCurrentTick();

            var tick = HighResTimer.GetCurrentTick();

            RenderToBackbuffer(tick);
            Backbuffer.EndFrame();

            image = Backbuffer.Snapshot();

            Backbuffer.BeginFrame();

            if (collectSynchronizationDiagnostics)
                lockReleased = HighResTimer.GetCurrentTick();
        }
        finally
        {
            RenderStateSynchronization.ExitGpuRender();
        }

        if (collectSynchronizationDiagnostics)
        {
            GpuRenderSynchronizationDiagnosticsCalculated?.Invoke(
                new GpuRenderSynchronizationDiagnostics(
                    HighResTimer.GetDuration(waitStarted, lockAcquired) * 1000d,
                    HighResTimer.GetDuration(lockAcquired, lockReleased) * 1000d));
        }

        return image;
    }

    private SKImage? ReplayRenderFrameSnapshot()
    {
        var diagnostics = GpuRenderSynchronizationDiagnosticsCalculated;
        long telemetryGeneration = Telemetry?.BeginSample() ?? 0;
        bool collectDiagnostics = diagnostics is not null || telemetryGeneration != 0;
        var slot = FrameMailbox.TryAcquire();
        if (slot is null) return Backbuffer.Snapshot();

        long acquired = !collectDiagnostics ? 0 : HighResTimer.GetCurrentTick();
        var counters = !collectDiagnostics ? default : FrameMailbox.Counters;
        long replayEnd = acquired;
        long pictureReplayTicks = 0;
        long backbufferFlushTicks = 0;
        long snapshotTicks = 0;
        int commands = !collectDiagnostics ? 0 : slot.Frame!.CommandCount;
        double age = !collectDiagnostics ? 0 :
            HighResTimer.GetDuration(slot.Frame!.ProducedTick, acquired) * 1000d;

        try
        {
            var frame = slot.Frame!;

            // A logical resize invalidates geometry, but context/MSAA recreation does
            // not: recordings contain CPU resources and are uploaded by the current GL context.
            if (frame.Width != Backbuffer.Width || frame.Height != Backbuffer.Height)
            {
                long snapshotStart = !collectDiagnostics ? 0 : HighResTimer.GetCurrentTick();
                var resizedSnapshot = Backbuffer.Snapshot();
                if (collectDiagnostics)
                    snapshotTicks = HighResTimer.GetCurrentTick() - snapshotStart;
                return resizedSnapshot;
            }

            Backbuffer.BeginFrame();

            long pictureStart = !collectDiagnostics ? 0 : HighResTimer.GetCurrentTick();
            frame.Replay(Backbuffer.Canvas);
            long pictureEnd = !collectDiagnostics ? 0 : HighResTimer.GetCurrentTick();

            Backbuffer.EndFrame();
            long flushEnd = !collectDiagnostics ? 0 : HighResTimer.GetCurrentTick();

            if (collectDiagnostics)
            {
                pictureReplayTicks = pictureEnd - pictureStart;
                backbufferFlushTicks = flushEnd - pictureEnd;
                replayEnd = flushEnd;
            }

            long snapshotStartTick = !collectDiagnostics ? 0 : HighResTimer.GetCurrentTick();
            var snapshot = Backbuffer.Snapshot();
            if (collectDiagnostics)
                snapshotTicks = HighResTimer.GetCurrentTick() - snapshotStartTick;

            return snapshot;
        }
        finally
        {
            try { Backbuffer.BeginFrame(); }
            finally { FrameMailbox.Release(slot); }

            if (collectDiagnostics)
            {
                double replayMs = HighResTimer.GetDuration(acquired, replayEnd) * 1000d;
                double pictureMs = HighResTimer.GetDuration(0, pictureReplayTicks) * 1000d;
                double flushMs = HighResTimer.GetDuration(0, backbufferFlushTicks) * 1000d;
                double snapshotMs = HighResTimer.GetDuration(0, snapshotTicks) * 1000d;
                if (telemetryGeneration != 0)
                {
                    Telemetry!.Record(telemetryGeneration, "replay.cpu.ms", replayMs);
                    Telemetry.Record(telemetryGeneration, "picture.cpu.ms", pictureMs);
                    Telemetry.Record(telemetryGeneration, "backbuffer.flush.cpu.ms", flushMs);
                    Telemetry.Record(telemetryGeneration, "snapshot.cpu.ms", snapshotMs);
                    Telemetry.Record(telemetryGeneration, "snapshot.age.ms", age);
                    Telemetry.Record(telemetryGeneration, "mailbox.published.lifetime", counters.Published);
                    Telemetry.Record(telemetryGeneration, "mailbox.dropped.lifetime", counters.Dropped);
                    Telemetry.Record(telemetryGeneration, "mailbox.slots", counters.InUse);
                    Telemetry.Record(telemetryGeneration, "snapshot.commands.approximate", commands);
                }
                diagnostics?.Invoke(new(0, 0)
                {
                    ReplayMilliseconds = replayMs,
                    PictureReplayMilliseconds = pictureMs,
                    BackbufferFlushMilliseconds = flushMs,
                    SnapshotMilliseconds = snapshotMs,
                    SnapshotAgeMilliseconds = age,
                    PublishedSnapshots = counters.Published,
                    DroppedSnapshots = counters.Dropped,
                    SnapshotSlotsInUse = counters.InUse,
                    SnapshotCommandCount = commands
                });
            }
        }
    }

    /// <summary>
    /// Renders the current scene frame and draws the GPU backbuffer surface directly to another
    /// GPU canvas. Linear scaling uses a scoped GPU texture snapshot because direct surface drawing
    /// does not support sampling options. No CPU pixel transfer is performed.
    /// </summary>
    /// <remarks>
    /// Call only from the active GPU paint callback while both surfaces share the current
    /// <see cref="GRContext"/>. The destination is drawn at the origin using the destination
    /// canvas's current transform, followed by the adapter's aspect-preserving presentation transform.
    /// </remarks>
    /// <param name="destinationCanvas">The active platform GPU canvas.</param>
    /// <returns><see langword="true"/> when a GPU surface was rendered and drawn; otherwise <see langword="false"/>.</returns>
    public bool GlRenderToCanvas(SKCanvas destinationCanvas)
    {
        ArgumentNullException.ThrowIfNull(destinationCanvas);

        if (!Backbuffer.IsGlThreadRendered)
            return false;

        if (UsesRenderFrameSnapshots)
        {
            using var image = ReplayRenderFrameSnapshot();
            DrawCurrentSurface(destinationCanvas);
            return true;
        }

        RenderStateSynchronization.EnterGpuRender();
        try
        {
            var tick = HighResTimer.GetCurrentTick();

            RenderToBackbuffer(tick);
            Backbuffer.EndFrame();

            try
            {
                DrawCurrentSurface(destinationCanvas);
                return true;
            }
            finally
            {
                Backbuffer.BeginFrame();
            }
        }
        finally
        {
            RenderStateSynchronization.ExitGpuRender();
        }
    }

    /// <summary>
    /// Draws the existing GPU backbuffer surface directly to another GPU canvas without rendering
    /// a new scene frame. Linear scaling uses a scoped GPU texture snapshot without CPU pixel transfer.
    /// </summary>
    /// <remarks>
    /// Call only from the active GPU paint callback while both surfaces share the current
    /// <see cref="GRContext"/>. This is intended for presentation loops that run more frequently
    /// than Gondwana's configured foreground/render cadence.
    /// </remarks>
    /// <param name="destinationCanvas">The active platform GPU canvas.</param>
    /// <returns><see langword="true"/> when the current GPU surface was drawn; otherwise <see langword="false"/>.</returns>
    public bool GlDrawCurrentFrameToCanvas(SKCanvas destinationCanvas)
    {
        ArgumentNullException.ThrowIfNull(destinationCanvas);

        if (!Backbuffer.IsGlThreadRendered)
            return false;

        DrawCurrentSurface(destinationCanvas);
        return true;
    }

    private void DrawCurrentSurface(SKCanvas canvas)
    {
        var presentation = RenderSurfaceAdapter?.Presentation ?? default;
        canvas.Clear(Backbuffer.ClearColor);
        if (presentation.Scale <= 0) return;
        canvas.Save();
        try
        {
            canvas.Translate(presentation.DestinationRect.Left, presentation.DestinationRect.Top);
            canvas.Scale(presentation.Scale);
            bool needsLinearSampling = Engine.Instance.Configuration.RenderScalingFilter == RenderScalingFilter.Linear &&
                (presentation.Scale != 1f ||
                 presentation.DestinationRect.Left != MathF.Floor(presentation.DestinationRect.Left) ||
                 presentation.DestinationRect.Top != MathF.Floor(presentation.DestinationRect.Top));
            if (needsLinearSampling)
            {
                // Skia's DrawSurface does not expose sampling and ignores SKPaint.FilterQuality.
                // A scoped snapshot is a GPU texture view, not a readback or an intermediate copy.
                using var image = Backbuffer.Snapshot();
                canvas.DrawImage(image, 0, 0, RenderSurfaceAdapterBase.PresentationSampling);
            }
            else
            {
                canvas.DrawSurface(Backbuffer.Canvas.Surface, 0, 0);
            }
        }
        finally { canvas.Restore(); }
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
        lock (RenderStateSynchronization.SyncRoot)
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
        Telemetry?.Dispose();
        if (disposing)
        {
            FrameMailbox.Dispose();
            Effects.Dispose();
        }

        RenderSurfaceHostRegistry.Unregister(this);
    }
}
