using Gondwana.Assets;
using Newtonsoft.Json;

namespace Gondwana.Scenes.GSCN;

public static partial class SceneDefinitionSerializer
{
    /// <summary>Reads a complete document, accepting historical singleton JSON.</summary>
    /// <param name="json">The JSON text.</param>
    /// <param name="sourceIdentity">Optional physical filename or entry name for legacy identity inference.</param>
    /// <returns>The complete definition document.</returns>
    public static SceneDefinitionDocument FromDocumentJson(string json, string? sourceIdentity = null) => new()
    {
        Definitions = DefinitionDocumentJson.Read<SceneDefinition>(json, json => ParseDefinition(json, null), definition => definition.ID)
    };

    /// <summary>Serializes a complete document with a Definitions envelope.</summary>
    /// <param name="document">The complete document.</param>
    /// <returns>Canonical document JSON.</returns>
    public static string ToDocumentJson(SceneDefinitionDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        DefinitionDocumentJson.Check(document.Definitions, definition => definition.ID);
        return JsonConvert.SerializeObject(document, Settings);
    }

    /// <summary>Loads every logical definition from a physical file.</summary>
    /// <param name="filePath">The physical document path.</param>
    /// <returns>The complete document with member-specific load stamps.</returns>
    public static SceneDefinitionDocument LoadDocument(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var path = Path.GetFullPath(filePath);
        var document = FromDocumentJson(File.ReadAllText(path), path);
        foreach (var definition in document.Definitions)
        {
            definition.Source = SceneDefinitionSource.LooseDefinitionFile(path);
            definition.LoadStamp = new(definition, filePath: path, definitionIdentity: definition.ID);
        }
        return document;
    }

    /// <summary>Loads a complete document from a stream without claiming physical provenance.</summary>
    /// <param name="stream">A readable stream, left open.</param>
    /// <returns>The complete document.</returns>
    public static SceneDefinitionDocument LoadDocument(Stream stream)
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
    public static SceneDefinitionDocument LoadDocument(AssetsFile assetsFile, string entryName)
    {
        ArgumentNullException.ThrowIfNull(assetsFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(entryName);
        using var stream = assetsFile.Get(AssetTypes.SceneDefinition, entryName)
            ?? throw new FileNotFoundException($"GSCN asset entry not found: {entryName}", entryName);
        using var reader = new StreamReader(stream);
        var document = FromDocumentJson(reader.ReadToEnd(), entryName);
        foreach (var definition in document.Definitions)
        {
            definition.Source = string.IsNullOrWhiteSpace(assetsFile.SourcePath)
                ? SceneDefinitionSource.None()
                : SceneDefinitionSource.PackedDefinitionFile(assetsFile.SourcePath, entryName);
            definition.LoadStamp = new(definition, assetsFilePath: assetsFile.SourcePath, entryName: entryName, definitionIdentity: definition.ID);
        }
        return document;
    }

    /// <summary>Validates the document identities and every member's format-specific data.</summary>
    /// <param name="document">The document to inspect.</param>
    /// <returns>Member-qualified diagnostics.</returns>
    public static IReadOnlyList<string> ValidateDocument(SceneDefinitionDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var errors = new List<string>();
        try { DefinitionDocumentJson.Check(document.Definitions, definition => definition.ID); }
        catch (InvalidDataException ex) { errors.Add(ex.Message); }
        foreach (var definition in document.Definitions ?? [])
            if (definition is not null)
                errors.AddRange(SceneDefinitionValidator.Validate(definition).Select(error => $"Scene '{definition.ID}': {error}"));
        return errors;
    }

    /// <summary>Saves all definitions, rebasing each member without changing the input objects.</summary>
    /// <param name="filePath">The destination physical path.</param>
    /// <param name="document">The entire document to save.</param>
    public static void SaveDocument(string filePath, SceneDefinitionDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        SaveAll(filePath, document.Definitions);
    }

    /// <summary>Saves all supplied definitions in one canonical document.</summary>
    /// <param name="filePath">The destination physical path.</param>
    /// <param name="definitions">All logical definitions to persist.</param>
    public static void SaveAll(string filePath, IEnumerable<SceneDefinition> definitions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(definitions);
        var path = Path.GetFullPath(filePath);
        var members = definitions.ToList();
        DefinitionDocumentJson.Check(members, definition => definition.ID, allowUnnamedSingleton: true);
        var saved = members.Select(definition => PrepareDocumentMember(definition, path)).ToList();
        var json = ToDocumentJson(new() { Definitions = saved });
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json);
    }

