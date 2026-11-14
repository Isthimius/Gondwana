namespace Gondwana.Tooling.Studio.Core.Extensibility;

/// <summary>Manifest compatibility independently of activation.</summary>
public enum PluginCompatibility
{
    Unknown,
    Compatible,
    Incompatible
}

/// <summary>Outcome of discovery or a subsequent lifecycle failure.</summary>
public enum PluginLoadStatus
{
    Loaded,
    Incompatible,
    InvalidPackage,
    FailedToLoad,
    Conflict,
    Disabled
}

/// <summary>
/// Immutable discovery row. A package with multiple implementations has one row per type;
/// packages rejected before type inspection have a null implementation. Unknown metadata is null.
/// </summary>
public sealed record PluginDiscoveryInfo(
    string? Id,
    string? DisplayName,
    string? Version,
    string SourceDirectory,
    string? EntryAssembly,
    bool IsLegacy,
    PluginCompatibility Compatibility,
    PluginLoadStatus Status,
    string? Diagnostic,
    string? Implementation = null);
