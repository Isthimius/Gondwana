using Gondwana.Assets;
using WeifenLuo.WinFormsUI.Docking;

namespace Gondwana.Tooling.Assets.WinForms;

/// <summary>
/// Standalone-app docking wrapper for <see cref="AssetEditorControl"/>.
/// </summary>
internal sealed class AssetEditorDocument : DockContent
{
    private readonly AssetsFile _assetsFile;
    internal bool CloseApproved { get; set; }
    internal Func<DialogResult> AskSave { get; set; }

    /// <summary>Gets the editor control owned by this dock tab.</summary>
    public AssetEditorControl Editor { get; }
    /// <summary>Gets the full path of the archive currently displayed by the editor.</summary>
    public string FilePath => Editor.FilePath;

    /// <summary>Creates a dock tab and takes responsibility for disposing its initial archive when the tab is disposed.</summary>
    /// <param name="assetsFile">The loaded archive to edit.</param>
    /// <param name="workspaceChanged">Callback invoked when saving changes the workspace's files.</param>
    /// <param name="isDocumentOpen">Callback testing whether a destination path is already open, to prevent Save As collisions.</param>
    public AssetEditorDocument(
        AssetsFile assetsFile,
        Action workspaceChanged,
        Func<string, bool> isDocumentOpen)
    {
        _assetsFile = assetsFile ??
            throw new ArgumentNullException(nameof(assetsFile));

        Editor = new AssetEditorControl(assetsFile)
        {
            WorkspaceChanged = workspaceChanged ??
                throw new ArgumentNullException(nameof(workspaceChanged)),
            IsDocumentOpen = isDocumentOpen ??
                throw new ArgumentNullException(nameof(isDocumentOpen))
        };
        AskSave = () => MessageBox.Show(this, $"Save changes to {Path.GetFileName(FilePath)}?",
            "Unsaved changes", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

        Text = Path.GetFileName(FilePath);
        TabText = Text;
        ToolTipText = FilePath;
        DockAreas = DockAreas.Document | DockAreas.Float;

        Controls.Add(Editor);
        DarkTheme.Apply(this);
    }

    /// <summary>
    /// Saves the current content to the destination file.
    /// </summary>
    public void Save() => Editor.Save();
    /// <summary>Prompts to save an archive copy without changing the current document path or dirty state.</summary>
    public void SaveAs() => Editor.SaveAs();
    /// <summary>Refreshes the filtered entry list from the editor's in-memory archive.</summary>
    public void RefreshEntries() => Editor.RefreshEntries();
    /// <summary>
    /// Shows the named editor pane.
    /// </summary>
    /// <param name="paneName">The name of the editor pane.</param>
    /// <returns><see langword="true"/> if the named pane was found; otherwise, <see langword="false"/>.</returns>
    public bool ShowPane(string paneName) => Editor.ShowPane(paneName);
    /// <summary>
    /// Determines whether the named editor pane is visible.
    /// </summary>
    /// <param name="paneName">The name of the editor pane.</param>
    /// <returns><see langword="true"/> if the pane is visible; otherwise, <see langword="false"/>.</returns>
    public bool IsPaneVisible(string paneName) => Editor.IsPaneVisible(paneName);
    /// <summary>
    /// Shows every editor pane.
    /// </summary>
    public void ShowAllPanes() => Editor.ShowAllPanes();

    internal bool ConfirmClose()
    {
        if (!Editor.IsDirty) return true;
        switch (AskSave())
        {
            case DialogResult.No: return true;
            case DialogResult.Yes:
                Editor.Save();
                return !Editor.IsDirty;
            default: return false;
        }
    }

    /// <inheritdoc/>
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        if (!e.Cancel && !CloseApproved) e.Cancel = !ConfirmClose();
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _assetsFile.Dispose();
    }
}
