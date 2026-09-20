using Gondwana.Tooling.Animations.Editing;
using WeifenLuo.WinFormsUI.Docking;

namespace Gondwana.Tooling.Animations.WinForms;

internal sealed class AnimationEditorDocument : DockContent
{
    private readonly Func<AnimationEditorDocument, bool, bool> _save;

    public AnimationDocument Document { get; }
    public AnimationEditorControl Editor { get; }
    public bool CloseApproved { get; set; }

    public AnimationEditorDocument(
        AnimationDocument document,
        Func<AnimationEditorDocument, bool, bool> save)
    {
        Document = document;
        _save = save;
        Editor = new AnimationEditorControl(document);

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

    public bool ConfirmClose()
    {
        if (!CommitEdits())
            return false;

        if (!Document.IsDirty)
            return true;

        var result = MessageBox.Show(
            this,
            $"Save changes to {Text.TrimEnd(' ', '*')}?",
            "Unsaved animation",
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
        Text = (
            Document.FilePath is null
                ? Document.Definition.Key
                : Path.GetFileName(Document.FilePath)) +
            (Document.IsDirty ? " *" : string.Empty);

        ToolTipText =
            Document.FilePath ??
            "Unsaved GANI definition";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            Document.Changed -= DocumentChanged;

        base.Dispose(disposing);
    }
}
