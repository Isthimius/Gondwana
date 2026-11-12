namespace Gondwana.Tooling.Assets;

/// <summary>Identifies one saved scene member and its exact layer.</summary>
public sealed record SceneLayerDrag(ResourceAddress Address, string LayerId) : IAuthoringResourceDrag;

/// <summary>A typed drag of a saved authoring resource.</summary>
public interface IAuthoringResourceDrag
{
    /// <summary>Gets the exact physical location and logical definition identity.</summary>
    ResourceAddress Address { get; }
}
