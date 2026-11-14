using System.Text;
using Gondwana.Tooling.Studio.Core.Extensibility;

namespace Gondwana.Tooling.Studio.WinForms.Panels;

/// <summary>Inspects the host snapshot and edits next-startup choices without rediscovering or activating code.</summary>
internal sealed partial class PluginManagerPanel : UserControl
{
    private readonly StudioPluginHost _host;
    private readonly PluginPreferences _preferences;
    private bool _updating;
    private string? _saveError;
    internal DataGridView Grid { get; } = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false,
        RowHeadersVisible = false,
        MultiSelect = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells,
        BackgroundColor = ToolingAppearance.Palette.Surface,
        BorderStyle = BorderStyle.None
    };
    internal TextBox DetailsBox { get; } = new()
    {
        Dock = DockStyle.Fill,
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Both,
        WordWrap = false,
        ShortcutsEnabled = true,
        AccessibleName = "Selected plugin details and diagnostics"
    };
    internal CheckBox EnabledChoice { get; } = new() { AutoSize = true, Text = "Enabled at next startup" };
    internal Label StatusLabel { get; } = new() { AutoSize = true, Dock = DockStyle.Fill };

    internal PluginManagerPanel(StudioPluginHost host, PluginPreferences preferences, PluginPackageOperations? operations = null)
    {
        _operations = operations;
        _host = host;
        _preferences = preferences;
        AutoScaleMode = AutoScaleMode.Dpi;
        Padding = new Padding(8);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 30));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        layout.Controls.Add(StatusLabel, 0, 0);
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
        layout.Controls.Add(CreatePackageActions(), 0, 1);
        layout.Controls.Add(Grid, 0, 2);
        layout.Controls.Add(EnabledChoice, 0, 3);
        layout.Controls.Add(DetailsBox, 0, 4);
        layout.Controls.Add(PendingGrid, 0, 5);
        Controls.Add(layout);
        foreach (var title in new[] { "Plugin", "Version", "Current state", "Next startup", "Compatibility", "Source" })
            Grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = title, SortMode = DataGridViewColumnSortMode.NotSortable });
        Grid.Columns[0].MinimumWidth = 150;
        // SelectionChanged can fire before CurrentRow moves; bind details to the committed current cell.
        Grid.CurrentCellChanged += (_, _) => ShowSelection();
        EnabledChoice.CheckedChanged += (_, _) => ChangePreference();
        _host.DiscoveryChanged += DiscoveryChanged;
        RefreshSnapshot();
    }

    private void DiscoveryChanged(object? sender, EventArgs e) => RefreshSnapshot();

    internal void RefreshSnapshot()
    {
        var selected = SelectedRecords?.FirstOrDefault();
        RefreshOperations();
        _updating = true;
        try
        {
            Grid.Rows.Clear();
            // Package implementations and redundant legacy records share one activation choice.
            foreach (var group in _host.Discovery.GroupBy(row => row.PreferenceId ?? $"source:{row.SourceDirectory}:{row.EntryAssembly}", StringComparer.Ordinal))
            {
                var records = group.ToArray();
                var first = records[0];
                var next = first.PreferenceId is { } id ? (_preferences.IsEnabled(id) ? "Enabled" : "Disabled") : "Unavailable";
                if (first.PreferenceId is { } key && _preferences.IsPending(key)) next += " (restart required)";
                var index = Grid.Rows.Add(first.DisplayName ?? first.Implementation ?? first.Id ?? Path.GetFileName(first.SourceDirectory),
                    first.Version ?? "Unknown", string.Join("; ", records.Select(row => StatusText(row.Status)).Distinct()), next,
                    string.Join("; ", records.Select(row => row.Compatibility.ToString()).Distinct()),
                    string.Join(" + ", records.Select(row => row.IsLegacy ? "Legacy" : "Packaged").Distinct()));
                Grid.Rows[index].Tag = records;
            }
            var restore = Grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(row => row.Tag is PluginDiscoveryInfo[] records &&
                records.Any(record => selected is not null && (selected.PreferenceId is not null
                    ? record.PreferenceId == selected.PreferenceId : record.SourceDirectory == selected.SourceDirectory)));
            if (restore is not null) Grid.CurrentCell = restore.Cells[0];
            else if (Grid.Rows.Count > 0) Grid.CurrentCell = Grid.Rows[0].Cells[0];
            StatusLabel.Text = _saveError ?? _operations?.Diagnostic ?? (_preferences.ReadError is { } error
                ? $"Preferences unavailable; startup defaults to enabled. Repair or remove '{_preferences.FilePath}' and restart. {error}"
                : (_preferences.RestartRequired || _operations?.RestartRequired == true) ? "Restart required. Saved choices apply at the next Studio startup; current plugins remain unchanged."
                : Grid.Rows.Count == 0 ? "No plugins discovered. Plugin packages are loaded when Studio starts."
                : "Changes apply at the next Studio startup. No restart is currently required.");
            if ((_preferences.RestartRequired || _operations?.RestartRequired == true) && !StatusLabel.Text.Contains("Restart required", StringComparison.Ordinal))
                StatusLabel.Text += " Restart required: saved activation/package changes are pending.";
            if (_operations?.Operations.Any(o => o.State is PluginPackageOperationState.Failed or PluginPackageOperationState.RecoveryRequired) == true)
                StatusLabel.Text += " Package operation failed; select its pending-operation row for diagnostics.";
        }
        finally { _updating = false; }
        ShowSelection();
    }

    private PluginDiscoveryInfo[]? SelectedRecords => Grid.CurrentRow?.Tag as PluginDiscoveryInfo[];

    private void ShowSelection()
    {
        if (_updating) return;
        _updating = true;
        try
        {
            var records = SelectedRecords;
            var id = records?.FirstOrDefault()?.PreferenceId;
            EnabledChoice.Enabled = !_busy && id is not null && _preferences.ReadError is null;
            UpdateActions();
            EnabledChoice.Checked = id is not null && _preferences.IsEnabled(id);
            var details = new StringBuilder();
            if (records is not null)
            {
                details.AppendLine($"Preference key: {id ?? "Unavailable (no valid identity discovered)"}");
                details.AppendLine($"Settings: {_preferences.FilePath}");
                if (id is not null)
                {
                    details.AppendLine($"Startup choice this session: {(_preferences.WasEnabledAtStartup(id) ? "Enabled" : "Disabled")}");
                    details.AppendLine($"Requested next startup: {(_preferences.IsEnabled(id) ? "Enabled" : "Disabled")}");
                    details.AppendLine(_preferences.IsPending(id) ? "Restart required." : "No pending activation change.");
                }
                details.AppendLine("Enabling does not bypass package validation or compatibility checks.");
                foreach (var row in records)
                {
                    details.AppendLine();
                    details.AppendLine($"ID: {row.Id ?? "Unknown"}");
                    details.AppendLine($"Name: {row.DisplayName ?? "Unknown"}");
                    details.AppendLine($"Version: {row.Version ?? "Unknown"}");
                    details.AppendLine($"Description: {row.Description ?? "Unknown"}");
                    details.AppendLine($"Author: {row.Author ?? "Unknown"}");
                    details.AppendLine($"Current state: {StatusText(row.Status)}");
                    details.AppendLine($"Compatibility: {row.Compatibility}");
                    details.AppendLine($"Source: {(row.IsLegacy ? "Legacy flat DLL" : "Manifest package")}");
                    details.AppendLine($"Location: {row.SourceDirectory}");
                    details.AppendLine($"Entry assembly: {row.EntryAssembly ?? "Unknown"}");
                    details.AppendLine($"Implementation: {row.Implementation ?? "Not inspected"}");
                    details.AppendLine($"Diagnostics: {row.Diagnostic ?? "None"}");
                    var action = row.Status switch
                    {
                        PluginLoadStatus.FailedToLoad => "Restore missing assemblies/dependencies from the publisher, or share these diagnostics with the author. Restart after correcting the package.",
                        PluginLoadStatus.InvalidPackage => "Correct the manifest or restore a complete package from the publisher, then restart.",
                        PluginLoadStatus.Incompatible => "Obtain a plugin version compatible with this Studio contract, then restart.",
                        PluginLoadStatus.Conflict => "Remove redundant installations or correct conflicting manifest IDs, then restart.",
                        PluginLoadStatus.Disabled => "Review the failure with the plugin author. Correct the cause before restarting; the saved activation preference is unchanged.",
                        PluginLoadStatus.UserDisabled => "Enable at next startup and restart Studio to activate this plugin.",
                        _ => null
                    };
                    if (action is not null) details.AppendLine($"Suggested action: {action}");
                }
            }
            details.AppendLine("Package management: legacy flat DLLs are inspection/activation-only. Bundled Studio plugins are protected; disabling remains available.");
            DetailsBox.Text = details.ToString();
        }
        finally { _updating = false; }
    }

    private void ChangePreference()
    {
        if (_updating || SelectedRecords?.FirstOrDefault()?.PreferenceId is not { } id) return;
        _preferences.TrySetEnabled(id, EnabledChoice.Checked, out _saveError);
        RefreshSnapshot();
    }

    internal static string StatusText(PluginLoadStatus status) => status switch
    {
        PluginLoadStatus.Loaded => "Loaded",
        PluginLoadStatus.UserDisabled => "Disabled by preference",
        PluginLoadStatus.Disabled => "Disabled after runtime failure",
        PluginLoadStatus.Incompatible => "Incompatible",
        PluginLoadStatus.InvalidPackage => "Invalid package",
        PluginLoadStatus.FailedToLoad => "Failed to load",
        PluginLoadStatus.Conflict => "Identity conflict",
        _ => status.ToString()
    };

    protected override void Dispose(bool disposing)
    {
        if (disposing) _host.DiscoveryChanged -= DiscoveryChanged;
        base.Dispose(disposing);
    }
}
