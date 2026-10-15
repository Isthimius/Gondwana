using Gondwana.Assets;

namespace Gondwana.Drawing.Tilesheets.GTS;

public static partial class TilesheetDefinitionSerializer
{
    /// <summary>Reads a complete document, accepting historical singleton JSON.</summary>
    /// <param name="json">The JSON text.</param>
    /// <param name="sourceIdentity">Optional physical filename or entry name for legacy identity inference.</param>
    /// <returns>The complete definition document.</returns>
    public static TilesheetDefinitionDocument FromDocumentJson(string json, string? sourceIdentity = null) => new()
    {
        Definitions = DefinitionDocumentJson.Read<TilesheetDefinition>(json, json => ParseDefinition(json, null), definition => definition.Name)
    };

    /// <summary>Serializes a complete document, omitting redundant physical self-origin paths.</summary>
    /// <param name="document">The complete document.</param>
    /// <returns>Canonical document JSON.</returns>
    public static string ToDocumentJson(TilesheetDefinitionDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        DefinitionDocumentJson.Check(document.Definitions, definition => definition.Name);
        return DefinitionDocumentPersistence.Serialize(document, Settings);
    }

    /// <summary>Loads every logical definition from a physical file.</summary>
    /// <param name="filePath">The physical document path.</param>
    /// <returns>The complete document with member-specific load stamps.</returns>
    public static TilesheetDefinitionDocument LoadDocument(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var path = Path.GetFullPath(filePath);
        var document = FromDocumentJson(File.ReadAllText(path), path);
        foreach (var definition in document.Definitions)
        {
            if (definition.Source.Kind != TilesheetDefinitionSourceKind.Generated)
                definition.Source = TilesheetDefinitionSource.LooseDefinitionFile(path);
            definition.LoadStamp = new(definition, filePath: path, definitionIdentity: definition.Name);
        }
        return document;
    }

