using Gondwana.Tooling.Scenes.WinForms;
using Gondwana.Tooling.WinForms;

namespace Gondwana.Tooling.Studio.WinForms;

/// <summary>Studio-wide commands only; individual editors retain their specialized toolbars.</summary>
public sealed partial class MainForm
{
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
        var bar = new DpiToolStrip(48, 24)
        {
            Name = "StudioToolbar",
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden,
            // Enable native ToolStrip tooltips after theme application; the shared theme
            // otherwise replaces them with mouse-move tooltips on the whole strip.
            ShowItemToolTips = false
        };

        var create = new ToolStripDropDownButton
        {
            Name = "New",
            AutoToolTip = false,
            AutoSize = false,
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
        bar.SetGlyph(create, pixels => ToolbarGlyphs.Studio("new", pixels, ToolingAppearance.Palette));
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

        _saveButton.Enabled = false;
        _saveAllButton.Enabled = false;
        _previewButton.Enabled = false;
        _validateButton.Enabled = false;
        _saveAllMenuItem.Enabled = false;
        return bar;
    }

    private ToolStripButton AddButton(DpiToolStrip bar, string name, string tooltip, string glyph, Action action)
    {
        var button = new ToolStripButton
        {
            Name = name,
            AutoToolTip = false,
            AutoSize = false,
            DisplayStyle = ToolStripItemDisplayStyle.Image,
            ToolTipText = tooltip,
            AccessibleName = tooltip
        };
        button.Click += (_, _) => action();
        bar.Items.Add(button);
        bar.SetGlyph(button, pixels => ToolbarGlyphs.Studio(glyph, pixels, ToolingAppearance.Palette));
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
                _output.Log($"Info: Validation passed: {document.Document.Caption}");
            else
            {
                document.Document.ShowPane("Validation");
                _output.Log($"Error: Validation found {errors.Count} error(s): {document.Document.Caption}");
                foreach (var error in errors) _output.Log("  Error: " + error);
            }
            return errors;
        }
        catch (Exception ex)
        {
            ReportError(ex);
            return null;
        }
    }


}
