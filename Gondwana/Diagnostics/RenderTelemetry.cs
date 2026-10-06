
namespace Gondwana.Diagnostics;

/// <summary>Neutral fixed render measurement definitions shared by runtime and platform adapters.</summary>
internal static class RenderTelemetry
{
    internal static readonly string[] Metrics =
    [
        "build.cpu.ms", "query.cpu.ms", "sort.cpu.ms", "record.cpu.ms", "overlay.cpu.ms",
        "visible.drawables", "visible.tiles", "atlas.batches", "atlas.tiles",
        "replay.cpu.ms", "picture.cpu.ms", "backbuffer.flush.cpu.ms", "snapshot.cpu.ms", "snapshot.age.ms",
        "mailbox.published.lifetime", "mailbox.dropped.lifetime", "mailbox.slots", "snapshot.commands.approximate",
        "gate.wait.cpu.ms", "gate.held.cpu.ms", "presentation.cpu.ms", "render.snapshot.cpu.ms", "blit.cpu.ms", "flush.cpu.ms"
    ];

    internal static readonly string[][] Layers = Enumerable.Range(0, 8).Select(i => new[]
    {
        $"layer.{i}.query.cpu.ms", $"layer.{i}.record.cpu.ms", $"layer.{i}.drawables", $"layer.{i}.tiles",
        $"layer.{i}.transformed.tiles", $"layer.{i}.tile.width.px", $"layer.{i}.tile.height.px", $"layer.{i}.z"
    }).ToArray();

    internal static TelemetrySource? Register(bool gpu, string adapter)
    {
        var source = Engine.Instance.Profiler.RegisterSource("Render surface",
            gpu ? (OperatingSystem.IsBrowser() ? "WebGL" : "Desktop GPU") : "Bitmap");
        if (source is null) return null;
        foreach (var name in Metrics)
        {
            var availability = TelemetryAvailability.NotYetSampled;
            if (!gpu) availability = TelemetryAvailability.Unsupported;
            else if (name.StartsWith("gate.", StringComparison.Ordinal) ||
                (OperatingSystem.IsBrowser() && (Array.IndexOf(Metrics, name) is >= 9 and <= 17)))
                availability = TelemetryAvailability.NotApplicable;
            else if (Array.IndexOf(Metrics, name) >= 20 && !adapter.Contains("WinForm", StringComparison.Ordinal))
                availability = TelemetryAvailability.Unsupported;
            source.Define(name, availability, name.EndsWith(".lifetime", StringComparison.Ordinal) ? TelemetryMetricKind.LifetimeCounter :
                name.Contains("cpu.ms", StringComparison.Ordinal) ? TelemetryMetricKind.Sample : TelemetryMetricKind.Gauge);
        }
        source.Define("presentation.count", gpu ? TelemetryAvailability.NotYetSampled : TelemetryAvailability.Unsupported);
        source.Define("layers.omitted");
        foreach (var layer in Layers)
            foreach (var name in layer) source.Define(name, gpu ? TelemetryAvailability.NotYetSampled : TelemetryAvailability.Unsupported);
        return source;
    }

}
