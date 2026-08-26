using System.Drawing;
using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;
using Gondwana.SkiaSharp;
using SkiaSharp;

namespace Gondwana.Drawing.Direct;

/// <summary>
/// Renders a view-space darkness / fog overlay with optional world-space reveal sources.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="DirectDarknessOverlay"/> is the "other half" of a localized lighting setup.
/// It draws a semi-transparent darkness layer over an entire <see cref="View"/>, then carves
/// out soft reveal regions based on one or more world-space <see cref="RevealSource"/> instances.
/// </para>
/// <para>
/// This is intentionally implemented as a <see cref="DirectDrawingBase"/> in
/// <see cref="DirectDrawingMode.View"/> so it remains modular and take-it-or-leave-it:
/// no renderer-wide lighting system is required.
/// </para>
/// <para>
/// Reveal sources can be controlled manually, can track individual <see cref="DirectRadialLight"/>
/// instances, or can track every light in a <see cref="DirectLightLayer"/>. Tracking is optional;
/// manual reveal sources remain useful for player vision, stealth cones, magic sight, and scripted fog.
/// </para>
/// <para>
/// Dirty-rectangle behavior: because this is a full-view overlay, any visible change typically
/// refreshes the full viewport. That is expected for this style of effect and keeps the feature
/// self-contained.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var lights = new DirectLightLayer(renderSurfaceHost, dungeonLayer);
/// var torch = lights.AddTorchLight(new PointF(520, 320), 110f, nickname: "torch-01");
///
/// var darkness = new DirectDarknessOverlay(renderSurfaceHost, mainView, dungeonLayer, "dungeon-darkness")
///     .SetDarknessColor(Color.Black)
///     .SetDarknessOpacity(190);
///
/// darkness.TrackLight(torch);
///
/// // Later during gameplay:
/// torch.MoveTo(playerWorldCenterPx);
/// // The torch glow and darkness reveal now move together.
/// </code>
/// </example>
public sealed class DirectDarknessOverlay : DirectDrawingBase
{
    private readonly List<RevealSource> _revealSources = [];
    private readonly List<TrackedLightReveal> _trackedLightReveals = [];
    private readonly List<TrackedLightLayerReveal> _trackedLightLayers = [];

    private readonly SKPaint _darknessPaint = new()
    {
        IsAntialias = true,
        BlendMode = SKBlendMode.SrcOver,
        Style = SKPaintStyle.Fill
    };

    private Color _darknessColor = Color.Black;
    private byte _darknessOpacity = 190;
    private float _innerClearRadiusRatio = 0.20f;
    private float _midpointRadiusRatio = 0.62f;
    private float _midpointStrength = 0.45f;

    /// <summary>
    /// Initializes a new darkness/fog overlay that covers a specific view.
    /// </summary>
    /// <param name="renderSurfaceHost">The render surface host that owns the target scene and view.</param>
    /// <param name="view">The target view whose viewport will receive the overlay.</param>
    /// <param name="projectionLayer">
    /// The scene layer used to project world-space reveal sources into screen-space.
    /// In most games this should be the main gameplay/world layer.
    /// </param>
    /// <param name="nickname">Optional friendly name for debugging and lookup.</param>
    public DirectDarknessOverlay(RenderSurfaceHostBase renderSurfaceHost,
                                 View view,
                                 SceneLayer projectionLayer,
                                 string? nickname = null)
        : base(renderSurfaceHost,
               DirectDrawingMode.View,
               sceneLayer: null,
               view: view,
               screenBounds: view.Viewport.TargetRectPx,
               worldBounds: null,
               nickname: nickname)
    {
        ProjectionLayer = projectionLayer ?? throw new ArgumentNullException(nameof(projectionLayer));
        ZOrder = 20_000;
        RebuildDarknessPaint();
    }

    /// <summary>
    /// Gets the scene layer used to project reveal source world positions into screen coordinates.
    /// </summary>
    public SceneLayer ProjectionLayer { get; }

    /// <summary>
    /// Gets a live, ordered list of reveal sources used by this overlay.
    /// </summary>
    public IReadOnlyList<RevealSource> RevealSources => _revealSources;

    /// <summary>
    /// Gets or sets the tint color of the overlay.
    /// </summary>
    public Color DarknessColor
    {
        get => _darknessColor;
        set
        {
            if (_darknessColor == value)
                return;

            _darknessColor = value;
            RebuildDarknessPaint();
            ForceRefresh();
        }
    }

