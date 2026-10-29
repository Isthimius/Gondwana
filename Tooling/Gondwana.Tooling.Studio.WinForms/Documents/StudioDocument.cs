using Gondwana.Assets;
using Gondwana.Tooling.Animations.Editing;
using Gondwana.Tooling.Animations.WinForms;
using Gondwana.Tooling.Assets;
using Gondwana.Tooling.Assets.WinForms;
using Gondwana.Tooling.Audio.Editing;
using Gondwana.Tooling.Audio.WinForms;
using Gondwana.Tooling.Editing;
using Gondwana.Tooling.Scenes.Editing;
using Gondwana.Tooling.Scenes.WinForms;
using Gondwana.Tooling.Sprites.Editing;
using Gondwana.Tooling.Sprites.WinForms;
using Gondwana.Tooling.Tilesheets.Editing;
using Gondwana.Tooling.Tilesheets.Sources;
using Gondwana.Tooling.Tilesheets.WinForms;

namespace Gondwana.Tooling.Studio.WinForms.Documents;

/// <summary>Shell bindings only: the owning tooling project implements editing and serialization.</summary>
internal sealed class StudioDocument : IDisposable
{
    internal required UserControl Editor { get; init; }
    internal required string Kind { get; init; }
    internal required string Extension { get; init; }
    internal string? SaveDirectoryHint { get; set; }
    internal required Func<string?> Path { get; init; }
    internal Func<AssetLocation?> PackedLocation { get; init; } = () => null;
    internal Func<byte[]>? SerializePacked { get; init; }
    internal Action? AcceptPackedSave { get; init; }
    internal byte[]? ArchiveHash { get; set; }
    internal byte[]? OriginalEntry { get; set; }
    internal string? ArchivePassword { get; set; }
    internal bool PreviewStale { get; set; }
    internal bool ContentStale { get; set; }
    internal HashSet<string> DependencyArchives { get; } = new(StringComparer.OrdinalIgnoreCase);
    internal AssetLocation? Location => PackedLocation() ?? (Path() is { } path
        ? new AssetLocation(TypeFor(Extension), System.IO.Path.GetFullPath(path)) : null);
    internal string Caption => PackedLocation() is { } packed
        ? $"{packed.AssetEntryName} ({System.IO.Path.GetFileName(packed.Path)})"
        : Path() is { } path ? System.IO.Path.GetFileName(path) : $"Untitled.{Extension}";
    internal string Tooltip => Location?.ToString() ?? $"Unsaved {Kind}";

    internal static AssetTypes TypeFor(string format) => format switch
    {
        "gts" => AssetTypes.TilesheetDefinition,
        "gani" => AssetTypes.AnimationDefinition,
        "gscn" => AssetTypes.SceneDefinition,
        "gspr" => AssetTypes.SpriteDefinition,
        "gsnd" => AssetTypes.AudioDefinition,
        _ => AssetTypes.Misc
    };

    internal static string? FormatFor(AssetTypes type) => type switch
    {
        AssetTypes.TilesheetDefinition => "gts",
        AssetTypes.AnimationDefinition => "gani",
        AssetTypes.SceneDefinition => "gscn",
        AssetTypes.SpriteDefinition => "gspr",
        AssetTypes.AudioDefinition => "gsnd",
        _ => null
    };

    internal required Func<bool> Dirty { get; init; }
    internal required Func<bool> CommitEdits { get; init; }
    internal required Func<IReadOnlyList<string>> Validate { get; init; }
    internal required Action<string, bool> Save { get; init; }
    internal required IReadOnlyList<string> PaneNames { get; init; }
    internal required Func<string, bool> ShowPane { get; init; }
    internal required Func<string, bool> IsPaneVisible { get; init; }
    internal required Action ShowAllPanes { get; init; }
    internal required Action ResetLayout { get; init; }
    private Action? _detach;
    private Action? _release;
    internal event EventHandler? Changed;

    internal EditHistory? History => Editor switch
    {
        TilesheetEditorControl e => e.Document.History,
        SceneEditorControl e => e.Document.History,
        AnimationEditorControl e => e.Document.History,
        SpriteEditorControl e => e.Document.History,
        AudioEditorControl e => e.Document.History,
        _ => null
    };

    internal string? SelectedMember => Editor switch
    {
        TilesheetEditorControl e => e.Document.Definition.Name,
        AnimationEditorControl e => e.Document.Definition.Key,
        SceneEditorControl e => e.Document.Definition.ID,
        SpriteEditorControl e => e.Document.Definition.Key,
        AudioEditorControl e => e.Document.Definition.Key,
        _ => null
    };

