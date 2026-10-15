using Gondwana.Assets;

namespace Gondwana.Drawing.Animation.GANI;

public static partial class AnimationDefinitionSerializer
{
    /// <summary>Reads a complete document, accepting historical singleton JSON.</summary>
    /// <param name="json">The JSON text.</param>
    /// <param name="sourceIdentity">Optional physical filename or entry name for legacy identity inference.</param>
    /// <returns>The complete definition document.</returns>
    public static AnimationDefinitionDocument FromDocumentJson(string json, string? sourceIdentity = null) => new()
    {
        Definitions = DefinitionDocumentJson.Read<AnimationDefinition>(
            json,
            json => ParseDefinition(json, null),
            definition => definition.Key,
            identityComparer: StringComparer.OrdinalIgnoreCase)
    };

    /// <summary>Serializes a complete document, omitting redundant physical self-origin paths.</summary>
    /// <param name="document">The complete document.</param>
    /// <returns>Canonical document JSON.</returns>
    public static string ToDocumentJson(AnimationDefinitionDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        DefinitionDocumentJson.Check(document.Definitions, definition => definition.Key, identityComparer: StringComparer.OrdinalIgnoreCase);
        return DefinitionDocumentPersistence.Serialize(document, Settings);
    }

    /// <summary>Loads every logical definition from a physical file.</summary>
    /// <param name="filePath">The physical document path.</param>
    /// <returns>The complete document with member-specific load stamps.</returns>
    public static AnimationDefinitionDocument LoadDocument(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var path = Path.GetFullPath(filePath);
        var document = FromDocumentJson(File.ReadAllText(path), path);
        foreach (var definition in document.Definitions)
        {
            definition.Source = AnimationDefinitionSource.LooseDefinitionFile(path);
            definition.LoadStamp = new(definition, filePath: path, definitionIdentity: definition.Key);
        }
        return document;
    }

    /// <summary>Loads a complete document from a stream without claiming physical provenance.</summary>
    /// <param name="stream">A readable stream, left open.</param>
    /// <returns>The complete document.</returns>
    public static AnimationDefinitionDocument LoadDocument(Stream stream)
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
    public static AnimationDefinitionDocument LoadDocument(AssetsFile assetsFile, string entryName)
    {
        ArgumentNullException.ThrowIfNull(assetsFile);
        ArgumentException.ThrowIfNullOrWhiteSpace(entryName);
        using var stream = assetsFile.Get(AssetTypes.AnimationDefinition, entryName)
            ?? throw new FileNotFoundException($"GANI asset entry not found: {entryName}", entryName);
        using var reader = new StreamReader(stream);
        var document = FromDocumentJson(reader.ReadToEnd(), entryName);
        foreach (var definition in document.Definitions)
        {
            definition.Source = string.IsNullOrWhiteSpace(assetsFile.SourcePath)
                ? AnimationDefinitionSource.None()
                : AnimationDefinitionSource.PackedDefinitionFile(assetsFile.SourcePath, entryName);
            definition.LoadStamp = new(definition, assetsFilePath: assetsFile.SourcePath, entryName: entryName, definitionIdentity: definition.Key);
        }
        return document;
    }

    /// <summary>Validates the document identities and every member's format-specific data.</summary>
    /// <param name="document">The document to inspect.</param>
    /// <returns>Member-qualified diagnostics.</returns>
    public static IReadOnlyList<string> ValidateDocument(AnimationDefinitionDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var errors = new List<string>();
        try { DefinitionDocumentJson.Check(document.Definitions, definition => definition.Key, identityComparer: StringComparer.OrdinalIgnoreCase); }
        catch (InvalidDataException ex) { errors.Add(ex.Message); }
        foreach (var definition in document.Definitions ?? [])
            if (definition is not null)
                errors.AddRange(AnimationDefinitionValidator.Validate(definition).Select(error => $"Animation '{definition.Key}': {error}"));
        return errors;
    }

    /// <summary>Saves all definitions, rebasing each member without changing the input objects.</summary>
    /// <param name="filePath">The destination physical path.</param>
    /// <param name="document">The entire document to save.</param>
    public static void SaveDocument(string filePath, AnimationDefinitionDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        SaveAll(filePath, document.Definitions);
    }

    /// <summary>Atomically saves all definitions, using the final destination for portable dependency paths.</summary>
    /// <param name="filePath">The destination physical path.</param>
    /// <param name="definitions">All logical definitions to persist.</param>
    public static void SaveAll(string filePath, IEnumerable<AnimationDefinition> definitions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(definitions);
        var path = Path.GetFullPath(filePath);
        var members = definitions.ToList();
        DefinitionDocumentJson.Check(members, definition => definition.Key, allowUnnamedSingleton: true, identityComparer: StringComparer.OrdinalIgnoreCase);
        var saved = members.Select(definition => PrepareDocumentMember(definition, path)).ToList();
        var json = ToDocumentJson(new() { Definitions = saved });
        DefinitionDocumentPersistence.WriteAtomic(path, json);
    }

