using System.Drawing;
using Gondwana.Rendering;
using Gondwana.Scenes;
using SkiaSharp;

namespace Gondwana.Drawing.Direct;

/// <summary>
/// Convenience owner for a group of scene-layer radial lights.
/// </summary>
/// <remarks>
/// <para>
/// This is intentionally not a new <see cref="SceneLayer"/> type. Each light is still a bounded
/// <see cref="DirectRadialLight"/> registered with <see cref="DirectDrawingManager"/>, so the existing
/// scene-layer drawable query can include only lights whose world bounds intersect the dirty world rect.
/// </para>
/// <para>
/// Use this class when a game wants a clear logical owner for torch/lamp lights without changing the
/// renderer's composition model.
/// </para>
/// </remarks>
public sealed class DirectLightLayer : IDisposable
{
    private readonly List<DirectRadialLight> _lights = [];

    /// <summary>
    /// Occurs after a light has been added to this owner.
    /// </summary>
    public event EventHandler<DirectRadialLight>? LightAdded;

    /// <summary>
    /// Occurs before a light is disposed and removed from this owner.
    /// </summary>
    public event EventHandler<DirectRadialLight>? LightRemoving;

    /// <summary>
    /// Initializes a new light owner for the given render surface and scene layer.
    /// </summary>
    public DirectLightLayer(RenderSurfaceHostBase renderSurfaceHost, SceneLayer sceneLayer)
    {
        RenderSurfaceHost = renderSurfaceHost ?? throw new ArgumentNullException(nameof(renderSurfaceHost));
        SceneLayer = sceneLayer ?? throw new ArgumentNullException(nameof(sceneLayer));
    }

    /// <summary>
    /// Gets the render surface host used by lights created from this owner.
    /// </summary>
    public RenderSurfaceHostBase RenderSurfaceHost { get; }

    /// <summary>
    /// Gets the scene layer where lights are drawn.
    /// </summary>
    public SceneLayer SceneLayer { get; }

    /// <summary>
    /// Gets the lights owned by this layer.
    /// </summary>
    public IReadOnlyList<DirectRadialLight> Lights => _lights;

    /// <summary>
    /// Gets or sets the Z-order assigned to newly-created lights.
    /// </summary>
    public int DefaultZOrder { get; set; } = 10_000;

    /// <summary>
    /// Creates a warm, screen-blended torch-style light.
    /// </summary>
    public DirectRadialLight AddTorchLight(
        PointF centerWorldPx,
        float radiusWorldPx,
        Color? color = null,
        string? nickname = null)
    {
        var light = new DirectRadialLight(
            color ?? Color.FromArgb(180, 255, 190, 80),
            RenderSurfaceHost,
            SceneLayer,
            centerWorldPx,
            radiusWorldPx,
            nickname)
        {
            ZOrder = DefaultZOrder,
            BlendMode = SKBlendMode.Screen,
            HotspotRadiusRatio = 0.06f,
            MidpointRadiusRatio = 0.55f,
            MidpointIntensityRatio = 0.35f
        };

        _lights.Add(light);
        LightAdded?.Invoke(this, light);
        return light;
    }

    /// <summary>
    /// Removes and disposes a light owned by this layer.
    /// </summary>
    public bool Remove(DirectRadialLight light)
    {
        if (!_lights.Remove(light))
            return false;

        LightRemoving?.Invoke(this, light);
        light.Dispose();
        return true;
    }

    /// <summary>
    /// Disposes every light owned by this layer.
    /// </summary>
    public void Clear()
    {
        foreach (var light in _lights.ToArray())
            Remove(light);

        _lights.Clear();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Clear();
        LightAdded = null;
        LightRemoving = null;
    }
}
