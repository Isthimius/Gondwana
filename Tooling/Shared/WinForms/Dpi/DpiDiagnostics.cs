using System.Diagnostics;

namespace Gondwana.Tooling.WinForms;

/// <summary>Debug-output snapshots only; no release subscriptions or UI.</summary>
internal static class DpiDiagnostics
{
    [Conditional("DEBUG")]
    internal static void Attach(ToolStrip strip)
    {
#if DEBUG
        string? previous = null;
        void Record(string reason)
        {
            var state = $"{strip.FindForm()?.Name}/{strip.Name} ({strip.GetType().Name}) " +
                $"DPI={strip.DeviceDpi}, awareness={Application.HighDpiMode}, bounds={strip.Bounds}, " +
                $"ImageScalingSize={strip.ImageScalingSize}; " + string.Join("; ",
                    strip.Items.Cast<ToolStripItem>().Select(item =>
                        $"{item.Name}/{item.Text}: bounds={item.Bounds}, image={item.Image?.Size}, margin={item.Margin}"));
            if (state == previous) return;
            Debug.WriteLine($"[Tooling DPI: {reason}] {state}");
            previous = state;
        }
        strip.HandleCreated += (_, _) => Record("handle created");
        strip.DpiChangedBeforeParent += (_, _) => Record("before parent DPI change");
        strip.DpiChangedAfterParent += (_, _) => Record("after parent DPI change");
        strip.LayoutCompleted += (_, _) => Record("layout/scaling");
        Record("attached");
#endif
    }
}
