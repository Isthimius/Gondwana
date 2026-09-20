namespace Gondwana.Audio.GAUD;

/// <summary>
/// Provides deterministic validation for GAUD authoring data without creating playback handles.
/// </summary>
public static class AudioDefinitionValidator
{
    public static IReadOnlyList<string> Validate(AudioDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var errors = new List<string>();
        var keys = new HashSet<string>(StringComparer.Ordinal);

        definition.Resources ??= [];

        for (int i = 0; i < definition.Resources.Count; i++)
        {
            var resource = definition.Resources[i];
            var label = $"Resource {i}";

            if (resource is null)
            {
                errors.Add($"{label}: resource definition is null.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(resource.Key))
                errors.Add($"{label}: key is empty.");
            else if (!keys.Add(resource.Key))
                errors.Add($"{label}: duplicate key '{resource.Key}'.");

            if (!Enum.IsDefined(resource.SourceKind))
            {
                errors.Add($"{label}: unknown source kind '{resource.SourceKind}'.");
                continue;
            }

            if (!float.IsFinite(resource.Volume) || resource.Volume < 0f || resource.Volume > 1f)
                errors.Add($"{label}: Volume must be between 0 and 1.");

            if (!float.IsFinite(resource.Pan) || resource.Pan < -1f || resource.Pan > 1f)
                errors.Add($"{label}: Pan must be between -1 and 1.");

            if (!float.IsFinite(resource.PlaybackSpeed) ||
                resource.PlaybackSpeed < AudioResource.MinimumPlaybackSpeed ||
                resource.PlaybackSpeed > AudioResource.MaximumPlaybackSpeed)
            {
                errors.Add(
                    $"{label}: PlaybackSpeed must be between {AudioResource.MinimumPlaybackSpeed} and {AudioResource.MaximumPlaybackSpeed}.");
            }

            switch (resource.SourceKind)
            {
                case AudioResourceSourceKind.LooseFile:
                    if (string.IsNullOrWhiteSpace(resource.FilePath))
                        errors.Add($"{label}: loose FilePath is empty.");
                    break;

                case AudioResourceSourceKind.PackedAsset:
                    if (string.IsNullOrWhiteSpace(resource.AssetsFilePath))
                        errors.Add($"{label}: AssetsFilePath is empty.");
                    if (string.IsNullOrWhiteSpace(resource.AssetEntryName))
                        errors.Add($"{label}: AssetEntryName is empty.");
                    if (string.IsNullOrWhiteSpace(resource.SourceExtension) &&
                        string.IsNullOrWhiteSpace(Path.GetExtension(resource.AssetEntryName)))
                    {
                        errors.Add($"{label}: packed audio requires SourceExtension when AssetEntryName has no extension.");
                    }
                    break;

                case AudioResourceSourceKind.Uri:
                    if (string.IsNullOrWhiteSpace(resource.SourceUri))
                        errors.Add($"{label}: SourceUri is empty.");
                    else if (!Uri.TryCreate(resource.SourceUri, UriKind.RelativeOrAbsolute, out _))
                        errors.Add($"{label}: SourceUri is invalid.");
                    break;
            }
        }

        return errors;
    }
}
