using System.Numerics;

namespace Gondwana.Drawing.Direct;

/// <summary>
/// Defines a container that can hold and manage a collection of direct composite children.
/// </summary>
public interface IDirectCompositeContainer
{
    /// <summary>
    /// Gets the read-only collection of child elements in this container.
    /// </summary>
    IReadOnlyCollection<IDirectCompositeChild> Children { get; }

    /// <summary>
    /// Adds a child element to this container.
    /// </summary>
    /// <param name="child">The child element to add to the container.</param>
    /// <param name="keepCurrentOffset">If true, maintains the child's current offset; otherwise, resets the offset.</param>
    /// <param name="explicitLocalOffsetPx">Optional explicit local offset in pixels to apply to the child.</param>
    /// <returns>The current container instance for method chaining.</returns>
    IDirectCompositeContainer Add(IDirectCompositeChild child, bool keepCurrentOffset = true, Vector2? explicitLocalOffsetPx = null);

    /// <summary>
    /// Removes a child element from this container.
    /// </summary>
    /// <param name="child">The child element to remove from the container.</param>
    /// <returns>The current container instance for method chaining.</returns>
    IDirectCompositeContainer Remove(IDirectCompositeChild child);

    /// <summary>
    /// Removes all child elements from this container.
    /// </summary>
    /// <returns>The current container instance for method chaining.</returns>
    IDirectCompositeContainer Clear();
}