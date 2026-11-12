using Gondwana.Assets;
using Gondwana.Drawing.Sprites.GSPR;
using Gondwana.Tooling.Assets;

namespace Gondwana.Tooling.Sprites.Editing;

public sealed partial class SpriteDocument
{
    /// <summary>Tracks in-memory edits, member switches and save-location changes for active gestures.</summary>
    public long Revision { get; private set; }

    /// <summary>The exact saved identity of this authoring session, if available.</summary>
    public AssetLocation? Location => PackedLocation ??
        (FilePath is { } path ? new AssetLocation(AssetTypes.SpriteDefinition, path) : null);

    private void NotifyChanged()
    {
        Revision++;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Resolves a stable ID in the requested logical member of this editing session.</summary>
    public SpriteInstanceDefinition? FindInstance(string memberKey, Guid id)
    {
        if (id == Guid.Empty) return null;
        var members = Definitions.Where(member => member.Key == memberKey).Take(2).ToArray();
        if (members.Length != 1) return null;
        var instances = members[0].Sprites.Where(sprite => sprite.Id == id).Take(2).ToArray();
        return instances.Length == 1 ? instances[0] : null;
    }

    /// <summary>Places an existing instance and records its validated scene reference in one history operation.</summary>
    public bool PlaceInstance(SpriteInstanceDrag source, ResourceAddress scene, string layerId, System.Drawing.PointF position)
    {
        if (!ReferenceEquals(source.Session, this) || source.Revision != Revision || Location is not { } location ||
            source.Address.Member is not { } key || new ResourceAddress(location, key) != source.Address with { FieldPath = null } ||
            FindInstance(key, source.SpriteId) is not { } sprite)
            throw new InvalidOperationException("The source sprite changed. Start a new placement.");
        if (scene.Location.AssetType != AssetTypes.SceneDefinition || string.IsNullOrWhiteSpace(scene.Member) ||
            string.IsNullOrWhiteSpace(layerId) || !float.IsFinite(position.X) || !float.IsFinite(position.Y))
            throw new InvalidDataException("Placement needs a valid scene, layer and finite position.");
        if (History.InTransaction || History.IsExecuting)
            throw new InvalidOperationException("Finish the current sprite edit before placing a sprite.");

        var member = Definitions.Single(definition => definition.Key == key);
        string sceneId = scene.Member;
        var references = member.SceneSources.Where(item => item.SceneId == sceneId).ToArray();
        if (references.Length > 1)
            throw new InvalidDataException($"Multiple scene sources are registered for '{sceneId}'.");
        if (references.Length == 1)
        {
            var existing = AuthoringResourceResolver.SpriteSceneSource(references[0], BaseDirectory);
            if (existing != scene with { FieldPath = null })
                throw new InvalidDataException($"'{sceneId}' is already registered from a different scene source.");
        }

        bool moves = sprite.SceneId != sceneId || sprite.SceneLayerId != layerId || sprite.Position != position;
        if (!moves && references.Length != 0)
            return false;

        const string description = "Place sprite in scene";
        using var edit = History.BeginTransaction(description);
        if (references.Length == 0)
        {
            if (scene.Location.AssetEntryName is { } entry)
                AddPackedSceneSource(member, sceneId, scene.Location.Path, entry);
            else
                SetLooseSceneSource(member, sceneId, scene.Location.Path);
        }
        if (moves)
        {
            Edit(sprite, description, () =>
            {
                sprite.SceneId = sceneId;
                sprite.SceneLayerId = layerId;
                sprite.Position = position;
            });
        }
        edit.Commit();
        return true;
    }
}
