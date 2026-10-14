using Gondwana.Assets;
using Newtonsoft.Json;

namespace Gondwana.Audio.GSND;

public static partial class AudioDefinitionSerializer
{
    /// <summary>Reads a complete document, accepting historical singleton JSON.</summary>
    /// <param name="json">The JSON text.</param>
    /// <param name="sourceIdentity">Optional physical filename or entry name for legacy identity inference.</param>
    /// <returns>The complete definition document.</returns>
    public static AudioDefinitionDocument FromDocumentJson(string json, string? sourceIdentity = null) => new()
    {
        Definitions = DefinitionDocumentJson.Read<AudioDefinition>(json, json => ParseDefinition(json, null), definition => definition.Key, definition => { if (string.IsNullOrWhiteSpace(definition.Key)) definition.Key = Path.GetFileNameWithoutExtension(sourceIdentity) is { Length: > 0 } inferred ? inferred : "audio"; })
    };

    /// <summary>Serializes a complete document with a Definitions envelope.</summary>
    /// <param name="document">The complete document.</param>
    /// <returns>Canonical document JSON.</returns>
    public static string ToDocumentJson(AudioDefinitionDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        DefinitionDocumentJson.Check(document.Definitions, definition => definition.Key);
        return JsonConvert.SerializeObject(document, Settings);
    }

    /// <summary>Loads every logical definition from a physical file.</summary>
    /// <param name="filePath">The physical document path.</param>
    /// <returns>The complete document with member-specific load stamps.</returns>
    public static AudioDefinitionDocument LoadDocument(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var path = Path.GetFullPath(filePath);
        var document = FromDocumentJson(File.ReadAllText(path), path);
        foreach (var definition in document.Definitions)
        {
            definition.Source = AudioDefinitionSource.LooseDefinitionFile(path);
            definition.LoadStamp = new(definition, filePath: path, definitionIdentity: definition.Key);
        }
        return document;
    }

