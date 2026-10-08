using Gondwana.Drawing.Sprites.GSPR;

namespace Gondwana.Tooling.Sprites.Editing;

/// <summary>UI-independent authoring session. Never creates runtime sprites or scenes.</summary>
public sealed class SpriteDocument
{
    /// <summary>
    /// Gets the definition.
    /// </summary>
    public SpriteDefinition Definition { get; private set; }
    /// <summary>
    /// Gets the file path.
    /// </summary>
    public string? FilePath { get; private set; }
    /// <summary>
    /// Gets the base directory.
    /// </summary>
    public string BaseDirectory { get; private set; }
    /// <summary>
    /// Gets whether the document contains unsaved changes.
    /// </summary>
    public bool IsDirty { get; private set; }
    /// <summary>
    /// Occurs when the document content changes.
    /// </summary>
    public event EventHandler? Changed;

    private SpriteDocument(SpriteDefinition definition, string directory, string? path, bool dirty)
    {
        Definition = definition;
        BaseDirectory = Path.GetFullPath(directory);
        FilePath = path;
        IsDirty = dirty;
    }

    /// <summary>
    /// Creates a new sprite document.
    /// </summary>
    /// <param name="directory">The directory.</param>
    /// <returns>The resulting sprite document.</returns>
    public static SpriteDocument Create(string directory) => new(new() { Source = SpriteDefinitionSource.Generated() }, directory, null, true);
    /// <summary>
    /// Opens the supplied file for use by the sprite document.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The resulting sprite document.</returns>
    public static SpriteDocument Open(string path)
    {
        path = Path.GetFullPath(path);
        return new(SpriteDefinitionSerializer.Load(path), Path.GetDirectoryName(path)!, path, false);
    }

    /// <summary>
    /// Adds a new sprite definition to the document.
    /// </summary>
    /// <returns>The new sprite definition added to the document.</returns>
    public SpriteInstanceDefinition AddSprite()
    {
        var sprite = new SpriteInstanceDefinition { Id = Guid.NewGuid(), Nickname = UniqueName("sprite") };
        Definition.Sprites.Add(sprite);
        MarkChanged();
        return sprite;
    }

    /// <summary>
    /// Copies the selected sprite definition into a new document entry.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>The copied sprite definition added to the document.</returns>
    public SpriteInstanceDefinition DuplicateSprite(SpriteInstanceDefinition source)
    {
        var sprite = SpriteDefinitionSerializer.FromJson(SpriteDefinitionSerializer.ToJson(new() { Sprites = [source] })).Sprites[0];
        sprite.Id = Guid.NewGuid();
        sprite.Nickname = UniqueName(source.Nickname ?? "sprite");
        Definition.Sprites.Add(sprite);
        MarkChanged();
        return sprite;
    }

    /// <summary>
    /// Removes the selected sprite definition from the document.
    /// </summary>
    /// <param name="sprite">The sprite.</param>
    /// <returns><see langword="true"/> if the entry was found and removed; otherwise, <see langword="false"/>.</returns>
    public bool RemoveSprite(SpriteInstanceDefinition sprite)
    {
        if (!Definition.Sprites.Remove(sprite)) return false;
        MarkChanged();
        return true;
    }

    private string UniqueName(string basis)
    {
        var name = basis;
        for (int i = 2; Definition.Sprites.Any(sprite => sprite.Nickname == name); i++) name = basis + "-" + i;
        return name;
    }

    /// <summary>
    /// Adds or updates a tilesheet reference to a loose GTS file.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="path">The path.</param>
    public void SetLooseTilesheetSource(string name, string path)
    {
        Definition.TilesheetSources.RemoveAll(source => source.Tilesheet == name);
        Definition.TilesheetSources.Add(SpriteTilesheetSourceDefinition.Loose(name, Path.GetRelativePath(BaseDirectory, Path.GetFullPath(path))));
        MarkChanged();
    }

    /// <summary>
    /// Adds or updates a scene reference to a loose GSCN file.
    /// </summary>
    /// <param name="id">The id.</param>
    /// <param name="path">The path.</param>
    public void SetLooseSceneSource(string id, string path)
    {
        Definition.SceneSources.RemoveAll(source => source.SceneId == id);
        Definition.SceneSources.Add(SpriteSceneSourceDefinition.Loose(id, Path.GetRelativePath(BaseDirectory, Path.GetFullPath(path))));
        MarkChanged();
    }

    /// <summary>
    /// Resolves a document-relative resource path to an absolute path.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The absolute path of the referenced resource.</returns>
    public string ResolveReferencePath(string path) => Path.GetFullPath(path, BaseDirectory);
    /// <summary>
    /// Checks the definition and collects validation errors.
    /// </summary>
    /// <returns>The validation errors; an empty collection indicates that validation passed.</returns>
    public IReadOnlyList<string> Validate() => SpriteDefinitionValidator.Validate(Definition);
    /// <summary>
    /// Marks the document as modified and notifies listeners.
    /// </summary>
    public void MarkChanged() { IsDirty = true; Changed?.Invoke(this, EventArgs.Empty); }

    /// <summary>
    /// Saves the current content to the destination file.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <param name="allowInvalid">Whether to save even when validation reports errors.</param>
    public void Save(string path, bool allowInvalid = false)
    {
        if (!allowInvalid && Validate().Count != 0) throw new InvalidDataException(string.Join(Environment.NewLine, Validate()));
        path = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(path)!;
        // A new document's relative references are based on its working directory.
        var originalSource = Definition.Source;
        Definition.Source = SpriteDefinitionSource.LooseDefinitionFile(FilePath ?? Path.Combine(BaseDirectory, "Untitled.gspr"));
        SpriteDefinition rebased;
        try { rebased = SpriteDefinitionSerializer.Rebase(Definition, directory); }
        finally { Definition.Source = originalSource; }
        Directory.CreateDirectory(directory);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            SpriteDefinitionSerializer.Save(temporary, rebased);
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        // Keep entry identity so selection and PropertyGrid references remain valid.
        Definition.TilesheetSources = rebased.TilesheetSources;
        Definition.SceneSources = rebased.SceneSources;
        Definition.Source = SpriteDefinitionSource.LooseDefinitionFile(path);
        FilePath = path;
        BaseDirectory = directory;
        IsDirty = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
