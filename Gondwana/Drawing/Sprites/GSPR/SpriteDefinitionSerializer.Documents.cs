using Gondwana.Assets;
using Newtonsoft.Json;

namespace Gondwana.Drawing.Sprites.GSPR;

public static partial class SpriteDefinitionSerializer
{
    /// <summary>Reads a complete document, accepting historical singleton JSON.</summary>
    /// <param name="json">The JSON text.</param>
    /// <param name="sourceIdentity">Optional physical filename or entry name for legacy identity inference.</param>
    /// <returns>The complete definition document.</returns>
    public static SpriteDefinitionDocument FromDocumentJson(string json, string? sourceIdentity = null) => new()
    {
        Definitions = DefinitionDocumentJson.Read<SpriteDefinition>(json, ParseDefinition, definition => definition.Key, definition => { if (string.IsNullOrWhiteSpace(definition.Key)) definition.Key = Path.GetFileNameWithoutExtension(sourceIdentity) is { Length: > 0 } inferred ? inferred : "sprite"; })
    };

    /// <summary>Serializes a complete document with a Definitions envelope.</summary>
    /// <param name="document">The complete document.</param>
    /// <returns>Canonical document JSON.</returns>
    public static string ToDocumentJson(SpriteDefinitionDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        DefinitionDocumentJson.Check(document.Definitions, definition => definition.Key);
        return JsonConvert.SerializeObject(document, Settings);
    }

    /// <summary>Loads every logical definition from a physical file.</summary>
    /// <param name="filePath">The physical document path.</param>
    /// <returns>The complete document with member-specific load stamps.</returns>
    public static SpriteDefinitionDocument LoadDocument(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var path = Path.GetFullPath(filePath);
        var document = FromDocumentJson(File.ReadAllText(path), path);
        foreach (var definition in document.Definitions)
        {
            definition.Source = SpriteDefinitionSource.LooseDefinitionFile(path);
            definition.LoadStamp = new(definition, filePath: path, definitionIdentity: definition.Key);
        }
        return document;
    }

