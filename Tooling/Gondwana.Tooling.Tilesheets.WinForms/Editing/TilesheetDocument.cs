using System.Drawing;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Tooling.Editing;

namespace Gondwana.Tooling.Tilesheets.Editing;

/// <summary>
/// UI-independent editing session. The complete loaded definition remains authoritative;
/// browsing frames and changing geometry never reconstructs or prunes its metadata.
/// </summary>
public sealed partial class TilesheetDocument
{
    /// <summary>
    /// Gets the definition.
    /// </summary>
    public TilesheetDefinition Definition { get; private set; }
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

    private TilesheetDocument(TilesheetDefinition definition, string? path, string baseDirectory)
    {
        _definitions = [definition];
        Definition = definition;
        FilePath = path;
        BaseDirectory = Path.GetFullPath(baseDirectory);
        IsDirty = path is null;
    }

    /// <summary>
    /// Opens the supplied file for use by the tilesheet document.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The resulting tilesheet document.</returns>
    public static TilesheetDocument Open(string path)
    {
        path = Path.GetFullPath(path);
        var definitions = TilesheetDefinitionSerializer.LoadAll(path).ToList();
        if (definitions.Count == 0) throw new InvalidDataException("Document contains no definitions.");
        var document = new TilesheetDocument(definitions[0], path, Path.GetDirectoryName(path)!);
        document._definitions = definitions;
        return document;
    }

    /// <summary>
    /// Creates a new tilesheet document.
    /// </summary>
    /// <param name="directory">The directory.</param>
    /// <returns>The resulting tilesheet document.</returns>
    public static TilesheetDocument Create(string directory) => new(new()
    {
        Name = "Untitled",
        Source = TilesheetDefinitionSource.Generated()
    }, null, directory);

