using Gondwana.Rendering;
using SkiaSharp;

namespace Gondwana.Extensibility;

/// <summary>
/// Defines lifecycle hooks for extending engine startup, simulation, rendering, and shutdown behavior.
/// </summary>
/// <remarks>
/// <para>
/// Implementations can register with <see cref="EnginePluginRegistry"/> to observe engine events
/// and run custom logic before or after key phases of engine execution.
/// </para>
/// <para>
/// Cycle hooks correspond to simulation updates, while frame-render hooks correspond to presentation
/// frames. In timer-driven mode, a single external <see cref="Engine.Tick"/> may execute zero or more
/// simulation cycles but no more than one rendered frame.
/// </para>
/// </remarks>
public interface IEnginePlugin
{
    /// <summary>
    /// Gets the display name of the plugin.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the plugin version string.
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Called when the engine is initializing.
    /// </summary>
    /// <param name="engine">The engine being initialized.</param>
    void OnInitialize(Engine engine);

    /// <summary>
    /// Called before each simulation cycle.
    /// </summary>
    /// <param name="engine">The engine running the simulation cycle.</param>
    /// <param name="deltaMs">
    /// The simulation delta for this cycle, in seconds. In timer-driven mode this is the fixed-step
    /// duration; in the normal desktop loop it is the elapsed wall-clock time since the previous cycle.
    /// </param>
    void OnPreCycle(Engine engine, double deltaMs);

    /// <summary>
    /// Called before a frame is rendered.
    /// </summary>
    /// <param name="engine">The engine preparing to render.</param>
    /// <param name="deltaMs">The elapsed wall-clock time since the previous frame render, in seconds.</param>
    void OnPreFrameRender(Engine engine, double deltaMs);

    /// <summary>
    /// Called after a frame has been rendered.
    /// </summary>
    /// <param name="engine">The engine that rendered the frame.</param>
    /// <param name="deltaMs">The elapsed wall-clock time since the previous frame render, in seconds.</param>
    void OnPostFrameRender(Engine engine, double deltaMs);

    /// <summary>
    /// Called after each simulation cycle completes.
    /// </summary>
    /// <param name="engine">The engine that completed the simulation cycle.</param>
    /// <param name="deltaMs">
    /// The simulation delta for this cycle, in seconds. In timer-driven mode this is the fixed-step
    /// duration; in the normal desktop loop it is the elapsed wall-clock time since the previous cycle.
    /// </param>
    void OnPostCycle(Engine engine, double deltaMs);

    /// <summary>
    /// Called after all scene content for a surface has been drawn to the backbuffer canvas,
    /// but before the frame is finalised and presented to the display adapter.
    /// </summary>
    /// <param name="engine">The engine that rendered the frame.</param>
    /// <param name="host">
    /// The render surface host whose backbuffer canvas has just been fully populated.
    /// Cast to <see cref="RenderSurfaceHost{TBackbuffer}"/> if you need access to typed
    /// backbuffer properties.
    /// </param>
    /// <param name="canvas">
    /// The <see cref="SKCanvas"/> for the backbuffer, ready to receive post-scene drawing
    /// (overlay effects, color grading, debug annotations, etc.).
    /// The canvas matrix is reset to identity and the clip covers the full surface.
    /// Callers must save and restore canvas state around any operations that change the
    /// matrix or clip region.
    /// </param>
    /// <remarks>
    /// <para>
    /// This hook fires on the thread that owns the canvas:
    /// <list type="bullet">
    /// <item><description>
    ///   <strong>CPU/bitmap surfaces</strong> — called on the engine thread. In the normal desktop
    ///   loop this is the engine background thread; in timer-driven mode it is the thread driving
    ///   <see cref="Engine.Tick"/>.
    /// </description></item>
    /// <item><description>
    ///   <strong>GPU/GL surfaces</strong> — called from the GL paint callback while the
    ///   <c>GRContext</c> is current. Do not marshal GPU operations to a different thread.
    /// </description></item>
    /// </list>
    /// </para>
    /// <para>
    /// This hook is not called when a frame is skipped (scene not dirty) or when the
    /// surface has no configured views.
    /// </para>
    /// </remarks>
    void OnPostRenderCanvas(Engine engine, RenderSurfaceHostBase host, SKCanvas canvas)
    {
    }

    /// <summary>
    /// Called when the engine is shutting down.
    /// </summary>
    /// <param name="engine">The engine being shut down.</param>
    void OnShutdown(Engine engine);
}