    /// <summary>Loads a complete document from a stream without claiming physical provenance.</summary>
    /// <param name="stream">A readable stream, left open.</param>
    /// <returns>The complete document.</returns>
    public static SpriteDefinitionDocument LoadDocument(Stream stream)
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
    public static SpriteDefinitionDocument LoadDocument(AssetsFile assetsFile, string entryName)
    {
        ArgumentNullException.ThrowIfNull(assetsFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(entryName);
        using var stream = assetsFile.Get(AssetTypes.SpriteDefinition, entryName)
            ?? throw new FileNotFoundException($"GSPR asset entry not found: {entryName}", entryName);
        using var reader = new StreamReader(stream);
        var document = FromDocumentJson(reader.ReadToEnd(), entryName);
        foreach (var definition in document.Definitions)
        {
            definition.Source = string.IsNullOrWhiteSpace(assetsFile.SourcePath)
                ? SpriteDefinitionSource.None()
                : SpriteDefinitionSource.PackedDefinitionFile(assetsFile.SourcePath, entryName);
            definition.LoadStamp = new(definition, assetsFilePath: assetsFile.SourcePath, entryName: entryName, definitionIdentity: definition.Key);
        }
        return document;
    }

    /// <summary>Validates the document identities and every member's format-specific data.</summary>
    /// <param name="document">The document to inspect.</param>
    /// <returns>Member-qualified diagnostics.</returns>
    public static IReadOnlyList<string> ValidateDocument(SpriteDefinitionDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var errors = new List<string>();
        try { DefinitionDocumentJson.Check(document.Definitions, definition => definition.Key); }
        catch (InvalidDataException ex) { errors.Add(ex.Message); }
        foreach (var definition in document.Definitions ?? [])
            if (definition is not null)
                errors.AddRange(SpriteDefinitionValidator.Validate(definition).Select(error => $"Sprite '{definition.Key}': {error}"));
        return errors;
    }

    /// <summary>Saves all definitions, rebasing each member without changing the input objects.</summary>
    /// <param name="filePath">The destination physical path.</param>
    /// <param name="document">The entire document to save.</param>
    public static void SaveDocument(string filePath, SpriteDefinitionDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        SaveAll(filePath, document.Definitions);
    }

    /// <summary>Saves all supplied definitions in one canonical document.</summary>
    /// <param name="filePath">The destination physical path.</param>
    /// <param name="definitions">All logical definitions to persist.</param>
    public static void SaveAll(string filePath, IEnumerable<SpriteDefinition> definitions)
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

    private static SpriteDefinition PrepareDocumentMember(SpriteDefinition definition, string path)
    {
        var clone = ParseDefinition(ToJson(definition));
        if (string.IsNullOrWhiteSpace(clone.Key)) clone.Key = Path.GetFileNameWithoutExtension(path);
        var origin = definition.Source.GsprFilePath ?? definition.Source.AssetsFilePath;
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
        foreach (var source in clone.SceneSources)
        {
            source.GscnPath = RebasePath(source.GscnPath);
            source.AssetsFilePath = RebasePath(source.AssetsFilePath);
        }
        clone.Source = SpriteDefinitionSource.LooseDefinitionFile(path);
        return clone;
    }

    /// <summary>Loads all logical definitions in document order.</summary>
    /// <param name="filePath">The document path.</param>
    /// <returns>All definitions.</returns>
    public static IReadOnlyList<SpriteDefinition> LoadAll(string filePath) => LoadDocument(filePath).Definitions;

    /// <summary>Loads a logical member by its case-sensitive Key.</summary>
    /// <param name="filePath">The document path.</param>
    /// <param name="key">The logical member identity; null requires a singleton.</param>
    /// <returns>The selected definition.</returns>
    public static SpriteDefinition Load(string filePath, string? key) =>
        DefinitionDocumentJson.Select(LoadAll(filePath), definition => definition.Key, key);

    /// <summary>Loads all logical definitions in document order.</summary>
    /// <param name="stream">The readable stream, left open.</param>
    /// <returns>All definitions.</returns>
    public static IReadOnlyList<SpriteDefinition> LoadAll(Stream stream) => LoadDocument(stream).Definitions;

    /// <summary>Loads a logical member by its case-sensitive Key.</summary>
    /// <param name="stream">The readable stream, left open.</param>
    /// <param name="key">The logical member identity; null requires a singleton.</param>
    /// <returns>The selected definition.</returns>
    public static SpriteDefinition Load(Stream stream, string? key) =>
        DefinitionDocumentJson.Select(LoadAll(stream), definition => definition.Key, key);

    /// <summary>Loads all logical definitions in document order.</summary>
    /// <param name="assetsFile">The archive.</param>
    /// <param name="entryName">The document entry name.</param>
    /// <returns>All definitions.</returns>
    public static IReadOnlyList<SpriteDefinition> LoadAll(AssetsFile assetsFile, string entryName) => LoadDocument(assetsFile, entryName).Definitions;

    /// <summary>Loads a logical member by its case-sensitive Key.</summary>
    /// <param name="assetsFile">The archive.</param>
    /// <param name="entryName">The document entry name.</param>
    /// <param name="key">The logical member identity; null requires a singleton.</param>
    /// <returns>The selected definition.</returns>
    public static SpriteDefinition Load(AssetsFile assetsFile, string entryName, string? key) =>
        DefinitionDocumentJson.Select(LoadAll(assetsFile, entryName), definition => definition.Key, key);

    /// <summary>Materializes the selected logical definition using registered dependencies.</summary>
    /// <param name="filePath">The document path.</param>
    /// <param name="identity">The logical identity; null requires a singleton.</param>
    /// <returns>The materialized runtime content.</returns>
    public static List<Sprite> LoadSprites(string filePath, string? identity) => ToSprites(Load(filePath, identity));

    /// <summary>Materializes all document members, rolling back newly created content on failure.</summary>
    /// <param name="filePath">The document path.</param>
    /// <returns>The materialized members in document order.</returns>
    public static IReadOnlyList<List<Sprite>> LoadAllSprites(string filePath) => ToAllSprites(LoadDocument(filePath));

    /// <summary>Materializes the selected logical definition using registered dependencies.</summary>
    /// <param name="assetsFile">The archive.</param>
    /// <param name="entryName">The document entry.</param>
    /// <param name="identity">The logical identity; null requires a singleton.</param>
    /// <returns>The materialized runtime content.</returns>
    public static List<Sprite> LoadSprites(AssetsFile assetsFile, string entryName, string? identity) => ToSprites(Load(assetsFile, entryName, identity));

    /// <summary>Materializes all document members, rolling back newly created content on failure.</summary>
    /// <param name="assetsFile">The archive.</param>
    /// <param name="entryName">The document entry.</param>
    /// <returns>The materialized members in document order.</returns>
    public static IReadOnlyList<List<Sprite>> LoadAllSprites(AssetsFile assetsFile, string entryName) => ToAllSprites(LoadDocument(assetsFile, entryName));

    /// <summary>Materializes all members and rolls back new runtime content on failure.</summary>
    /// <param name="document">The document to materialize.</param>
    /// <returns>Runtime content in member order.</returns>
    public static IReadOnlyList<List<Sprite>> ToAllSprites(SpriteDefinitionDocument document)
    {
        var errors = ValidateDocument(document);
        if (errors.Count != 0) throw new InvalidDataException(string.Join(Environment.NewLine, errors));
        var created = new List<List<Sprite>>();
        var previous = SpriteManager.Instance.DefinitionProvenance;
        try
        {
            foreach (var definition in document.Definitions) created.Add(ToSprites(definition));
            if (created.Count != 1) SpriteManager.Instance.DefinitionProvenance = null;
            return created;
        }
        catch
        {
            foreach (var sprite in created.SelectMany(group => group))
            {
                SpriteManager.Instance._spriteList.Remove(sprite);
                sprite.DisposeImmediate();
            }
            SpriteManager.Instance.DefinitionProvenance = previous;
            throw;
        }
    }
}
