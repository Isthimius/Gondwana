using Gondwana.Audio.GSND;
using Gondwana.Tooling.Audio.Editing;
using Gondwana.Tooling.WinForms;
using WeifenLuo.WinFormsUI.Docking;

namespace Gondwana.Tooling.Audio.WinForms;

/// <summary>
/// Hostable WinForms editor for one GSND audio definition.
/// </summary>
public sealed class AudioEditorControl : UserControl
{
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

    public AudioDocument Document { get; }
    public AudioDefinition Definition => Document.Definition;

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

        _workspace = new EditorDockWorkspace();
        Controls.Add(_workspace);
        var dock = _workspace.DockPanel;
        var resources = _workspace.AddPane("Audio resources", _resources, BuildResourceToolbar());
        var properties = _workspace.AddPane("Properties", _properties);
        var validation = _workspace.AddPane("Validation", _validation);

        resources.Show(dock, DockState.Document);
        properties.Show(resources.Pane, DockAlignment.Right, .38);
        validation.Show(properties.Pane, DockAlignment.Bottom, .30);

        DarkTheme.Apply(this);
        RefreshView();
    }

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

    public IReadOnlyList<string> UpdateValidation()
    {
        var errors = Document.Validate();
        _validation.Text = errors.Count == 0
            ? "No validation errors."
            : string.Join(Environment.NewLine, errors.Select(error => "ERROR: " + error));
        return errors;
    }

    public bool ShowPane(string paneName) =>
        _workspace.ShowPane(paneName);

    public bool IsPaneVisible(string paneName) =>
        _workspace.IsPaneVisible(paneName);

    public void ShowAllPanes() =>
        _workspace.ShowAllPanes();

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
        MessageBox.Show(
            this,
            ex.Message,
            "GSND editor",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
}
