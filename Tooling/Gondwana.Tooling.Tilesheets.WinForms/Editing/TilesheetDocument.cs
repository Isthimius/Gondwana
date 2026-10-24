using System.Drawing;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Tooling.Editing;

namespace Gondwana.Tooling.Tilesheets.Editing;

/// <summary>UI-independent editing session for all definitions in a GTS document; browsing frames or changing geometry preserves existing frame metadata until explicitly pruned.</summary>
public sealed partial class TilesheetDocument
{
    /// <summary>Gets the selected mutable logical definition; direct edits require <see cref="MarkChanged"/>.</summary>
    public TilesheetDefinition Definition { get; private set; }
    /// <summary>Gets the full loose-file path, or null for an unsaved or packed document.</summary>
    public string? FilePath { get; private set; }
    /// <summary>Gets the absolute directory used to resolve relative dependencies; for packed documents, the containing archive directory.</summary>
    public string BaseDirectory { get; private set; }
    /// <summary>
    /// Gets whether the document contains unsaved changes.
    /// </summary>
    public bool IsDirty { get; private set; }
    /// <summary>Occurs synchronously when content is marked modified, selection changes, or a save is accepted.</summary>
    public event EventHandler? Changed;

    private TilesheetDocument(TilesheetDefinition definition, string? path, string baseDirectory)
    {
        _definitions = [definition];
        Definition = definition;
        FilePath = path;
        BaseDirectory = Path.GetFullPath(baseDirectory);
        IsDirty = path is null;
    }

    /// <summary>Loads all logical definitions from a loose GTS file and selects the first member.</summary>
    /// <param name="path">Source file path, resolved against the process current directory when relative.</param>
    /// <returns>A clean editing session retaining the file's definition order.</returns>
    /// <exception cref="InvalidDataException">The loaded document contains no definitions.</exception>
    public static TilesheetDocument Open(string path)
    {
        path = Path.GetFullPath(path);
        var definitions = TilesheetDefinitionSerializer.LoadAll(path).ToList();
        if (definitions.Count == 0) throw new InvalidDataException("Document contains no definitions.");
        var document = new TilesheetDocument(definitions[0], path, Path.GetDirectoryName(path)!);
        document._definitions = definitions;
        return document;
    }

    /// <summary>Creates an unsaved GTS document with one default definition and marks it dirty.</summary>
    /// <param name="directory">Base directory for dependency resolution; no file or directory is created.</param>
    /// <returns>A new editing session with no loose or packed save location.</returns>
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

    /// <summary>Resolves the selected tilesheet's loose image path without checking existence.</summary>
    /// <returns>The full image path relative to <see cref="BaseDirectory"/>, or null if no loose image path is specified.</returns>
    public string? ResolveImagePath() => string.IsNullOrWhiteSpace(Definition.Image?.FilePath)
        ? null : Path.GetFullPath(Definition.Image.FilePath, BaseDirectory);

    /// <summary>Resolves the selected tilesheet's image archive path without opening it.</summary>
    /// <returns>The explicit archive path resolved against <see cref="BaseDirectory"/>, the document's own archive for an implicit packed entry, or null if neither applies.</returns>
    public string? ResolveAssetsFilePath() => string.IsNullOrWhiteSpace(Definition.Image?.AssetsFilePath)
        ? (string.IsNullOrWhiteSpace(Definition.Image?.AssetEntryName) ? null : PackedLocation?.Path)
        : Path.GetFullPath(Definition.Image.AssetsFilePath, BaseDirectory);

    /// <summary>Replaces the selected tilesheet's image source with a loose path and marks the document dirty.</summary>
    /// <param name="path">Image path to store unchanged; relative paths are later resolved against <see cref="BaseDirectory"/>.</param>
    /// <remarks>Clears any packed image reference without reading the image.</remarks>
    public void SetImage(string path)
    {
        // Changing the source is explicit. Unrelated edits never clear packed fields.
        Definition.Image = new() { FilePath = path };
        MarkChanged();
    }

    /// <summary>Replaces the selected tilesheet's image source with a packed entry and marks the document dirty.</summary>
    /// <param name="assetsFilePath">Archive path, normalized against the process current directory.</param>
    /// <param name="assetEntryName">Nonempty image entry name stored unchanged.</param>
    /// <remarks>Clears the loose image reference without checking archive or entry existence.</remarks>
    /// <exception cref="ArgumentException">Either argument is null, empty, or whitespace.</exception>
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

    /// <summary>Validates all logical definitions plus the selected tilesheet's image source and optional image bounds.</summary>
    /// <param name="imageSize">Selected source image dimensions, or null to omit image bounds checks.</param>
    /// <returns>Document, geometry, and selected image-source errors; empty when the performed checks pass.</returns>
    /// <remarks>Checks selected loose-image existence. A complete packed image reference is not opened or checked for entry existence.</remarks>
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