    private static SceneDefinition PrepareDocumentMember(SceneDefinition definition, string path)
    {
        var clone = ParseDefinition(ToJson(definition), null);
        if (string.IsNullOrWhiteSpace(clone.ID)) clone.ID = Path.GetFileNameWithoutExtension(path);
        var origin = definition.Source.GscnFilePath ?? definition.Source.AssetsFilePath;
        var oldDirectory = definition.LoadStamp?.ReferenceBaseDirectory ??
            (string.IsNullOrWhiteSpace(origin) ? null : Path.GetDirectoryName(Path.GetFullPath(origin)));
        var newDirectory = Path.GetDirectoryName(path)!;
        string? RebasePath(string? value) => string.IsNullOrWhiteSpace(value) || (oldDirectory is null && !Path.IsPathRooted(value)) ? value :
            Path.GetRelativePath(newDirectory, oldDirectory is null ? Path.GetFullPath(value) : Path.GetFullPath(value, oldDirectory));
        foreach (var source in clone.TilesheetSources)
        {
            source.GtsPath = RebasePath(source.GtsPath);
            source.AssetsFilePath = RebasePath(source.AssetsFilePath);
        }
        foreach (var source in clone.AnimationSources)
        {
            source.GaniPath = RebasePath(source.GaniPath);
            source.AssetsFilePath = RebasePath(source.AssetsFilePath);
        }
        clone.Source = SceneDefinitionSource.LooseDefinitionFile(path);
        return clone;
    }

    /// <summary>Loads all logical definitions in document order.</summary>
    /// <param name="filePath">The document path.</param>
    /// <returns>All definitions.</returns>
    public static IReadOnlyList<SceneDefinition> LoadAll(string filePath) => LoadDocument(filePath).Definitions;

    /// <summary>Loads a logical member by its case-sensitive ID.</summary>
    /// <param name="filePath">The document path.</param>
    /// <param name="sceneId">The logical member identity; null requires a singleton.</param>
    /// <returns>The selected definition.</returns>
    public static SceneDefinition Load(string filePath, string? sceneId) =>
        DefinitionDocumentJson.Select(LoadAll(filePath), definition => definition.ID, sceneId);

    /// <summary>Loads all logical definitions in document order.</summary>
    /// <param name="stream">The readable stream, left open.</param>
    /// <returns>All definitions.</returns>
    public static IReadOnlyList<SceneDefinition> LoadAll(Stream stream) => LoadDocument(stream).Definitions;

    /// <summary>Loads a logical member by its case-sensitive ID.</summary>
    /// <param name="stream">The readable stream, left open.</param>
    /// <param name="sceneId">The logical member identity; null requires a singleton.</param>
    /// <returns>The selected definition.</returns>
    public static SceneDefinition Load(Stream stream, string? sceneId) =>
        DefinitionDocumentJson.Select(LoadAll(stream), definition => definition.ID, sceneId);

    /// <summary>Loads all logical definitions in document order.</summary>
    /// <param name="assetsFile">The archive.</param>
    /// <param name="entryName">The document entry name.</param>
    /// <returns>All definitions.</returns>
    public static IReadOnlyList<SceneDefinition> LoadAll(AssetsFile assetsFile, string entryName) => LoadDocument(assetsFile, entryName).Definitions;

    /// <summary>Loads a logical member by its case-sensitive ID.</summary>
    /// <param name="assetsFile">The archive.</param>
    /// <param name="entryName">The document entry name.</param>
    /// <param name="sceneId">The logical member identity; null requires a singleton.</param>
    /// <returns>The selected definition.</returns>
    public static SceneDefinition Load(AssetsFile assetsFile, string entryName, string? sceneId) =>
        DefinitionDocumentJson.Select(LoadAll(assetsFile, entryName), definition => definition.ID, sceneId);

    /// <summary>Materializes the selected logical definition using registered dependencies.</summary>
    /// <param name="filePath">The document path.</param>
    /// <param name="identity">The logical identity; null requires a singleton.</param>
    /// <returns>The materialized runtime content.</returns>
    public static Scene LoadScene(string filePath, string? identity) => ToScene(Load(filePath, identity));

    /// <summary>Materializes all document members, rolling back newly created content on failure.</summary>
    /// <param name="filePath">The document path.</param>
    /// <returns>The materialized members in document order.</returns>
    public static IReadOnlyList<Scene> LoadAllScenes(string filePath) => ToAllScenes(LoadDocument(filePath));

    /// <summary>Materializes the selected logical definition using registered dependencies.</summary>
    /// <param name="assetsFile">The archive.</param>
    /// <param name="entryName">The document entry.</param>
    /// <param name="identity">The logical identity; null requires a singleton.</param>
    /// <returns>The materialized runtime content.</returns>
    public static Scene LoadScene(AssetsFile assetsFile, string entryName, string? identity) => ToScene(Load(assetsFile, entryName, identity));

    /// <summary>Materializes all document members, rolling back newly created content on failure.</summary>
    /// <param name="assetsFile">The archive.</param>
    /// <param name="entryName">The document entry.</param>
    /// <returns>The materialized members in document order.</returns>
    public static IReadOnlyList<Scene> LoadAllScenes(AssetsFile assetsFile, string entryName) => ToAllScenes(LoadDocument(assetsFile, entryName));

    /// <summary>Materializes all members and rolls back new runtime content on failure.</summary>
    /// <param name="document">The document to materialize.</param>
    /// <returns>Runtime content in member order.</returns>
    public static IReadOnlyList<Scene> ToAllScenes(SceneDefinitionDocument document)
    {
        var errors = ValidateDocument(document);
        if (errors.Count != 0) throw new InvalidDataException(string.Join(Environment.NewLine, errors));
        var created = new List<Scene>();
        try
        {
            foreach (var definition in document.Definitions) created.Add(ToScene(definition));
            return created;
        }
        catch
        {
            foreach (var item in created) item.Dispose();
            throw;
        }
    }
}
