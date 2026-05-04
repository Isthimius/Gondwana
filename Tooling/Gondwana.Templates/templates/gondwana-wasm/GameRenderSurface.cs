using Gondwana.Avalonia.Rendering;

namespace MyGame;

/// <summary>
/// The render surface for MyGame.
/// Wraps <see cref="AvaloniaBitmapRenderSurfaceControl"/> (CPU bitmap renderer,
/// compatible with all Avalonia targets including browser/WASM).
/// </summary>
internal sealed class GameRenderSurface : AvaloniaBitmapRenderSurfaceControl
{
}
