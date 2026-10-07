using System.Drawing;
using Gondwana.Diagnostics;
using Gondwana.Rendering;
using Gondwana.Rendering.Views;
using Gondwana.Widgets.Hud;

namespace Gondwana.Examples;

/// <summary>Ordinary application usage, compiled by Gondwana.Tests without any viewer dependency.</summary>
public static class RuntimeTelemetryExample
{
    /// <summary>Starts a request to retain for the application's diagnostic session.</summary>
    /// <returns>A request that the application must dispose when finished.</returns>
    public static IDisposable StartCollection() => Engine.Instance.Profiler.Start();

    /// <summary>Creates an in-game profiler widget that owns collection while it is shown.</summary>
    /// <param name="host">Render surface host for the game.</param>
    /// <param name="view">View that should contain the diagnostics HUD.</param>
    /// <returns>A profiler widget. Call Show/Hide to control both display and its collection request.</returns>
    public static ProfilerWidget CreateWidget(RenderSurfaceHostBase host, View view)
    {
        var widget = new ProfilerWidget(
            host,
            view,
            new Rectangle(12, 12, 700, 520));

        // Hide any metric globally, or target a single named source.
        widget.SetMeasurementVisible("layers.omitted", false);
        widget.SetMeasurementVisible("Render surface", "snapshot.commands.approximate", false);
        widget.ContextInfo = ProfilerContextInfo.View | ProfilerContextInfo.Backbuffer;
        widget.AdditionalLinesProvider = _ => ["Diagnostics: application-defined line"];
        return widget;
    }

    /// <summary>Formats an already detached snapshot at the application's chosen display cadence.</summary>
    /// <param name="profiler">Usually Engine.Instance.Profiler.</param>
    /// <param name="output">Application output; never called on a hot producer path.</param>
    public static void PrintLatest(RuntimeProfiler profiler, TextWriter output)
    {
        var snapshot = profiler.GetLatestSnapshot();
        if (snapshot is null) return;
        foreach (var source in snapshot.Sources)
        {
            if (source.Metrics.TryGetValue("cycle.cpu.ms", out var cycle) &&
                cycle.Availability == TelemetryAvailability.Available)
                output.WriteLine($"CPS={cycle.Count / snapshot.ElapsedSeconds:F1}; CPU mean={cycle.Mean:F3} ms");
        }
    }
}
