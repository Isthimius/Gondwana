using WeifenLuo.WinFormsUI.Docking;

namespace Gondwana.Tooling.Studio.WinForms.Documents;

internal sealed class StudioDockDocument : DockContent
{
    internal StudioDocument Document { get; }
    internal bool CloseApproved { get; set; }
    private readonly Func<StudioDockDocument, Task<bool>> _confirmClose;
    private bool _closePending;

    internal StudioDockDocument(StudioDocument document, Func<StudioDockDocument, Task<bool>> confirmClose)
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

    /// <inheritdoc/>
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        if (e.Cancel || CloseApproved) return;
        if (_closePending) { e.Cancel = true; return; }
        var approval = _confirmClose(this);
        if (approval.IsCompleted) { e.Cancel = !approval.GetAwaiter().GetResult(); return; }
        e.Cancel = true;
        _closePending = true;
        FinishCloseAsync(approval);
    }

    private async void FinishCloseAsync(Task<bool> approval)
    {
        try
        {
            if (await approval && !IsDisposed)
            {
                CloseApproved = true;
                Close();
            }
        }
        finally { _closePending = false; }
    }

    /// <inheritdoc/>
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