    /// <summary>
    /// Gets or sets the opacity of the darkness overlay.
    /// </summary>
    public byte DarknessOpacity
    {
        get => _darknessOpacity;
        set
        {
            if (_darknessOpacity == value)
                return;

            _darknessOpacity = value;
            RebuildDarknessPaint();
            ForceRefresh();
        }
    }

    /// <summary>
    /// Gets or sets the radius ratio that remains fully revealed before falloff begins.
    /// </summary>
    /// <remarks>
    /// A value of 0 means reveal falloff begins immediately from the center.
    /// A value near 1 means most of the reveal radius remains fully visible before fading.
    /// </remarks>
    public float InnerClearRadiusRatio
    {
        get => _innerClearRadiusRatio;
        set
        {
            float clamped = Math.Clamp(value, 0f, 1f);
            if (Math.Abs(_innerClearRadiusRatio - clamped) < 0.0001f)
                return;

            _innerClearRadiusRatio = clamped;
            if (_midpointRadiusRatio < _innerClearRadiusRatio)
                _midpointRadiusRatio = _innerClearRadiusRatio;

            ForceRefresh();
        }
    }

    /// <summary>
    /// Gets or sets the radius ratio of the midpoint alpha stop used for reveal falloff.
    /// </summary>
    public float MidpointRadiusRatio
    {
        get => _midpointRadiusRatio;
        set
        {
            float clamped = Math.Clamp(value, 0f, 1f);
            clamped = Math.Max(clamped, _innerClearRadiusRatio);

            if (Math.Abs(_midpointRadiusRatio - clamped) < 0.0001f)
                return;

            _midpointRadiusRatio = clamped;
            ForceRefresh();
        }
    }

    /// <summary>
    /// Gets or sets the removal strength at the midpoint stop.
    /// </summary>
    /// <remarks>
    /// 1.0 keeps the reveal strong deep into the radius; lower values produce a faster falloff.
    /// </remarks>
    public float MidpointStrength
    {
        get => _midpointStrength;
        set
        {
            float clamped = Math.Clamp(value, 0f, 1f);
            if (Math.Abs(_midpointStrength - clamped) < 0.0001f)
                return;

            _midpointStrength = clamped;
            ForceRefresh();
        }
    }

    /// <summary>
    /// Adds a new manually controlled reveal source in world-space.
    /// </summary>
    public RevealSource AddRevealSource(PointF centerWorldPx, float radiusWorldPx, string? nickname = null)
    {
        var source = new RevealSource(this, centerWorldPx, radiusWorldPx, nickname);
        _revealSources.Add(source);
        ForceRefresh();
        return source;
    }

    /// <summary>
    /// Adds a reveal source that automatically tracks a <see cref="DirectRadialLight"/>.
    /// </summary>
    /// <param name="light">The light whose center/radius/intensity should drive the reveal.</param>
    /// <param name="radiusScale">Multiplier applied to the light radius when calculating reveal radius.</param>
    /// <param name="intensityScale">Multiplier applied to the light intensity when calculating reveal intensity.</param>
    /// <param name="trackIntensity">
    /// When true, the reveal intensity follows <see cref="DirectRadialLight.EffectiveIntensity"/>.
    /// When false, the reveal uses <paramref name="intensityScale"/> as a constant intensity.
    /// </param>
    /// <param name="nickname">Optional friendly name for the reveal source.</param>
    public RevealSource TrackLight(DirectRadialLight light,
                                   float radiusScale = 1f,
                                   float intensityScale = 1f,
                                   bool trackIntensity = true,
                                   string? nickname = null)
    {
        if (light is null)
            throw new ArgumentNullException(nameof(light));

        if (_trackedLightReveals.Any(t => ReferenceEquals(t.Light, light)))
            return _trackedLightReveals.First(t => ReferenceEquals(t.Light, light)).Source;

        var source = new RevealSource(this, light.CenterWorldPx, light.RadiusWorldPx * Math.Max(0f, radiusScale), nickname ?? light.Nickname);
        _revealSources.Add(source);

        var tracked = new TrackedLightReveal(light, source, Math.Max(0f, radiusScale), Math.Max(0f, intensityScale), trackIntensity);
        _trackedLightReveals.Add(tracked);

        light.Changed += OnTrackedLightChanged;
        light.Disposing += OnTrackedLightDisposing;

        SyncTrackedLight(tracked);
        ForceRefresh();
        return source;
    }

