using Gondwana.Scenes;

namespace Gondwana.Physics.Movement;

/// <summary>Implemented by grid-space IMovable objects that belong to a specific <see cref="Scenes.SceneLayer"/>.</summary>
public interface IMovableOnSceneLayer : IMovable
{
    /// <summary>
    /// Gets the <see cref="Scenes.SceneLayer"/> that this movable object belongs to.
    /// </summary>
    /// <value>The scene layer containing this movable object.</value>
    SceneLayer SceneLayer { get; }
}
