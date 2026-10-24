using Gondwana.Tooling.Importers;
using Gondwana.Tooling.Studio.Core.Extensibility;

namespace Gondwana.Tooling.Studio.Plugin.ExternalImport;

/// <summary>Edits import options and presents background provider results through UI-thread polling.</summary>
/// <remarks>Construct and access the panel on the UI thread. Provider work runs on worker threads without accessing controls.</remarks>
public sealed class ExternalImportPanel : UserControl
{
    private readonly IReadOnlyList<IExternalAssetImporter> providers;
    private readonly ComboBox format = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
    private readonly TextBox source = new() { Dock = DockStyle.Fill };
    private readonly TextBox output = new() { Dock = DockStyle.Fill };
    private readonly CheckBox overwrite = new() { Text = "Overwrite existing generated files", AutoSize = true };
    private readonly Button analyze = new() { Text = "Analyze", AutoSize = true };
    private readonly Button import = new() { Text = "Import", AutoSize = true, Enabled = false };
    private readonly Button cancel = new() { Text = "Cancel", AutoSize = true };
    private readonly ListView artifacts = new() { View = View.Details, Dock = DockStyle.Fill, FullRowSelect = true };
    private readonly TextBox diagnostics = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill };
    private readonly TableLayoutPanel inputs = new() { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 3 };
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 100 };
    private CancellationTokenSource? cancellation;
    private Task<ExternalImportResult>? work;
    private bool importing;
    /// <summary>Gets or sets borrowed services for logging, refreshing, and opening imported files; null disables host integration.</summary>
    public IStudioPluginHostServices? HostServices { get; set; }
    /// <summary>Gets or sets the external source file text; changing it cancels tracked work and clears the analysis.</summary>
    public string SourcePath { get => source.Text; set => source.Text = value; }
    /// <summary>Gets or sets the destination directory text; changing it cancels tracked work and clears the analysis.</summary>
    public string OutputDirectory { get => output.Text; set => output.Text = value; }
    /// <summary>Gets or sets permission to overwrite generated files; changing it cancels tracked work and clears the analysis.</summary>
    public bool Overwrite { get => overwrite.Checked; set => overwrite.Checked = value; }
    /// <summary>Gets whether the Import button is enabled by a successful analysis that permits importing.</summary>
    public bool CanImport => import.Enabled;
    /// <summary>Gets whether a worker task is still tracked, including completed work awaiting <see cref="Poll"/>.</summary>
    public bool IsBusy => work is not null;
    /// <summary>Gets the last polled analysis or import analysis, or null before results arrive or after invalidation.</summary>
    public ExternalImportAnalysis? Analysis { get; private set; }
    /// <summary>Gets a snapshot of format choices: Auto-detect followed by provider display names in registration order.</summary>
    public IReadOnlyList<string> Formats => format.Items.Cast<string>().ToArray();

    /// <summary>Creates the import controls and polling timer with the built-in importer providers.</summary>
    public ExternalImportPanel() : this(ExternalImporterRegistry.CreateProviders()) { }

    /// <summary>Creates the import controls and starts a UI timer that polls worker completion every 100 milliseconds.</summary>
    /// <param name="providers">Borrowed providers retained in order; auto-detection uses the first provider accepting the source.</param>
    /// <remarks>The panel does not dispose providers. Keep the provider list stable during its lifetime.</remarks>
    public ExternalImportPanel(IReadOnlyList<IExternalAssetImporter> providers)
    {
        this.providers = providers;
        Dock = DockStyle.Fill;
        format.Items.Add("Auto-detect");
        foreach (var provider in providers) format.Items.Add(provider.DisplayName);
        format.SelectedIndex = 0;
        inputs.ColumnStyles.Add(new(SizeType.AutoSize)); inputs.ColumnStyles.Add(new(SizeType.Percent, 100)); inputs.ColumnStyles.Add(new(SizeType.AutoSize));
        void Row(string label, Control field, Control? browse = null)
        {
            int row = inputs.RowCount++;
            inputs.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
            inputs.Controls.Add(field, 1, row);
            if (browse is not null) inputs.Controls.Add(browse, 2, row);
        }
        var chooseSource = new Button { Text = "…", AutoSize = true };
        chooseSource.Click += (_, _) => { using var dialog = new OpenFileDialog { Filter = "External assets|*.tres;*.tsx;*.tmx;*.ase;*.aseprite|All files|*.*" }; if (dialog.ShowDialog(this) == DialogResult.OK) SourcePath = dialog.FileName; };
        var chooseOutput = new Button { Text = "…", AutoSize = true };
        chooseOutput.Click += (_, _) => { using var dialog = new FolderBrowserDialog { SelectedPath = OutputDirectory }; if (dialog.ShowDialog(this) == DialogResult.OK) OutputDirectory = dialog.SelectedPath; };
        Row("Format", format); Row("Source", source, chooseSource); Row("Output directory", output, chooseOutput); Row("", overwrite);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true }; buttons.Controls.AddRange([analyze, import, cancel]);
        artifacts.Columns.Add("Type", 70); artifacts.Columns.Add("Proposed output", 400); artifacts.Columns.Add("Logical definitions", 300);
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        body.RowStyles.Add(new(SizeType.Percent, 45)); body.RowStyles.Add(new(SizeType.Percent, 55)); body.Controls.Add(artifacts); body.Controls.Add(diagnostics);
        Controls.Add(body); Controls.Add(buttons); Controls.Add(inputs);
        analyze.Click += (_, _) => Analyze(); import.Click += (_, _) => Import(); cancel.Click += (_, _) => Cancel();
        source.TextChanged += (_, _) => Cancel(); output.TextChanged += (_, _) => Cancel(); overwrite.CheckedChanged += (_, _) => Cancel(); format.SelectedIndexChanged += (_, _) => Cancel();
        timer.Tick += (_, _) => Poll(); timer.Start();
    }

    private void InvalidateAnalysis() { Analysis = null; import.Enabled = false; artifacts.Items.Clear(); diagnostics.Clear(); }
    /// <summary>Starts background analysis without writing outputs and clears any previous analysis.</summary>
    /// <remarks>Does nothing while busy or disposed. Missing input or an unrecognized source is reported in the panel.</remarks>
    public void Analyze() => Start(false);
    /// <summary>Starts provider import with the current options when <see cref="CanImport"/> is true.</summary>
    /// <remarks>Clears the displayed analysis and disables inputs while the worker runs. Results are applied by <see cref="Poll"/>.</remarks>
    public void Import() { if (CanImport) Start(true); }
    private void Start(bool write)
    {
        if (IsDisposed || work is not null) return;
        if (string.IsNullOrWhiteSpace(SourcePath) || string.IsNullOrWhiteSpace(OutputDirectory)) { diagnostics.Text = "Choose a source and output directory."; return; }
        var provider = format.SelectedIndex == 0 ? providers.FirstOrDefault(p => p.CanImport(SourcePath)) : providers[format.SelectedIndex - 1];
        if (provider is null) { diagnostics.Text = "No importer recognizes this extension."; return; }
        var request = new ExternalImportRequest(SourcePath, OutputDirectory, Overwrite);
        InvalidateAnalysis(); importing = write;
        inputs.Enabled = analyze.Enabled = import.Enabled = false;
        diagnostics.Text = write ? "Importing…" : "Analyzing…";
        cancellation = new(); var token = cancellation.Token;
        work = Task.Run(() => write ? provider.Import(request, token) : new ExternalImportResult(provider.Analyze(request, token), []), token);
    }

    /// <summary>Applies a completed worker result on the UI thread; does nothing while work is incomplete or the panel is disposed.</summary>
    /// <remarks>
    /// Displays failures and cancellation. After files are written, logs their paths, refreshes the host browser,
    /// and opens the first GSCN output, or the first GTS if no GSCN was written, when host services are attached.
    /// </remarks>
    public void Poll()
    {
        if (IsDisposed || work is not { IsCompleted: true } completed) return;
        work = null; cancellation?.Dispose(); cancellation = null;
        inputs.Enabled = analyze.Enabled = true;
        try
        {
            var result = completed.GetAwaiter().GetResult(); Analysis = result.Analysis;
            foreach (var artifact in Analysis.Artifacts) artifacts.Items.Add(new ListViewItem([artifact.Kind, artifact.OutputPath, string.Join(", ", artifact.LogicalKeys)]));
            diagnostics.Text = string.Join(Environment.NewLine, Analysis.Diagnostics.Select(d => $"{d.Severity}: {d.Message}"));
            import.Enabled = !importing && Analysis.CanImport;
            if (importing && result.WrittenFiles.Count > 0)
            {
                diagnostics.AppendText(Environment.NewLine + $"Imported {result.WrittenFiles.Count} files.");
                foreach (string path in result.WrittenFiles) HostServices?.Log($"[External Import] {path}");
                HostServices?.RefreshWorkingDirectory();
                var primary = result.WrittenFiles.FirstOrDefault(p => Path.GetExtension(p) == ".gscn") ?? result.WrittenFiles.FirstOrDefault(p => Path.GetExtension(p) == ".gts");
                if (primary is not null) HostServices?.OpenDocument(primary);
            }
        }
        catch (OperationCanceledException) { diagnostics.Text = "Cancelled."; }
        catch (Exception ex) { diagnostics.Text = $"Error: {ex.GetBaseException().Message}"; import.Enabled = false; }
    }

    /// <summary>Requests cooperative cancellation, abandons the tracked result, clears analysis, and re-enables inputs.</summary>
    /// <remarks>Returns without waiting for the provider to stop and does not undo files already written.</remarks>
    public void Cancel()
    {
        cancellation?.Cancel(); cancellation?.Dispose(); cancellation = null;
        if (work is { } previous)
            _ = previous.ContinueWith(t => System.Diagnostics.Trace.TraceError(t.Exception!.ToString()), CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        work = null; InvalidateAnalysis(); inputs.Enabled = analyze.Enabled = true;
    }
    /// <summary>Stops and disposes the polling timer, cancels tracked work, and disposes child controls.</summary>
    /// <param name="disposing">Whether to release managed resources and request cancellation.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing) { timer.Stop(); timer.Dispose(); Cancel(); }
        base.Dispose(disposing);
    }
}
