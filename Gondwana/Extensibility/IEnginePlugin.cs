using Gondwana.Rendering;
using SkiaSharp;

namespace Gondwana.Extensibility;

/// <summary>
/// Defines lifecycle hooks for extending engine startup, update, render, and shutdown behavior.
/// </summary>
/// <remarks>
/// Implementations can register with <see cref="EnginePluginRegistry"/> to observe engine events
/// and run custom logic before or after key phases of the engine loop.
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
    /// Called before each engine cycle.
    /// </summary>
    /// <param name="engine">The engine running the cycle.</param>
    /// <param name="deltaMs">The elapsed time since the previous cycle, in milliseconds.</param>
    void OnPreCycle(Engine engine, double deltaMs);

    /// <summary>
    /// Called before a frame is rendered.
    /// </summary>
    /// <param name="engine">The engine preparing to render.</param>
    /// <param name="deltaMs">The elapsed time since the previous frame render, in milliseconds.</param>
    void OnPreFrameRender(Engine engine, double deltaMs);

    /// <summary>
    /// Called after a frame has been rendered.
    /// </summary>
    /// <param name="engine">The engine that rendered the frame.</param>
    /// <param name="deltaMs">The elapsed time since the previous frame render, in milliseconds.</param>
    void OnPostFrameRender(Engine engine, double deltaMs);

    /// <summary>
    /// Called after each engine cycle completes.
    /// </summary>
    /// <param name="engine">The engine that completed the cycle.</param>
    /// <param name="deltaMs">The elapsed time since the previous cycle, in milliseconds.</param>
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
    /// This hook fires on the same thread that owns the canvas:
    /// <list type="bullet">
    /// <item><description>
    ///   <strong>CPU/bitmap surfaces</strong> — called on the engine background thread.
    /// </description></item>
    /// <item><description>
    ///   <strong>GPU/GL surfaces</strong> — called on the GL thread from within
    ///   <c>PaintSurface</c>, while the <c>GRContext</c> is current.
    ///   Do not marshal GPU operations to a different thread.
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
