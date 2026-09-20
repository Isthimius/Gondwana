using Gondwana.Assets;
using Newtonsoft.Json;

namespace Gondwana.Audio.GAUD;

/// <summary>
/// Provides loading, saving, conversion, and materialization helpers for Gondwana audio (.gaud) files.
/// </summary>
public static class AudioDefinitionSerializer
{
    private static readonly JsonSerializerSettings Settings = new()
    {
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Ignore,
        DefaultValueHandling = DefaultValueHandling.Include,
        MissingMemberHandling = MissingMemberHandling.Ignore
    };

    public static AudioDefinition Load(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("GAUD file path must be a non-empty string.", nameof(filePath));

        var fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"GAUD file not found: {fullPath}", fullPath);

        try
        {
            var definition = FromJson(File.ReadAllText(fullPath), fullPath);
            ApplyDefaultSource(definition, AudioDefinitionSource.LooseDefinitionFile(fullPath));
            return definition;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Failed to deserialize GAUD file: {fullPath}", ex);
        }
    }

    public static AudioDefinition Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
            throw new ArgumentException("Stream must be readable.", nameof(stream));

        using var reader = new StreamReader(stream, leaveOpen: true);
        return FromJson(reader.ReadToEnd());
    }

    public static AudioDefinition Load(AssetsFile assetsFile, string entryName)
    {
        ArgumentNullException.ThrowIfNull(assetsFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(entryName);

        using var stream = assetsFile.Get(AssetTypes.AudioDefinition, entryName)
            ?? throw new FileNotFoundException($"GAUD asset entry not found: {entryName}", entryName);

        var definition = Load(stream);
        if (definition.Source.Kind == AudioDefinitionSourceKind.None &&
            !string.IsNullOrWhiteSpace(assetsFile.FilePath))
        {
            definition.Source = AudioDefinitionSource.PackedDefinitionFile(
                assetsFile.FilePath,
                entryName);
        }

        return definition;
    }

    public static void Save(string filePath, AudioDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("GAUD file path must be a non-empty string.", nameof(filePath));

        ArgumentNullException.ThrowIfNull(definition);

        var fullPath = Path.GetFullPath(filePath);
        var directory = Path.GetDirectoryName(fullPath) ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var clone = FromJson(ToJson(definition));
        RebaseReferences(clone, GetReferenceBaseDirectory(definition.Source), directory);
        clone.Source = AudioDefinitionSource.LooseDefinitionFile(fullPath);

        File.WriteAllText(fullPath, ToJson(clone));
    }

    public static void Save(string filePath, AudioResourceManager manager)
    {
        ArgumentNullException.ThrowIfNull(manager);
        Save(filePath, FromManager(manager));
    }

    public static string ToJson(AudioDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return JsonConvert.SerializeObject(definition, Settings);
    }

    public static AudioDefinition FromJson(string json) =>
        FromJson(json, sourceDescription: null);

    public static AudioDefinition FromManager(
        AudioResourceManager manager,
        string? baseDirectory = null,
        bool makePathsRelative = false)
    {
        ArgumentNullException.ThrowIfNull(manager);

        var definition = new AudioDefinition
        {
            Resources = manager.GetAll()
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => FromResource(pair.Value))
                .ToList(),
            Source = AudioDefinitionSource.Generated()
        };

        if (makePathsRelative && !string.IsNullOrWhiteSpace(baseDirectory))
            RebaseReferences(definition, oldBaseDirectory: null, baseDirectory);

        return definition;
    }

    public static AudioResourceDefinition FromResource(AudioResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);

        var definition = new AudioResourceDefinition
        {
            Key = resource.Key,
            Volume = resource.Volume,
            Pan = resource.Pan,
            PlaybackSpeed = resource.PlaybackSpeed,
            IsLooping = resource.IsLooping,
            SourceExtension = resource.SourceExtension
        };

        if (resource.AssetIdentifier is { } asset)
        {
            definition.SourceKind = AudioResourceSourceKind.PackedAsset;
            definition.AssetsFilePath = asset.AssetsFile.FilePath;
            definition.AssetEntryName = asset.AssetName;
        }
        else if (!string.IsNullOrWhiteSpace(resource.SourceFilePath))
        {
            definition.SourceKind = AudioResourceSourceKind.LooseFile;
            definition.FilePath = resource.SourceFilePath;
        }
        else if (!string.IsNullOrWhiteSpace(resource.SourceUri))
        {
            definition.SourceKind = AudioResourceSourceKind.Uri;
            definition.SourceUri = resource.SourceUri;
        }
        else
        {
            throw new InvalidOperationException(
                $"AudioResource '{resource.Key}' does not have a persistable file, packed-asset, or URI source.");
        }

        return definition;
    }

    public static IReadOnlyList<AudioResource> LoadIntoManager(
        AudioDefinition definition,
        bool overwriteExisting = true,
        string? baseDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var errors = AudioDefinitionValidator.Validate(definition);
        if (errors.Count != 0)
        {
            throw new InvalidDataException(
                "GAUD definition is invalid:" + Environment.NewLine +
                string.Join(Environment.NewLine, errors.Select(error => "- " + error)));
        }

        baseDirectory ??= GetReferenceBaseDirectory(definition.Source);
        var manager = AudioResourceManager.Instance;
        var loaded = new List<AudioResource>();

        foreach (var resourceDefinition in definition.Resources)
        {
            if (manager.TryGet(resourceDefinition.Key, out var existing) && existing is not null)
            {
                if (!overwriteExisting)
                {
                    ApplySettings(existing, resourceDefinition);
                    loaded.Add(existing);
                    continue;
                }

                manager.Unload(resourceDefinition.Key);
            }

            AudioResource resource = resourceDefinition.SourceKind switch
            {
                AudioResourceSourceKind.LooseFile =>
                    manager.LoadFromFile(
                        resourceDefinition.Key,
                        ResolveReferencePath(resourceDefinition.FilePath!, baseDirectory),
                        resourceDefinition.Volume,
                        resourceDefinition.Pan,
                        resourceDefinition.PlaybackSpeed),

                AudioResourceSourceKind.Uri =>
                    manager.LoadFromUri(
                        resourceDefinition.Key,
                        resourceDefinition.SourceUri!,
                        resourceDefinition.Volume,
                        resourceDefinition.Pan,
                        resourceDefinition.PlaybackSpeed),

                AudioResourceSourceKind.PackedAsset =>
                    LoadPackedResource(resourceDefinition, baseDirectory, manager),

                _ => throw new InvalidDataException(
                    $"Unknown GAUD source kind '{resourceDefinition.SourceKind}'.")
            };

            resource.IsLooping = resourceDefinition.IsLooping;
            loaded.Add(resource);
        }

        return loaded;
    }

    public static IReadOnlyList<AudioResource> LoadIntoManager(
        string filePath,
        bool overwriteExisting = true) =>
        LoadIntoManager(
            Load(filePath),
            overwriteExisting,
            Path.GetDirectoryName(Path.GetFullPath(filePath)));

    private static AudioResource LoadPackedResource(
        AudioResourceDefinition definition,
        string? baseDirectory,
        AudioResourceManager manager)
    {
        var assetsPath = ResolveReferencePath(definition.AssetsFilePath!, baseDirectory);
        var assetsFile = AssetsFile.AllAssetsFiles.FirstOrDefault(
            file => string.Equals(
                Path.GetFullPath(file.FilePath),
                Path.GetFullPath(assetsPath),
                StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException(
                $"GAUD resource '{definition.Key}' references assets file '{assetsPath}', but it is not loaded.");

        using var stream = assetsFile.Get(AssetTypes.Audio, definition.AssetEntryName!)
            ?? throw new InvalidDataException(
                $"GAUD resource '{definition.Key}' references missing audio entry '{definition.AssetEntryName}' in '{assetsPath}'.");

        var extension = !string.IsNullOrWhiteSpace(definition.SourceExtension)
            ? definition.SourceExtension!
            : Path.GetExtension(definition.AssetEntryName!);

        var resource = manager.LoadFromStream(
            definition.Key,
            stream,
            extension,
            definition.Volume,
            definition.Pan,
            definition.PlaybackSpeed);

        resource.SetAssetIdentifier(
            new AssetsFileIdentifier(
                assetsFile,
                AssetTypes.Audio,
                definition.AssetEntryName!));

        return resource;
    }

    private static void ApplySettings(
        AudioResource resource,
        AudioResourceDefinition definition)
    {
        resource.Volume = definition.Volume;
        resource.Pan = definition.Pan;
        resource.PlaybackSpeed = definition.PlaybackSpeed;
        resource.IsLooping = definition.IsLooping;
    }

    private static AudioDefinition FromJson(
        string json,
        string? sourceDescription)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("GAUD JSON content must be a non-empty string.", nameof(json));

        try
        {
            var definition = JsonConvert.DeserializeObject<AudioDefinition>(json, Settings);
            if (definition is null)
            {
                var source = string.IsNullOrWhiteSpace(sourceDescription)
                    ? "GAUD JSON content"
                    : sourceDescription;
                throw new InvalidDataException($"Failed to deserialize {source}. Result was null.");
            }

            definition.Resources ??= [];
            for (int i = 0; i < definition.Resources.Count; i++)
            {
                if (definition.Resources[i] is null)
                    throw new InvalidDataException($"GAUD Resources cannot contain a null entry at index {i}.");
            }

            return definition;
        }
        catch (JsonException ex)
        {
            var source = string.IsNullOrWhiteSpace(sourceDescription)
                ? "GAUD JSON content"
                : sourceDescription;
            throw new InvalidDataException($"Failed to deserialize {source}.", ex);
        }
    }

    private static void ApplyDefaultSource(
        AudioDefinition definition,
        AudioDefinitionSource source)
    {
        if (definition.Source.Kind == AudioDefinitionSourceKind.None)
            definition.Source = source;
    }

    private static string? GetReferenceBaseDirectory(AudioDefinitionSource source)
    {
        return source.Kind switch
        {
            AudioDefinitionSourceKind.LooseDefinitionFile
                when !string.IsNullOrWhiteSpace(source.GaudFilePath) =>
                Path.GetDirectoryName(Path.GetFullPath(source.GaudFilePath)),

            AudioDefinitionSourceKind.PackedDefinitionFile
                when !string.IsNullOrWhiteSpace(source.AssetsFilePath) =>
                Path.GetDirectoryName(Path.GetFullPath(source.AssetsFilePath)),

            _ => null
        };
    }

    private static void RebaseReferences(
        AudioDefinition definition,
        string? oldBaseDirectory,
        string newBaseDirectory)
    {
        foreach (var resource in definition.Resources)
        {
            switch (resource.SourceKind)
            {
                case AudioResourceSourceKind.LooseFile
                    when !string.IsNullOrWhiteSpace(resource.FilePath):
                    resource.FilePath = MakeReferencePath(
                        ResolveReferencePath(resource.FilePath, oldBaseDirectory),
                        newBaseDirectory);
                    break;

                case AudioResourceSourceKind.PackedAsset
                    when !string.IsNullOrWhiteSpace(resource.AssetsFilePath):
                    resource.AssetsFilePath = MakeReferencePath(
                        ResolveReferencePath(resource.AssetsFilePath, oldBaseDirectory),
                        newBaseDirectory);
                    break;
            }
        }
    }

    private static string ResolveReferencePath(
        string path,
        string? baseDirectory)
    {
        if (Path.IsPathRooted(path))
            return Path.GetFullPath(path);

        return string.IsNullOrWhiteSpace(baseDirectory)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(path, baseDirectory);
    }

    private static string MakeReferencePath(
        string path,
        string baseDirectory)
    {
        var fullPath = Path.GetFullPath(path);
        var fullBase = Path.GetFullPath(baseDirectory);
        if (!string.Equals(
                Path.GetPathRoot(fullPath),
                Path.GetPathRoot(fullBase),
                StringComparison.OrdinalIgnoreCase))
        {
            return fullPath;
        }

        return Path.GetRelativePath(fullBase, fullPath);
    }
}
