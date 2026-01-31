namespace Gondwana.Drawing.Direct;

/// <summary>
/// Specifies where direct drawing operations are applied within the rendering pipeline.
/// </summary>
/// <remarks>
/// This mode determines the coordinate space and lifecycle of direct drawing:
/// <list type="bullet">
/// <item>
/// <term><see cref="SceneLayer"/></term>
/// <description>
/// Draws in world space as part of a specific scene layer. Content scrolls, zooms,
/// and parallax-moves with the camera.
/// </description>
/// </item>
/// <item>
/// <term><see cref="View"/></term>
/// <description>
/// Draws in screen space at the view level. Content is fixed to the viewport and is
/// not affected by camera movement, making it suitable for UI, overlays, and debug visuals.
/// </description>
/// </item>
/// </list>
/// </remarks>
public enum DirectDrawingMode
{
    /// <summary>
    /// Performs direct drawing within a scene layer using world-space coordinates.
    /// </summary>
    /// <remarks>
    /// Drawing in this mode is affected by camera position, zoom, and layer parallax,
    /// and participates in the normal scene rendering and refresh logic.
    /// </remarks>
    SceneLayer,

    /// <summary>
    /// Performs direct drawing at the view level using screen-space coordinates.
    /// </summary>
    /// <remarks>
    /// Drawing in this mode is independent of camera movement and world transforms,
    /// making it ideal for HUD elements, overlays, and diagnostic rendering.
    /// </remarks>
    View
}
