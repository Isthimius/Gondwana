namespace Gondwana.WinForms.Rendering;

/// <summary>
/// Captures measured work performed by one WinForms GPU paint callback.
/// </summary>
public sealed record WinFormGpuFrameDiagnostics(
    double TotalCallbackMilliseconds,
    double RenderAndSnapshotMilliseconds,
    double BlitMilliseconds,
    double FlushMilliseconds);
