using Gondwana.Drawing.Sprites.GSPR;

namespace Gondwana.Tooling.Sprites.Editing;

/// <summary>UI-independent authoring session. Never creates runtime sprites or scenes.</summary>
public sealed class SpriteDocument
{
    public SpriteDefinition Definition { get; private set; }
    public string? FilePath { get; private set; }
    public string BaseDirectory { get; private set; }
    public bool IsDirty { get; private set; }
    public event EventHandler? Changed;

    private SpriteDocument(SpriteDefinition definition, string directory, string? path, bool dirty)
    {
        Definition = definition;
        BaseDirectory = Path.GetFullPath(directory);
        FilePath = path;
        IsDirty = dirty;
    }

    public static SpriteDocument Create(string directory) => new(new() { Source = SpriteDefinitionSource.Generated() }, directory, null, true);
    public static SpriteDocument Open(string path)
    {
        path = Path.GetFullPath(path);
        return new(SpriteDefinitionSerializer.Load(path), Path.GetDirectoryName(path)!, path, false);
    }

    public SpriteInstanceDefinition AddSprite()
    {
        var sprite = new SpriteInstanceDefinition { Id = Guid.NewGuid(), Nickname = UniqueName("sprite") };
        Definition.Sprites.Add(sprite);
        MarkChanged();
        return sprite;
    }

    public SpriteInstanceDefinition DuplicateSprite(SpriteInstanceDefinition source)
    {
        var sprite = SpriteDefinitionSerializer.FromJson(SpriteDefinitionSerializer.ToJson(new() { Sprites = [source] })).Sprites[0];
        sprite.Id = Guid.NewGuid();
        sprite.Nickname = UniqueName(source.Nickname ?? "sprite");
        Definition.Sprites.Add(sprite);
        MarkChanged();
        return sprite;
    }

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

    public void SetLooseTilesheetSource(string name, string path)
    {
        Definition.TilesheetSources.RemoveAll(source => source.Tilesheet == name);
        Definition.TilesheetSources.Add(SpriteTilesheetSourceDefinition.Loose(name, Path.GetRelativePath(BaseDirectory, Path.GetFullPath(path))));
        MarkChanged();
    }

    public void SetLooseSceneSource(string id, string path)
    {
        Definition.SceneSources.RemoveAll(source => source.SceneId == id);
        Definition.SceneSources.Add(SpriteSceneSourceDefinition.Loose(id, Path.GetRelativePath(BaseDirectory, Path.GetFullPath(path))));
        MarkChanged();
    }

    public string ResolveReferencePath(string path) => Path.GetFullPath(path, BaseDirectory);
    public IReadOnlyList<string> Validate() => SpriteDefinitionValidator.Validate(Definition);
    public void MarkChanged() { IsDirty = true; Changed?.Invoke(this, EventArgs.Empty); }

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
