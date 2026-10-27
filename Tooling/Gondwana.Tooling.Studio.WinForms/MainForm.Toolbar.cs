using System.Drawing.Drawing2D;
using Gondwana.Tooling.Scenes.WinForms;

namespace Gondwana.Tooling.Studio.WinForms;

/// <summary>Studio-wide commands only; individual editors retain their specialized toolbars.</summary>
public sealed partial class MainForm
{
    private static readonly Size ToolbarButtonSize = new(48, 48);
    private readonly List<Image> _toolbarIcons = [];
    private ToolStripMenuItem _saveAllMenuItem = null!;
    private ToolStripButton _saveButton = null!;
    private ToolStripButton _saveAllButton = null!;
    private ToolStripButton _previewButton = null!;
    private ToolStripButton _validateButton = null!;
    private bool _savingAll;

    /// <summary>The Studio-level toolbar, below the menu strip and outside the editor dock panel.</summary>
    internal ToolStrip StudioToolbar { get; private set; } = null!;

    private ToolStrip BuildToolbar()
    {
        var bar = new ToolStrip
        {
            Name = "StudioToolbar",
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden,
            ImageScalingSize = new Size(24, 24),
            ShowItemToolTips = true
        };

        var create = new ToolStripDropDownButton
        {
            Name = "New",
            AutoSize = false,
            Size = ToolbarButtonSize,
            Image = Glyph("new"),
            DisplayStyle = ToolStripItemDisplayStyle.Image,
            ToolTipText = "New document (choose a Gondwana format)",
            AccessibleName = "New document"
        };
        foreach (var (format, label) in NewFileFormats)
        {
            var selectedFormat = format;
            create.DropDownItems.Add(label, null, (_, _) => TryNew(selectedFormat));
        }
        bar.Items.Add(create);
        AddButton(bar, "Open", "Open documents (Ctrl+O)", "open", OpenFiles);
        bar.Items.Add(new ToolStripSeparator());

        _saveButton = AddButton(bar, "Save", "Save active document (Ctrl+S)", "save",
            async () => { if (ActiveDocument is { } doc) await SaveDocument(doc); });
        _saveAllButton = AddButton(bar, "SaveAll", "Save all modified documents (Ctrl+Alt+S)", "save-all",
            async () => await SaveAllDocuments());
        bar.Items.Add(new ToolStripSeparator());

        AddButton(bar, "WorkingDirectory", "Choose working directory", "folder", ChooseDirectory);
        AddButton(bar, "Refresh", "Refresh working directory", "refresh", () =>
        {
            Browser.RefreshDirectory();
            SyncActiveDocumentSelection();
        });
        bar.Items.Add(new ToolStripSeparator());

        _previewButton = AddButton(bar, "Preview", "View active scene in Scene Viewer", "preview",
            async () =>
            {
                if (ActiveDocument?.Document.Editor is SceneEditorControl scene)
                    await scene.ViewSceneAsync();
            });
        _validateButton = AddButton(bar, "Validate", "Validate active document", "validate",
            () => ValidateActiveDocument());

        bar.Disposed += (_, _) =>
        {
            foreach (var image in _toolbarIcons) image.Dispose();
            _toolbarIcons.Clear();
        };

        _saveButton.Enabled = false;
        _saveAllButton.Enabled = false;
        _previewButton.Enabled = false;
        _validateButton.Enabled = false;
        _saveAllMenuItem.Enabled = false;
        return bar;
    }

    private ToolStripButton AddButton(ToolStrip bar, string name, string tooltip, string glyph, Action action)
    {
        var button = new ToolStripButton
        {
            Name = name,
            AutoSize = false,
            Size = ToolbarButtonSize,
            Image = Glyph(glyph),
            DisplayStyle = ToolStripItemDisplayStyle.Image,
            ToolTipText = tooltip,
            AccessibleName = tooltip
        };
        button.Click += (_, _) => action();
        bar.Items.Add(button);
        return button;
    }

    private void ToolbarDocumentChanged(object? sender, EventArgs e) => UpdateToolbarState();

    private void UpdateToolbarState()
    {
        if (_disposed || IsDisposed || _saveButton is null) return;
        var active = ActiveDocument;
        bool hasActive = active is { IsDisposed: false };
        _saveButton.Enabled = hasActive;
        _previewButton.Enabled = hasActive && active!.Document.Editor is SceneEditorControl;
        // Archive editors have no definition validator or Validation pane.
        _validateButton.Enabled = hasActive && active!.Document.Kind != "asset";
        // A focused PropertyGrid can have uncommitted edits while its model still reports clean.
        // Keep Save All invokable with open documents; its handler commits edits before checking dirty state.
        bool hasDocuments = _documents.Any(document => !document.IsDisposed);
        _saveAllButton.Enabled = !_savingAll && hasDocuments;
        _saveAllMenuItem.Enabled = !_savingAll && hasDocuments;
    }