    /// <summary>
    /// Tracks every current and future light in a <see cref="DirectLightLayer"/>.
    /// </summary>
    /// <remarks>
    /// This is a convenience wrapper for games that want one logical light owner and one darkness overlay.
    /// Existing lights are tracked immediately. Lights added to the layer later are tracked automatically.
    /// </remarks>
    public void TrackLightLayer(DirectLightLayer lightLayer,
                                float radiusScale = 1f,
                                float intensityScale = 1f,
                                bool trackIntensity = true)
    {
        if (lightLayer is null)
            throw new ArgumentNullException(nameof(lightLayer));

        if (_trackedLightLayers.Any(t => ReferenceEquals(t.LightLayer, lightLayer)))
            return;

        var trackedLayer = new TrackedLightLayerReveal(
            lightLayer,
            Math.Max(0f, radiusScale),
            Math.Max(0f, intensityScale),
            trackIntensity);

        _trackedLightLayers.Add(trackedLayer);

        foreach (var light in lightLayer.Lights)
            TrackLight(light, trackedLayer.RadiusScale, trackedLayer.IntensityScale, trackedLayer.TrackIntensity);

        lightLayer.LightAdded += OnTrackedLayerLightAdded;
        lightLayer.LightRemoving += OnTrackedLayerLightRemoving;
    }

    /// <summary>
    /// Stops tracking a specific light and removes its reveal source.
    /// </summary>
    public bool UntrackLight(DirectRadialLight light)
    {
        var tracked = _trackedLightReveals.FirstOrDefault(t => ReferenceEquals(t.Light, light));
        if (tracked is null)
            return false;

        UntrackLight(tracked, removeRevealSource: true);
        return true;
    }

    /// <summary>
    /// Removes a reveal source from this overlay.
    /// </summary>
    public bool RemoveRevealSource(RevealSource source)
    {
        if (source is null)
            return false;

        var tracked = _trackedLightReveals.FirstOrDefault(t => ReferenceEquals(t.Source, source));
        if (tracked is not null)
            UntrackLight(tracked, removeRevealSource: false);

        bool removed = _revealSources.Remove(source);
        if (removed)
            ForceRefresh();

        return removed;
    }

    /// <summary>
    /// Removes all reveal sources and stops all automatic tracking.
    /// </summary>
    public void ClearRevealSources()
    {
        if (_revealSources.Count == 0 && _trackedLightReveals.Count == 0 && _trackedLightLayers.Count == 0)
            return;

        foreach (var trackedLayer in _trackedLightLayers.ToArray())
        {
            trackedLayer.LightLayer.LightAdded -= OnTrackedLayerLightAdded;
            trackedLayer.LightLayer.LightRemoving -= OnTrackedLayerLightRemoving;
        }

        _trackedLightLayers.Clear();

        foreach (var tracked in _trackedLightReveals.ToArray())
            UntrackLight(tracked, removeRevealSource: false);

        _revealSources.Clear();
        ForceRefresh();
    }

    /// <summary>
    /// Fluent helper to set the darkness tint.
    /// </summary>
    public DirectDarknessOverlay SetDarknessColor(Color color)
    {
        DarknessColor = color;
        return this;
    }

    /// <summary>
    /// Fluent helper to set the darkness opacity.
    /// </summary>
    public DirectDarknessOverlay SetDarknessOpacity(byte opacity)
    {
        DarknessOpacity = opacity;
        return this;
    }

    /// <summary>
    /// Fluent helper to set the fully-revealed radius ratio.
    /// </summary>
    public DirectDarknessOverlay SetInnerClearRadiusRatio(float ratio)
    {
        InnerClearRadiusRatio = ratio;
        return this;
    }

    /// <summary>
    /// Fluent helper to set the midpoint radius ratio.
    /// </summary>
    public DirectDarknessOverlay SetMidpointRadiusRatio(float ratio)
    {
        MidpointRadiusRatio = ratio;
        return this;
    }

    /// <summary>
    /// Fluent helper to set the midpoint reveal strength.
    /// </summary>
    public DirectDarknessOverlay SetMidpointStrength(float strength)
    {
        MidpointStrength = strength;
        return this;
    }

    /// <inheritdoc />
    public override void Update(long tick)
    {
        // Keep the overlay pinned to the current viewport bounds.
        var targetRect = View!.Viewport.TargetRectPx;
        if (ScreenBounds != targetRect)
            ScreenBounds = targetRect;

        base.Update(tick);
    }

