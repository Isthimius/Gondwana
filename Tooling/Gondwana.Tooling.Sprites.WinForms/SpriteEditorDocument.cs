using Gondwana.Tooling.Sprites.Editing;
using WeifenLuo.WinFormsUI.Docking;

namespace Gondwana.Tooling.Sprites.WinForms;

internal sealed class SpriteEditorDocument : DockContent
{
    private readonly Func<SpriteEditorDocument, bool, bool> _save;

    public SpriteDocument Document { get; }
    public SpriteEditorControl Editor { get; }
    public bool CloseApproved { get; set; }

    public SpriteEditorDocument(
        SpriteDocument document,
        Func<SpriteEditorDocument, bool, bool> save)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        _save = save ?? throw new ArgumentNullException(nameof(save));
        Editor = new SpriteEditorControl(document);

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

    public void AddSceneSources(IEnumerable<string> paths) =>
        Editor.AddSceneSources(paths);

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
            "Unsaved sprite definition",
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
                ? "Untitled.gspr"
                : Path.GetFileName(Document.FilePath)) +
            (Document.IsDirty ? " *" : string.Empty);

        ToolTipText =
            Document.FilePath ??
            "Unsaved GSPR definition";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            Document.Changed -= DocumentChanged;

        base.Dispose(disposing);
    }
}

