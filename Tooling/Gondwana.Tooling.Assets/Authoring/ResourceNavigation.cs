using Gondwana.Assets;

namespace Gondwana.Tooling.Assets;

/// <summary>An exact physical resource with an optional logical member and diagnostic field path.</summary>
public sealed record ResourceAddress(AssetLocation Location, string? Member = null, string? FieldPath = null)
{
    /// <summary>Creates an address from the shared authoring resource identity.</summary>
    public ResourceAddress(AuthoringResource resource, string? member = null, string? fieldPath = null)
        : this(new AssetLocation(resource.AssetType, Path.GetFullPath(resource.Path), resource.EntryName), member, fieldPath) { }

    /// <summary>Gets the equivalent authoring resolver input without losing packed entry identity.</summary>
    public AuthoringResource Resource => new(Location.Path, Location.AssetType, Location.AssetEntryName);

    /// <summary>Compares physical and logical identities using their established matching rules.</summary>
    public bool Equals(ResourceAddress? other) => other is not null && DefinitionReferenceDiscovery.Same(Location, other.Location) &&
        (Member is null ? other.Member is null : other.Member is not null && DefinitionReferenceQuery.MembersEqual(Location.AssetType, Member, other.Member)) &&
        StringComparer.Ordinal.Equals(FieldPath, other.FieldPath);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(DefinitionReferenceDiscovery.Key(Location),
        Member is null ? 0 : (Location.AssetType == AssetTypes.AnimationDefinition ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal).GetHashCode(Member), FieldPath);

    /// <summary>Describes all identity components without abbreviating paths.</summary>
    public override string ToString() => $"{Location}{(Member is null ? "" : $" :: member '{Member}'")}{(FieldPath is null ? "" : $" :: {FieldPath}")}";
}

/// <summary>Result of read-only navigation, including exactness and actionable fallback information.</summary>
public sealed record ResourceNavigationResult(bool Opened, bool Exact, string Message);

/// <summary>Host adapter for opening or activating an authored resource without changing saved content.</summary>
public interface IResourceNavigator
{
    /// <summary>Activates the requested resource and nearest supported selection, preserving existing editor state.</summary>
    ResourceNavigationResult Navigate(ResourceAddress address);
}

/// <summary>Queries a completed discovery snapshot without rescanning or inferring logical matches from filenames.</summary>
public sealed class DefinitionReferenceQuery(DefinitionReferenceDiscovery scan)
{
    private static bool Matches(AssetLocation candidate, AssetLocation requested) =>
        requested.AssetType == AssetTypes.Misc && requested.AssetEntryName is null && AssetArchiveFormat.IsArchive(requested.Path)
            ? DefinitionReferenceDiscovery.Paths.Equals(candidate.Path, requested.Path)
            : DefinitionReferenceDiscovery.Same(candidate, requested);

    /// <summary>Gets outbound references owned by the physical document or selected member.</summary>
    public IReadOnlyList<DefinitionReference> Dependencies(ResourceAddress source) => scan.References.Where(r =>
        Matches(r.Document, source.Location) &&
        (source.Member is null || MembersEqual(source.Location.AssetType, r.Member, source.Member))).ToArray();

    /// <summary>Gets inbound references with an exact recorded selector when a member is requested.</summary>
    public IReadOnlyList<DefinitionReference> Dependents(ResourceAddress target) => scan.References.Where(r => Matches(r.Target, target.Location)).Where(r =>
        target.Member is null || r.Selector is not null && MembersEqual(target.Location.AssetType, r.Selector, target.Member)).ToArray();

    /// <summary>Gets physical matches whose absent selector cannot establish membership.</summary>
    public IReadOnlyList<DefinitionReference> UnspecifiedDependents(ResourceAddress target) => target.Member is null ? [] :
        scan.References.Where(r => Matches(r.Target, target.Location) && r.Selector is null).ToArray();

    /// <summary>Gets explicit outbound references whose targets failed validation; these are not confirmed dependencies.</summary>
    public IReadOnlyList<DefinitionReference> UnresolvedDependencies(ResourceAddress source) => Unresolved().Where(r =>
        Matches(r.Document, source.Location) && (source.Member is null || MembersEqual(source.Location.AssetType, r.Member, source.Member))).ToArray();

    /// <summary>Gets explicit inbound references whose targets failed validation, retaining the recorded selector boundary.</summary>
    public IReadOnlyList<DefinitionReference> UnresolvedDependents(ResourceAddress target) => Unresolved().Where(r =>
        Matches(r.Target, target.Location) && (target.Member is null || r.Selector is not null && MembersEqual(target.Location.AssetType, r.Selector, target.Member))).ToArray();

    private IEnumerable<DefinitionReference> Unresolved() => scan.Problems.Where(p => p.Reference is not null).Select(p => p.Reference!);

    /// <summary>Uses the native definition matching convention; animation keys are case insensitive.</summary>
    public static bool MembersEqual(AssetTypes type, string left, string right) =>
        (type == AssetTypes.AnimationDefinition ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal).Equals(left, right);
}
