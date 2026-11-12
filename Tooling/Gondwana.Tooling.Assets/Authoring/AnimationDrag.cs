namespace Gondwana.Tooling.Assets;

/// <summary>Identifies one saved GANI member in a loose file or an exact packed entry.</summary>
/// <remarks>The drop target must resolve and validate the animation before editing its document.</remarks>
public sealed record AnimationDrag(ResourceAddress Address);