    /// <summary>Loads a complete document from a stream without claiming physical provenance.</summary>
    /// <param name="stream">A readable stream, left open.</param>
    /// <returns>The complete document.</returns>
    public static TilesheetDefinitionDocument LoadDocument(Stream stream)
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
    public static TilesheetDefinitionDocument LoadDocument(AssetsFile assetsFile, string entryName)
    {
        ArgumentNullException.ThrowIfNull(assetsFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(entryName);
        using var stream = assetsFile.Get(AssetTypes.TilesheetDefinition, entryName)
            ?? throw new FileNotFoundException($"GTS asset entry not found: {entryName}", entryName);
        using var reader = new StreamReader(stream);
        var document = FromDocumentJson(reader.ReadToEnd(), entryName);
        foreach (var definition in document.Definitions)
        {
            definition.Source = string.IsNullOrWhiteSpace(assetsFile.SourcePath)
                ? TilesheetDefinitionSource.None()
                : TilesheetDefinitionSource.PackedDefinitionFile(assetsFile.SourcePath, entryName);
            definition.LoadStamp = new(definition, assetsFilePath: assetsFile.SourcePath, entryName: entryName, definitionIdentity: definition.Name);
        }
        return document;
    }

    /// <summary>Validates the document identities and every member's format-specific data.</summary>
    /// <param name="document">The document to inspect.</param>
    /// <returns>Member-qualified diagnostics.</returns>
    public static IReadOnlyList<string> ValidateDocument(TilesheetDefinitionDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var errors = new List<string>();
        try { DefinitionDocumentJson.Check(document.Definitions, definition => definition.Name); }
        catch (InvalidDataException ex) { errors.Add(ex.Message); }
        foreach (var definition in document.Definitions ?? [])
            if (definition is not null)
                errors.AddRange(TilesheetDefinitionValidator.Validate(definition).Select(error => $"Tilesheet '{definition.Name}': {error}"));
        return errors;
    }

    /// <summary>Saves all definitions, rebasing each member without changing the input objects.</summary>
    /// <param name="filePath">The destination physical path.</param>
    /// <param name="document">The entire document to save.</param>
    public static void SaveDocument(string filePath, TilesheetDefinitionDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        SaveAll(filePath, document.Definitions);
    }

    /// <summary>Atomically saves all definitions, using the final destination for portable dependency paths.</summary>
    /// <param name="filePath">The destination physical path.</param>
    /// <param name="definitions">All logical definitions to persist.</param>
    public static void SaveAll(string filePath, IEnumerable<TilesheetDefinition> definitions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(definitions);
        var path = Path.GetFullPath(filePath);
        var members = definitions.ToList();
        DefinitionDocumentJson.Check(members, definition => definition.Name, allowUnnamedSingleton: true);
        var saved = members.Select(definition => PrepareDocumentMember(definition, path)).ToList();
        var json = ToDocumentJson(new() { Definitions = saved });
        DefinitionDocumentPersistence.WriteAtomic(path, json);
    }

    private static TilesheetDefinition PrepareDocumentMember(TilesheetDefinition definition, string path)
    {
        var clone = ParseDefinition(ToJson(definition), null);
        var origin = definition.Source.GtsFilePath ?? definition.Source.AssetsFilePath;
        var oldDirectory = definition.LoadStamp?.ReferenceBaseDirectory ??
            (string.IsNullOrWhiteSpace(origin) || !Path.IsPathFullyQualified(origin) ? null : Path.GetDirectoryName(origin));
        var newDirectory = Path.GetDirectoryName(path)!;
        string? RebasePath(string? value) => string.IsNullOrWhiteSpace(value) || (oldDirectory is null && !Path.IsPathRooted(value)) ? value :
            Path.GetRelativePath(newDirectory, oldDirectory is null ? Path.GetFullPath(value) : Path.GetFullPath(value, oldDirectory));
        if (clone.Image is { } image)
        {
            if (string.IsNullOrWhiteSpace(image.FilePath) && string.IsNullOrWhiteSpace(image.AssetsFilePath) &&
                !string.IsNullOrWhiteSpace(image.AssetEntryName))
                image.AssetsFilePath = definition.LoadStamp?.AssetsFilePath ?? definition.Source.AssetsFilePath;
            image.FilePath = RebasePath(image.FilePath);
            image.AssetsFilePath = RebasePath(image.AssetsFilePath);
        }
        ApplyDefaultSource(clone, TilesheetDefinitionSource.LooseDefinitionFile(path));
        return clone;
    }

    /// <summary>Loads all logical definitions in document order.</summary>
    /// <param name="filePath">The document path.</param>
    /// <returns>All definitions.</returns>
    public static IReadOnlyList<TilesheetDefinition> LoadAll(string filePath) => LoadDocument(filePath).Definitions;

    /// <summary>Loads a logical member by its case-sensitive Name.</summary>
    /// <param name="filePath">The document path.</param>
    /// <param name="name">The logical member identity; null requires a singleton.</param>
    /// <returns>The selected definition.</returns>
    public static TilesheetDefinition Load(string filePath, string? name) =>
        DefinitionDocumentJson.Select(LoadAll(filePath), definition => definition.Name, name);

    /// <summary>Loads all logical definitions in document order.</summary>
    /// <param name="stream">The readable stream, left open.</param>
    /// <returns>All definitions.</returns>
    public static IReadOnlyList<TilesheetDefinition> LoadAll(Stream stream) => LoadDocument(stream).Definitions;

    /// <summary>Loads a logical member by its case-sensitive Name.</summary>
    /// <param name="stream">The readable stream, left open.</param>
    /// <param name="name">The logical member identity; null requires a singleton.</param>
    /// <returns>The selected definition.</returns>
    public static TilesheetDefinition Load(Stream stream, string? name) =>
        DefinitionDocumentJson.Select(LoadAll(stream), definition => definition.Name, name);

    /// <summary>Loads all logical definitions in document order.</summary>
    /// <param name="assetsFile">The archive.</param>
    /// <param name="entryName">The document entry name.</param>
    /// <returns>All definitions.</returns>
    public static IReadOnlyList<TilesheetDefinition> LoadAll(AssetsFile assetsFile, string entryName) => LoadDocument(assetsFile, entryName).Definitions;

    /// <summary>Loads a logical member by its case-sensitive Name.</summary>
    /// <param name="assetsFile">The archive.</param>
    /// <param name="entryName">The document entry name.</param>
    /// <param name="name">The logical member identity; null requires a singleton.</param>
    /// <returns>The selected definition.</returns>
    public static TilesheetDefinition Load(AssetsFile assetsFile, string entryName, string? name) =>
        DefinitionDocumentJson.Select(LoadAll(assetsFile, entryName), definition => definition.Name, name);

    /// <summary>Loads the only definition in a typed GAF entry; fails for ambiguous documents.</summary>
    /// <param name="assetsFile">The archive.</param>
    /// <param name="entryName">The physical document entry.</param>
    /// <returns>The singleton definition.</returns>
    public static TilesheetDefinition Load(AssetsFile assetsFile, string entryName) => Load(assetsFile, entryName, null);
}
