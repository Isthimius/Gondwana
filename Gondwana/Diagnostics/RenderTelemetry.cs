
namespace Gondwana.Diagnostics;

/// <summary>Neutral fixed render measurement definitions shared by runtime and platform adapters.</summary>
internal static class RenderTelemetry
{
    /// <summary>Fixed neutral names; CPU durations are milliseconds, remaining values are counts or gauges.</summary>
    internal static readonly string[] Metrics =
    [
        "build.cpu.ms", "query.cpu.ms", "sort.cpu.ms", "record.cpu.ms", "overlay.cpu.ms",
        "visible.drawables", "visible.tiles", "atlas.batches", "atlas.tiles",
        "replay.cpu.ms", "picture.cpu.ms", "backbuffer.flush.cpu.ms", "snapshot.cpu.ms", "snapshot.age.ms",
        "mailbox.published.lifetime", "mailbox.dropped.lifetime", "mailbox.slots", "snapshot.commands.approximate",
        "gate.wait.cpu.ms", "gate.held.cpu.ms", "presentation.cpu.ms", "render.snapshot.cpu.ms", "blit.cpu.ms", "flush.cpu.ms"
    ];

    /// <summary>At most eight layer indices are retained; measurements count draw instances across views.</summary>
    internal static readonly string[][] Layers = Enumerable.Range(0, 8).Select(i => new[]
    {
        $"layer.{i}.query.cpu.ms", $"layer.{i}.record.cpu.ms", $"layer.{i}.drawables", $"layer.{i}.tiles",
        $"layer.{i}.transformed.tiles", $"layer.{i}.tile.width.px", $"layer.{i}.tile.height.px", $"layer.{i}.z"
    }).ToArray();

    /// <summary>Defines bounded render measurements when a host is constructed.</summary>
    /// <param name="gpu">Whether this host uses a GPU backbuffer.</param>
    /// <returns>A neutral handle without references to the host, or null if source capacity is exhausted.</returns>
    internal static TelemetrySource? Register(bool gpu)
    {
        var source = Engine.Instance.Profiler.TryRegisterSource("Render surface",
            gpu ? (OperatingSystem.IsBrowser() ? "WebGL" : "Desktop GPU") : "Bitmap");
        if (source is null) return null;
        foreach (var name in Metrics)
        {
            var availability = TelemetryAvailability.NotYetSampled;
            if (!gpu) availability = TelemetryAvailability.Unsupported;
            else if (name.StartsWith("gate.", StringComparison.Ordinal))
                availability = OperatingSystem.IsBrowser() ? TelemetryAvailability.Unsupported : TelemetryAvailability.NotApplicable;
            else if (OperatingSystem.IsBrowser() && Array.IndexOf(Metrics, name) is >= 9 and <= 17)
                availability = TelemetryAvailability.NotApplicable;
            else if (OperatingSystem.IsBrowser() && name.StartsWith("atlas.", StringComparison.Ordinal))
                availability = TelemetryAvailability.Unsupported;
            else if (Array.IndexOf(Metrics, name) >= 20)
                availability = TelemetryAvailability.Unsupported;
            source.Define(name, availability, GetMetricKind(name));
        }
        source.Define("presentation.count", gpu ? TelemetryAvailability.NotYetSampled : TelemetryAvailability.Unsupported);
        source.Define("layers.omitted", gpu ? TelemetryAvailability.NotYetSampled : TelemetryAvailability.Unsupported);
        foreach (var layer in Layers)
        {
            for (int index = 0; index < layer.Length; index++)
            {
                source.Define(
                    layer[index],
                    gpu ? TelemetryAvailability.NotYetSampled : TelemetryAvailability.Unsupported,
                    index <= 3 ? TelemetryMetricKind.Sample : TelemetryMetricKind.Gauge);
            }
        }

        return source;
    }

    private static TelemetryMetricKind GetMetricKind(string name)
    {
        if (name.EndsWith(".lifetime", StringComparison.Ordinal))
            return TelemetryMetricKind.LifetimeCounter;

        if (name.Contains("cpu.ms", StringComparison.Ordinal) ||
            name == "snapshot.age.ms" ||
            name.StartsWith("visible.", StringComparison.Ordinal) ||
            name.StartsWith("atlas.", StringComparison.Ordinal))
        {
            return TelemetryMetricKind.Sample;
        }

        return TelemetryMetricKind.Gauge;
    }
}
