using Gondwana.Assets;
using Gondwana.Tooling.Assets;
using Gondwana.Tooling.Studio.Shared.WinForms;

namespace Gondwana.Tooling.Studio.Assets.WinForms;

public sealed partial class MainForm
{
    private readonly EditorSourceMonitorController _sources = new();
    private void MonitorDocument(AssetEditorDocument tab)
    {
        var editor = tab.Editor;
        AssetLocation Owner() => new(AssetTypes.Misc, editor.FilePath);
        tab.Sources = _sources.Register(tab, new()
        {
            Location = Owner,
            ReferenceOwner = Owner,
            Accepted = () => editor.SourceVersion,
            Dirty = () => editor.IsDirty,
            Commit = editor.CommitEdits,
            Roots = () => DocumentSourceDependencies.ArchiveRoots(editor.AssetsFile),
            Archive = true,
            Password = path => string.Equals(path, editor.FilePath, StringComparison.OrdinalIgnoreCase) ? editor.AssetsFile.Password : null,
            SaveAs = editor.SaveAs,
            RefreshPreviews = editor.RefreshEntries,
            Reload = () =>
            {
                var selected = editor.SelectedEntry;
                var version = DocumentSourceVersion.Read(Owner());
                if (version.Status != DocumentSourceStatus.Available)
                    throw new IOException("The archive is missing or inaccessible. The current document has been retained.");
                var loaded = AssetsFile.LoadOrCreate(editor.FilePath, editor.AssetsFile.Password, editor.AssetsFile.UseEncryption, register: false);
                try
                {
                    _ = loaded.GetAllEntries().ToArray();
                    DocumentSourceSave.Verify(Owner(), version);
                    EditorSourceMonitorController.Replace(tab, () =>
                    {
                        var replacement = ShowDocument(loaded, version);
                        replacement.Editor.SelectEntry(selected);
                        return replacement;
                    }, () => tab.CloseApproved = true);
                }
                catch { loaded.Dispose(); throw; }
            }
        });
        editor.Changed += tab.Sources.Changed;
        editor.SourceSaved += tab.Sources.Saved;
        editor.SourceSaveFailed = tab.Sources.SaveFailed;
        var workspaceChanged = editor.WorkspaceChanged;
        editor.WorkspaceChanged = () =>
        {
            // Save As is a separate copy; its unchanged accepted version must not clear a conflict.
            tab.Sources.Changed(editor, EventArgs.Empty);
            workspaceChanged();
        };
        tab.Disposed += (_, _) => { editor.Changed -= tab.Sources.Changed; editor.SourceSaved -= tab.Sources.Saved; };
    }
}
