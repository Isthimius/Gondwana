using WeifenLuo.WinFormsUI.Docking;

namespace Gondwana.Tooling.Studio.WinForms.Documents;

internal sealed class StudioDockDocument : DockContent
{
    internal StudioDocument Document { get; }
    internal bool CloseApproved { get; set; }
    private readonly Func<StudioDockDocument, bool> _confirmClose;

    internal StudioDockDocument(StudioDocument document, Func<StudioDockDocument, bool> confirmClose)
    {
        Document = document;
        _confirmClose = confirmClose;
        DockAreas = DockAreas.Document | DockAreas.Float;
        Controls.Add(document.Editor);
        document.Changed += DocumentChanged;
        UpdateCaption();
    }

    internal void UpdateCaption()
    {
        Text = (Document.Path() is { } path ? Path.GetFileName(path) : $"Untitled.{Document.Extension}")
            + (Document.Dirty() ? " *" : "");
        ToolTipText = Document.Path() ?? $"Unsaved {Document.Kind}";
    }

    private void DocumentChanged(object? sender, EventArgs e) => UpdateCaption();

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        if (!e.Cancel && !CloseApproved)
            e.Cancel = !_confirmClose(this);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Document.Changed -= DocumentChanged;
            Document.Dispose();
        }
        base.Dispose(disposing);
    }
}
