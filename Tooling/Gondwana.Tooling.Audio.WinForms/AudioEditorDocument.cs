using Gondwana.Tooling.Audio.Editing;
using WeifenLuo.WinFormsUI.Docking;

namespace Gondwana.Tooling.Audio.WinForms;

internal sealed class AudioEditorDocument : DockContent
{
    private readonly Func<AudioEditorDocument, bool, bool> _save;

    public AudioDocument Document { get; }
    public AudioEditorControl Editor { get; }
    public bool CloseApproved { get; set; }

    public AudioEditorDocument(
        AudioDocument document,
        Func<AudioEditorDocument, bool, bool> save)
    {
        Document = document;
        _save = save;
        Editor = new AudioEditorControl(document);

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

    public void AddLooseFiles(IEnumerable<string> paths) =>
        Editor.AddLooseFiles(paths);

    public bool ConfirmClose()
    {
        if (!CommitEdits())
            return false;

        if (!Document.IsDirty)
            return true;

        var result = MessageBox.Show(
            this,
            $"Save changes to {Text.TrimEnd(' ', '*')}?",
            "Unsaved audio definition",
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
                ? "Untitled.gaud"
                : Path.GetFileName(Document.FilePath)) +
            (Document.IsDirty ? " *" : string.Empty);

        ToolTipText =
            Document.FilePath ??
            "Unsaved GAUD definition";
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            Document.Changed -= DocumentChanged;

        base.Dispose(disposing);
    }
}
