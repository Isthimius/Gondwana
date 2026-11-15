using Gondwana.Assets;
using Gondwana.Tooling.Assets;
using Gondwana.Tooling.Assets.WinForms;
using Gondwana.Tooling.Studio.WinForms.Documents;

namespace Gondwana.Tooling.Studio.WinForms;

public sealed partial class MainForm
{
    private readonly Dictionary<string, string?> _archivePasswords = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _failedRestoreArchives = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string>? _loadingDependencies;
    internal Action? BeforeArchiveInstall { get; set; }
    internal Func<StudioDocument, bool> ConfirmReload { get; set; } = model =>
        ThemedMessageBox.Show($"Discard unsaved changes and reload {model.Caption}?", "Reload document",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;

    private StudioArchiveSnapshot ReadArchive(string path, string? password = null)
    {
        path = Path.GetFullPath(path);
        if (_session.IsRestoring && _failedRestoreArchives.Contains(path))
            throw new OperationCanceledException($"Archive opening already failed or was cancelled during restoration: {path}. Open it manually to retry.");
        try { return ReadArchiveCore(path, password); }
        catch
        {
            // Suppress repeated authentication only for this startup restoration.
            // A missing/invalid definition entry must not suppress its valid siblings.
            if (_session.IsRestoring) _failedRestoreArchives.Add(path);
            throw;
        }
    }

    private StudioArchiveSnapshot ReadArchiveCore(string path, string? password)
    {
        path = Path.GetFullPath(path);
        if (!File.Exists(path)) throw new FileNotFoundException("Archive not found.", path);
        if (password is null) _archivePasswords.TryGetValue(path, out password);
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
            if (password is null) throw new OperationCanceledException("Archive opening cancelled.");
            var snapshot = StudioArchiveSnapshot.Open(path, password);
            _archivePasswords[path] = password;
            return snapshot;
        }
    }

    private StudioDocument CreateModel(AssetLocation location, string? password)
    {
        if (!File.Exists(location.Path)) throw new FileNotFoundException("Authoring file does not exist.", location.Path);
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
                if (format == "gaf") snapshot.Detach(); // the GAF editor owns this buffered snapshot
                model.ArchiveHash = snapshot.Hash;
                model.OriginalEntry = original;
                model.ArchivePassword = snapshot.Archive.Password;
                _archivePasswords[location.Path] = snapshot.Archive.Password;
            }
            else model = StudioDocument.Create(format, Browser.WorkingDirectory, location.Path, _packages);
            model.DependencyArchives.UnionWith(dependencies);
            return model;
        }
        finally { _loadingDependencies = null; }
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
            StudioArchiveSnapshot.CheckVersion(destination, model.ArchiveHash);
            using var current = StudioArchiveSnapshot.Open(destination, model.ArchivePassword);
            if (!StudioArchiveSnapshot.Entry(current.Archive, location).AsSpan().SequenceEqual(model.OriginalEntry))
                throw new IOException($"The entry changed since it was opened: {location}. Reload document, or Save As to retain your edits separately.");
            using var bytes = new MemoryStream(entryBytes, writable: false);
            current.Archive.Add(location.AssetType, location.AssetEntryName!, bytes);
            hash = StudioArchiveSnapshot.Commit(current.Archive, destination, model.ArchiveHash, BeforeArchiveInstall);
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
                AssetArchiveCopy.Save(editor.AssetsFile, destination, editor.AssetsFile.Password, editor.AssetsFile.UseEncryption);
                using var copied = StudioArchiveSnapshot.Open(destination, editor.AssetsFile.Password);
                hash = copied.Hash;
            }
            // An empty ZIP has no entries from which to infer its requested encryption policy.
            editor.AcceptSavedArchive(AssetsFile.LoadOrCreate(destination, editor.AssetsFile.Password,
                editor.AssetsFile.UseEncryption, register: false));
            model.ArchiveHash = hash;
            model.ArchivePassword = editor.AssetsFile.Password;
        }
        _archivePasswords[destination] = model.ArchivePassword;
        _packages.Invalidate(destination);
        using var persisted = StudioArchiveSnapshot.Open(destination, model.ArchivePassword);
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
                    parent.AcceptSavedArchive(saved.Detach(), externalRefresh: true);
                }
                else if (other != document && candidate.PackedLocation() is { } packed)
                {
                    try { candidate.ContentStale = !StudioArchiveSnapshot.Entry(persisted.Archive, packed).AsSpan().SequenceEqual(candidate.OriginalEntry); }
                    catch (FileNotFoundException) { candidate.ContentStale = true; }
                }
            }
            if (candidate.DependencyArchives.Contains(destination)) candidate.PreviewStale = true;
            other.UpdateCaption();
        }
    }

    internal bool ReloadDocument(StudioDockDocument document)
    {
        if (_saves.TryGetValue(document, out var pending) && !pending.IsCompleted) return false;
        if (document.Document.Location is not { } location) return false;
        if (!document.Document.CommitEdits()) return false;
        if (document.Document.Dirty() && !ConfirmReload(document.Document)) return false;
        try
        {
            _packages.Invalidate(location.Path);
            var replacement = CreateModel(location, null);
            document.CloseApproved = true;
            document.Close();
            ShowDocument(replacement);
            return true;
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception ex) { ReportError(ex); return false; }
    }

    internal void ReloadPreviews(StudioDockDocument document)
    {
        _loadingDependencies = document.Document.DependencyArchives;
        try
        {
            switch (document.Document.Editor)
            {
                case Gondwana.Tooling.Tilesheets.WinForms.TilesheetEditorControl editor: editor.ReloadImage(); break;
                case Gondwana.Tooling.Animations.WinForms.AnimationEditorControl editor: editor.ReloadSources(); break;
                case Gondwana.Tooling.Scenes.WinForms.SceneEditorControl editor: editor.ReloadSources(); break;
                case Gondwana.Tooling.Sprites.WinForms.SpriteEditorControl editor: editor.ReloadSources(); break;
            }
            document.Document.PreviewStale = false;
            document.UpdateCaption();
        }
        finally { _loadingDependencies = null; }
    }
}
