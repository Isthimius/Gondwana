using Gondwana.Scenes;

namespace Gondwana.Movement;

/// <summary>Implemented by grid-space IMovable objects that belong to a specific <see cref="Scenes.SceneLayer"/>.</summary>
public interface IMovableOnSceneLayer : IMovable
{
    SceneLayer SceneLayer { get; }
}
