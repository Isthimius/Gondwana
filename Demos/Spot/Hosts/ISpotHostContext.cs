using Gondwana.Rendering;
using Gondwana.Scenes;

namespace Gondwana.Demos.Spot;

/// <summary>
/// Provides the rendering-backend-specific context that <see cref="SpotHostCore"/> needs
/// to remain independent of the concrete <c>WinFormsGameHost</c> / <c>WinFormsGpuGameHost</c>
/// base classes. This is only needed to allow for both Bitmap and GPU rendering from the same project.
/// </summary>
internal interface ISpotHostContext
{
    Gondwana.Engine Engine { get; }
    Scene Scene { get; }
    RenderSurfaceHostBase SurfaceHost { get; }
    int SurfaceWidth { get; }
    int SurfaceHeight { get; }
}
