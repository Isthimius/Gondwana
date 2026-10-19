using Gondwana.Assets;

namespace Gondwana.Tooling.Studio.WinForms.Panels;

/// <summary>A packed resource's physical identity, independent of its display label.</summary>
internal sealed record PackedAssetNodeIdentity(string ArchivePath, AssetTypes AssetType, string EntryName);
