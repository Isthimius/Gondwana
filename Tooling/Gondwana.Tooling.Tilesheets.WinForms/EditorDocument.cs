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

    /// <summary>Gets the mutable document hosted in this dock tab.</summary>
    public TilesheetDocument Document { get; }
    /// <summary>Gets the editor control owned and disposed by this dock tab.</summary>
    public TilesheetEditorControl Editor { get; }
    /// <summary>Gets or sets whether closing bypasses the pending-edit and unsaved-change checks.</summary>
    public bool CloseApproved { get; set; }
    /// <summary>Gets the loaded preview dimensions, or null when no image preview is available.</summary>
    public Size? ImageSize => Editor.ImageSize;

    /// <summary>Creates a dock tab hosting a tilesheet editor and tracking document changes.</summary>
    /// <param name="document">The mutable document to edit.</param>
    /// <param name="save">Callback receiving this tab and a Save As flag; close confirmation passes false.</param>
    /// <param name="overlaySettings">Shared preview overlay settings, or null for defaults.</param>
    /// <param name="assetPackages">Borrowed package cache, or null for an editor-owned cache.</param>
    /// <param name="packedImagePicker">Optional host image picker; returning null cancels selection.</param>
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

        ToolingAppearance.Apply(this);
        UpdateCaption();
    }

    /// <summary>Sets a loose image source and refreshes the editor.</summary>
    /// <param name="path">Image path, or null to show the file picker.</param>
    public void ChooseImage(string? path = null) => Editor.ChooseImage(path);
    /// <summary>Sets a packed image source and refreshes the editor.</summary>
    /// <param name="source">The archive entry identifying the image.</param>
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

        var result = ThemedMessageBox.Show(
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
