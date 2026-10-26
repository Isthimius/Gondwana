using Gondwana.Audio.GSND;
using Gondwana.Tooling.Audio.Editing;
using Gondwana.Tooling.WinForms;
using WeifenLuo.WinFormsUI.Docking;

namespace Gondwana.Tooling.Audio.WinForms;

/// <summary>Shared standalone and Studio editor for the logical definitions in a GSND document.</summary>
public sealed class AudioEditorControl : UserControl
{
    /// <summary>Gets the pane titles accepted by the pane visibility methods.</summary>
    public static IReadOnlyList<string> PaneNames { get; } =
        Array.AsReadOnly(["Audio resources", "Properties", "Validation"]);

    private readonly ListView _resources = new()
    {
        Dock = DockStyle.Fill,
        FullRowSelect = true,
        HideSelection = false,
        MultiSelect = false,
        View = View.Details
    };

    private readonly PropertyGrid _properties = new()
    {
        Dock = DockStyle.Fill,
        ToolbarVisible = false,
        HelpVisible = true
    };

    private EditorDockWorkspace _workspace = null!;

    private readonly TextBox _validation = new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical
    };

    /// <summary>Gets the mutable authoring document displayed by this editor.</summary>
    public AudioDocument Document { get; }
    /// <summary>Gets the mutable definition currently selected in the document.</summary>
    public AudioDefinition Definition => Document.Definition;

    /// <summary>Creates an audio definition editor and subscribes to document changes.</summary>
    /// <param name="document">The mutable document to edit.</param>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is null.</exception>
    public AudioEditorControl(AudioDocument document)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        Size = new Size(1050, 700);
        Dock = DockStyle.Fill;

        _resources.Columns.Add("Key", 180);
        _resources.Columns.Add("Source", 110);
        _resources.Columns.Add("Location", 420);
        _resources.SelectedIndexChanged += (_, _) => BindSelectedResource();
        _properties.PropertyValueChanged += (_, _) =>
        {
            Document.MarkChanged();
            RefreshResourceList();
            UpdateValidation();
        };

        _workspace = new EditorDockWorkspace("gsnd");
        Controls.Add(_workspace);
        var dock = _workspace.DockPanel;
        var resources = _workspace.AddPane("resources", "Audio resources", _resources, BuildResourceToolbar());
        var properties = _workspace.AddPane("properties", "Properties", _properties);
        var validation = _workspace.AddPane("validation", "Validation", _validation);

        _workspace.Place(resources, () => resources.Show(dock, DockState.Document));
        _workspace.Place(properties, () => properties.Show(resources.Pane, DockAlignment.Right, .38));
        _workspace.Place(validation, () => validation.Show(properties.Pane, DockAlignment.Bottom, .30));
        _workspace.InitializeLayout();

        var definitionBar = new DefinitionSelectionBar<AudioDefinition>(
            () => Document.Definitions, () => Document.SelectedDefinition, definition => definition.Key,
            Document.SelectDefinition, () => Document.AddDefinition(), () => Document.DuplicateDefinition(),
            () => Document.RemoveDefinition(Document.SelectedDefinition), Document.RenameDefinition, RefreshView, "gsnd", (path, select) => Document.ImportDefinitions(path, select));
        Controls.Add(definitionBar);
        Document.Changed += definitionBar.RefreshMembers;
        definitionBar.Disposed += (_, _) => Document.Changed -= definitionBar.RefreshMembers;
        ToolingAppearance.Apply(this);
        RefreshView();
    }

    /// <summary>
    /// Adds loose files.
    /// </summary>
    /// <param name="paths">The paths of the source files to add.</param>
    public void AddLooseFiles(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        AudioResourceDefinition? last = null;
        foreach (var path in paths)
            last = Document.AddLooseFile(path);

        RefreshView();
        if (last is not null)
            SelectResource(last);
    }

    /// <summary>Validates the document and refreshes the diagnostics pane.</summary>
    /// <returns>The document's structural validation errors.</returns>
    public IReadOnlyList<string> UpdateValidation()
    {
        var errors = Document.Validate();
        _validation.Text = errors.Count == 0
            ? "No validation errors."
            : string.Join(Environment.NewLine, errors.Select(error => "ERROR: " + error));
        return errors;
    }

    /// <summary>
    /// Shows the named editor pane.
    /// </summary>
    /// <param name="paneName">The name of the editor pane.</param>
    /// <returns><see langword="true"/> if the named pane was found; otherwise, <see langword="false"/>.</returns>
    public bool ShowPane(string paneName) =>
        _workspace.ShowPane(paneName);

    /// <summary>
    /// Determines whether the named editor pane is visible.
    /// </summary>
    /// <param name="paneName">The name of the editor pane.</param>
    /// <returns><see langword="true"/> if the pane is visible; otherwise, <see langword="false"/>.</returns>
    public bool IsPaneVisible(string paneName) =>
        _workspace.IsPaneVisible(paneName);

    /// <summary>Restore the default pane arrangement for this editor type.</summary>
    public void ResetLayout() => _workspace.ResetLayout();

    /// <summary>
    /// Shows every editor pane.
    /// </summary>
    public void ShowAllPanes() =>
        _workspace.ShowAllPanes();

    /// <summary>Moves focus out of the property editor and validates pending control input without saving the document.</summary>
    /// <returns><see langword="true"/> if control validation succeeds and the property editor releases focus; otherwise, <see langword="false"/>.</returns>
    public bool CommitEdits()
    {
        _validation.Focus();
        return !_properties.ContainsFocus && ValidateChildren();
    }

    private ToolStrip BuildResourceToolbar()
    {
        var bar = new ToolStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden
        };

        bar.Items.Add("Add file…", null, (_, _) => ChooseLooseFiles());
        bar.Items.Add("Add URI…", null, (_, _) => AddUri());
        bar.Items.Add(new ToolStripSeparator());
        bar.Items.Add("Remove", null, (_, _) => RemoveSelected());
        return bar;
    }

    private void ChooseLooseFiles()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Audio files|*.wav;*.mp3;*.ogg;*.flac;*.aif;*.aiff;*.wma|All files|*.*",
            Multiselect = true,
            InitialDirectory = Document.BaseDirectory
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            AddLooseFiles(dialog.FileNames);
        }
        catch (Exception ex) when (
            ex is IOException or
            ArgumentException or
            UnauthorizedAccessException or
            InvalidOperationException or
            NotSupportedException)
        {
            ShowError(ex);
        }
    }

    private void AddUri()
    {
        using var dialog = new UriPrompt();
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            var resource = Document.AddUri(dialog.ResourceKey, dialog.SourceUri);
            RefreshView();
            SelectResource(resource);
        }
        catch (Exception ex) when (
            ex is ArgumentException or
            InvalidOperationException or
            NotSupportedException)
        {
            ShowError(ex);
        }
    }

    private void RemoveSelected()
    {
        if (_resources.SelectedItems.Count != 1 ||
            _resources.SelectedItems[0].Tag is not AudioResourceDefinition resource)
        {
            return;
        }

        Document.Remove(resource);
        _properties.SelectedObject = null;
        RefreshView();
    }

    private void RefreshView()
    {
        RefreshResourceList();
        BindSelectedResource();
        UpdateValidation();
    }

    private void RefreshResourceList()
    {
        AudioResourceDefinition? selected =
            _resources.SelectedItems.Count == 1
                ? _resources.SelectedItems[0].Tag as AudioResourceDefinition
                : null;

        _resources.BeginUpdate();
        try
        {
            _resources.Items.Clear();

            foreach (var resource in Definition.Resources)
            {
                var item = new ListViewItem(resource.Key)
                {
                    Tag = resource
                };
                item.SubItems.Add(resource.SourceKind.ToString());
                item.SubItems.Add(SourceDescription(resource));
                _resources.Items.Add(item);
            }
        }
        finally
        {
            _resources.EndUpdate();
        }

        if (selected is not null)
            SelectResource(selected);
    }

    private void BindSelectedResource()
    {
        _properties.SelectedObject =
            _resources.SelectedItems.Count == 1
                ? _resources.SelectedItems[0].Tag
                : null;
    }

    private void SelectResource(AudioResourceDefinition resource)
    {
        foreach (ListViewItem item in _resources.Items)
        {
            if (!ReferenceEquals(item.Tag, resource))
                continue;

            item.Selected = true;
            item.Focused = true;
            item.EnsureVisible();
            break;
        }
    }

    private static string SourceDescription(AudioResourceDefinition resource) =>
        resource.SourceKind switch
        {
            AudioResourceSourceKind.LooseFile =>
                resource.FilePath ?? string.Empty,
            AudioResourceSourceKind.PackedAsset =>
                $"{resource.AssetsFilePath} :: {resource.AssetEntryName}",
            AudioResourceSourceKind.Uri =>
                resource.SourceUri ?? string.Empty,
            _ => string.Empty
        };

    private void ShowError(Exception ex) =>
        ThemedMessageBox.Show(
            this,
            ex.Message,
            "GSND editor",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
}
