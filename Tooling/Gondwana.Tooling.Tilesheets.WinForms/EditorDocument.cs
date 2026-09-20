using Gondwana.Tooling.Tilesheets.Editing;
using Gondwana.Tooling.Tilesheets.Sources;
using WeifenLuo.WinFormsUI.Docking;

namespace Gondwana.Tooling.Tilesheets.WinForms;

/// <summary>
/// Standalone-app docking wrapper for <see cref="TilesheetEditorControl"/>.
/// Editor behavior lives in the hostable control rather than this DockContent.
/// </summary>
internal sealed class EditorDocument : DockContent
{
    private readonly Func<EditorDocument, bool, bool> _save;

    public TilesheetDocument Document { get; }
    public TilesheetEditorControl Editor { get; }
    public bool CloseApproved { get; set; }
    public Size? ImageSize => Editor.ImageSize;

    public EditorDocument(
        TilesheetDocument document,
        Func<EditorDocument, bool, bool> save,
        OverlaySettings? overlaySettings = null,
        AssetPackageCatalog? assetPackages = null,
        Func<IWin32Window, PackedImageSource?>? packedImagePicker = null)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        _save = save ?? throw new ArgumentNullException(nameof(save));
        Editor = new TilesheetEditorControl(document, overlaySettings, assetPackages)
        {
            PackedImagePicker = packedImagePicker
        };

        DockAreas = DockAreas.Document | DockAreas.Float;
        Controls.Add(Editor);

        Document.Changed += DocumentChanged;
        FormClosing += (_, e) =>
        {
            if (!CloseApproved)
                e.Cancel = !ConfirmClose();
        };

        DarkTheme.Apply(this);
        UpdateCaption();
    }

    public void ChooseImage(string? path = null) => Editor.ChooseImage(path);
    public void ChoosePackedImage(PackedImageSource source) => Editor.ChoosePackedImage(source);
    public void AddRegion() => Editor.AddRegion();
    public void RefreshView() => Editor.RefreshView();
    public IReadOnlyList<string> UpdateValidation() => Editor.UpdateValidation();
    public bool CommitEdits() => Editor.CommitEdits();

    public bool ConfirmClose()
    {
        if (!CommitEdits())
            return false;

        if (!Document.IsDirty)
            return true;

        var result = MessageBox.Show(
            this,
            $"Save changes to {Text.TrimEnd(' ', '*')}?",
            "Unsaved definition",
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Warning);

        return result == DialogResult.No ||
               result == DialogResult.Yes &&
               _save(this, false);
    }

    private void DocumentChanged(object? sender, EventArgs e) => UpdateCaption();

    private void UpdateCaption()
    {
        Text = (
            Document.FilePath is null
                ? Document.Definition.Name
                : Path.GetFileName(Document.FilePath)) +
            (Document.IsDirty ? " *" : string.Empty);

        ToolTipText = Document.FilePath ?? "Unsaved GTS definition";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            Document.Changed -= DocumentChanged;

        base.Dispose(disposing);
    }
}