    /// <summary>
    /// Marks the document as modified and notifies listeners.
    /// </summary>
    public void MarkChanged()
    {
        IsDirty = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Resolves the tilesheet's loose image path.
    /// </summary>
    /// <returns>The requested string, or <see langword="null"/> when unavailable.</returns>
    public string? ResolveImagePath() => string.IsNullOrWhiteSpace(Definition.Image?.FilePath)
        ? null : Path.GetFullPath(Definition.Image.FilePath, BaseDirectory);

    /// <summary>
    /// Resolves the assets file path for the tilesheet's packed image.
    /// </summary>
    /// <returns>The requested string, or <see langword="null"/> when unavailable.</returns>
    public string? ResolveAssetsFilePath() => string.IsNullOrWhiteSpace(Definition.Image?.AssetsFilePath)
        ? (string.IsNullOrWhiteSpace(Definition.Image?.AssetEntryName) ? null : PackedLocation?.Path)
        : Path.GetFullPath(Definition.Image.AssetsFilePath, BaseDirectory);

    /// <summary>
    /// Sets a loose image file as the tilesheet source.
    /// </summary>
    /// <param name="path">The path.</param>
    public void SetImage(string path)
    {
        // Changing the source is explicit. Unrelated edits never clear packed fields.
        Definition.Image = new() { FilePath = path };
        MarkChanged();
    }

    /// <summary>
    /// Sets an image entry in an assets file as the tilesheet source.
    /// </summary>
    /// <param name="assetsFilePath">The path to the assets file.</param>
    /// <param name="assetEntryName">The entry name inside the assets file.</param>
    public void SetPackedImage(string assetsFilePath, string assetEntryName)
    {
        if (string.IsNullOrWhiteSpace(assetsFilePath))
            throw new ArgumentException("Assets file path must be a non-empty string.", nameof(assetsFilePath));
        if (string.IsNullOrWhiteSpace(assetEntryName))
            throw new ArgumentException("Asset entry name must be a non-empty string.", nameof(assetEntryName));

        Definition.Image = new()
        {
            AssetsFilePath = Path.GetFullPath(assetsFilePath),
            AssetEntryName = assetEntryName
        };
        MarkChanged();
    }

    /// <summary>
    /// Checks the definition and collects validation errors.
    /// </summary>
    /// <param name="imageSize">The source image size used to validate frame bounds, when known.</param>
    /// <returns>The validation errors; an empty collection indicates that validation passed.</returns>
    public IReadOnlyList<string> Validate(Size? imageSize = null)
    {
        var errors = TilesheetDefinitionSerializer.ValidateDocument(new TilesheetDefinitionDocument { Definitions = _definitions }).ToList();
        if (imageSize.HasValue)
            errors.AddRange(TilesheetDefinitionValidator.Validate(Definition, imageSize.Value.Width, imageSize.Value.Height)
                .Select(error => $"Tilesheet '{Definition.Name}': {error}").Where(error => !errors.Contains(error)));
        // The shared validator owns all geometry rules. These are loose-file I/O checks
        // (the core validator deliberately does not access the filesystem).
        var image = Definition.Image;
        if (image is null)
            errors.Add("Image source is missing.");
        else if (!string.IsNullOrWhiteSpace(image.FilePath))
        {
            if (!string.IsNullOrWhiteSpace(image.AssetsFilePath) || !string.IsNullOrWhiteSpace(image.AssetEntryName))
                errors.Add("Image source is ambiguous: loose and packed sources are both specified.");
            try
            {
                if (!File.Exists(ResolveImagePath())) errors.Add("Image file does not exist: " + image.FilePath);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException)
            {
                errors.Add("Invalid image path: " + ex.Message);
            }
        }
        else if (ResolveAssetsFilePath() is null || string.IsNullOrWhiteSpace(image.AssetEntryName))
            errors.Add("Specify a loose image file (or a complete existing packed image source).");
        return errors;
    }

    /// <summary>
    /// Finds the stored metadata for a frame in the selected region.
    /// </summary>
    /// <param name="region">The region.</param>
    /// <param name="x">The zero-based column in the tile grid.</param>
    /// <param name="y">The zero-based row in the tile grid.</param>
    /// <returns>The requested tilesheet frame definition, or <see langword="null"/> when unavailable.</returns>
    public TilesheetFrameDefinition? FindFrame(TilesheetRegionDefinition region, int x, int y) =>
        region.Frames.FirstOrDefault(f => f is not null && f.XTile == x && f.YTile == y);

    /// <summary>
    /// Gets or creates editable metadata for a frame in the selected region.
    /// </summary>
    /// <param name="region">The region.</param>
    /// <param name="x">The zero-based column in the tile grid.</param>
    /// <param name="y">The zero-based row in the tile grid.</param>
    /// <returns>The editable frame metadata, created when none was previously stored.</returns>
    public TilesheetFrameDefinition EditFrame(TilesheetRegionDefinition region, int x, int y)
    {
        var frame = FindFrame(region, x, y);
        if (frame is not null) return frame;
        frame = new() { XTile = x, YTile = y };
        region.Frames.Add(frame);
        return frame;
    }

    /// <summary>
    /// Removes frame metadata whose coordinates fall outside the region grid.
    /// </summary>
    /// <param name="region">The region.</param>
    /// <returns>The number of frame metadata entries removed.</returns>
    public int RemoveOutOfGridMetadata(TilesheetRegionDefinition region)
    {
        var (columns, rows) = TilesheetDefinitionValidator.GridSize(region);
        int removed = region.Frames.RemoveAll(f => f is not null &&
            (f.XTile < 0 || f.YTile < 0 || f.XTile >= columns || f.YTile >= rows));
        if (removed > 0) MarkChanged();
        return removed;
    }

    /// <summary>
    /// Saves the current content to the destination file.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <param name="imageSize">The source image size used to validate frame bounds, when known.</param>
    /// <param name="allowInvalid">Whether to save even when validation reports errors.</param>
    public void Save(string path, Size? imageSize = null, bool allowInvalid = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var errors = Validate(imageSize);
        if (!allowInvalid && errors.Count > 0) throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        // Identity ambiguity is never allowed, even when saving an incomplete authoring draft.
        if (_definitions.Any(member => string.IsNullOrWhiteSpace(member.Name)) ||
            _definitions.Select(member => member.Name).Distinct(StringComparer.Ordinal).Count() != _definitions.Count)
            throw new InvalidDataException("Every definition needs a unique, non-empty logical identity.");
        path = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var snapshots = _definitions.Select(member =>
        {
            var clone = TilesheetDefinitionSerializer.FromJson(TilesheetDefinitionSerializer.ToJson(member));
            if (clone.Image is not null)
            {
                if (PackedLocation is not null &&
                    string.IsNullOrWhiteSpace(clone.Image.AssetsFilePath) && !string.IsNullOrWhiteSpace(clone.Image.AssetEntryName))
                    clone.Image.AssetsFilePath = PackedLocation.Path;
                clone.Image.FilePath = Rebase(clone.Image.FilePath, directory);
                clone.Image.AssetsFilePath = Rebase(clone.Image.AssetsFilePath, directory);
            }
            if (clone.Source.Kind != TilesheetDefinitionSourceKind.Generated)
                clone.Source = TilesheetDefinitionSource.LooseDefinitionFile(path);
            return clone;
        }).ToList();
        TilesheetDefinitionSerializer.SaveAll(path, snapshots);
        var saved = TilesheetDefinitionSerializer.LoadAll(path);
        for (int i = 0; i < _definitions.Count; i++)
        {
            var member = _definitions[i];
            var persisted = saved[i];
            if (member.Image is not null && persisted.Image is not null)
            {
                member.Image.FilePath = persisted.Image.FilePath;
                member.Image.AssetsFilePath = persisted.Image.AssetsFilePath;
            }
            member.Source = persisted.Source;
        }
        FilePath = path;
        BaseDirectory = directory;
        PackedLocation = null;
        IsDirty = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private string? Rebase(string? path, string directory)
    {
        if (string.IsNullOrWhiteSpace(path)) return path;
        // Preserve existing relative spelling on ordinary Save.
        if (!Path.IsPathRooted(path) && string.Equals(directory, BaseDirectory, StringComparison.OrdinalIgnoreCase))
            return path;
        return Path.GetRelativePath(directory, Path.GetFullPath(path, BaseDirectory)).Replace('\\', '/');
    }

    private List<TilesheetDefinition> _definitions;

    /// <summary>Gets all logical definitions owned by this physical document.</summary>
    public IReadOnlyList<TilesheetDefinition> Definitions => _definitions.AsReadOnly();

    /// <summary>Gets the current logical definition without reconstructing its metadata.</summary>
    public TilesheetDefinition SelectedDefinition => Definition;

    /// <summary>Selects an existing member without changing the physical document's dirty state.</summary>
    /// <param name="definition">The member to select.</param>
    public void SelectDefinition(TilesheetDefinition definition)
    {
        if (!_definitions.Contains(definition)) throw new ArgumentException("Definition does not belong to this document.", nameof(definition));
        Definition = definition;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Adds and selects a new logical definition with a unique default identity.</summary>
    /// <returns>The new member.</returns>
    public TilesheetDefinition AddDefinition()
    {
        var definition = Create(BaseDirectory).Definition;
        definition.Name = UniqueDefinitionIdentity("tilesheet");
        _definitions.Add(definition);
        Definition = definition;
        MarkChanged();
        return definition;
    }

    /// <summary>Duplicates and selects the current definition with a unique identity.</summary>
    /// <returns>The new independent member.</returns>
    public TilesheetDefinition DuplicateDefinition()
    {
        var definition = TilesheetDefinitionSerializer.FromJson(TilesheetDefinitionSerializer.ToJson(Definition));
        definition.Source = Definition.Source;
        definition.Name = UniqueDefinitionIdentity(Definition.Name + "-copy");
        _definitions.Add(definition);
        Definition = definition;
        MarkChanged();
        return definition;
    }

    /// <summary>Removes a member while retaining at least one editable definition.</summary>
    /// <param name="definition">The member to remove.</param>
    /// <returns>Whether the member was removed.</returns>
    public bool RemoveDefinition(TilesheetDefinition definition)
    {
        if (_definitions.Count == 1 || !_definitions.Remove(definition)) return false;
        if (ReferenceEquals(Definition, definition)) Definition = _definitions[0];
        MarkChanged();
        return true;
    }

    /// <summary>Renames the selected member, rejecting empty or duplicate identities.</summary>
    /// <param name="identity">The new case-sensitive logical identity.</param>
    public void RenameDefinition(string identity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identity);
        if (_definitions.Any(member => !ReferenceEquals(member, Definition) && member.Name == identity))
            throw new InvalidDataException($"Duplicate definition identity '{identity}'.");
        Definition.Name = identity;
        MarkChanged();
    }

    /// <summary>Imports selected members without replacing existing definitions or copying resources.</summary>
    /// <param name="path">A same-format source file; legacy singletons are supported.</param>
    /// <param name="select">Selects source indices. Omit to import all; return null or an empty list to cancel.</param>
    /// <returns>The number imported. The first imported member becomes selected.</returns>
    public int ImportDefinitions(string path, Func<IReadOnlyList<TilesheetDefinition>, IReadOnlyList<int>?>? select = null)
    {
        path = Path.GetFullPath(path);
        if (!string.Equals(Path.GetExtension(path), ".gts", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Choose a .gts definition document.", nameof(path));
        var sourceDirectory = Path.GetDirectoryName(path)!;
        string? Rebase(string? value) => DefinitionImport.Rebase(value, sourceDirectory, BaseDirectory);
        var imported = DefinitionImport.Prepare(path, Definitions, TilesheetDefinitionSerializer.LoadAll, select, member =>
        {
            var clone = TilesheetDefinitionSerializer.FromJson(TilesheetDefinitionSerializer.ToJson(member));
            if (clone.Image is not null)
            {
                clone.Image.FilePath = Rebase(clone.Image.FilePath);
                clone.Image.AssetsFilePath = Rebase(clone.Image.AssetsFilePath);
            }
            return clone;
        }, member => member.Name, (member, identity) => member.Name = identity, "tilesheet");
        if (imported.Count == 0) return 0;
        _definitions.AddRange(imported);
        Definition = imported[0];
        MarkChanged();
        return imported.Count;
    }

    private string UniqueDefinitionIdentity(string basis)
    {
        var candidate = basis;
        for (int i = 2; _definitions.Any(member => member.Name == candidate); i++) candidate = basis + "-" + i;
        return candidate;
    }
}
