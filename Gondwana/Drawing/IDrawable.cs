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
    /// Draws the object on the specified surface.
    /// </summary>
    void Draw();
}