    private static AnimationDefinition PrepareDocumentMember(AnimationDefinition definition, string path)
    {
        var clone = ParseDefinition(ToJson(definition), null);
        var origin = definition.Source.GaniFilePath ?? definition.Source.AssetsFilePath;
        var oldDirectory = definition.LoadStamp?.ReferenceBaseDirectory ??
            (string.IsNullOrWhiteSpace(origin) || !Path.IsPathFullyQualified(origin) ? null : Path.GetDirectoryName(origin));
        var newDirectory = Path.GetDirectoryName(path)!;
        string? RebasePath(string? value) => string.IsNullOrWhiteSpace(value) || (oldDirectory is null && !Path.IsPathRooted(value)) ? value :
            Path.GetRelativePath(newDirectory, oldDirectory is null ? Path.GetFullPath(value) : Path.GetFullPath(value, oldDirectory));
        foreach (var source in clone.TilesheetSources)
        {
            source.GtsPath = RebasePath(source.GtsPath);
            source.AssetsFilePath = RebasePath(source.AssetsFilePath);
        }
        clone.Source = AnimationDefinitionSource.LooseDefinitionFile(path);
        return clone;
    }

    /// <summary>Loads all logical definitions in document order.</summary>
    /// <param name="filePath">The document path.</param>
    /// <returns>All definitions.</returns>
    public static IReadOnlyList<AnimationDefinition> LoadAll(string filePath) => LoadDocument(filePath).Definitions;

    /// <summary>Loads a logical member by its case-insensitive Key.</summary>
    /// <param name="filePath">The document path.</param>
    /// <param name="key">The logical member identity; null requires a singleton.</param>
    /// <returns>The selected definition.</returns>
    public static AnimationDefinition Load(string filePath, string? key) =>
        DefinitionDocumentJson.Select(LoadAll(filePath), definition => definition.Key, key, StringComparer.OrdinalIgnoreCase);

    /// <summary>Loads all logical definitions in document order.</summary>
    /// <param name="stream">The readable stream, left open.</param>
    /// <returns>All definitions.</returns>
    public static IReadOnlyList<AnimationDefinition> LoadAll(Stream stream) => LoadDocument(stream).Definitions;

    /// <summary>Loads a logical member by its case-insensitive Key.</summary>
    /// <param name="stream">The readable stream, left open.</param>
    /// <param name="key">The logical member identity; null requires a singleton.</param>
    /// <returns>The selected definition.</returns>
    public static AnimationDefinition Load(Stream stream, string? key) =>
        DefinitionDocumentJson.Select(LoadAll(stream), definition => definition.Key, key, StringComparer.OrdinalIgnoreCase);

    /// <summary>Loads all logical definitions in document order.</summary>
    /// <param name="assetsFile">The archive.</param>
    /// <param name="entryName">The document entry name.</param>
    /// <returns>All definitions.</returns>
    public static IReadOnlyList<AnimationDefinition> LoadAll(AssetsFile assetsFile, string entryName) => LoadDocument(assetsFile, entryName).Definitions;

    /// <summary>Loads a logical member by its case-insensitive Key.</summary>
    /// <param name="assetsFile">The archive.</param>
    /// <param name="entryName">The document entry name.</param>
    /// <param name="key">The logical member identity; null requires a singleton.</param>
    /// <returns>The selected definition.</returns>
    public static AnimationDefinition Load(AssetsFile assetsFile, string entryName, string? key) =>
        DefinitionDocumentJson.Select(LoadAll(assetsFile, entryName), definition => definition.Key, key, StringComparer.OrdinalIgnoreCase);

    /// <summary>Materializes the selected logical definition using registered dependencies.</summary>
    /// <param name="filePath">The document path.</param>
    /// <param name="identity">The logical identity; null requires a singleton.</param>
    /// <returns>The materialized runtime content.</returns>
    public static Cycle LoadCycle(string filePath, string? identity) => ToCycle(Load(filePath, identity));

    /// <summary>Materializes all document members, rolling back newly created content on failure.</summary>
    /// <param name="filePath">The document path.</param>
    /// <returns>The materialized members in document order.</returns>
    public static IReadOnlyList<Cycle> LoadAllCycles(string filePath) => ToAllCycles(LoadDocument(filePath));

    /// <summary>Materializes the selected logical definition using registered dependencies.</summary>
    /// <param name="assetsFile">The archive.</param>
    /// <param name="entryName">The document entry.</param>
    /// <param name="identity">The logical identity; null requires a singleton.</param>
    /// <returns>The materialized runtime content.</returns>
    public static Cycle LoadCycle(AssetsFile assetsFile, string entryName, string? identity) => ToCycle(Load(assetsFile, entryName, identity));

    /// <summary>Materializes all document members, rolling back newly created content on failure.</summary>
    /// <param name="assetsFile">The archive.</param>
    /// <param name="entryName">The document entry.</param>
    /// <returns>The materialized members in document order.</returns>
    public static IReadOnlyList<Cycle> LoadAllCycles(AssetsFile assetsFile, string entryName) => ToAllCycles(LoadDocument(assetsFile, entryName));

    /// <summary>Materializes all members and rolls back new runtime content on failure.</summary>
    /// <param name="document">The document to materialize.</param>
    /// <returns>Runtime content in member order.</returns>
    public static IReadOnlyList<Cycle> ToAllCycles(AnimationDefinitionDocument document)
    {
        var errors = ValidateDocument(document);
        if (errors.Count != 0) throw new InvalidDataException(string.Join(Environment.NewLine, errors));
        var created = new List<Cycle>();
        var previous = new Dictionary<string, Cycle>(Cycle._cycles, StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var definition in document.Definitions) created.Add(MaterializeCycle(definition));
            for (int i = 0; i < created.Count; i++) ApplyNextCycle(created[i], document.Definitions[i]);
            return created;
        }
        catch
        {
            foreach (var item in created) item.Dispose();
            foreach (var pair in previous) Cycle._cycles[pair.Key] = pair.Value;
            throw;
        }
    }
}
