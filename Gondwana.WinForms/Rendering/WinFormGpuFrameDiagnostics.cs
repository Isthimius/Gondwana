namespace Gondwana.WinForms.Rendering;

/// <summary>
/// Captures measured work performed by one WinForms GPU paint callback.
/// </summary>
/// <param name="TotalCallbackMilliseconds">The total callback milliseconds.</param>
/// <param name="RenderAndSnapshotMilliseconds">The render and snapshot milliseconds.</param>
/// <param name="BlitMilliseconds">The blit milliseconds.</param>
/// <param name="FlushMilliseconds">The flush milliseconds.</param>
public sealed record WinFormGpuFrameDiagnostics(
    double TotalCallbackMilliseconds,
    double RenderAndSnapshotMilliseconds,
    double BlitMilliseconds,
    double FlushMilliseconds);