    /// <summary>Finds existing frame metadata without adding it or changing dirty state.</summary>
    /// <param name="region">Region whose stored frame metadata is searched.</param>
    /// <param name="x">Zero-based tile column; bounds are not validated.</param>
    /// <param name="y">Zero-based tile row; bounds are not validated.</param>
    /// <returns>The first matching metadata object, or null when the frame has no explicit metadata.</returns>
    public TilesheetFrameDefinition? FindFrame(TilesheetRegionDefinition region, int x, int y) =>
        region.Frames.FirstOrDefault(f => f is not null && f.XTile == x && f.YTile == y);

    /// <summary>Gets or adds mutable frame metadata without marking the document dirty.</summary>
    /// <param name="region">Region whose frame metadata is edited.</param>
    /// <param name="x">Zero-based tile column; bounds are not validated.</param>
    /// <param name="y">Zero-based tile row; bounds are not validated.</param>
    /// <returns>The existing metadata object or a new one appended to the region.</returns>
    /// <remarks>Call <see cref="MarkChanged"/> when committing an edit.</remarks>
    public TilesheetFrameDefinition EditFrame(TilesheetRegionDefinition region, int x, int y)
    {
        var frame = FindFrame(region, x, y);
        if (frame is not null) return frame;
        frame = new() { XTile = x, YTile = y };
        region.Frames.Add(frame);
        return frame;
    }

    /// <summary>Removes frame metadata outside the current grid and marks the document dirty if anything was removed.</summary>
    /// <param name="region">Region whose grid geometry determines valid metadata coordinates.</param>
    /// <returns>The number of frame metadata entries removed.</returns>
    public int RemoveOutOfGridMetadata(TilesheetRegionDefinition region)
    {
        var (columns, rows) = TilesheetDefinitionValidator.GridSize(region);
        int removed = region.Frames.RemoveAll(f => f is not null &&
            (f.XTile < 0 || f.YTile < 0 || f.XTile >= columns || f.YTile >= rows));
        if (removed > 0) MarkChanged();
        return removed;
    }

    /// <summary>Saves every logical member to a loose file, rebases dependencies, and adopts that file as the editing location.</summary>
    /// <param name="path">Destination path; missing parent directories are created and existing files may be overwritten.</param>
    /// <param name="imageSize">Known image dimensions for validating the selected tilesheet, or null to omit image bounds checks.</param>
    /// <param name="allowInvalid">Whether to bypass ordinary validation errors; logical identities must still be nonempty and unique.</param>
    /// <remarks>On success, clears the packed location and dirty flag and raises <see cref="Changed"/>. Saving a packed document leaves its original archive entry unchanged.</remarks>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="path"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidOperationException">Validation fails when <paramref name="allowInvalid"/> is false.</exception>
    /// <exception cref="InvalidDataException">Logical identities are empty or duplicated.</exception>
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

    /// <summary>Selects an existing member without changing the physical document's dirty state and raises <see cref="Changed"/>.</summary>
    /// <param name="definition">The member instance already owned by this document.</param>
    /// <exception cref="ArgumentException"><paramref name="definition"/> does not belong to this document.</exception>
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

    /// <summary>Removes a member, marks the document dirty, and selects the first remaining member if necessary.</summary>
    /// <param name="definition">The member instance to remove.</param>
    /// <returns>True if removed; false if it is absent or is the document's only member.</returns>
    public bool RemoveDefinition(TilesheetDefinition definition)
    {
        if (_definitions.Count == 1 || !_definitions.Remove(definition)) return false;
        if (ReferenceEquals(Definition, definition)) Definition = _definitions[0];
        MarkChanged();
        return true;
    }

    /// <summary>Renames the selected member and marks the document dirty, without rewriting references in other documents.</summary>
    /// <param name="identity">The new case-sensitive logical identity.</param>
    /// <exception cref="ArgumentNullException"><paramref name="identity"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="identity"/> is empty or whitespace.</exception>
    /// <exception cref="InvalidDataException">Another member already uses the identity.</exception>
    public void RenameDefinition(string identity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identity);
        if (_definitions.Any(member => !ReferenceEquals(member, Definition) && member.Name == identity))
            throw new InvalidDataException($"Duplicate definition identity '{identity}'.");
        Definition.Name = identity;
        MarkChanged();
    }

    /// <summary>Imports selected GTS members as independent copies, rebasing dependencies without copying resources.</summary>
    /// <param name="path">A same-format source file; legacy singletons are supported.</param>
    /// <param name="select">Selects zero-based source indices. Omit to import all; return null or an empty list to cancel.</param>
    /// <returns>The number imported in source order; zero leaves the document unchanged.</returns>
    /// <remarks>Conflicting logical identities receive unique suffixes. The first imported member becomes selected and the document is marked dirty.</remarks>
    /// <exception cref="ArgumentException">The file extension is not .gts, or selection contains duplicate or out-of-range indices.</exception>
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
