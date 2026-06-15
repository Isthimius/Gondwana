using Gondwana.Avalonia.Rendering;

namespace TestBrowserApp;

/// <summary>
/// The render surface for TestBrowserApp.
/// Wraps <see cref="AvaloniaBitmapRenderSurfaceControl"/> (CPU bitmap renderer,
/// compatible with all Avalonia targets including browser/WASM).
/// </summary>
internal sealed class GameRenderSurface : AvaloniaBitmapRenderSurfaceControl
{
}
