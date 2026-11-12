namespace Gondwana.Tooling.Assets;

/// <summary>Identifies a saved GTS frame by physical resource, logical member, region and zero-based coordinates.</summary>
/// <remarks>The drop target must resolve the address and validate the frame before editing its document.</remarks>
public sealed record TilesheetFrameDrag(ResourceAddress Address, string RegionName, int X, int Y) : IAuthoringResourceDrag;
