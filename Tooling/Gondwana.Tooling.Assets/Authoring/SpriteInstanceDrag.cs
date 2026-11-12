namespace Gondwana.Tooling.Assets;

/// <summary>Identifies an instance in an exact, live GSPR editing session.</summary>
/// <param name="Address">The GSPR document and its logical definition key.</param>
/// <param name="SpriteId">The stable authored instance ID.</param>
/// <param name="Session">An opaque host token that changes when the document is reopened.</param>
/// <param name="Revision">The live session revision at gesture start.</param>
public sealed record SpriteInstanceDrag(ResourceAddress Address, Guid SpriteId, object Session, long Revision) : IAuthoringResourceDrag;
