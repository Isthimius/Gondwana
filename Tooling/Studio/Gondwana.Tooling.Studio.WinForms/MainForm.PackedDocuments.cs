using System.ComponentModel;
using Gondwana.Assets;
using Gondwana.Tooling.Assets;
using Gondwana.Tooling.Studio.Assets.WinForms;
using Gondwana.Tooling.Studio.Shared.WinForms.Theming;
using Gondwana.Tooling.Studio.WinForms.Documents;

namespace Gondwana.Tooling.Studio.WinForms;

public sealed partial class MainForm
{
    private readonly Dictionary<string, string?> _archivePasswords = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _failedRestoreArchives = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string>? _loadingDependencies;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal Action? BeforeArchiveInstall { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal Func<StudioDocument, bool> ConfirmReload { get; set; } = model =>
        ThemedMessageBox.Show($"Discard unsaved changes and reload {model.Caption}?", "Reload document",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;

    private StudioArchiveSnapshot ReadArchive(string path, string? password = null)
    {
        path = Path.GetFullPath(path);
        if (_session.IsRestoring && _failedRestoreArchives.Contains(path))
            throw new OperationCanceledException($"Archive opening already failed or was cancelled during restoration: {path}. Open it manually to retry.");
        try
        {
            return ReadArchiveCore(path, password);
        }
        catch
        {
            // Suppress repeated authentication only for this startup restoration.
            // A missing/invalid definition entry must not suppress its valid siblings.
            if (_session.IsRestoring)
                _failedRestoreArchives.Add(path);
            throw;
        }
    }

    private StudioArchiveSnapshot ReadArchiveCore(string path, string? password)
    {
        path = Path.GetFullPath(path);
        if (!File.Exists(path))
            throw new FileNotFoundException("Archive not found.", path);
        if (password is null)
            _archivePasswords.TryGetValue(path, out password);
        try
        {
            var snapshot = StudioArchiveSnapshot.Open(path, password);
            _archivePasswords[path] = password;
            return snapshot;
        }
        catch
        {
            _archivePasswords.Remove(path);
            password = AssetPassword(path);
            if (password is null)
                throw new OperationCanceledException("Archive opening cancelled.");
            var snapshot = StudioArchiveSnapshot.Open(path, password);
            _archivePasswords[path] = password;
            return snapshot;
        }
    }

    private StudioDocument CreateModel(AssetLocation location, string? password)
    {
        if (!File.Exists(location.Path))
            throw new FileNotFoundException("Authoring file does not exist.", location.Path);
        string format = (location.AssetEntryName is null ? StudioDocument.FormatFor(location.Path) : StudioDocument.FormatFor(location.AssetType))
            ?? throw new NotSupportedException($"Unsupported document: {location}");
        var dependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _loadingDependencies = dependencies;
        try
        {
            StudioDocument model;
            if (location.AssetEntryName is not null || format == "gaf")
            {
                using var snapshot = ReadArchive(location.Path, password);
                var original = location.AssetEntryName is null ? null : StudioArchiveSnapshot.Entry(snapshot.Archive, location);
                model = StudioDocument.Create(format, Path.GetDirectoryName(location.Path)!, location.Path,
                    _packages, archive: snapshot.Archive, entryName: location.AssetEntryName);
                if (format == "gaf")
                    snapshot.Detach(); // the GAF editor owns this buffered snapshot
                model.ArchiveHash = snapshot.Hash;
                model.OriginalEntry = original;
                model.ArchivePassword = snapshot.Archive.Password;
                _archivePasswords[location.Path] = snapshot.Archive.Password;
            }
            else
                model = StudioDocument.Create(format, Browser.WorkingDirectory, location.Path, _packages);
            model.DependencyArchives.UnionWith(dependencies);
            return model;
        }
        finally
        {
            _loadingDependencies = null;
        }
    }

    private void SaveArchive(StudioDockDocument document, string destination, byte[]? entryBytes)
    {
        var model = document.Document;
        byte[] hash;
        if (entryBytes is not null)
        {
            var location = model.PackedLocation()!;
            var parent = _documents.FirstOrDefault(item => item.Document.Kind == "asset" &&
                string.Equals(item.Document.Path(), destination, StringComparison.OrdinalIgnoreCase));
            if (parent?.Document.Dirty() == true)
                throw new InvalidOperationException("The GAF editor has unsaved changes. Save it first, then retry this packed save. Conflicting entry changes will require Reload document or Save As.");
            using var current = StudioArchiveSnapshot.Open(destination, model.ArchivePassword);
            if (!StudioArchiveSnapshot.Entry(current.Archive, location).AsSpan().SequenceEqual(model.OriginalEntry))
                throw new IOException($"The entry changed since it was opened: {location}. Reload document, or Save As to retain your edits separately.");
            using var bytes = new MemoryStream(entryBytes, writable: false);
            current.Archive.Add(location.AssetType, location.AssetEntryName!, bytes);
            hash = StudioArchiveSnapshot.Commit(current.Archive, destination, current.Hash, BeforeArchiveInstall);
            model.ArchiveHash = hash;
            model.OriginalEntry = entryBytes;
            model.AcceptPackedSave!();
        }
        else
        {
            var editor = (AssetEditorControl)model.Editor;
            if (string.Equals(editor.FilePath, destination, StringComparison.OrdinalIgnoreCase))
                hash = StudioArchiveSnapshot.Commit(editor.AssetsFile, destination, model.ArchiveHash, BeforeArchiveInstall);
            else
            {
                // Existing Save As semantics: copy/rebase into a new archive, then adopt it.
                hash = AssetArchiveCopy.SaveVersioned(editor.AssetsFile, destination, editor.AssetsFile.Password, editor.AssetsFile.UseEncryption);
            }
            // An empty ZIP has no entries from which to infer its requested encryption policy.
            using var savedArchive = StudioArchiveSnapshot.Open(destination, editor.AssetsFile.Password);
            if (!savedArchive.Hash.AsSpan().SequenceEqual(hash))
            {
                var changedDestination = new AssetLocation(AssetTypes.Misc, destination);
                throw new DocumentSourceConflictException(changedDestination, DocumentSourceVersion.Read(changedDestination));
            }

            if (editor.AssetsFile.UseEncryption && !savedArchive.Archive.GetAllEntries().Any())
            {
                // Empty ZIPs cannot encode encryption; retain the editor's explicit policy.
                var empty = AssetsFile.LoadOrCreate(destination, editor.AssetsFile.Password, true, register: false);
                try
                {
                    StudioArchiveSnapshot.CheckVersion(destination, hash);
                    editor.AcceptSavedArchive(empty);
                }
                catch
                {
                    empty.Dispose();
                    throw;
                }
            }
            else
                editor.AcceptSavedArchive(savedArchive.Detach());
            model.ArchiveHash = hash;
            model.ArchivePassword = editor.AssetsFile.Password;
        }

        _archivePasswords[destination] = model.ArchivePassword;
        _packages.Invalidate(destination);
        using var persisted = StudioArchiveSnapshot.Open(destination, model.ArchivePassword);
        if (!persisted.Hash.AsSpan().SequenceEqual(hash))
            throw new DocumentSourceConflictException(model.Location!, DocumentSourceVersion.Read(model.Location!, model.ArchivePassword));
        foreach (var other in _documents)
        {
            var candidate = other.Document;
            if (candidate.Location is { } location && string.Equals(location.Path, destination, StringComparison.OrdinalIgnoreCase))
            {
                candidate.ArchiveHash = hash;
                candidate.ArchivePassword = model.ArchivePassword;
                if (other != document && candidate.Editor is AssetEditorControl parent && !candidate.Dirty())
                {
                    using var saved = StudioArchiveSnapshot.Open(destination, model.ArchivePassword);
                    if (!saved.Hash.AsSpan().SequenceEqual(hash))
                        throw new DocumentSourceConflictException(location, DocumentSourceVersion.Read(location, model.ArchivePassword));
                    parent.AcceptSavedArchive(saved.Detach(), externalRefresh: true);
                    candidate.ContentStale = false;
                    candidate.SourceProblem = null;
                }
                else if (other != document && candidate.PackedLocation() is { } packed)
                {
                    try
                    {
                        candidate.ContentStale = !StudioArchiveSnapshot.Entry(persisted.Archive, packed).AsSpan().SequenceEqual(candidate.OriginalEntry);
                    }
                    catch (FileNotFoundException)
                    {
                        candidate.ContentStale = true;
                    }
                }
            }

            foreach (var source in candidate.Sources.Values.Where(s => s.Role == DocumentSourceRole.Dependency &&
                string.Equals(s.Location.Path, destination, StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    var contentHash = source.Location.AssetEntryName is null ? Convert.ToHexString(hash) :
                        DocumentSourceVersion.Hash(StudioArchiveSnapshot.Entry(persisted.Archive, source.Location));
                    if (source.Accepted.ContentHash != contentHash)
                        candidate.PreviewStale = true;
                }
                catch (FileNotFoundException)
                {
                    candidate.PreviewStale = true;
                }
            }

            other.UpdateCaption();
        }
    }

    internal bool ReloadDocument(StudioDockDocument document)
    {
        if (_saves.TryGetValue(document, out var pending) && !pending.IsCompleted)
            return false;
        if (document.Document.Location is not { } location)
            return false;
        if (!document.Document.CommitEdits())
            return false;
        if (document.Document.Dirty() && !ConfirmReload(document.Document))
            return false;
        StudioDocument? replacement = null;
        StudioDockDocument? opened = null;
        var active = ActiveDocument;
        var selected = document.Document.SelectedMember;
        var focused = FocusedControlPath(document.Document.Editor);
        float? zoom = document.Document.Editor switch
        {
            Gondwana.Tooling.Studio.Tilesheets.WinForms.TilesheetEditorControl e => e.PreviewZoom,
            Gondwana.Tooling.Studio.Scenes.WinForms.SceneEditorControl e => e.PreviewZoom,
            _ => null
        };
        try
        {
            foreach (var source in document.Document.Sources.Keys)
                _packages.Invalidate(source.Path);
            _packages.Invalidate(location.Path);
            replacement = CreateModel(location, document.Document.ArchivePassword);
            if (selected is not null)
                replacement.SelectMember(selected);
            opened = ShowDocument(replacement, document);
            replacement = null; // The new dock owns it now.
            if (opened.Document.Editor is Gondwana.Tooling.Studio.Tilesheets.WinForms.TilesheetEditorControl sheet)
                sheet.PreviewZoom = zoom;
            if (opened.Document.Editor is Gondwana.Tooling.Studio.Scenes.WinForms.SceneEditorControl scene)
                scene.PreviewZoom = zoom;
            document.CloseApproved = true;
            document.Close();
            if (active is not null && active != document && !active.IsDisposed)
                active.Activate();
            else
                RestoreFocusedControl(opened.Document.Editor, focused);
            InvalidateDependencies();
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex)
        {
            ReportError(ex);
            return false;
        }
        finally
        {
            replacement?.Dispose();
            if (!document.IsDisposed)
            {
                // Replacement construction/show failed: restore the original registry and editor.
                if (opened is not null)
                    opened.Dispose();
                _paths[location] = document;
                if (active == document)
                    document.Activate();
            }
        }
    }

    private static int[] FocusedControlPath(Control root)
    {
        var result = new List<int>();
        while (root.Controls.Cast<Control>().FirstOrDefault(c => c.ContainsFocus) is { } focused)
        {
            result.Add(root.Controls.IndexOf(focused));
            root = focused;
        }

        return result.ToArray();
    }

    private static void RestoreFocusedControl(Control root, int[] path)
    {
        foreach (var index in path)
        {
            if (index >= root.Controls.Count)
                break;
            root = root.Controls[index];
        }

        if (root.CanFocus)
            root.Focus();
    }

    internal void ReloadPreviews(StudioDockDocument document)
    {
        if (document.IsDisposed)
            return;
        RefreshSourceRegistrations(document);
        var versions = document.Document.Sources.Where(p => p.Value.Role == DocumentSourceRole.Dependency)
            .ToDictionary(p => p.Key, p => DocumentSourceVersion.Read(p.Key, _archivePasswords.GetValueOrDefault(p.Key.Path)), DocumentLocationComparer.Instance);
        foreach (var source in versions.Keys)
            _packages.Invalidate(source.Path);
        _loadingDependencies = document.Document.DependencyArchives;
        try
        {
            // Editor reload methods can report failures as diagnostics instead of throwing.
            // Never adopt source versions when an editor failed to load its previews.
            bool loaded = document.Document.Editor switch
            {
                Gondwana.Tooling.Studio.Tilesheets.WinForms.TilesheetEditorControl editor => ReloadImage(editor),
                Gondwana.Tooling.Studio.Animations.WinForms.AnimationEditorControl editor => ReloadAnimation(editor),
                Gondwana.Tooling.Studio.Scenes.WinForms.SceneEditorControl editor => ReloadScene(editor),
                Gondwana.Tooling.Studio.Sprites.WinForms.SpriteEditorControl editor => ReloadSprite(editor),
                Gondwana.Tooling.Audio.WinForms.AudioEditorControl editor => editor.UpdateValidation().Count == 0,
                _ => true
            };
            var newlyUnavailable = versions.Any(pair =>
                pair.Value.Status != DocumentSourceStatus.Available &&
                pair.Value != document.Document.Sources[pair.Key].Accepted);
            if (loaded && !newlyUnavailable)
            {
                // Unavailable sources already in the accepted inventory are not new preview
                // changes; retain their baseline while adopting successfully refreshed sources.
                var acceptedVersions = versions.ToDictionary(pair => pair.Key, pair =>
                    pair.Value.Status == DocumentSourceStatus.Available
                        ? pair.Value : document.Document.Sources[pair.Key].Accepted,
                    DocumentLocationComparer.Instance);
                RefreshSourceRegistrations(document, acceptedVersions);
                document.Document.PreviewStale = false;
            }
            else
                document.Document.PreviewStale = true;
            document.UpdateCaption();
        }
        catch (Exception ex)
        {
            document.Document.PreviewStale = true;
            document.UpdateCaption();
            ReportError(ex);
        }
        finally
        {
            _loadingDependencies = null;
        }

        static bool ReloadImage(Gondwana.Tooling.Studio.Tilesheets.WinForms.TilesheetEditorControl editor)
        {
            editor.ReloadImage();
            return editor.PreviewLoadSucceeded;
        }

        static bool ReloadAnimation(Gondwana.Tooling.Studio.Animations.WinForms.AnimationEditorControl editor)
        {
            editor.ReloadSources();
            return editor.SourcesLoadedSuccessfully;
        }

        static bool ReloadScene(Gondwana.Tooling.Studio.Scenes.WinForms.SceneEditorControl editor)
        {
            editor.ReloadSources();
            return editor.SourcesLoadedSuccessfully;
        }

        static bool ReloadSprite(Gondwana.Tooling.Studio.Sprites.WinForms.SpriteEditorControl editor)
        {
            editor.ReloadSources();
            return editor.SourcesLoadedSuccessfully;
        }
    }
}
