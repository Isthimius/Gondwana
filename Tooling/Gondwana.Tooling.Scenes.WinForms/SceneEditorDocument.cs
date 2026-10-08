using Gondwana.Tooling.Scenes.Editing;
using WeifenLuo.WinFormsUI.Docking;

namespace Gondwana.Tooling.Scenes.WinForms;

internal sealed class SceneEditorDocument : DockContent
{
    private readonly Func<SceneEditorDocument, bool, bool> _save;

    /// <summary>
    /// Gets the document.
    /// </summary>
    public SceneDocument Document { get; }
    /// <summary>
    /// Gets the editor.
    /// </summary>
    public SceneEditorControl Editor { get; }
    /// <summary>
    /// Gets or sets whether close approved is enabled.
    /// </summary>
    public bool CloseApproved { get; set; }

    /// <summary>
    /// Initializes a new instance of the <c>SceneEditorDocument</c> class.
    /// </summary>
    /// <param name="document">The document displayed or edited by the control.</param>
    /// <param name="save">The callback that saves the document and reports whether saving succeeded.</param>
    public SceneEditorDocument(
        SceneDocument document,
        Func<SceneEditorDocument, bool, bool> save)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        _save = save ?? throw new ArgumentNullException(nameof(save));
        Editor = new SceneEditorControl(document);
        Editor.SaveRequested = () => _save(this, false);

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
    /// Commits pending editor input to the document.
    /// </summary>
    /// <returns><see langword="true"/> if the pending edits were committed; otherwise, <see langword="false"/>.</returns>
    public bool CommitEdits() => Editor.CommitEdits();

    /// <summary>
    /// Validates the current document and refreshes the displayed diagnostics.
    /// </summary>
    /// <returns>The validation errors; an empty collection indicates that validation passed.</returns>
    public IReadOnlyList<string> UpdateValidation() =>
        Editor.UpdateValidation();

    /// <summary>
    /// Adds tilesheet definition files to the document's source list.
    /// </summary>
    /// <param name="paths">The paths of the source files to add.</param>
    public void AddTilesheetSources(IEnumerable<string> paths) =>
        Editor.AddTilesheetSources(paths);

    /// <summary>
    /// Adds animation definition files to the document's source list.
    /// </summary>
    /// <param name="paths">The paths of the source files to add.</param>
    public void AddAnimationSources(IEnumerable<string> paths) =>
        Editor.AddAnimationSources(paths);

    /// <summary>
    /// Shows the named editor pane.
    /// </summary>
    /// <param name="paneName">The name of the editor pane.</param>
    /// <returns><see langword="true"/> if the named pane was found; otherwise, <see langword="false"/>.</returns>
    public bool ShowPane(string paneName) =>
        Editor.ShowPane(paneName);

    /// <summary>
    /// Determines whether the named editor pane is visible.
    /// </summary>
    /// <param name="paneName">The name of the editor pane.</param>
    /// <returns><see langword="true"/> if the pane is visible; otherwise, <see langword="false"/>.</returns>
    public bool IsPaneVisible(string paneName) =>
        Editor.IsPaneVisible(paneName);

    /// <summary>
    /// Shows every editor pane.
    /// </summary>
    public void ShowAllPanes() =>
        Editor.ShowAllPanes();

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
            "Unsaved scene definition",
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Warning);

        return result == DialogResult.No ||
               result == DialogResult.Yes &&
               _save(this, false);
    }

    private void DocumentChanged(object? sender, EventArgs e) =>
        UpdateCaption();

    private void UpdateCaption()
    {
        Text = (Document.FilePath is null
                ? "Untitled.gscn"
                : Path.GetFileName(Document.FilePath)) +
            (Document.IsDirty ? " *" : string.Empty);

        ToolTipText =
            Document.FilePath ??
            "Unsaved GSCN definition";
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
            Document.Changed -= DocumentChanged;

        base.Dispose(disposing);
    }
}
