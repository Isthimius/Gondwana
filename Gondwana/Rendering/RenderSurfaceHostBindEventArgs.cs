using Gondwana.Scenes;

namespace Gondwana.Rendering;

/// <summary>
/// Provides data for the <see cref="RenderSurfaceHost{TBackbuffer}.BindToScene"/> event.
/// </summary>
/// <remarks>
/// This event argument class contains information about scene binding operations, including
/// references to both the previously bound scene (if any) and the newly bound scene.
/// Use this to respond to scene changes in render surface hosts.
/// </remarks>
public class RenderSurfaceHostBindEventArgs : EventArgs
{
    /// <summary>
    /// Gets the scene that was previously bound to the render surface host before the binding operation.
    /// </summary>
    /// <value>
    /// The <see cref="Scene"/> instance that was previously bound, or <see langword="null"/> if no scene
    /// was previously bound.
    /// </value>
    public Scene? OldScene { get; }

    /// <summary>
    /// Gets the scene that is now bound to the render surface host after the binding operation.
    /// </summary>
    /// <value>
    /// The <see cref="Scene"/> instance that is now bound, or <see langword="null"/> if the scene
    /// was unbound without binding a new one.
    /// </value>
    public Scene? NewScene { get; }

    private RenderSurfaceHostBindEventArgs() : this(null, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RenderSurfaceHostBindEventArgs"/> class.
    /// </summary>
    /// <param name="oldScene">
    /// The scene that was previously bound to the render surface host, or <see langword="null"/> if
    /// no scene was previously bound.
    /// </param>
    /// <param name="newScene">
    /// The scene that is now bound to the render surface host, or <see langword="null"/> if the
    /// scene was unbound without binding a new one.
    /// </param>
    public RenderSurfaceHostBindEventArgs(Scene? oldScene, Scene? newScene)
    {
        OldScene = oldScene;
        NewScene = newScene;
    }
}