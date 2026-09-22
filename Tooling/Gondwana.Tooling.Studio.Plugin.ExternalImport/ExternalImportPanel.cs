using Gondwana.Tooling.Importers;
using Gondwana.Tooling.Studio.Core.Extensibility;

namespace Gondwana.Tooling.Studio.Plugin.ExternalImport;

/// <summary>Presentation and UI-thread polling only. Providers and workers never access controls.</summary>
public sealed class ExternalImportPanel : UserControl
{
    private readonly IReadOnlyList<IExternalAssetImporter> providers = ExternalImporterRegistry.CreateProviders();
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
    public IStudioPluginHostServices? HostServices { get; set; }
    public string SourcePath { get => source.Text; set => source.Text = value; }
    public string OutputDirectory { get => output.Text; set => output.Text = value; }
    public bool Overwrite { get => overwrite.Checked; set => overwrite.Checked = value; }
    public bool CanImport => import.Enabled;
    public bool IsBusy => work is not null;
    public ExternalImportAnalysis? Analysis { get; private set; }
    public IReadOnlyList<string> Formats => format.Items.Cast<string>().ToArray();

    public ExternalImportPanel()
    {
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
        artifacts.Columns.Add("Type", 70); artifacts.Columns.Add("Proposed output", 500);
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        body.RowStyles.Add(new(SizeType.Percent, 45)); body.RowStyles.Add(new(SizeType.Percent, 55)); body.Controls.Add(artifacts); body.Controls.Add(diagnostics);
        Controls.Add(body); Controls.Add(buttons); Controls.Add(inputs);
        analyze.Click += (_, _) => Analyze(); import.Click += (_, _) => Import(); cancel.Click += (_, _) => Cancel();
        source.TextChanged += (_, _) => InvalidateAnalysis(); output.TextChanged += (_, _) => InvalidateAnalysis(); overwrite.CheckedChanged += (_, _) => InvalidateAnalysis(); format.SelectedIndexChanged += (_, _) => InvalidateAnalysis();
        timer.Tick += (_, _) => Poll(); timer.Start();
    }

    private void InvalidateAnalysis() { Analysis = null; import.Enabled = false; artifacts.Items.Clear(); diagnostics.Clear(); }
    public void Analyze() => Start(false);
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

    public void Poll()
    {
        if (IsDisposed || work is not { IsCompleted: true } completed) return;
        work = null; cancellation?.Dispose(); cancellation = null;
        inputs.Enabled = analyze.Enabled = true;
        try
        {
            var result = completed.GetAwaiter().GetResult(); Analysis = result.Analysis;
            foreach (var artifact in Analysis.Artifacts) artifacts.Items.Add(new ListViewItem([artifact.Kind, artifact.OutputPath]));
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

    public void Cancel()
    {
        cancellation?.Cancel(); cancellation?.Dispose(); cancellation = null;
        if (work is { } previous)
            _ = previous.ContinueWith(t => System.Diagnostics.Trace.TraceError(t.Exception!.ToString()), CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        work = null; InvalidateAnalysis(); inputs.Enabled = analyze.Enabled = true;
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) { timer.Stop(); timer.Dispose(); Cancel(); }
        base.Dispose(disposing);
    }
}
