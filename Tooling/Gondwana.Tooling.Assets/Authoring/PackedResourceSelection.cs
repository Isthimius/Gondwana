using Gondwana.Assets;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Scenes.GSCN;
using Gondwana.Tooling.Tilesheets.Sources;

namespace Gondwana.Tooling.Assets;

/// <summary>An exact physical entry and, for definition documents, a separate logical member.</summary>
public sealed record PackedResourceSelection(AuthoringResource Resource, string? Member = null, string? SourceExtension = null)
{
    /// <summary>Reads selectable logical identities without substituting a first member.</summary>
    public static IReadOnlyList<string> GetMembers(AssetPackageCatalog packages, AuthoringResource resource)
    {
        RequirePacked(resource);
        using var stream = packages.OpenAsset(resource.Path, resource.AssetType, resource.EntryName!);
        IReadOnlyList<string> members = resource.AssetType switch
        {
            AssetTypes.TilesheetDefinition => TilesheetDefinitionSerializer.LoadAll(stream).Select(x => x.Name).ToArray(),
            AssetTypes.AnimationDefinition => AnimationDefinitionSerializer.LoadAll(stream).Select(x => x.Key).ToArray(),
            AssetTypes.SceneDefinition => SceneDefinitionSerializer.LoadAll(stream).Select(x => x.ID).ToArray(),
            AssetTypes.Audio or AssetTypes.Image => [],
            _ => throw new InvalidDataException($"Unsupported dependency type: {resource.AssetType}.")
        };
        if (members.Count == 0 && resource.AssetType is not (AssetTypes.Audio or AssetTypes.Image))
            throw new InvalidDataException($"Entry '{resource.EntryName}' contains no logical definitions.");
        return members;
    }

    /// <summary>Validates the exact typed entry and selected definition before a caller changes its document.</summary>
    public void Validate(AssetPackageCatalog packages, AssetTypes expectedType)
    {
        RequirePacked(Resource);
        if (Resource.AssetType != expectedType)
            throw new InvalidDataException($"Expected {expectedType}, received {Resource.AssetType}.");
        var resolver = new AuthoringResourceResolver(packages);
        IReadOnlyList<string> errors;
        switch (expectedType)
        {
            case AssetTypes.TilesheetDefinition:
                RequireMember();
                errors = TilesheetDefinitionValidator.Validate(resolver.LoadTilesheet(Resource, Member));
                break;
            case AssetTypes.AnimationDefinition:
                RequireMember();
                errors = AnimationDefinitionValidator.Validate(resolver.LoadAnimation(Resource, Member));
                break;
            case AssetTypes.SceneDefinition:
                RequireMember();
                errors = SceneDefinitionValidator.Validate(resolver.LoadScene(Resource, Member));
                break;
            case AssetTypes.Audio:
            case AssetTypes.Image:
                if (Member is not null) throw new InvalidDataException("Media entries have no logical definition member.");
                if (expectedType == AssetTypes.Audio && string.IsNullOrWhiteSpace(Path.GetExtension(Resource.EntryName)) &&
                    string.IsNullOrWhiteSpace(SourceExtension))
                    throw new InvalidDataException("This audio entry has no extension. Supply its source extension (for example .wav).");
                using (var stream = packages.OpenAsset(Resource.Path, expectedType, Resource.EntryName!))
                    if (stream.ReadByte() < 0) throw new InvalidDataException("The selected media entry is empty.");
                return;
            default:
                throw new InvalidDataException($"Unsupported dependency type: {expectedType}.");
        }
        if (errors.Count > 0)
            throw new InvalidDataException($"Invalid definition '{Member}' in '{Resource.EntryName}':{Environment.NewLine}" + string.Join(Environment.NewLine, errors));
    }

    private void RequireMember()
    {
        if (string.IsNullOrWhiteSpace(Member)) throw new InvalidDataException("Select a logical definition member.");
    }

    private static void RequirePacked(AuthoringResource resource)
    {
        if (string.IsNullOrWhiteSpace(resource.EntryName)) throw new InvalidDataException("Select an exact packed entry name.");
    }
}
