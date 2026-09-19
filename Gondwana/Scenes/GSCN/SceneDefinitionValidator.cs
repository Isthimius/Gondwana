using Gondwana.Drawing.Coordinates;
using Gondwana.Physics.Collisions;

namespace Gondwana.Scenes.GSCN;

/// <summary>
/// Provides deterministic validation for GSCN authoring data without constructing runtime scenes.
/// </summary>
public static class SceneDefinitionValidator
{
    private static readonly string[] StandardGroups =
        ["WorldStatic", "Actors", "Projectiles", "Triggers"];

    private static readonly string[] StandardProfiles =
        [CollisionProfileNames.World, CollisionProfileNames.Actor, CollisionProfileNames.Projectile, CollisionProfileNames.Sensor];

    /// <summary>
    /// Validates structural scene, layer, tile, frame, and collision-profile metadata.
    /// </summary>
    public static IReadOnlyList<string> Validate(SceneDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var errors = new List<string>();
        var knownGroups = new HashSet<string>(StandardGroups, StringComparer.OrdinalIgnoreCase);
        var definedGroups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var group in definition.CollisionGroups ?? [])
        {
            if (string.IsNullOrWhiteSpace(group))
            {
                errors.Add("CollisionGroups contains an empty name.");
                continue;
            }

            if (!definedGroups.Add(group))
                errors.Add($"Collision group '{group}' is duplicated.");

            knownGroups.Add(group);
        }

        var knownProfiles = new HashSet<string>(StandardProfiles, StringComparer.OrdinalIgnoreCase);
        var definedProfiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var profile in definition.CollisionProfiles ?? [])
        {
            if (profile is null)
            {
                errors.Add("CollisionProfiles contains a null entry.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(profile.Name))
                errors.Add("CollisionProfiles contains a profile with an empty name.");
            else
            {
                if (!definedProfiles.Add(profile.Name))
                    errors.Add($"Collision profile '{profile.Name}' is duplicated.");
                knownProfiles.Add(profile.Name);
            }

            if (string.IsNullOrWhiteSpace(profile.CollisionGroup))
                errors.Add($"Collision profile '{profile.Name}' has an empty CollisionGroup.");
            else if (!knownGroups.Contains(profile.CollisionGroup))
                errors.Add($"Collision profile '{profile.Name}' references unknown group '{profile.CollisionGroup}'.");

            foreach (var group in profile.CollidesWith ?? [])
            {
                if (!knownGroups.Contains(group))
                    errors.Add($"Collision profile '{profile.Name}' references unknown CollidesWith group '{group}'.");
            }
        }

        var layerIds = new HashSet<string>(StringComparer.Ordinal);
        int layerIndex = 0;
        foreach (var layer in definition.Layers ?? [])
        {
            if (layer is null)
            {
                errors.Add($"Layers contains a null entry at index {layerIndex++}.");
                continue;
            }

            var label = string.IsNullOrWhiteSpace(layer.ID)
                ? $"Layer #{layerIndex}"
                : $"Layer '{layer.ID}'";

            if (!string.IsNullOrWhiteSpace(layer.ID) && !layerIds.Add(layer.ID))
                errors.Add($"{label}: duplicate ID.");

            if (layer.Columns < 0 || layer.Rows < 0)
                errors.Add($"{label}: Columns and Rows cannot be negative.");

            if (layer.TileWidth <= 0 || layer.TileHeight <= 0)
                errors.Add($"{label}: TileWidth and TileHeight must be positive.");

            if (!float.IsFinite(layer.Parallax))
                errors.Add($"{label}: Parallax must be finite.");

            if (!Enum.IsDefined(layer.CoordinateSystemType))
                errors.Add($"{label}: unknown coordinate system type '{layer.CoordinateSystemType}'.");

            if (string.IsNullOrWhiteSpace(layer.DefaultTileCollisionProfile))
                errors.Add($"{label}: DefaultTileCollisionProfile cannot be empty.");
            else if (!knownProfiles.Contains(layer.DefaultTileCollisionProfile))
                errors.Add($"{label}: unknown default collision profile '{layer.DefaultTileCollisionProfile}'.");

            var coordinates = new HashSet<(int X, int Y)>();
            foreach (var tile in layer.Tiles ?? [])
            {
                if (tile is null)
                {
                    errors.Add($"{label}: Tiles contains a null entry.");
                    continue;
                }

                var tileLabel = $"{label}, tile ({tile.X}, {tile.Y})";
                if (!coordinates.Add((tile.X, tile.Y)))
                    errors.Add($"{tileLabel}: duplicate tile entry.");

                if (tile.X < 0 || tile.Y < 0 || tile.X >= layer.Columns || tile.Y >= layer.Rows)
                    errors.Add($"{tileLabel}: coordinates are outside the layer grid.");

                if (!Enum.IsDefined(tile.CollisionType))
                    errors.Add($"{tileLabel}: unknown collision type '{tile.CollisionType}'.");

                if (!string.IsNullOrWhiteSpace(tile.CollisionProfileName) &&
                    !knownProfiles.Contains(tile.CollisionProfileName))
                {
                    errors.Add($"{tileLabel}: unknown collision profile '{tile.CollisionProfileName}'.");
                }

                if (tile.Frame is { } frame)
                {
                    if (string.IsNullOrWhiteSpace(frame.Tilesheet))
                        errors.Add($"{tileLabel}: frame Tilesheet cannot be empty.");

                    if (string.IsNullOrWhiteSpace(frame.RegionName))
                        errors.Add($"{tileLabel}: frame RegionName cannot be empty.");

                    if (frame.XTile < 0 || frame.YTile < 0)
                        errors.Add($"{tileLabel}: frame coordinates cannot be negative.");
                }
            }

            layerIndex++;
        }

        return errors;
    }
}
