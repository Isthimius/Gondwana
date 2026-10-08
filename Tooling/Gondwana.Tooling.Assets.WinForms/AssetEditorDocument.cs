using Gondwana.Assets;
using WeifenLuo.WinFormsUI.Docking;

namespace Gondwana.Tooling.Assets.WinForms;

/// <summary>
/// Standalone-app docking wrapper for <see cref="AssetEditorControl"/>.
/// </summary>
internal sealed class AssetEditorDocument : DockContent
{
    private readonly AssetsFile _assetsFile;

    /// <summary>
    /// Gets the editor.
    /// </summary>
    public AssetEditorControl Editor { get; }
    /// <summary>
    /// Gets the file path.
    /// </summary>
    public string FilePath => Editor.FilePath;

    /// <summary>
    /// Initializes a new instance of the <c>AssetEditorDocument</c> class.
    /// </summary>
    /// <param name="assetsFile">The assets file containing the requested entry.</param>
    /// <param name="workspaceChanged">The workspace changed.</param>
    /// <param name="isDocumentOpen">The is document open.</param>
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
    /// <summary>
    /// Saves as.
    /// </summary>
    public void SaveAs() => Editor.SaveAs();
    /// <summary>
    /// Refreshes entries.
    /// </summary>
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

    /// <inheritdoc/>
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _assetsFile.Dispose();
        base.OnFormClosed(e);
    }
}
