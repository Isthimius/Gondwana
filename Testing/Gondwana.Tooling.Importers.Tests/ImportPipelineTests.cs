using System.Text;
using Gondwana.Drawing.Tilesheets.GTS;

namespace Gondwana.Tooling.Importers.Tests;

public sealed class ImportPipelineTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "ImportPipeline-" + Guid.NewGuid().ToString("N"));
    private readonly ExternalImportRequest request;
    public ImportPipelineTests()
    {
        Directory.CreateDirectory(directory);
        string source = Path.Combine(directory, "source.txt"); File.WriteAllText(source, "source");
        request = new(source, Path.Combine(directory, "output"));
    }
    public void Dispose() => Directory.Delete(directory, true);
    private sealed class Provider(Action<ImportPlan> build) : ExternalAssetImporter
    {
        public override string Id => "test";
        public override string DisplayName => "Test";
        public override IReadOnlyList<string> SupportedExtensions => [".txt"];
        protected override void BuildPlan(ImportPlan plan, CancellationToken cancellationToken) => build(plan);
    }
    private static void Add(ImportPlan p, string file, string? key = null) => p.Add(file, "GTS", key, Encoding.UTF8.GetBytes("new"));

    [Theory]
    [InlineData("output.duplicate")]
    [InlineData("key.duplicate")]
    [InlineData("dependency.missing")]
    [InlineData("source.invalid")]
    public void InvalidPlansNeverWrite(string code)
    {
        var provider = new Provider(p =>
        {
            Add(p, "one.gts", "one");
            switch (code)
            {
                case "output.duplicate": Add(p, "one.gts", "two"); break;
                case "key.duplicate": Add(p, "two.gts", "one"); break;
                case "dependency.missing": p.Dependencies.Add(Path.Combine(directory, "absent.png")); break;
                default: throw new InvalidDataException("Invalid source after planning an output.");
            }
        });
        var result = provider.Import(request);
        Assert.False(result.Analysis.CanImport); Assert.Empty(result.WrittenFiles);
        Assert.Contains(result.Analysis.Diagnostics, d => d.Code == code);
        Assert.False(Directory.Exists(request.OutputDirectory));
    }

    [Fact]
    public void ExistingLogicalKeysAreProtectedEvenWithOverwrite()
    {
        Directory.CreateDirectory(request.OutputDirectory);
        File.WriteAllText(Path.Combine(request.OutputDirectory, "other.gts"), TilesheetDefinitionSerializer.ToJson(new TilesheetDefinition { Name = "one" }));
        var analysis = new Provider(p => Add(p, "one.gts", "one")).Analyze(request with { Overwrite = true });
        Assert.Contains(analysis.Diagnostics, d => d.Code == "key.exists");
        Assert.False(analysis.CanImport);
    }

    [Fact]
    public void SourceDependencyCannotBeOverwritten()
    {
        var result = new Provider(p => Add(p, "source.txt")).Import(request with { OutputDirectory = directory, Overwrite = true });
        Assert.Contains(result.Analysis.Diagnostics, d => d.Code == "output.source");
        Assert.Equal("source", File.ReadAllText(request.SourcePath));
    }

    [Fact]
    public void CancellationBeforeWritingDoesNotCreateOutput()
    {
        using var cancellation = new CancellationTokenSource();
        var provider = new Provider(p => { Add(p, "one.gts"); cancellation.Cancel(); });
        Assert.Throws<OperationCanceledException>(() => provider.Import(request, cancellation.Token));
        Assert.False(Directory.Exists(request.OutputDirectory));
    }

    [Fact]
    public void FailedCommitRestoresEarlierReplacementsAndRemovesStaging()
    {
        // Windows sharing denial deterministically fails the second move after the first committed.
        if (!OperatingSystem.IsWindows()) return;
        Directory.CreateDirectory(request.OutputDirectory);
        string first = Path.Combine(request.OutputDirectory, "one.txt"), second = Path.Combine(request.OutputDirectory, "two.txt");
        File.WriteAllText(first, "original one"); File.WriteAllText(second, "original two");
        var provider = new Provider(p => { Add(p, "one.txt"); Add(p, "two.txt"); });
        using (var locked = new FileStream(second, FileMode.Open, FileAccess.Read, FileShare.Read))
            Assert.ThrowsAny<IOException>(() => provider.Import(request with { Overwrite = true }));
        Assert.Equal("original one", File.ReadAllText(first)); Assert.Equal("original two", File.ReadAllText(second));
        Assert.Empty(Directory.GetDirectories(request.OutputDirectory));
    }
}
