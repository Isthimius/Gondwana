using System.Drawing;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Rendering.Views;

namespace Gondwana.Drawing;

/// <summary>
/// Represents an object that can be drawn on a visual surface, with properties for visibility and stacking order.
/// </summary>
/// <remarks>The <see cref="IDrawable"/> interface defines the contract for drawable objects, including a unique
/// identifier, optional nickname, visibility state, and z-order for determining the drawing order. Implementations of
/// this interface must provide a mechanism to render the object via the <see cref="Draw"/> method.</remarks>
public interface IDrawable
{
    /// <summary>
    /// Auto-assigned unique identifier for the object.
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// Optional human-readable name associated with the object.
    /// </summary>
    string? Nickname { get; }
    
    /// <summary>
    /// Gets a value indicating whether the object is visible.
    /// </summary>
    bool Visible { get; }
    
    /// <summary>
    /// Gets the z-order of the element, which determines its visual stacking order relative to other elements.
    /// Higher z-order values are drawn on top of lower ones.
    /// </summary>
    int ZOrder { get; }

    /// <summary>
    /// Computes the object's destination rectangle in SCREEN pixels for rendering
    /// on the Backbuffer, using the provided View to project from world-space.
    /// </summary>
    /// <param name="view">
    /// The View providing camera, zoom, parallax, and viewport offsets used to
    /// convert the object's world-space bounds into screen-space.
    /// </param>
    /// <returns>
    /// A rectangle in absolute SCREEN pixel coordinates on the Backbuffer.
    /// </returns>
    RectangleF GetDrawLocationScreen(View view);

    /// <summary>
    /// Draws the object to the Backbuffer using the provided SCREEN-space destination
    /// rectangle. The rectangle is assumed to already be projected into screen-space;
    /// no world-to-screen conversion should occur within this method.
    /// </summary>
    /// <param name="backbuffer">
    /// The Backbuffer to draw onto.
    /// </param>
    /// <param name="destRectScreen">
    /// The destination rectangle in absolute SCREEN pixel coordinates on the Backbuffer.
    /// </param>
    void Draw(BackbufferBase backbuffer, RectangleF destRectScreen);
}
