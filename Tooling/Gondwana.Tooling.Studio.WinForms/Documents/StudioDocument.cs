using Gondwana.Assets;
using Gondwana.Tooling.Sprites.Editing;
using Gondwana.Tooling.Sprites.WinForms;
using Gondwana.Tooling.Animations.Editing;
using Gondwana.Tooling.Animations.WinForms;
using Gondwana.Tooling.Assets.WinForms;
using Gondwana.Tooling.Audio.Editing;
using Gondwana.Tooling.Audio.WinForms;
using Gondwana.Tooling.Scenes.Editing;
using Gondwana.Tooling.Scenes.WinForms;
using Gondwana.Tooling.Tilesheets.Editing;
using Gondwana.Tooling.Tilesheets.WinForms;
using Gondwana.Tooling.Tilesheets.Sources;

namespace Gondwana.Tooling.Studio.WinForms.Documents;

/// <summary>Shell bindings only: the owning tooling project implements editing and serialization.</summary>
internal sealed class StudioDocument : IDisposable
{
    internal required UserControl Editor { get; init; }
    internal required string Kind { get; init; }
    internal required string Extension { get; init; }
    internal required Func<string?> Path { get; init; }
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

    internal static string? FormatFor(string path) => System.IO.Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".gaf" or ".zip" => "gaf",
        ".gts" => "gts",
        ".gani" => "gani",
        ".gsnd" => "gsnd",
        ".gscn" => "gscn",
        ".gspr" => "gspr",
        _ => null
    };

    internal static StudioDocument Create(string format, string directory, string? path = null,
        AssetPackageCatalog? packages = null, string? password = null, bool encrypt = false)
    {
        StudioDocument result;
        switch (format)
        {
            case "gts":
                {
                    var model = path is null ? TilesheetDocument.Create(directory) : TilesheetDocument.Open(path);
                    var editor = packages is null ? new TilesheetEditorControl(model) : new TilesheetEditorControl(model, packages);
                    if (packages is not null)
                        editor.PackedImagePicker = owner => PackedImagePicker.Pick(owner, packages, directory);
                    result = new()
                    {
                        Kind = "tilesheet",
                        Extension = "gts",
                        Editor = editor,
                        Path = () => model.FilePath,
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
                    var model = path is null ? AnimationDocument.Create(directory) : AnimationDocument.Open(path);
                    var editor = new AnimationEditorControl(model);
                    result = new()
                    {
                        Kind = "animation",
                        Extension = "gani",
                        Editor = editor,
                        Path = () => model.FilePath,
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
                    var model = path is null ? AudioDocument.Create(directory) : AudioDocument.Open(path);
                    var editor = new AudioEditorControl(model);
                    result = new()
                    {
                        Kind = "sound",
                        Extension = "gsnd",
                        Editor = editor,
                        Path = () => model.FilePath,
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
                    var model = path is null ? SceneDocument.Create(directory) : SceneDocument.Open(path);
                    var editor = new SceneEditorControl(model);
                    result = new()
                    {
                        Kind = "scene",
                        Extension = "gscn",
                        Editor = editor,
                        Path = () => model.FilePath,
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
                    var model = path is null ? SpriteDocument.Create(directory) : SpriteDocument.Open(path);
                    var editor = new SpriteEditorControl(model);
                    result = new()
                    {
                        Kind = "sprite",
                        Extension = "gspr",
                        Editor = editor,
                        Path = () => model.FilePath,
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
                    if (path is null) throw new ArgumentException("Choose an asset file path first.");
                    var assets = AssetsFile.LoadOrCreate(path, password, encrypt || !string.IsNullOrEmpty(password), register: false);
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
                    catch { assets.Dispose(); throw; }
                }
            default: throw new NotSupportedException($"Unsupported authoring format: {format}");
        }
    }

    private void OnChanged(object? sender, EventArgs e) => Changed?.Invoke(this, EventArgs.Empty);

    public void Dispose()
    {
        _detach?.Invoke();
        _detach = null;
        Editor.Dispose();
        _release?.Invoke();
        _release = null;
    }
}
