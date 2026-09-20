using Gondwana.Assets;
using WeifenLuo.WinFormsUI.Docking;

namespace Gondwana.Tooling.Assets.WinForms;

/// <summary>
/// Standalone-app docking wrapper for <see cref="AssetEditorControl"/>.
/// </summary>
internal sealed class AssetEditorDocument : DockContent
{
    private readonly AssetsFile _assetsFile;

    public AssetEditorControl Editor { get; }
    public string FilePath => Editor.FilePath;

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

    public void Save() => Editor.Save();
    public void SaveAs() => Editor.SaveAs();
    public void RefreshEntries() => Editor.RefreshEntries();

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _assetsFile.Dispose();
        base.OnFormClosed(e);
    }
}
