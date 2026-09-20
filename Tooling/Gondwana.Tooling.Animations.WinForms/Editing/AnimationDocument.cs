using Gondwana.Drawing.Animation;
using Gondwana.Drawing.Animation.GANI;

namespace Gondwana.Tooling.Animations.Editing;

/// <summary>
/// UI-independent editing session for one GANI animation definition.
/// </summary>
public sealed class AnimationDocument
{
    public AnimationDefinition Definition { get; }
    public string? FilePath { get; private set; }
    public string BaseDirectory { get; private set; }
    public bool IsDirty { get; private set; }
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

    public void MarkChanged()
    {
        IsDirty = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlyList<string> Validate() =>
        AnimationDefinitionValidator.Validate(Definition);

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

        FilePath = path;
        BaseDirectory = directory;
        IsDirty = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
