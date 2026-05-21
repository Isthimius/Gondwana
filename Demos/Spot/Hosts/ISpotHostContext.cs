using Gondwana.Rendering;
using Gondwana.Scenes;

namespace Gondwana.Demos.Spot;

/// <summary>
/// Provides the rendering-backend-specific context that <see cref="SpotHostCore"/> needs
/// to remain independent of the concrete <c>WinFormsGameHost</c> / <c>WinFormsGpuGameHost</c>
/// base classes.
/// </summary>
internal interface ISpotHostContext
{
    Engine Engine { get; }
    Scene Scene { get; }
    RenderSurfaceHostBase SurfaceHost { get; }
    int SurfaceWidth { get; }
    int SurfaceHeight { get; }
}
