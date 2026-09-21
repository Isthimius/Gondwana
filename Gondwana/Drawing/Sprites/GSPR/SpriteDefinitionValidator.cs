namespace Gondwana.Drawing.Sprites.GSPR;

/// <summary>Non-mutating structural validation; authoring paths are never opened.</summary>
public static class SpriteDefinitionValidator
{
    public static IReadOnlyList<string> Validate(SpriteDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var errors = new List<string>();
        var ids = new HashSet<Guid>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        if (definition.Sprites is null)
            errors.Add("Sprites collection is required.");
        foreach (var sprite in definition.Sprites ?? [])
        {
            if (sprite is null) { errors.Add("Sprites cannot contain null entries."); continue; }
            var label = $"Sprite '{sprite.Nickname ?? sprite.Id.ToString()}'";
            if (sprite.Id != Guid.Empty && !ids.Add(sprite.Id)) errors.Add($"{label}: duplicate Id.");
            if (!string.IsNullOrWhiteSpace(sprite.Nickname) && !names.Add(sprite.Nickname)) errors.Add($"{label}: duplicate Nickname.");
            if (string.IsNullOrWhiteSpace(sprite.SceneId)) errors.Add($"{label}: SceneId is required.");
            if (string.IsNullOrWhiteSpace(sprite.SceneLayerId)) errors.Add($"{label}: SceneLayerId is required.");
            // A default/unassigned frame is valid in the existing Sprite persistence contract.
            if (sprite.Frame is { } frame)
            {
                if (string.IsNullOrWhiteSpace(frame.Tilesheet)) errors.Add($"{label}: Frame Tilesheet is required.");
                if (string.IsNullOrWhiteSpace(frame.RegionName)) errors.Add($"{label}: Frame RegionName is required.");
                if (frame.XTile < 0 || frame.YTile < 0) errors.Add($"{label}: Frame coordinates must be nonnegative.");
            }
            if (!float.IsFinite(sprite.Position.X) || !float.IsFinite(sprite.Position.Y)) errors.Add($"{label}: Position must be finite.");
            if (!float.IsFinite(sprite.Rotation)) errors.Add($"{label}: Rotation must be finite.");
            if (sprite.RenderSize.Width < 0 || sprite.RenderSize.Height < 0) errors.Add($"{label}: RenderSize must be nonnegative.");
            if (sprite.ZOrder < 1) errors.Add($"{label}: ZOrder must be at least 1.");
            if (!Enum.IsDefined(sprite.HorizAlign) || !Enum.IsDefined(sprite.VertAlign)) errors.Add($"{label}: invalid alignment.");
            if (!Enum.IsDefined(sprite.CollisionType)) errors.Add($"{label}: invalid CollisionType.");
        }
        var sheets = new HashSet<string>(StringComparer.Ordinal);
        foreach (var source in definition.TilesheetSources ?? [])
        {
            if (source is null) { errors.Add("Null TilesheetSources entry."); continue; }
            if (string.IsNullOrWhiteSpace(source.Tilesheet) || !sheets.Add(source.Tilesheet)) errors.Add("Invalid or duplicate TilesheetSources identity.");
            ValidateSource(source.Kind == SpriteTilesheetSourceKind.LooseDefinitionFile, Enum.IsDefined(source.Kind), source.GtsPath, source.AssetsFilePath, source.AssetEntryName, "TilesheetSources", errors);
        }
        var scenes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var source in definition.SceneSources ?? [])
        {
            if (source is null) { errors.Add("Null SceneSources entry."); continue; }
            if (string.IsNullOrWhiteSpace(source.SceneId) || !scenes.Add(source.SceneId)) errors.Add("Invalid or duplicate SceneSources identity.");
            ValidateSource(source.Kind == SpriteSceneSourceKind.LooseDefinitionFile, Enum.IsDefined(source.Kind), source.GscnPath, source.AssetsFilePath, source.AssetEntryName, "SceneSources", errors);
        }
        return errors;
    }

    private static void ValidateSource(bool loose, bool validKind, string? path, string? archive, string? entry, string label, List<string> errors)
    {
        if (!validKind || (loose ? string.IsNullOrWhiteSpace(path) : string.IsNullOrWhiteSpace(archive) || string.IsNullOrWhiteSpace(entry)))
            errors.Add($"{label}: source location is incomplete or invalid.");
    }
}
