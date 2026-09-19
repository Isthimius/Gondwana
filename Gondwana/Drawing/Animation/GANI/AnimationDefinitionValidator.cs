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