    /// <inheritdoc />
    protected override void OnDraw(BackbufferBase backbuffer, RectangleF destRectScreen)
    {
        var canvas = backbuffer.Canvas;
        var destRect = destRectScreen.ToSKRect();

        // Work in an isolated layer so "holes" reveal the already-rendered scene beneath,
        // rather than clearing the backbuffer itself.
        canvas.SaveLayer(destRect, null);

        canvas.DrawRect(destRect, _darknessPaint);

        for (int i = 0; i < _revealSources.Count; i++)
        {
            var source = _revealSources[i];
            if (!source.Enabled || source.RadiusWorldPx <= 0.01f || source.Intensity <= 0.001f)
                continue;

            var centerScreen = View!.WorldPxToScreenPx(ProjectionLayer, source.CenterWorldPx);

            var radiusProbeWorld = new PointF(source.CenterWorldPx.X + source.RadiusWorldPx, source.CenterWorldPx.Y);
            var radiusProbeScreen = View.WorldPxToScreenPx(ProjectionLayer, radiusProbeWorld);
            float screenRadius = Math.Abs(radiusProbeScreen.X - centerScreen.X);

            if (screenRadius <= 0.01f)
                continue;

            using var revealPaint = BuildRevealPaint(centerScreen, screenRadius, source.Intensity);
            canvas.DrawRect(destRect, revealPaint);
        }

        canvas.Restore();
    }

    internal void RefreshFromSourceChange()
    {
        ForceRefresh();
    }

    private void OnTrackedLightChanged(DirectRadialLight light)
    {
        foreach (var tracked in _trackedLightReveals.Where(t => ReferenceEquals(t.Light, light)).ToArray())
            SyncTrackedLight(tracked);
    }

    private void OnTrackedLightDisposing(object? sender, IDirectDrawable drawing)
    {
        if (drawing is DirectRadialLight light)
            UntrackLight(light);
    }

    private void OnTrackedLayerLightAdded(DirectRadialLight light)
    {
        foreach (var trackedLayer in _trackedLightLayers.Where(t => t.LightLayer.Lights.Contains(light)).ToArray())
            TrackLight(light, trackedLayer.RadiusScale, trackedLayer.IntensityScale, trackedLayer.TrackIntensity);
    }

    private void OnTrackedLayerLightRemoving(DirectRadialLight light)
    {
        UntrackLight(light);
    }

    private void SyncTrackedLight(TrackedLightReveal tracked)
    {
        var light = tracked.Light;
        float intensity = tracked.TrackIntensity
            ? light.EffectiveIntensity * tracked.IntensityScale
            : tracked.IntensityScale;

        tracked.Source.SyncFromTrackedLight(
            light.CenterWorldPx,
            light.RadiusWorldPx * tracked.RadiusScale,
            intensity);
    }

    private void UntrackLight(TrackedLightReveal tracked, bool removeRevealSource)
    {
        tracked.Light.Changed -= OnTrackedLightChanged;
        tracked.Light.Disposing -= OnTrackedLightDisposing;
        _trackedLightReveals.Remove(tracked);

        if (removeRevealSource)
            _revealSources.Remove(tracked.Source);

        ForceRefresh();
    }

    private void RebuildDarknessPaint()
    {
        _darknessPaint.Color = Color.FromArgb(_darknessOpacity, _darknessColor).ToSKColor();
    }

    private SKPaint BuildRevealPaint(PointF centerScreenPx, float radiusScreenPx, float intensity)
    {
        byte centerAlpha = (byte)Math.Clamp((int)Math.Round(255f * Math.Clamp(intensity, 0f, 1f)), 0, 255);
        byte midAlpha = (byte)Math.Clamp((int)Math.Round(255f * Math.Clamp(intensity, 0f, 1f) * _midpointStrength), 0, 255);

        var shader = SKShader.CreateRadialGradient(
            new SKPoint(centerScreenPx.X, centerScreenPx.Y),
            radiusScreenPx,
            colors:
            [
                new SKColor(255, 255, 255, centerAlpha),
                new SKColor(255, 255, 255, centerAlpha),
                new SKColor(255, 255, 255, midAlpha),
                new SKColor(255, 255, 255, 0)
            ],
            colorPos:
            [
                0f,
                _innerClearRadiusRatio,
                _midpointRadiusRatio,
                1f
            ],
            mode: SKShaderTileMode.Clamp);

        return new SKPaint
        {
            IsAntialias = true,
            BlendMode = SKBlendMode.DstOut,
            Shader = shader,
            Style = SKPaintStyle.Fill
        };
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ClearRevealSources();
            _darknessPaint.Dispose();
        }