    /// <summary>Saves modified documents sequentially, stopping at the first cancelled or failed save.</summary>
    /// <returns>True only when every modified document was saved.</returns>
    internal async Task<bool> SaveAllDocuments()
    {
        if (_savingAll) return false;
        _savingAll = true;
        UpdateToolbarState();
        try
        {
            // The GAF editor owns a buffered snapshot. Saving it before any packed child
            // lets subsequent entry saves rebase against the updated archive, while
            // SaveArchive still detects genuine same-entry conflicts.
            foreach (var document in _documents.ToArray().OrderBy(item => item.Document.Kind == "asset" ? 0 : 1))
            {
                if (document.IsDisposed) continue;
                // Commit pending PropertyGrid/text edits before deciding that a document is clean.
                if (!document.Document.CommitEdits()) return false;
                if (document.Document.Dirty() && !await SaveDocument(document)) return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            ReportError(ex);
            return false;
        }
        finally
        {
            _savingAll = false;
            UpdateToolbarState();
        }
    }

    /// <summary>Runs the active editor's existing validation and reveals its diagnostics on failure.</summary>
    /// <returns>Validation errors, or null when no active document exists or pending edits cannot be committed.</returns>
    internal IReadOnlyList<string>? ValidateActiveDocument()
    {
        if (ActiveDocument is not { IsDisposed: false } document || document.Document.Kind == "asset") return null;
        try
        {
            if (!document.Document.CommitEdits()) return null;
            var errors = document.Document.Validate();
            if (errors.Count == 0)
                _output.Log($"Validation passed: {document.Document.Caption}");
            else
            {
                document.Document.ShowPane("Validation");
                _output.Log($"Validation found {errors.Count} error(s): {document.Document.Caption}");
                foreach (var error in errors) _output.Log("  " + error);
            }
            return errors;
        }
        catch (Exception ex)
        {
            ReportError(ex);
            return null;
        }
    }

    private Image Glyph(string kind)
    {
        var bitmap = new Bitmap(24, 24);
        using (var g = Graphics.FromImage(bitmap))
        using (var pen = new Pen(ToolingAppearance.Palette.Foreground, 2f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
            LineJoin = LineJoin.Round
        })
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            void Lines(params PointF[] points) => g.DrawLines(pen, points);
            void Line(float x1, float y1, float x2, float y2) => g.DrawLine(pen, x1, y1, x2, y2);
            switch (kind)
            {
                case "new":
                    Lines(new(6, 3), new(15, 3), new(19, 7), new(19, 13));
                    Lines(new(15, 3), new(15, 7), new(19, 7));
                    Lines(new(6, 3), new(6, 21), new(13, 21));
                    Line(17, 16, 17, 22);
                    Line(14, 19, 20, 19);
                    break;
                case "open":
                case "folder":
                    Lines(new(2, 8), new(2, 5), new(9, 5), new(11, 8), new(21, 8), new(21, 19), new(3, 19), new(2, 8));
                    if (kind == "open")
                    {
                        Lines(new(5, 12), new(22, 12), new(19, 21), new(3, 21), new(5, 12));
                    }
                    break;
                case "save":
                case "save-all":
                    if (kind == "save-all")
                    {
                        Lines(new(3, 6), new(3, 3), new(17, 3), new(20, 6), new(20, 17));
                        g.TranslateTransform(2, 2);
                    }
                    Lines(new(4, 3), new(16, 3), new(20, 7), new(20, 21), new(4, 21), new(4, 3));
                    Lines(new(8, 3), new(8, 10), new(16, 10), new(16, 3));
                    Lines(new(8, 21), new(8, 14), new(16, 14), new(16, 21));
                    break;
                case "refresh":
                    g.DrawArc(pen, 4, 4, 16, 16, 35, 285);
                    Lines(new(17, 3), new(21, 5), new(20, 10));
                    break;
                case "preview":
                    Lines(new(8, 4), new(20, 12), new(8, 20), new(8, 4));
                    break;
                case "validate":
                    g.DrawEllipse(pen, 3, 3, 18, 18);
                    Lines(new(7, 12), new(11, 16), new(17, 9));
                    break;
            }
        }
        _toolbarIcons.Add(bitmap);
        return bitmap;
    }
}