    /// <summary>Loads a complete document from a stream without claiming physical provenance.</summary>
    /// <param name="stream">A readable stream, left open.</param>
    /// <returns>The complete document.</returns>
    public static AudioDefinitionDocument LoadDocument(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead) throw new ArgumentException("Stream must be readable.", nameof(stream));
        using var reader = new StreamReader(stream, leaveOpen: true);
        return FromDocumentJson(reader.ReadToEnd());
    }

    /// <summary>Loads a complete document from a typed GAF entry.</summary>
    /// <param name="assetsFile">The containing archive.</param>
    /// <param name="entryName">The physical document entry name.</param>
    /// <returns>The complete document with shared physical and distinct logical provenance.</returns>
    public static AudioDefinitionDocument LoadDocument(AssetsFile assetsFile, string entryName)
    {
        ArgumentNullException.ThrowIfNull(assetsFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(entryName);
        using var stream = assetsFile.Get(AssetTypes.AudioDefinition, entryName)
            ?? throw new FileNotFoundException($"GSND asset entry not found: {entryName}", entryName);
        using var reader = new StreamReader(stream);
        var document = FromDocumentJson(reader.ReadToEnd(), entryName);
        foreach (var definition in document.Definitions)
        {
            definition.Source = string.IsNullOrWhiteSpace(assetsFile.SourcePath)
                ? AudioDefinitionSource.None()
                : AudioDefinitionSource.PackedDefinitionFile(assetsFile.SourcePath, entryName);
            definition.LoadStamp = new(definition, assetsFilePath: assetsFile.SourcePath, entryName: entryName, definitionIdentity: definition.Key);
        }
        return document;
    }

    /// <summary>Validates the document identities and every member's format-specific data.</summary>
    /// <param name="document">The document to inspect.</param>
    /// <returns>Member-qualified diagnostics.</returns>
    public static IReadOnlyList<string> ValidateDocument(AudioDefinitionDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var errors = new List<string>();
        try { DefinitionDocumentJson.Check(document.Definitions, definition => definition.Key); }
        catch (InvalidDataException ex) { errors.Add(ex.Message); }
        foreach (var definition in document.Definitions ?? [])
            if (definition is not null)
                errors.AddRange(AudioDefinitionValidator.Validate(definition).Select(error => $"Audio '{definition.Key}': {error}"));
        return errors;
    }

    /// <summary>Saves all definitions, rebasing each member without changing the input objects.</summary>
    /// <param name="filePath">The destination physical path.</param>
    /// <param name="document">The entire document to save.</param>
    public static void SaveDocument(string filePath, AudioDefinitionDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        SaveAll(filePath, document.Definitions);
    }

    /// <summary>Saves all supplied definitions in one canonical document.</summary>
    /// <param name="filePath">The destination physical path.</param>
    /// <param name="definitions">All logical definitions to persist.</param>
    public static void SaveAll(string filePath, IEnumerable<AudioDefinition> definitions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(definitions);
        var path = Path.GetFullPath(filePath);
        var members = definitions.ToList();
        DefinitionDocumentJson.Check(members, definition => definition.Key, allowUnnamedSingleton: true);
        var saved = members.Select(definition => PrepareDocumentMember(definition, path)).ToList();
        var json = ToDocumentJson(new() { Definitions = saved });
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json);
    }

    private static AudioDefinition PrepareDocumentMember(AudioDefinition definition, string path)
    {
        var clone = ParseDefinition(ToJson(definition), null);
        if (string.IsNullOrWhiteSpace(clone.Key)) clone.Key = Path.GetFileNameWithoutExtension(path);
        var origin = definition.Source.GsndFilePath ?? definition.Source.AssetsFilePath;
        var oldDirectory = definition.LoadStamp?.ReferenceBaseDirectory ??
            (string.IsNullOrWhiteSpace(origin) ? null : Path.GetDirectoryName(Path.GetFullPath(origin)));
        var newDirectory = Path.GetDirectoryName(path)!;
        string? RebasePath(string? value) => string.IsNullOrWhiteSpace(value) || (oldDirectory is null && !Path.IsPathRooted(value)) ? value :
            Path.GetRelativePath(newDirectory, oldDirectory is null ? Path.GetFullPath(value) : Path.GetFullPath(value, oldDirectory));
        foreach (var resource in clone.Resources)
        {
            resource.FilePath = RebasePath(resource.FilePath);
            resource.AssetsFilePath = RebasePath(resource.AssetsFilePath);
        }
        clone.Source = AudioDefinitionSource.LooseDefinitionFile(path);
        return clone;
    }

    /// <summary>Loads all logical definitions in document order.</summary>
    /// <param name="filePath">The document path.</param>
    /// <returns>All definitions.</returns>
    public static IReadOnlyList<AudioDefinition> LoadAll(string filePath) => LoadDocument(filePath).Definitions;

    /// <summary>Loads a logical member by its case-sensitive Key.</summary>
    /// <param name="filePath">The document path.</param>
    /// <param name="key">The logical member identity; null requires a singleton.</param>
    /// <returns>The selected definition.</returns>
    public static AudioDefinition Load(string filePath, string? key) =>
        DefinitionDocumentJson.Select(LoadAll(filePath), definition => definition.Key, key);

    /// <summary>Loads all logical definitions in document order.</summary>
    /// <param name="stream">The readable stream, left open.</param>
    /// <returns>All definitions.</returns>
    public static IReadOnlyList<AudioDefinition> LoadAll(Stream stream) => LoadDocument(stream).Definitions;

    /// <summary>Loads a logical member by its case-sensitive Key.</summary>
    /// <param name="stream">The readable stream, left open.</param>
    /// <param name="key">The logical member identity; null requires a singleton.</param>
    /// <returns>The selected definition.</returns>
    public static AudioDefinition Load(Stream stream, string? key) =>
        DefinitionDocumentJson.Select(LoadAll(stream), definition => definition.Key, key);

    /// <summary>Loads all logical definitions in document order.</summary>
    /// <param name="assetsFile">The archive.</param>
    /// <param name="entryName">The document entry name.</param>
    /// <returns>All definitions.</returns>
    public static IReadOnlyList<AudioDefinition> LoadAll(AssetsFile assetsFile, string entryName) => LoadDocument(assetsFile, entryName).Definitions;

    /// <summary>Loads a logical member by its case-sensitive Key.</summary>
    /// <param name="assetsFile">The archive.</param>
    /// <param name="entryName">The document entry name.</param>
    /// <param name="key">The logical member identity; null requires a singleton.</param>
    /// <returns>The selected definition.</returns>
    public static AudioDefinition Load(AssetsFile assetsFile, string entryName, string? key) =>
        DefinitionDocumentJson.Select(LoadAll(assetsFile, entryName), definition => definition.Key, key);

    /// <summary>Loads one selected audio group using the manager's existing collision policy.</summary>
    /// <param name="filePath">The document path.</param>
    /// <param name="key">The logical audio group key.</param>
    /// <param name="overwriteExisting">Whether existing resource keys are replaced.</param>
    /// <returns>The loaded or reused audio resources.</returns>
    public static IReadOnlyList<AudioResource> LoadIntoManager(string filePath, string key, bool overwriteExisting = true) =>
        LoadIntoManager(Load(filePath, key), overwriteExisting);

    /// <summary>Loads every audio group, restoring previous resources if any member fails.</summary>
    /// <param name="filePath">The document path.</param>
    /// <param name="overwriteExisting">Whether existing resource keys are replaced.</param>
    /// <returns>One resource collection per logical definition, in document order.</returns>
    public static IReadOnlyList<IReadOnlyList<AudioResource>> LoadAllIntoManager(string filePath, bool overwriteExisting = true) =>
        LoadDocumentIntoManager(LoadDocument(filePath), overwriteExisting);

    /// <summary>Loads one selected audio group using the manager's existing collision policy.</summary>
    /// <param name="assetsFile">The archive.</param>
    /// <param name="entryName">The physical document entry.</param>
    /// <param name="key">The logical audio group key.</param>
    /// <param name="overwriteExisting">Whether existing resource keys are replaced.</param>
    /// <returns>The loaded or reused audio resources.</returns>
    public static IReadOnlyList<AudioResource> LoadIntoManager(AssetsFile assetsFile, string entryName, string key, bool overwriteExisting = true) =>
        LoadIntoManager(Load(assetsFile, entryName, key), overwriteExisting);

    /// <summary>Loads every audio group, restoring previous resources if any member fails.</summary>
    /// <param name="assetsFile">The archive.</param>
    /// <param name="entryName">The physical document entry.</param>
    /// <param name="overwriteExisting">Whether existing resource keys are replaced.</param>
    /// <returns>One resource collection per logical definition, in document order.</returns>
    public static IReadOnlyList<IReadOnlyList<AudioResource>> LoadAllIntoManager(AssetsFile assetsFile, string entryName, bool overwriteExisting = true) =>
        LoadDocumentIntoManager(LoadDocument(assetsFile, entryName), overwriteExisting);

    /// <summary>Loads all logical groups without flattening their definition roots.</summary>
    /// <param name="document">The audio document.</param>
    /// <param name="overwriteExisting">Whether existing resource keys are replaced.</param>
    /// <returns>One result collection per member.</returns>
    public static IReadOnlyList<IReadOnlyList<AudioResource>> LoadDocumentIntoManager(AudioDefinitionDocument document, bool overwriteExisting = true)
    {
        var errors = ValidateDocument(document);
        if (errors.Count != 0) throw new InvalidDataException(string.Join(Environment.NewLine, errors));
        var manager = AudioResourceManager.Instance;
        return manager.LoadDefinitionDocument<IReadOnlyList<IReadOnlyList<AudioResource>>>(() =>
        {
            var groups = document.Definitions.Select(definition => LoadIntoManager(definition, overwriteExisting)).ToList();
            if (groups.Count != 1) manager.DefinitionProvenance = null;
            return groups;
        });
    }

    /// <summary>Captures a single logical group's runtime resources without merging other groups.</summary>
    /// <param name="resources">The group's resources.</param>
    /// <returns>The runtime projection used for persistence and member-specific baseline comparison.</returns>
    public static AudioDefinition FromResources(IEnumerable<AudioResource> resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        return new() { Resources = resources.OrderBy(resource => resource.Key, StringComparer.Ordinal).Select(FromResource).ToList(), Source = AudioDefinitionSource.Generated() };
    }
}