        base.Dispose(disposing);
    }

    private sealed class TrackedLightReveal(
        DirectRadialLight light,
        RevealSource source,
        float radiusScale,
        float intensityScale,
        bool trackIntensity)
    {
        public DirectRadialLight Light { get; } = light;
        public RevealSource Source { get; } = source;
        public float RadiusScale { get; } = radiusScale;
        public float IntensityScale { get; } = intensityScale;
        public bool TrackIntensity { get; } = trackIntensity;
    }

    private sealed class TrackedLightLayerReveal(
        DirectLightLayer lightLayer,
        float radiusScale,
        float intensityScale,
        bool trackIntensity)
    {
        public DirectLightLayer LightLayer { get; } = lightLayer;
        public float RadiusScale { get; } = radiusScale;
        public float IntensityScale { get; } = intensityScale;
        public bool TrackIntensity { get; } = trackIntensity;
    }

    /// <summary>
    /// Represents a single world-space reveal source used by a <see cref="DirectDarknessOverlay"/>.
    /// </summary>
    public sealed class RevealSource
    {
        private readonly DirectDarknessOverlay _owner;
        private PointF _centerWorldPx;
        private float _radiusWorldPx;
        private float _intensity = 1f;
        private bool _enabled = true;

        internal RevealSource(DirectDarknessOverlay owner, PointF centerWorldPx, float radiusWorldPx, string? nickname)
        {
            _owner = owner;
            _centerWorldPx = centerWorldPx;
            _radiusWorldPx = Math.Max(0f, radiusWorldPx);
            Nickname = nickname;
        }

        /// <summary>
        /// Optional human-readable name for debugging and lookup.
        /// </summary>
        public string? Nickname { get; }

        /// <summary>
        /// Gets or sets whether this reveal source is currently active.
        /// </summary>
        public bool Enabled
        {
            get => _enabled;
            set
            {
                if (_enabled == value)
                    return;

                _enabled = value;
                _owner.RefreshFromSourceChange();
            }
        }

        /// <summary>
        /// Gets or sets the reveal center in world-space pixels.
        /// </summary>
        public PointF CenterWorldPx
        {
            get => _centerWorldPx;
            set
            {
                if (_centerWorldPx == value)
                    return;

                _centerWorldPx = value;
                _owner.RefreshFromSourceChange();
            }
        }

        /// <summary>
        /// Gets or sets the reveal radius in world-space pixels.
        /// </summary>
        public float RadiusWorldPx
        {
            get => _radiusWorldPx;
            set
            {
                float clamped = Math.Max(0f, value);
                if (Math.Abs(_radiusWorldPx - clamped) < 0.0001f)
                    return;

                _radiusWorldPx = clamped;
                _owner.RefreshFromSourceChange();
            }
        }

        /// <summary>
        /// Gets or sets the reveal intensity from 0..1.
        /// </summary>
        /// <remarks>
        /// 1 fully removes darkness at the center; lower values create weaker vision / dimmer reveal.
        /// </remarks>
        public float Intensity
        {
            get => _intensity;
            set
            {
                float clamped = Math.Clamp(value, 0f, 1f);
                if (Math.Abs(_intensity - clamped) < 0.0001f)
                    return;

                _intensity = clamped;
                _owner.RefreshFromSourceChange();
            }
        }

        /// <summary>
        /// Moves the reveal source to a new world-space position.
        /// </summary>
        public void MoveTo(PointF centerWorldPx)
        {
            CenterWorldPx = centerWorldPx;
        }

        /// <summary>
        /// Sets the reveal radius in world-space pixels.
        /// </summary>
        public void SetRadius(float radiusWorldPx)
        {
            RadiusWorldPx = radiusWorldPx;
        }

        internal void SyncFromTrackedLight(PointF centerWorldPx, float radiusWorldPx, float intensity)
        {
            bool changed =
                _centerWorldPx != centerWorldPx ||
                Math.Abs(_radiusWorldPx - radiusWorldPx) >= 0.0001f ||
                Math.Abs(_intensity - intensity) >= 0.0001f;

            if (!changed)
                return;

            _centerWorldPx = centerWorldPx;
            _radiusWorldPx = Math.Max(0f, radiusWorldPx);
            _intensity = Math.Clamp(intensity, 0f, 1f);
            _owner.RefreshFromSourceChange();
        }
    }
}
