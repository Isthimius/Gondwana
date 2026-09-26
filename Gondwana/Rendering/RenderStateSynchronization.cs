namespace Gondwana.Rendering;

/// <summary>
/// Serializes live render-state mutation on the engine thread with GPU scene traversal
/// on platform GL threads.
/// </summary>
/// <remarks>
/// Desktop GPU hosts render live scene and DirectDrawing objects from a platform-owned
/// GL callback while the engine updates those same objects on its background thread.
/// Holding this gate across each engine cycle and each live GPU render prevents native
/// Skia resources from being configured or disposed while the GL thread is using them.
/// </remarks>
internal static class RenderStateSynchronization
{
    internal static object SyncRoot { get; } = new();
}
