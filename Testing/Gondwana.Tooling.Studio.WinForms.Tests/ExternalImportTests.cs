using System.Runtime.ExceptionServices;
using Gondwana.Tooling.Studio.Core.Extensibility;
using Gondwana.Tooling.Studio.Plugin.ExternalImport;
using Gondwana.Tooling.Importers;
using SkiaSharp;

namespace Gondwana.Tooling.Studio.WinForms.Tests;

public sealed class ExternalImportTests
{
    private sealed class Host(string directory) : IStudioPluginHostServices
    {
        public string CurrentWorkingDirectory => directory;
        public int Refreshes;
        public List<string> Opened = [];
        public List<string> Messages = [];
        public void Log(string message) => Messages.Add(message);
        public void RefreshWorkingDirectory() => Refreshes++;
        public void OpenDocument(string path) => Opened.Add(path);
    }

    [Fact]
    public void DiscoversExternalPluginAlongsideDiagnostics()
    {
        var host = new StudioPluginHost(_ => { }); host.DiscoverAndLoad();
        Assert.Contains(host.Plugins, p => p.Name == "External Asset Importer");
        Assert.Contains(host.Plugins, p => p.Name == "Project Diagnostics");
    }

    [Fact]
    public void PanelAnalysisImportOverwriteAndHostServices() => RunSta(() =>
    {
        string directory = Path.Combine(Path.GetTempPath(), "StudioImport-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try
        {
            using var image = new SKBitmap(2, 2); image.Erase(SKColors.Red);
            using var png = image.Encode(SKEncodedImageFormat.Png, 100); File.WriteAllBytes(Path.Combine(directory, "atlas.png"), png.ToArray());
            string source = Path.Combine(directory, "tiles.tsx");
            File.WriteAllText(source, "<tileset name='tiles' tilewidth='2' tileheight='2' tilecount='1' columns='1'><image source='atlas.png'/></tileset>");
            var services = new Host(directory); var plugin = new ExternalImportPlugin(); plugin.AttachHostServices(services);
            using var panel = (ExternalImportPanel)plugin.CreatePanel(); plugin.OnProjectOpened(directory);
            Assert.Equal(directory, panel.OutputDirectory); Assert.Equal(5, panel.Formats.Count); Assert.False(panel.CanImport);
            panel.SourcePath = source; panel.Analyze(); Wait(panel);
            Assert.True(panel.CanImport); Assert.Single(panel.Analysis!.Artifacts);
            panel.Import(); Wait(panel);
            Assert.Single(services.Opened); Assert.Equal(1, services.Refreshes); Assert.Single(services.Messages);
            panel.Analyze(); Wait(panel); Assert.False(panel.CanImport);
            panel.Overwrite = true; panel.Analyze(); Wait(panel); Assert.True(panel.CanImport);
            panel.Import(); Wait(panel); Assert.Equal(2, services.Refreshes);
            File.WriteAllText(source, "<tileset/>"); panel.Analyze(); Wait(panel); Assert.False(panel.CanImport);
            panel.Analyze(); plugin.OnProjectClosed(); Assert.Null(panel.Analysis); Assert.False(panel.CanImport); Assert.Equal("", panel.OutputDirectory);
            plugin.OnProjectOpened(directory); panel.Analyze(); panel.Dispose();
        }
        finally { Directory.Delete(directory, true); }
    });

    private static void Wait(ExternalImportPanel panel)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (panel.IsBusy && clock.Elapsed < TimeSpan.FromSeconds(15)) { panel.Poll(); Thread.Sleep(10); }
        Assert.False(panel.IsBusy);
    }

    private sealed class DelayedProvider : IExternalAssetImporter, IDisposable
    {
        public string Id => "delayed";
        public string DisplayName => "Delayed";
        public IReadOnlyList<string> SupportedExtensions => [".test"];
        public readonly ManualResetEventSlim Started = new(), Release = new(), Finished = new();
        public CancellationToken Token;
        public bool CanImport(string path) => true;
        public ExternalImportAnalysis Analyze(ExternalImportRequest request, CancellationToken cancellationToken = default)
        {
            Token = cancellationToken; Started.Set();
            if (!Release.Wait(TimeSpan.FromSeconds(10), CancellationToken.None)) throw new TimeoutException();
            Finished.Set();
            // Deliberately return a stale success to exercise the panel's abandoned-result handling.
            return new(Id, [], [new("GTS", "stale.gts", "stale")], []);
        }
        public ExternalImportResult Import(ExternalImportRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Dispose() { Started.Dispose(); Release.Dispose(); Finished.Dispose(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CancelOrDisposeDiscardsDelayedWorkerCompletion(bool dispose) => RunSta(() =>
    {
        using var provider = new DelayedProvider();
        using var panel = new ExternalImportPanel([provider]) { SourcePath = "input.test", OutputDirectory = Path.GetTempPath() };
        panel.Analyze(); Assert.True(provider.Started.Wait(TimeSpan.FromSeconds(5)));
        if (dispose) panel.Dispose(); else panel.SourcePath = "changed.test";
        Assert.True(provider.Token.IsCancellationRequested);
        provider.Release.Set(); Assert.True(provider.Finished.Wait(TimeSpan.FromSeconds(5)));
        panel.Poll();
        Assert.Null(panel.Analysis); Assert.False(panel.IsBusy); Assert.False(panel.CanImport);
    });
    private static void RunSta(Action action)
    {
        Exception? error = null;
        var thread = new Thread(() => { try { action(); } catch (Exception ex) { error = ex; } });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (error is not null) ExceptionDispatchInfo.Capture(error).Throw();
    }
}
