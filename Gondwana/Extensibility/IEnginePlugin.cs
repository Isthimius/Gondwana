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
    /// Called when the engine is shutting down.
    /// </summary>
    /// <param name="engine">The engine being shut down.</param>
    void OnShutdown(Engine engine);
}
