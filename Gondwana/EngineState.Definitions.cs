using Gondwana.Assets;
using Newtonsoft.Json;

namespace Gondwana;

public sealed partial class EngineState
{
    // Keep the historic archive property names, but never deserialize a live archive.
    private sealed class AssetsFileStateEntry
    {
        public string FilePath { get; set; } = string.Empty;
        public string? Password { get; set; }
        public bool UseEncryption { get; set; }
    }

    private abstract class DefinitionStateEntry
    {
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? AssetsFilePath { get; set; }
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string? AssetEntryName { get; set; }

        internal void Validate(string? loosePath, object? definition, object? legacy = null)
        {
            bool hasArchive = AssetsFilePath is not null;
            bool hasEntry = AssetEntryName is not null;
            if (hasArchive != hasEntry ||
                (hasArchive && (string.IsNullOrWhiteSpace(AssetsFilePath) || string.IsNullOrWhiteSpace(AssetEntryName))))
                throw new InvalidDataException($"Packed definition requires both AssetsFilePath and AssetEntryName: '{AssetsFilePath}' / '{AssetEntryName}'.");
            int sources = (loosePath is not null ? 1 : 0) + (definition is not null ? 1 : 0) +
                (hasArchive ? 1 : 0) + (legacy is not null ? 1 : 0);
            if (sources != 1 || (loosePath is not null && string.IsNullOrWhiteSpace(loosePath)))
                throw new InvalidDataException("Definition state entry requires exactly one source: inline Definition, loose path, or AssetsFilePath + AssetEntryName.");
        }
    }

    private static IEnumerable<DefinitionStateEntry> PackedEntries(EngineStateSnapshot snapshot, EngineStateParts parts)
    {
        if (parts.HasFlag(EngineStateParts.Tilesheets) && snapshot.Tilesheets is not null)
            foreach (var entry in snapshot.Tilesheets.Values)
                if (entry?.AssetsFilePath is not null) yield return entry;
        if (parts.HasFlag(EngineStateParts.Cycles) && snapshot.Cycles is not null)
            foreach (var entry in snapshot.Cycles.Values)
                if (entry?.AssetsFilePath is not null) yield return entry;
        if (parts.HasFlag(EngineStateParts.Scenes) && snapshot.Scenes is not null)
            foreach (var entry in snapshot.Scenes)
                if (entry?.AssetsFilePath is not null) yield return entry;
        if (parts.HasFlag(EngineStateParts.Audio) && snapshot.Audio?.AssetsFilePath is not null)
            yield return snapshot.Audio;
        if (parts.HasFlag(EngineStateParts.Sprites) && snapshot.Sprites?.AssetsFilePath is not null)
            yield return snapshot.Sprites;
    }

    private static void ValidateSelectedSources(EngineStateSnapshot snapshot, EngineStateParts parts)
    {
        if (parts.HasFlag(EngineStateParts.Tilesheets) && snapshot.Tilesheets is not null)
            foreach (var entry in snapshot.Tilesheets.Values)
                entry?.Validate(entry.GtsPath, entry.Definition);
        if (parts.HasFlag(EngineStateParts.Cycles) && snapshot.Cycles is not null)
            foreach (var entry in snapshot.Cycles.Values)
                entry?.Validate(entry.GaniPath, entry.Definition);
        if (parts.HasFlag(EngineStateParts.Scenes) && snapshot.Scenes is not null)
            foreach (var entry in snapshot.Scenes)
                entry?.Validate(entry.GscnPath, entry.Definition, entry.LegacyScene);
        if (parts.HasFlag(EngineStateParts.Audio) && snapshot.Audio is { } audio)
            audio.Validate(audio.GsndPath, audio.Definition);
        if (parts.HasFlag(EngineStateParts.Sprites) && snapshot.Sprites is { } sprites)
            sprites.Validate(sprites.GsprPath, sprites.Definition, sprites.LegacySprites);
    }

    private static bool TryPreserve(DefinitionStateEntry entry, DefinitionProvenance? provenance,
        object definition, DefinitionPersistence persistence, string? baseDirectory, out string? loosePath)
    {
        loosePath = null;
        if (persistence != DefinitionPersistence.PreserveSource || provenance is null || !provenance.Matches(definition))
            return false;
        if (provenance.AssetsFilePath is { } archivePath)
        {
            entry.AssetsFilePath = MakeRelativePath(archivePath, baseDirectory);
            entry.AssetEntryName = provenance.AssetEntryName;
            return true;
        }
        if (provenance.FilePath is { } filePath)
        {
            loosePath = MakeRelativePath(filePath, baseDirectory);
            return true;
        }
        return false;
    }

    private static AssetsFile? FindAssetsFile(string path) => AssetsFile.AllAssetsFiles.FirstOrDefault(
        file => !string.IsNullOrWhiteSpace(file.SourcePath) && string.Equals(
            Path.GetFullPath(file.SourcePath), Path.GetFullPath(path),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));

    private static T LoadPacked<T>(DefinitionStateEntry entry, string? baseDirectory, AssetTypes expectedType, Func<AssetsFile, string, T> load)
    {
        var path = ResolvePath(entry.AssetsFilePath!, baseDirectory);
        try
        {
            var archive = FindAssetsFile(path);
            if (archive is null)
            {
                if (!File.Exists(path)) throw new FileNotFoundException("Archive not found.", path);
                archive = AssetsFile.LoadOrCreate(path);
            }
            // EngineState references are exact identities, not the convenience basename
            // aliases accepted by AssetsFile.Get for interactive runtime lookup.
            if (!archive.GetAllEntries().Any(candidate => candidate.AssetType == expectedType &&
                string.Equals(candidate.AssetName, entry.AssetEntryName, StringComparison.Ordinal)))
                throw new InvalidDataException($"Entry does not exist with expected type '{expectedType}'.");
            return load(archive, entry.AssetEntryName!);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            throw new InvalidDataException($"Cannot load packed definition '{entry.AssetEntryName}' from GAF '{path}'.", ex);
        }
    }
}
