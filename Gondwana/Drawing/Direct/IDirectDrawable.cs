using Gondwana.Rendering;
using System.Drawing;

namespace Gondwana.Drawing.Direct;

/// <summary>
/// Represents any drawable object (movable or static) that can render directly to a RenderSurfaceHost,
/// report its position and bounds, and optionally support movement or composition.
/// </summary>
public interface IDirectDrawable : IDrawable, IDisposable
{
    /// <summary>
    /// Occurs when the object is being disposed.
    /// </summary>
    event EventHandler<IDirectDrawable>? Disposing;

    /// <summary>
    /// The rendering surface to which this drawable belongs.
    /// </summary>
    RenderSurfaceHostBase RenderSurfaceHost { get; }

    /// <summary>
    /// Gets the drawing mode that determines where and how this drawable is rendered.
    /// </summary>
    /// <remarks>
    /// The drawing mode defines the coordinate space and rendering lifecycle:
    /// <list type="bullet">
    /// <item>
    /// <term><see cref="DirectDrawingMode.SceneLayer"/></term>
    /// <description>
    /// Renders in world space as part of a scene layer and is affected by camera position,
    /// zoom, and parallax.
    /// </description>
    /// </item>
    /// <item>
    /// <term><see cref="DirectDrawingMode.View"/></term>
    /// <description>
    /// Renders in screen space at the view level and is independent of camera movement,
    /// making it suitable for UI, overlays, and debug visuals.
    /// </description>
    /// </item>
    /// </list>
    /// </remarks>
    DirectDrawingMode Mode { get; }

    /// <summary>
    /// The bounding rectangle of this drawable in SCREEN space.
    /// </summary>
    Rectangle ScreenBounds { get; }

    /// <summary>
    /// The bounding rectangle of this drawable in WORLD space.
    /// </summary>
    Rectangle WorldBounds { get; }

    /// <summary>
    /// Updates the state of the object based on the specified tick value.
    /// </summary>
    /// <param name="tick">The current tick value from Engine.Cycle().</param>
    void Update(long tick);
}
