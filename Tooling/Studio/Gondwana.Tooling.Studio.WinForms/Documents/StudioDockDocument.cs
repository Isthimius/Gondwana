using WeifenLuo.WinFormsUI.Docking;

namespace Gondwana.Tooling.Studio.WinForms.Documents;

internal sealed class StudioDockDocument : DockContent
{
    internal StudioDocument Document { get; }
    internal bool CloseApproved { get; set; }
    private readonly Func<StudioDockDocument, Task<bool>> _confirmClose;
    private bool _closePending;
    private readonly Icon _tabIcon;

    internal StudioDockDocument(StudioDocument document, Func<StudioDockDocument, Task<bool>> confirmClose)
    {
        Document = document;
        _confirmClose = confirmClose;
        _tabIcon = StudioFileTypeIcons.CreateDocumentIcon(document.Extension);
        Icon = _tabIcon;
        DockAreas = DockAreas.Document | DockAreas.Float;
        Controls.Add(document.Editor);
        document.Changed += DocumentChanged;
        UpdateCaption();
    }

    internal void UpdateCaption()
    {
        Text = Document.Caption
             + (Document.Dirty() ? " *" : "")
             + (Document.ContentStale || Document.PreviewStale ? " [reload]" : "");

        ToolTipText = Document.Tooltip
                    + (Document.ContentStale ? "\nThis entry changed. Use File > Reload document from disk, or Save As to retain your edits separately." : "")
                    + (Document.PreviewStale ? "\nDependencies changed. Use File > Reload dependency previews." : "");
    }

    private void DocumentChanged(object? sender, EventArgs e) => UpdateCaption();

    /// <summary>Defers closing until the host approves saving or discarding the document's edits.</summary>
    /// <param name="e">Closing event whose cancellation flag is set while approval is pending or denied.</param>
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);

        if (e.Cancel || CloseApproved)
            return;

        if (_closePending)
        {
            e.Cancel = true;
            return;
        }

        var approval = _confirmClose(this);
        if (approval.IsCompleted)
        {
            e.Cancel = !approval.GetAwaiter().GetResult();
            return;
        }

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
        finally
        {
            _closePending = false;
        }
    }

    /// <summary>Unsubscribes from document changes and disposes the owned document and dock content.</summary>
    /// <param name="disposing">Whether to release the managed document and controls.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Document.Changed -= DocumentChanged;
            Document.Dispose();
        }

        base.Dispose(disposing);
        if (disposing)
            _tabIcon.Dispose();
    }
}
