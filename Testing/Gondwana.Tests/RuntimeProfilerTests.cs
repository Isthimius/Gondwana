using Gondwana.Diagnostics;

namespace Gondwana.Tests;

public sealed class RuntimeProfilerTests
{
    [Fact]
    public void LeasesWindowsResetAndDetachedQueries()
    {
        double clock = 0;
        using var profiler = new RuntimeProfiler(() => clock);
        using var source = profiler.RegisterSource("game", "test")!;
        source.Define("update.cpu.ms");
        Assert.False(profiler.IsCollecting);
        Assert.Equal(0, source.BeginSample());
        using var first = profiler.Start();
        using var second = profiler.Start();
        long epoch = source.BeginSample();
        source.Record(epoch, "update.cpu.ms", 2);
        source.Record(epoch, "update.cpu.ms", 6);
        clock = .25;
        var bucket = profiler.GetLatestSnapshot()!;
        var summary = Assert.Single(bucket.Sources).Metrics["update.cpu.ms"];
        Assert.Equal(2, summary.Count);
        Assert.Equal(8, summary.Sum);
        Assert.Equal(4, summary.Mean);
        Assert.Equal(2, summary.Minimum);
        Assert.Equal(6, summary.Maximum);
        Assert.Equal(8, summary.Count / bucket.ElapsedSeconds);
        Assert.Same(bucket, profiler.GetLatestSnapshot());
        Assert.Single(profiler.GetHistory());
        first.Dispose();
        Assert.True(profiler.IsCollecting);
        profiler.Reset();
        source.Record(epoch, "update.cpu.ms", 1000);
        clock = .5;
        Assert.Equal(TelemetryAvailability.NotYetSampled,
            profiler.GetLatestSnapshot()!.Sources[0].Metrics["update.cpu.ms"].Availability);
        Assert.Equal(8, summary.Sum);
        second.Dispose();
        Assert.False(profiler.IsCollecting);
        clock = 100;
        using var third = profiler.Start();
        source.Record(source.BeginSample(), "update.cpu.ms", 3);
        clock = 100.25;
        Assert.Equal(.25, profiler.GetLatestSnapshot()!.ElapsedSeconds);
    }

    [Fact]
    public void CapacityAvailabilityRetirementAndWeightedHistory()
    {
        double clock = 0;
        using var profiler = new RuntimeProfiler(() => clock);
        profiler.Configure(new() { HistoryCapacity = 2, SourceCapacity = 1, MetricCapacity = 8 });
        using var source = profiler.RegisterSource("surface", "Bitmap")!;
        Assert.Null(profiler.RegisterSource("overflow", "Bitmap"));
        source.Define("cpu.ms");
        source.Define("hardware.ms", TelemetryAvailability.Unsupported);
        source.Define("gate.ms", TelemetryAvailability.NotApplicable);
        for (int i = 0; i < 5; i++) Assert.True(source.Define($"extension.{i}"));
        Assert.False(source.Define("overflow"));
        using var request = profiler.Start();
        var epoch = source.BeginSample();
        source.Record(epoch, "cpu.ms", 10);
        clock = .25;
        var first = profiler.GetLatestSnapshot()!;
        source.Record(epoch, "cpu.ms", 2);
        source.Record(epoch, "cpu.ms", 3);
        source.Record(epoch, "cpu.ms", 5);
        clock = .5;
        var second = profiler.GetLatestSnapshot()!;
        var aggregate = TelemetrySummary.Combine(profiler.GetHistory().Select(s => s.Sources[0].Metrics["cpu.ms"]));
        Assert.Equal(5, aggregate.Mean);
        Assert.Equal(4, aggregate.Count);
        Assert.True(second.Truncated);
        Assert.Equal(TelemetryAvailability.Unsupported, second.Sources[0].Metrics["hardware.ms"].Availability);
        Assert.Equal(TelemetryAvailability.NotApplicable, second.Sources[0].Metrics["gate.ms"].Availability);
        clock = .75;
        profiler.GetLatestSnapshot();
        Assert.Equal(2, profiler.GetHistory().Count);
        Assert.DoesNotContain(first, profiler.GetHistory());
        source.Dispose();
        source.Record(epoch, "cpu.ms", 999);
        clock = 1;
        Assert.Empty(profiler.GetLatestSnapshot()!.Sources);
        Assert.Single(second.Sources);
    }

    [Fact]
    public void ConfigurationAndDisposalArePredictable()
    {
        using var profiler = new RuntimeProfiler();
        Assert.Throws<ArgumentOutOfRangeException>(() => profiler.Configure(new() { HistoryCapacity = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => profiler.Configure(new() { Interval = TimeSpan.Zero }));
        Assert.Throws<ArgumentOutOfRangeException>(() => profiler.Configure(new() { SourceCapacity = 1000 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => profiler.Configure(new() { MetricCapacity = 1000 }));
        using var request = profiler.Start();
        Assert.Throws<InvalidOperationException>(() => profiler.Configure(new()));
        profiler.Dispose();
        request.Dispose();
        Assert.False(profiler.IsCollecting);
        Assert.Throws<ObjectDisposedException>(() => profiler.Start());
    }

    [Fact]
    public async Task ConcurrentProducersAndReadersPreserveTotalsAndSeparateSources()
    {
        double clock = 0;
        using var profiler = new RuntimeProfiler(() => Volatile.Read(ref clock));
        using var engine = profiler.RegisterSource("engine", "Core")!;
        using var gl = profiler.RegisterSource("surface", "GPU")!;
        engine.Define("cycle.cpu.ms");
        gl.Define("presentation.count");
        using var request = profiler.Start();
        long generation = engine.BeginSample();
        await Task.WhenAll(
            Task.Run(() => { for (int i = 0; i < 10000; i++) engine.Record(generation, "cycle.cpu.ms", 2); }),
            Task.Run(() => { for (int i = 0; i < 3000; i++) gl.Record(generation, "presentation.count", 1); }),
            Task.Run(() => { for (int i = 0; i < 1000; i++) profiler.GetHistory(); }));
        Volatile.Write(ref clock, 1);
        var snapshot = profiler.GetLatestSnapshot()!;
        Assert.Equal(10000, snapshot.Sources.Single(s => s.Id == engine.Id).Metrics["cycle.cpu.ms"].Count);
        Assert.Equal(3000, snapshot.Sources.Single(s => s.Id == gl.Id).Metrics["presentation.count"].Count);
    }

    [Fact]
    public void HotObservationsDoNotAllocate()
    {
        using var profiler = new RuntimeProfiler(() => 0);
        using var source = profiler.RegisterSource("test", "test")!;
        source.Define("cpu.ms");
        using var request = profiler.Start();
        var generation = source.BeginSample();
        for (int i = 0; i < 1000; i++) source.Record(generation, "cpu.ms", 1);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++) source.Record(generation, "cpu.ms", 1);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
