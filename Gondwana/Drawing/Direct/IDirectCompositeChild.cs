using Gondwana.Physics.Movement;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;

namespace Gondwana.Drawing.Direct;

/// <summary>
/// Defines a movable direct drawable that may be owned by a
/// <see cref="DirectComposite"/>.
/// </summary>
/// <remarks>
/// <para>
/// The interface provides the coordinate target and visual operations required
/// for recursive composition without requiring <see cref="DirectComposite"/> to
/// recognize concrete child types.
/// </para>
/// <para>
/// Container implementations should apply visibility, Z-order, opacity, and
/// fade operations recursively to their descendants.
/// </para>
/// </remarks>
public interface IDirectCompositeChild : IDirectDrawable, IMovable
{
    /// <summary>
    /// Gets the scene layer used by a scene-layer child, or
    /// <see langword="null"/> for a view child.
    /// </summary>
    SceneLayer? SceneLayer { get; }

    /// <summary>
    /// Gets the view used by a view child, or <see langword="null"/> for a
    /// scene-layer child.
    /// </summary>
    View? View { get; }

    /// <summary>
    /// Applies a visibility value to this child.
    /// </summary>
    /// <param name="visible">The visibility value to apply.</param>
    void SetIsVisible(bool visible);

    /// <summary>
    /// Applies a Z-order value to this child.
    /// </summary>
    /// <param name="zOrder">The Z-order value to apply.</param>
    void SetZOrder(int zOrder);

    /// <summary>
    /// Applies an opacity value to this child.
    /// </summary>
    /// <param name="opacity">The opacity value in the range 0 through 1.</param>
    void SetOpacity(float opacity);

    /// <summary>
    /// Fades this child to the requested opacity.
    /// </summary>
    /// <param name="targetOpacity">The target opacity in the range 0 through 1.</param>
    /// <param name="durationSec">The fade duration in seconds.</param>
    void FadeTo(float targetOpacity, float durationSec);
}
