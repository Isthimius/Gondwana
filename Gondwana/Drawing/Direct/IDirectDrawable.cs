using Gondwana.Rendering;
using System.Drawing;

namespace Gondwana.Drawing.Direct;

/// <summary>
/// Represents any drawable object (movable or static) that can render to a surface,
/// report its position and bounds, and optionally support movement or composition.
/// </summary>
public interface IDirectDrawable : IDisposable
{
    /// <summary>
    /// Occurs when the object is being disposed.
    /// </summary>
    event EventHandler<IDirectDrawable>? Disposing;

    /// <summary>
    /// Name associated with the object.
    /// </summary>
    string Name { get; set; }

    /// <summary>
    /// The rendering surface to which this drawable belongs.
    /// </summary>
    RenderSurfaceHostBase RenderSurfaceHost { get; }

    /// <summary>
    /// The bounding rectangle of this drawable in pixel space.
    /// </summary>
    Rectangle Bounds { get; }

    /// <summary>
    /// Gets the z-order of the element, which determines its visual stacking order relative to other elements.
    /// Higher z-order values are drawn on top of lower ones.
    /// </summary>
    int ZOrder { get; }

    /// <summary>
    /// Updates the state of the object based on the specified tick value.
    /// </summary>
    /// <param name="tick">The current tick value from Engine.Cycle().</param>
    void Update(long tick);
}
