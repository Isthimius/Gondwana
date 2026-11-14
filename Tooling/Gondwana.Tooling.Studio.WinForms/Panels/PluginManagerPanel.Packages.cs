using System.Text;
using Gondwana.Tooling.Studio.Core.Extensibility;

namespace Gondwana.Tooling.Studio.WinForms.Panels;

internal sealed partial class PluginManagerPanel
{
    private readonly PluginPackageOperations? _operations;
    private bool _busy;
    internal Button InstallButton { get; } = new() { Text = "Install Plugin…", AutoSize = true };
    internal Button UpdateButton { get; } = new() { Text = "Update from File…", AutoSize = true };
    internal Button RemoveButton { get; } = new() { Text = "Uninstall…", AutoSize = true };
    internal Button CancelButton { get; } = new() { Text = "Cancel Pending Operation", AutoSize = true };
    internal DataGridView PendingGrid { get; } = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        MultiSelect = false,
        RowHeadersVisible = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells,
        AccessibleName = "Pending plugin package operations",
        ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText
    };
    internal Func<PluginPackagePreview, bool> ConfirmPackage { get; set; } = ShowPreview;

    private Control CreatePackageActions()
    {
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        actions.Controls.AddRange([InstallButton, UpdateButton, RemoveButton, CancelButton]);
        foreach (var title in new[] { "Pending plugin", "Operation", "State", "Diagnostics" })
            PendingGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = title, SortMode = DataGridViewColumnSortMode.NotSortable });
        InstallButton.Click += (_, _) => ChooseSource(InstallButton, null);
        UpdateButton.Click += (_, _) => ChooseSource(UpdateButton, SelectedPackageId);
        RemoveButton.Click += async (_, _) => await RunPackageChange(null, SelectedPackageId, remove: true);
        CancelButton.Click += async (_, _) =>
        {
            if (PendingGrid.CurrentRow?.Tag is not PluginPackageOperation operation) return;
            await RunBusy(() => Task.Run(() => _operations!.Cancel(operation.PluginId)));
        };
        PendingGrid.CurrentCellChanged += (_, _) => { UpdateActions(); if (PendingGrid.Focused) ShowPendingDetails(); };
        PendingGrid.Enter += (_, _) => ShowPendingDetails();
        Grid.Enter += (_, _) => ShowSelection();
        return actions;
    }

    private string? SelectedPackageId => SelectedRecords?.FirstOrDefault()?.PreferenceId;

    private void ChooseSource(Control button, string? updateId)
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("ZIP archive…", null, async (_, _) =>
        {
            using var dialog = new OpenFileDialog { Filter = "Plugin ZIP archives (*.zip)|*.zip", Title = "Select local plugin package" };
            if (dialog.ShowDialog(this) == DialogResult.OK) await RunPackageChange(dialog.FileName, updateId);
        });
        menu.Items.Add("Package directory…", null, async (_, _) =>
        {
            using var dialog = new FolderBrowserDialog { Description = "Select the package directory containing plugin.json", UseDescriptionForTitle = true };
            if (dialog.ShowDialog(this) == DialogResult.OK) await RunPackageChange(dialog.SelectedPath, updateId);
        });
        menu.Closed += (_, _) => menu.Dispose();
        ToolingAppearance.Apply(menu);
        menu.Show(button, new Point(0, button.Height));
    }

    internal async Task RunPackageChange(string? source, string? id, bool remove = false)
    {
        await RunBusy(async () =>
        {
            PluginPackagePreview? preview = null;
            try
            {
                preview = await Task.Run(() => remove ? _operations!.PreviewRemoval(id!) : _operations!.Preview(source!, id));
                if (IsDisposed || !ConfirmPackage(preview)) return;
                StatusLabel.Text = "Saving reviewed package operation…";
                await Task.Run(() => _operations!.Queue(preview, confirmed: true, replacementConfirmed: true));
            }
            finally
            {
                if (preview is not null) await Task.Run(() => _operations!.Discard(preview));
            }
        });
    }

    private async Task RunBusy(Func<Task> operation)
    {
        if (_busy || _operations is null) return;
        _busy = true;
        _saveError = null;
        UpdateActions();
        EnabledChoice.Enabled = false;
        StatusLabel.Text = "Inspecting and staging plugin files. Current plugins remain unchanged…";
        UseWaitCursor = true;
        try { await operation(); }
        catch (Exception ex) { _saveError = "Package operation failed: " + ex.Message; _operations.ReportFailure(ex.ToString()); }
        finally
        {
            _busy = false;
            if (!IsDisposed) { UseWaitCursor = false; RefreshSnapshot(); }
        }
    }

    private void RefreshOperations()
    {
        if (_operations is null) return;
        _operations.Refresh();
        var selected = (PendingGrid.CurrentRow?.Tag as PluginPackageOperation)?.PluginId;
        PendingGrid.Rows.Clear();
        foreach (var operation in _operations.Operations)
        {
            var index = PendingGrid.Rows.Add(operation.Name, operation.Kind, operation.State, operation.Diagnostic ?? "Applies before discovery at the next safe startup.");
            PendingGrid.Rows[index].Tag = operation;
            if (operation.PluginId == selected) PendingGrid.CurrentCell = PendingGrid.Rows[index].Cells[0];
        }
    }

    private void UpdateActions()
    {
        var available = !_busy && _operations?.CanManage == true;
        InstallButton.Enabled = available;
        var records = SelectedRecords;
        var id = SelectedPackageId;
        var pending = _operations?.Operations.FirstOrDefault(o => o.PluginId == id);
        var managed = available && id is not null && records is { Length: > 0 } && records.All(r => !r.IsLegacy) &&
            records.Select(r => r.SourceDirectory).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1 &&
            !PluginPackageOperations.IsBundled(id, records[0].SourceDirectory);
        UpdateButton.Enabled = managed && (pending is null || pending is { Kind: PluginPackageOperationKind.Update, State: PluginPackageOperationState.Pending });
        RemoveButton.Enabled = managed && pending is null;
        CancelButton.Enabled = available && PendingGrid.CurrentRow?.Tag is PluginPackageOperation
        { State: PluginPackageOperationState.Pending or PluginPackageOperationState.Failed };
    }

    private void ShowPendingDetails()
    {
        if (PendingGrid.CurrentRow?.Tag is not PluginPackageOperation operation) return;
        DetailsBox.Text = $"Package operation: {operation.Kind}\r\nPlugin: {operation.Name}\r\nID: {operation.PluginId}\r\nInstalled version: {operation.PreviousVersion ?? operation.Version}\r\nProposed version: {operation.Version}\r\nState: {operation.State}\r\nDestination: {Path.Combine(_operations!.PluginRoot, operation.Folder)}\r\nDiagnostics: {operation.Diagnostic ?? "Restart required; close all Studio instances before restarting."}\r\nActivation preferences are unchanged. Current plugin instances remain loaded until exit.";
    }

    internal static string PreviewText(PluginPackagePreview preview)
    {
        var text = new StringBuilder();
        var m = preview.Manifest;
        text.AppendLine($"{preview.Operation.Kind}: {m.Name}");
        text.AppendLine($"ID: {m.Id}\r\nVersion: {m.Version}\r\nInstalled version: {preview.Operation.PreviousVersion ?? "Not applicable"}\r\nChange: {preview.VersionChange}");
        text.AppendLine($"Author: {m.Author ?? "Not declared"}\r\nDescription: {m.Description ?? "Not declared"}\r\nEntry assembly: {m.EntryAssembly}\r\nContract: {m.ContractVersion} (Studio supports {PluginManifest.SupportedContractVersion})");
        text.AppendLine($"Destination: {preview.Destination}\r\nDependency/support files: {string.Join(", ", preview.Dependencies)}");
        text.AppendLine("\r\nChanges take effect before discovery at the next safe startup. Close all other Studio instances first. Activation preferences are preserved.");
        text.AppendLine("\r\n" + PluginPackagePreview.TrustWarning);
        return text.ToString();
    }

    private static bool ShowPreview(PluginPackagePreview preview)
    {
        using var dialog = new Form { Text = "Review plugin package operation", Size = new Size(720, 540), MinimumSize = new Size(480, 340), StartPosition = FormStartPosition.CenterParent, AutoScaleMode = AutoScaleMode.Dpi, MinimizeBox = false, MaximizeBox = false };
        var details = new TextBox { Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Both, Text = PreviewText(preview) };
        var consent = new CheckBox { Text = "I confirm this " + preview.VersionChange.ToLowerInvariant() + ".", AutoSize = true, Dock = DockStyle.Bottom };
        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft };
        var cancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
        var accept = new Button { Text = "Confirm for next startup", AutoSize = true, Enabled = false, DialogResult = DialogResult.OK };
        consent.CheckedChanged += (_, _) => accept.Enabled = consent.Checked;
        buttons.Controls.AddRange([cancel, accept]);
        dialog.Controls.Add(details); dialog.Controls.Add(consent); dialog.Controls.Add(buttons);
        dialog.AcceptButton = accept; dialog.CancelButton = cancel;
        ToolingAppearance.Apply(dialog);
        return dialog.ShowDialog() == DialogResult.OK;
    }
}