    internal bool SelectMember(string identity)
    {
        return Editor switch
        {
            TilesheetEditorControl e => Select(e.Document.Definitions, x => x.Name, e.SelectDefinition),
            AnimationEditorControl e => Select(e.Document.Definitions, x => x.Key, e.SelectDefinition),
            SceneEditorControl e => Select(e.Document.Definitions, x => x.ID, e.SelectDefinition),
            SpriteEditorControl e => Select(e.Document.Definitions, x => x.Key, e.SelectDefinition),
            AudioEditorControl e => Select(e.Document.Definitions, x => x.Key, e.SelectDefinition),
            _ => false
        };

        bool Select<T>(IReadOnlyList<T> members, Func<T, string> key, Action<T> select)
        {
            var matches = members.Where(x => DefinitionReferenceQuery.MembersEqual(TypeFor(Extension), key(x), identity)).ToArray();
            if (matches.Length != 1) return false;
            select(matches[0]);
            return true;
        }
    }

    /// <summary>Identifies the authoring format using shared package recognition.</summary>
    /// <param name="path">Physical document path.</param>
    /// <returns>The supported editor format, or null.</returns>
    internal static string? FormatFor(string path) => Gondwana.Tooling.Assets.AssetArchiveFormat.IsArchive(path) ? "gaf" : System.IO.Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".gts" => "gts",
        ".gani" => "gani",
        ".gsnd" => "gsnd",
        ".gscn" => "gscn",
        ".gspr" => "gspr",
        _ => null
    };

    internal static StudioDocument Create(string format, string directory, string? path = null,
        AssetPackageCatalog? packages = null, string? password = null, bool encrypt = false,
        AssetsFile? archive = null, string? entryName = null)
    {
        StudioDocument result;

        switch (format)
        {
            case "gts":
                {
                    var model = archive is not null ?
                                TilesheetDocument.Open(archive, entryName!) :
                                path is null ? TilesheetDocument.Create(directory) : TilesheetDocument.Open(path);

                    var editor = packages is null ?
                                 new TilesheetEditorControl(model) :
                                 new TilesheetEditorControl(model, packages);

                    if (packages is not null)
                        editor.PackedImagePicker = owner => PackedImagePicker.Pick(owner, packages, directory);

                    result = new()
                    {
                        Kind = "tilesheet",
                        Extension = "gts",
                        Editor = editor,
                        Path = () => model.FilePath,
                        PackedLocation = () => model.PackedLocation,
                        SerializePacked = model.SerializePacked,
                        AcceptPackedSave = model.AcceptPackedSave,
                        Dirty = () => model.IsDirty,
                        CommitEdits = editor.CommitEdits,
                        Validate = editor.UpdateValidation,
                        Save = (destination, invalid) => model.Save(destination, editor.ImageSize, invalid),
                        PaneNames = TilesheetEditorControl.PaneNames,
                        ShowPane = editor.ShowPane,
                        IsPaneVisible = editor.IsPaneVisible,
                        ShowAllPanes = editor.ShowAllPanes,
                        ResetLayout = editor.ResetLayout
                    };

                    model.Changed += result.OnChanged;
                    result._detach = () => model.Changed -= result.OnChanged;

                    return result;
                }

            case "gani":
                {
                    var model = archive is not null ?
                                AnimationDocument.Open(archive, entryName!) :
                                path is null ? AnimationDocument.Create(directory) : AnimationDocument.Open(path);

                    var editor = new AnimationEditorControl(model, packages);

                    result = new()
                    {
                        Kind = "animation",
                        Extension = "gani",
                        Editor = editor,
                        Path = () => model.FilePath,
                        PackedLocation = () => model.PackedLocation,
                        SerializePacked = model.SerializePacked,
                        AcceptPackedSave = model.AcceptPackedSave,
                        Dirty = () => model.IsDirty,
                        CommitEdits = editor.CommitEdits,
                        Validate = editor.UpdateValidation,
                        Save = model.Save,
                        PaneNames = AnimationEditorControl.PaneNames,
                        ShowPane = editor.ShowPane,
                        IsPaneVisible = editor.IsPaneVisible,
                        ShowAllPanes = editor.ShowAllPanes,
                        ResetLayout = editor.ResetLayout
                    };

                    model.Changed += result.OnChanged;
                    result._detach = () => model.Changed -= result.OnChanged;

                    return result;
                }

            case "gsnd":
                {
                    var model = archive is not null ?
                                AudioDocument.Open(archive, entryName!) :
                                path is null ? AudioDocument.Create(directory) : AudioDocument.Open(path);

                    var editor = new AudioEditorControl(model, packages);

                    result = new()
                    {
                        Kind = "sound",
                        Extension = "gsnd",
                        Editor = editor,
                        Path = () => model.FilePath,
                        PackedLocation = () => model.PackedLocation,
                        SerializePacked = model.SerializePacked,
                        AcceptPackedSave = model.AcceptPackedSave,
                        Dirty = () => model.IsDirty,
                        CommitEdits = editor.CommitEdits,
                        Validate = editor.UpdateValidation,
                        Save = model.Save,
                        PaneNames = AudioEditorControl.PaneNames,
                        ShowPane = editor.ShowPane,
                        IsPaneVisible = editor.IsPaneVisible,
                        ShowAllPanes = editor.ShowAllPanes,
                        ResetLayout = editor.ResetLayout
                    };

                    model.Changed += result.OnChanged;
                    result._detach = () => model.Changed -= result.OnChanged;

                    return result;
                }

            case "gscn":
                {
                    var model = archive is not null ?
                                SceneDocument.Open(archive, entryName!) :
                                path is null ? SceneDocument.Create(directory) : SceneDocument.Open(path);

                    var editor = new SceneEditorControl(model, packages);

                    result = new()
                    {
                        Kind = "scene",
                        Extension = "gscn",
                        Editor = editor,
                        Path = () => model.FilePath,
                        PackedLocation = () => model.PackedLocation,
                        SerializePacked = model.SerializePacked,
                        AcceptPackedSave = model.AcceptPackedSave,
                        Dirty = () => model.IsDirty,
                        CommitEdits = editor.CommitEdits,
                        Validate = editor.UpdateValidation,
                        Save = model.Save,
                        PaneNames = SceneEditorControl.PaneNames,
                        ShowPane = editor.ShowPane,
                        IsPaneVisible = editor.IsPaneVisible,
                        ShowAllPanes = editor.ShowAllPanes,
                        ResetLayout = editor.ResetLayout
                    };

                    model.Changed += result.OnChanged;
                    result._detach = () => model.Changed -= result.OnChanged;

                    return result;
                }

            case "gspr":
                {
                    var model = archive is not null ?
                                SpriteDocument.Open(archive, entryName!) :
                                path is null ? SpriteDocument.Create(directory) : SpriteDocument.Open(path);

                    var editor = new SpriteEditorControl(model, packages);

                    result = new()
                    {
                        Kind = "sprite",
                        Extension = "gspr",
                        Editor = editor,
                        Path = () => model.FilePath,
                        PackedLocation = () => model.PackedLocation,
                        SerializePacked = model.SerializePacked,
                        AcceptPackedSave = model.AcceptPackedSave,
                        Dirty = () => model.IsDirty,
                        CommitEdits = editor.CommitEdits,
                        Validate = editor.UpdateValidation,
                        Save = model.Save,
                        PaneNames = SpriteEditorControl.PaneNames,
                        ShowPane = editor.ShowPane,
                        IsPaneVisible = editor.IsPaneVisible,
                        ShowAllPanes = editor.ShowAllPanes,
                        ResetLayout = editor.ResetLayout
                    };

                    model.Changed += result.OnChanged;
                    result._detach = () => model.Changed -= result.OnChanged;

                    return result;
                }

            case "gaf":
                {
                    if (path is null)
                        throw new ArgumentException("Choose an asset file path first.");

                    var assets = archive ?? AssetsFile.LoadOrCreate(path, password, encrypt || !string.IsNullOrEmpty(password), register: false);

                    try
                    {
                        var editor = new AssetEditorControl(assets);
                        result = new()
                        {
                            Kind = "asset",
                            Extension = System.IO.Path.GetExtension(path).TrimStart('.'),
                            Editor = editor,
                            Path = () => editor.FilePath,
                            Dirty = () => editor.IsDirty || !File.Exists(editor.FilePath),
                            CommitEdits = () => true,
                            Validate = () => [],
                            Save = (destination, _) => editor.SaveTo(destination),
                            PaneNames = AssetEditorControl.PaneNames,
                            ShowPane = editor.ShowPane,
                            IsPaneVisible = editor.IsPaneVisible,
                            ShowAllPanes = editor.ShowAllPanes,
                            ResetLayout = editor.ResetLayout,
                            _release = assets.Dispose
                        };

                        editor.Changed += result.OnChanged;
                        result._detach = () => editor.Changed -= result.OnChanged;

                        return result;
                    }
                    catch
                    {
                        assets.Dispose();
                        throw;
                    }
                }

            default: throw new NotSupportedException($"Unsupported authoring format: {format}");
        }
    }

    private void OnChanged(object? sender, EventArgs e) => Changed?.Invoke(this, EventArgs.Empty);

    /// <summary>Detaches model change handlers, disposes the editor, and releases the owned GAF archive when present.</summary>
    public void Dispose()
    {
        _detach?.Invoke();
        _detach = null;

        Editor.Dispose();
        History?.Dispose();

        _release?.Invoke();
        _release = null;
    }
}
