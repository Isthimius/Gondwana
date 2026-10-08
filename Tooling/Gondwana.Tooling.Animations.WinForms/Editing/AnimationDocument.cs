using Gondwana.Drawing.Animation;
using Gondwana.Drawing.Animation.GANI;

namespace Gondwana.Tooling.Animations.Editing;

/// <summary>
/// UI-independent editing session for one GANI animation definition.
/// </summary>
public sealed class AnimationDocument
{
    /// <summary>
    /// Gets the definition.
    /// </summary>
    public AnimationDefinition Definition { get; }
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

    private AnimationDocument(
        AnimationDefinition definition,
        string? filePath,
        string baseDirectory,
        bool isDirty)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        FilePath = filePath;
        BaseDirectory = Path.GetFullPath(baseDirectory);
        IsDirty = isDirty;
    }

    /// <summary>
    /// Opens the supplied file for use by the animation document.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The resulting animation document.</returns>
    public static AnimationDocument Open(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("GANI path must be a non-empty string.", nameof(path));

        path = Path.GetFullPath(path);
        return new AnimationDocument(
            AnimationDefinitionSerializer.Load(path),
            path,
            Path.GetDirectoryName(path)!,
            isDirty: false);
    }

    /// <summary>
    /// Creates a new animation document.
    /// </summary>
    /// <param name="directory">The directory.</param>
    /// <returns>The resulting animation document.</returns>
    public static AnimationDocument Create(string directory) =>
        new(
            new AnimationDefinition
            {
                Key = "Untitled",
                ThrottleTime = 0.1,
                CycleType = CycleType.Repeating,
                Source = AnimationDefinitionSource.Generated()
            },
            filePath: null,
            directory,
            isDirty: true);

    /// <summary>
    /// Marks the document as modified and notifies listeners.
    /// </summary>
    public void MarkChanged()
    {
        IsDirty = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Checks the definition and collects validation errors.
    /// </summary>
    /// <returns>The validation errors; an empty collection indicates that validation passed.</returns>
    public IReadOnlyList<string> Validate() =>
        AnimationDefinitionValidator.Validate(Definition);

    /// <summary>
    /// Adds or updates a tilesheet reference to a loose GTS file.
    /// </summary>
    /// <param name="tilesheet">The tilesheet.</param>
    /// <param name="gtsPath">The path to the loose GTS tilesheet definition.</param>
    /// <returns><see langword="true"/> if the source reference changed; otherwise, <see langword="false"/>.</returns>
    public bool SetLooseTilesheetSource(
        string tilesheet,
        string gtsPath)
    {
        if (string.IsNullOrWhiteSpace(tilesheet))
            throw new ArgumentException("Tilesheet name must be a non-empty string.", nameof(tilesheet));

        if (string.IsNullOrWhiteSpace(gtsPath))
            throw new ArgumentException("GTS path must be a non-empty string.", nameof(gtsPath));

        string fullPath = Path.GetFullPath(gtsPath);
        string persistedPath = MakeReferencePath(fullPath, BaseDirectory);

        var existing = Definition.TilesheetSources.FirstOrDefault(source =>
            string.Equals(
                source.Tilesheet,
                tilesheet,
                StringComparison.Ordinal));

        if (existing is not null &&
            existing.Kind == AnimationTilesheetSourceKind.LooseDefinitionFile &&
            !string.IsNullOrWhiteSpace(existing.GtsPath))
        {
            string existingFullPath = ResolveReferencePath(existing.GtsPath);
            if (string.Equals(
                    existingFullPath,
                    fullPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        Definition.TilesheetSources.RemoveAll(source =>
            string.Equals(
                source.Tilesheet,
                tilesheet,
                StringComparison.Ordinal));

        Definition.TilesheetSources.Add(
            AnimationTilesheetSourceDefinition.Loose(
                tilesheet,
                persistedPath));

        MarkChanged();
        return true;
    }

    /// <summary>
    /// Removes the named tilesheet source from the document.
    /// </summary>
    /// <param name="tilesheet">The tilesheet.</param>
    /// <returns><see langword="true"/> if the entry was found and removed; otherwise, <see langword="false"/>.</returns>
    public bool RemoveTilesheetSource(string tilesheet)
    {
        int removed = Definition.TilesheetSources.RemoveAll(source =>
            string.Equals(
                source.Tilesheet,
                tilesheet,
                StringComparison.Ordinal));

        if (removed == 0)
            return false;

        MarkChanged();
        return true;
    }

    /// <summary>
    /// Resolves a document-relative resource path to an absolute path.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The absolute path of the referenced resource.</returns>
    public string ResolveReferencePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Reference path must be a non-empty string.", nameof(path));

        return Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(path, BaseDirectory);
    }

    /// <summary>
    /// Saves the current content to the destination file.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <param name="allowInvalid">Whether to save even when validation reports errors.</param>
    public void Save(string path, bool allowInvalid = false)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("GANI path must be a non-empty string.", nameof(path));

        var errors = Validate();
        if (errors.Count > 0 && !allowInvalid)
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));

        path = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);

        // Snapshot through the official serializer so the editor never maintains a
        // separate persistence schema.
        var snapshot = AnimationDefinitionSerializer.FromJson(
            AnimationDefinitionSerializer.ToJson(Definition));

        RebaseTilesheetSources(
            snapshot,
            BaseDirectory,
            directory);

        var temporary = Path.Combine(
            directory,
            $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");

        try
        {
            AnimationDefinitionSerializer.Save(temporary, snapshot);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }

        Definition.TilesheetSources = snapshot.TilesheetSources
            .Select(CloneTilesheetSource)
            .ToList();

        FilePath = path;
        BaseDirectory = directory;
        IsDirty = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static void RebaseTilesheetSources(
        AnimationDefinition definition,
        string oldBaseDirectory,
        string newBaseDirectory)
    {
        foreach (var source in definition.TilesheetSources)
        {
            switch (source.Kind)
            {
                case AnimationTilesheetSourceKind.LooseDefinitionFile
                    when !string.IsNullOrWhiteSpace(source.GtsPath):
                    source.GtsPath = MakeReferencePath(
                        ResolveReferencePath(
                            source.GtsPath,
                            oldBaseDirectory),
                        newBaseDirectory);
                    break;

                case AnimationTilesheetSourceKind.PackedDefinitionFile
                    when !string.IsNullOrWhiteSpace(source.AssetsFilePath):
                    source.AssetsFilePath = MakeReferencePath(
                        ResolveReferencePath(
                            source.AssetsFilePath,
                            oldBaseDirectory),
                        newBaseDirectory);
                    break;
            }
        }
    }

    private static AnimationTilesheetSourceDefinition CloneTilesheetSource(
        AnimationTilesheetSourceDefinition source) =>
        new()
        {
            Tilesheet = source.Tilesheet,
            Kind = source.Kind,
            GtsPath = source.GtsPath,
            AssetsFilePath = source.AssetsFilePath,
            AssetEntryName = source.AssetEntryName
        };

    private static string ResolveReferencePath(
        string path,
        string baseDirectory) =>
        Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(path, baseDirectory);

    private static string MakeReferencePath(
        string path,
        string baseDirectory)
    {
        string fullPath = Path.GetFullPath(path);
        string fullBaseDirectory = Path.GetFullPath(baseDirectory);

        string? pathRoot = Path.GetPathRoot(fullPath);
        string? baseRoot = Path.GetPathRoot(fullBaseDirectory);

        if (!string.Equals(
                pathRoot,
                baseRoot,
                StringComparison.OrdinalIgnoreCase))
        {
            return fullPath;
        }

        return Path.GetRelativePath(
            fullBaseDirectory,
            fullPath);
    }
}
