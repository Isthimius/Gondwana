namespace Gondwana.Drawing.Animation.GANI;

/// <summary>
/// Provides deterministic GANI authoring diagnostics without creating runtime cycles.
/// </summary>
public static class AnimationDefinitionValidator
{
    /// <summary>
    /// Validates the structural contents of an animation definition.
    /// Tilesheet existence and frame bounds are validated when the definition is materialized.
    /// </summary>
    public static IReadOnlyList<string> Validate(AnimationDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(definition.Key))
            errors.Add("Animation key is empty.");

        if (!double.IsFinite(definition.ThrottleTime) || definition.ThrottleTime < 0)
            errors.Add("ThrottleTime must be a finite, non-negative number.");

        if (!Enum.IsDefined(definition.CycleType))
            errors.Add($"Unknown cycle type '{definition.CycleType}'.");

        definition.TilesheetSources ??= [];
        var sourceNames = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < definition.TilesheetSources.Count; i++)
        {
            var source = definition.TilesheetSources[i];
            var label = $"Tilesheet source {i}";

            if (source is null)
            {
                errors.Add($"{label}: source definition is null.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(source.Tilesheet))
            {
                errors.Add($"{label}: tilesheet name is empty.");
            }
            else if (!sourceNames.Add(source.Tilesheet))
            {
                errors.Add(
                    $"{label}: duplicate source for tilesheet '{source.Tilesheet}'.");
            }

            if (!Enum.IsDefined(source.Kind))
            {
                errors.Add($"{label}: unknown source kind '{source.Kind}'.");
                continue;
            }

            switch (source.Kind)
            {
                case AnimationTilesheetSourceKind.LooseDefinitionFile:
                    if (string.IsNullOrWhiteSpace(source.GtsPath))
                        errors.Add($"{label}: loose GTS source path is empty.");
                    break;

                case AnimationTilesheetSourceKind.PackedDefinitionFile:
                    if (string.IsNullOrWhiteSpace(source.AssetsFilePath))
                        errors.Add($"{label}: assets file path is empty.");

                    if (string.IsNullOrWhiteSpace(source.AssetEntryName))
                        errors.Add($"{label}: packed GTS entry name is empty.");
                    break;
            }
        }

        definition.Frames ??= [];
        if (definition.Frames.Count == 0)
            errors.Add("Animation must contain at least one frame.");

        for (int i = 0; i < definition.Frames.Count; i++)
        {
            var frame = definition.Frames[i];
            var label = $"Frame {i}";

            if (frame is null)
            {
                errors.Add($"{label}: frame definition is null.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(frame.Tilesheet))
                errors.Add($"{label}: tilesheet name is empty.");

            if (string.IsNullOrWhiteSpace(frame.RegionName))
                errors.Add($"{label}: region name is empty.");

            if (frame.XTile < 0 || frame.YTile < 0)
                errors.Add($"{label}: frame coordinates cannot be negative.");
        }

        return errors;
    }
}
