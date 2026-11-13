namespace Gondwana.Tooling.Studio.Core.Extensibility;

/// <summary>Identifies the layout used to discover a Studio plugin.</summary>
public enum StudioPluginSource { ManifestPackage, LegacyAssembly }

/// <summary>Describes the result of checking the declared Studio contract version.</summary>
public enum StudioPluginCompatibility { Unknown, Compatible, Incompatible }

/// <summary>Describes the discovery or activation result for a package or legacy plugin.</summary>
public enum StudioPluginStatus
{
    Loaded,
    PartiallyLoaded,
    InvalidPackage,
    Incompatible,
    FailedToLoad,
    Duplicate,
    Disabled
}

/// <summary>A discovery result with the information needed to locate and diagnose a plugin.</summary>
/// <remarks>Missing manifest metadata remains null. Legacy IDs identify individual implementation types.</remarks>
public sealed record StudioPluginDiscovery(
    string? Id,
    string? DisplayName,
    string? Version,
    string SourceDirectory,
    string? EntryAssembly,
    StudioPluginSource Source,
    StudioPluginCompatibility Compatibility,
    StudioPluginStatus Status,
    string? Diagnostic = null,
    string? Description = null,
    string? Author = null,
    int? ContractVersion = null);
