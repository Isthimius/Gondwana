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

    /// <summary>
    /// Gets the document.
    /// </summary>
    public TilesheetDocument Document { get; }
    /// <summary>
    /// Gets the editor.
    /// </summary>
    public TilesheetEditorControl Editor { get; }
    /// <summary>
    /// Gets or sets whether close approved is enabled.
    /// </summary>
    public bool CloseApproved { get; set; }
    /// <summary>
    /// Gets the image size.
    /// </summary>
    public Size? ImageSize => Editor.ImageSize;

    /// <summary>
    /// Initializes a new instance of the <c>EditorDocument</c> class.
    /// </summary>
    /// <param name="document">The document displayed or edited by the control.</param>
    /// <param name="save">The callback that saves the document and reports whether saving succeeded.</param>
    /// <param name="overlaySettings">The overlay settings.</param>
    /// <param name="assetPackages">The asset packages.</param>
    /// <param name="packedImagePicker">The packed image picker.</param>
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

    /// <summary>
    /// Selects a loose image as the tilesheet source.
    /// </summary>
    /// <param name="path">The path.</param>
    public void ChooseImage(string? path = null) => Editor.ChooseImage(path);
    /// <summary>
    /// Selects an image from an assets file as the tilesheet source.
    /// </summary>
    /// <param name="source">The source.</param>
    public void ChoosePackedImage(PackedImageSource source) => Editor.ChoosePackedImage(source);
    /// <summary>
    /// Adds a new region to the tilesheet definition.
    /// </summary>
    public void AddRegion() => Editor.AddRegion();
    /// <summary>
    /// Refreshes the editor controls from the current document.
    /// </summary>
    public void RefreshView() => Editor.RefreshView();
    /// <summary>
    /// Validates the current document and refreshes the displayed diagnostics.
    /// </summary>
    /// <returns>The validation errors; an empty collection indicates that validation passed.</returns>
    public IReadOnlyList<string> UpdateValidation() => Editor.UpdateValidation();
    /// <summary>
    /// Commits pending editor input to the document.
    /// </summary>
    /// <returns><see langword="true"/> if the pending edits were committed; otherwise, <see langword="false"/>.</returns>
    public bool CommitEdits() => Editor.CommitEdits();
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

    /// <summary>
    /// Prompts to save pending changes before closing the document.
    /// </summary>
    /// <returns><see langword="true"/> if the document may be closed; otherwise, <see langword="false"/>.</returns>
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

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
            Document.Changed -= DocumentChanged;

        base.Dispose(disposing);
    }
}
