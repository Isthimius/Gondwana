using Gondwana.Tooling.Scenes.Editing;
using WeifenLuo.WinFormsUI.Docking;

namespace Gondwana.Tooling.Scenes.WinForms;

internal sealed class SceneEditorDocument : DockContent
{
    private readonly Func<SceneEditorDocument, bool, bool> _save;

    public SceneDocument Document { get; }
    public SceneEditorControl Editor { get; }
    public bool CloseApproved { get; set; }

    public SceneEditorDocument(
        SceneDocument document,
        Func<SceneEditorDocument, bool, bool> save)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        _save = save ?? throw new ArgumentNullException(nameof(save));
        Editor = new SceneEditorControl(document);

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

    public bool CommitEdits() => Editor.CommitEdits();

    public IReadOnlyList<string> UpdateValidation() =>
        Editor.UpdateValidation();

    public void AddTilesheetSources(IEnumerable<string> paths) =>
        Editor.AddTilesheetSources(paths);

    public void AddAnimationSources(IEnumerable<string> paths) =>
        Editor.AddAnimationSources(paths);

    public bool ShowPane(string paneName) =>
        Editor.ShowPane(paneName);

    public bool IsPaneVisible(string paneName) =>
        Editor.IsPaneVisible(paneName);

    public void ShowAllPanes() =>
        Editor.ShowAllPanes();

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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            Document.Changed -= DocumentChanged;

        base.Dispose(disposing);
    }
}
