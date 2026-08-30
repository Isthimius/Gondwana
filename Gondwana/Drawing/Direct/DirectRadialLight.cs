using System.Drawing;
using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Scenes;
using Gondwana.Timers;
using SkiaSharp;

namespace Gondwana.Drawing.Direct;

/// <summary>
/// Draws a bounded, radial light effect as a scene-layer direct drawing.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="DirectRadialLight"/> is intended for localized draw-time lighting effects such as
/// torches, lamps, spell glows, and other finite-radius light sources. It is deliberately modeled
/// as a normal <see cref="DirectDrawingBase"/> instead of a post-process effect so it participates
/// in Gondwana's existing dirty-rectangle selection path.
/// </para>
/// <para>
/// The important contract is its bounds: <see cref="WorldBounds"/> is the full world-space area that
/// may be affected by the light. When a light moves or changes radius, both the old and new bounds
/// are marked dirty by the inherited <see cref="DirectDrawingBase.WorldBounds"/> setter.
/// </para>
/// <para>
/// Rendering uses a Skia radial-gradient <see cref="SKShader"/> on an <see cref="SKPaint"/>. This keeps
/// the first lighting primitive compatible with both bitmap and GPU-backed backbuffers while still
/// exercising the same "shader during draw" idea that a later SkSL-backed implementation would use.
/// </para>
/// </remarks>
public sealed class DirectRadialLight : DirectDrawingBase
{
    private PointF _centerWorldPx;
    private float _radiusWorldPx;
    private Color _lightColor;
    private float _intensity = 1f;
    private float _hotspotRadiusRatio = 0.06f;
    private float _midpointRadiusRatio = 0.55f;
    private float _midpointIntensityRatio = 0.35f;
    private bool _isAntialias = true;
    private SKBlendMode _blendMode = SKBlendMode.Screen;

    private bool _flickerEnabled;
    private float _flickerAmount = 0.08f;
    private int _flickerRefreshHz = 12;
    private float _flickerPhaseSec;
    private float _flickerMultiplier = 1f;
    private long _lastFlickerRefreshTick;

    /// <summary>
    /// Occurs when this light changes in a way tracked darkness overlays care about.
    /// </summary>
    /// <remarks>
    /// This event is primarily a synchronization hook for <see cref="DirectDarknessOverlay"/>
    /// and <see cref="DirectSceneLayerDarknessOverlay"/>. It is raised when the light's center,
    /// radius, base intensity, or effective flicker intensity changes. It is not intended to be a
    /// complete "any paint setting changed" notification for every cosmetic property.
    /// </remarks>
    public event Action<DirectRadialLight>? Changed;

    /// <summary>
    /// Initializes a new scene-layer radial light.
    /// </summary>
    /// <param name="lightColor">The color and base alpha of the light.</param>
    /// <param name="renderSurfaceHost">The render surface host that owns the drawing.</param>
    /// <param name="sceneLayer">The scene layer where the light is positioned.</param>
    /// <param name="centerWorldPx">The center of the light in world pixels.</param>
    /// <param name="radiusWorldPx">The light radius in world pixels.</param>
    /// <param name="nickname">Optional name used by <see cref="DirectDrawingManager"/>.</param>
    public DirectRadialLight(
        Color lightColor,
        RenderSurfaceHostBase renderSurfaceHost,
        SceneLayer sceneLayer,
        PointF centerWorldPx,
        float radiusWorldPx,
        string? nickname = null)
        : base(
            renderSurfaceHost,
            DirectDrawingMode.SceneLayer,
            sceneLayer,
            view: null,
            screenBounds: null,
            worldBounds: BoundsFromCenter(centerWorldPx, radiusWorldPx),
            nickname)
    {
        _lightColor = lightColor;
        _centerWorldPx = centerWorldPx;
        _radiusWorldPx = Math.Max(1f, radiusWorldPx);
    }

    /// <summary>
    /// Gets the light center in world pixels.
    /// </summary>
    public PointF CenterWorldPx
    {
        get => _centerWorldPx;
        set
        {
            if (ApproximatelyEqual(_centerWorldPx, value))
                return;

            _centerWorldPx = value;
            UpdateWorldBounds();
        }
    }

    /// <summary>
    /// Gets the light radius in world pixels.
    /// </summary>
    /// <remarks>
    /// Changing the radius changes <see cref="WorldBounds"/>, which dirties the old and new light areas.
    /// </remarks>
    public float RadiusWorldPx
    {
        get => _radiusWorldPx;
        set
        {
            var clamped = Math.Max(1f, value);
            if (Math.Abs(_radiusWorldPx - clamped) < 0.001f)
                return;

            _radiusWorldPx = clamped;
            UpdateWorldBounds();
        }
    }

    /// <summary>
    /// Gets or sets the color and base alpha of the light.
    /// </summary>
    public Color LightColor
    {
        get => _lightColor;
        set
        {
            if (_lightColor.ToArgb() == value.ToArgb())
                return;

            _lightColor = value;
            ForceRefresh();
            OnChanged();
        }
    }

    /// <summary>
    /// Gets or sets the overall intensity multiplier from 0 to 1.
    /// </summary>
    public float Intensity
    {
        get => _intensity;
        set
        {
            var clamped = Clamp01(value);
            if (Math.Abs(_intensity - clamped) < 0.001f)
                return;

            _intensity = clamped;
            ForceRefresh();
            OnChanged();
        }
    }

    /// <summary>
    /// Gets the current intensity after flicker has been applied.
    /// </summary>
    public float EffectiveIntensity => Clamp01(_intensity * _flickerMultiplier);

    /// <summary>
    /// Gets or sets the blend mode used when compositing the light over scene content.
    /// </summary>
    public SKBlendMode BlendMode
    {
        get => _blendMode;
        set
        {
            if (_blendMode == value)
                return;

            _blendMode = value;
            ForceRefresh();
        }
    }

    /// <summary>
    /// Gets or sets the radius ratio for the hottest inner portion of the gradient.
    /// </summary>
    public float HotspotRadiusRatio
    {
        get => _hotspotRadiusRatio;
        set
        {
            var clamped = Clamp01(value);
            if (Math.Abs(_hotspotRadiusRatio - clamped) < 0.001f)
                return;

            _hotspotRadiusRatio = clamped;
            ForceRefresh();
        }
    }

    /// <summary>
    /// Gets or sets the radius ratio for the middle gradient stop.
    /// </summary>
    public float MidpointRadiusRatio
    {
        get => _midpointRadiusRatio;
        set
        {
            var clamped = Clamp01(value);
            if (Math.Abs(_midpointRadiusRatio - clamped) < 0.001f)
                return;

            _midpointRadiusRatio = clamped;
            ForceRefresh();
        }
    }

    /// <summary>
    /// Gets or sets the middle stop intensity multiplier from 0 to 1.
    /// </summary>
    public float MidpointIntensityRatio
    {
        get => _midpointIntensityRatio;
        set
        {
            var clamped = Clamp01(value);
            if (Math.Abs(_midpointIntensityRatio - clamped) < 0.001f)
                return;

            _midpointIntensityRatio = clamped;
            ForceRefresh();
        }
    }

    /// <summary>
    /// Gets or sets whether Skia antialiasing is enabled for the light draw.
    /// </summary>
    public bool IsAntialias
    {
        get => _isAntialias;
        set
        {
            if (_isAntialias == value)
                return;

            _isAntialias = value;
            ForceRefresh();
        }
    }

    /// <summary>
    /// Gets or sets whether the light should periodically vary its intensity.
    /// </summary>
    /// <remarks>
    /// Flicker marks the light's full bounds dirty at <see cref="FlickerRefreshHz"/>. If a
    /// <see cref="DirectDarknessOverlay"/> reveal source is tracking this light with intensity tracking
    /// enabled, the overlay will also refresh at this cadence.
    /// </remarks>
    public bool FlickerEnabled
    {
        get => _flickerEnabled;
        set
        {
            if (_flickerEnabled == value)
                return;

            _flickerEnabled = value;
            _flickerMultiplier = 1f;
            _lastFlickerRefreshTick = 0;
            ForceRefresh();
            OnChanged();
        }
    }

    /// <summary>
    /// Gets or sets how much flicker may alter the base intensity. Recommended range: 0 to 0.25.
    /// </summary>
    public float FlickerAmount
    {
        get => _flickerAmount;
        set
        {
            var clamped = Math.Clamp(value, 0f, 1f);
            if (Math.Abs(_flickerAmount - clamped) < 0.001f)
                return;

            _flickerAmount = clamped;
            ForceRefresh();
            OnChanged();
        }
    }

    /// <summary>
    /// Gets or sets how often flicker is recomputed and dirtied.
    /// </summary>
    public int FlickerRefreshHz
    {
        get => _flickerRefreshHz;
        set => _flickerRefreshHz = Math.Max(1, value);
    }

    /// <summary>
    /// Moves the light center and returns this instance for chaining.
    /// </summary>
    public DirectRadialLight MoveTo(PointF centerWorldPx)
    {
        CenterWorldPx = centerWorldPx;
        return this;
    }

    /// <summary>
    /// Sets the light radius and returns this instance for chaining.
    /// </summary>
    public DirectRadialLight SetRadius(float radiusWorldPx)
    {
        RadiusWorldPx = radiusWorldPx;
        return this;
    }

    /// <summary>
    /// Sets the light intensity and returns this instance for chaining.
    /// </summary>
    public DirectRadialLight SetIntensity(float intensity)
    {
        Intensity = intensity;
        return this;
    }

    /// <inheritdoc />
    public override void Update(long tick)
    {
        UpdateFlicker(tick);
        base.Update(tick);
    }

    /// <inheritdoc />
    protected override void OnDraw(BackbufferBase backbuffer, RectangleF destRectScreen)
    {
        if (destRectScreen.Width <= 0f || destRectScreen.Height <= 0f)
            return;

        var canvas = backbuffer.Canvas;
        var worldBounds = WorldBounds;

        if (worldBounds.Width <= 0 || worldBounds.Height <= 0)
            return;

        var rect = new SKRect(destRectScreen.Left, destRectScreen.Top, destRectScreen.Right, destRectScreen.Bottom);
        float scaleX = destRectScreen.Width / worldBounds.Width;
        float scaleY = destRectScreen.Height / worldBounds.Height;

        // Use the actual world-space center/radius rather than the integer-rounded
        // WorldBounds midpoint. The bounds are intentionally pixel-aligned for dirty
        // rectangles, but the shader geometry should preserve sub-pixel light movement
        // so a moving torch does not pop by ~0.5px as its bounds floor/ceil change.
        var center = new SKPoint(
            destRectScreen.Left + (_centerWorldPx.X - worldBounds.Left) * scaleX,
            destRectScreen.Top + (_centerWorldPx.Y - worldBounds.Top) * scaleY);

        var radius = Math.Max(Math.Abs(_radiusWorldPx * scaleX), Math.Abs(_radiusWorldPx * scaleY));

        if (radius <= 0f)
            return;

        var hotspot = Math.Clamp(_hotspotRadiusRatio, 0f, 0.95f);
        var midpoint = Math.Clamp(_midpointRadiusRatio, hotspot + 0.001f, 0.999f);
        var effectiveIntensity = EffectiveIntensity;

        var inner = ToSkColor(_lightColor, effectiveIntensity);
        var middle = ToSkColor(_lightColor, effectiveIntensity * _midpointIntensityRatio);
        var outer = ToSkColor(_lightColor, 0f);

        using var shader = SKShader.CreateRadialGradient(
            center,
            radius,
            new[] { inner, inner, middle, outer },
            new[] { 0f, hotspot, midpoint, 1f },
            SKShaderTileMode.Clamp);

        using var paint = new SKPaint
        {
            Shader = shader,
            Style = SKPaintStyle.Fill,
            BlendMode = _blendMode,
            IsAntialias = _isAntialias
        };

        canvas.DrawRect(rect, paint);
    }

    private void UpdateFlicker(long tick)
    {
        if (!_flickerEnabled)
            return;

        if (_lastFlickerRefreshTick == 0)
        {
            _lastFlickerRefreshTick = tick;
            return;
        }

        if (tick <= _lastFlickerRefreshTick)
            return;

        var dt = HighResTimer.GetDuration(_lastFlickerRefreshTick, tick);
        var refreshIntervalSec = 1f / _flickerRefreshHz;

        if (dt < refreshIntervalSec)
            return;

        _flickerPhaseSec += dt;

        var primary = 0.5f + 0.5f * MathF.Sin(_flickerPhaseSec * MathF.Tau * _flickerRefreshHz);
        var secondary = 0.5f + 0.5f * MathF.Sin(_flickerPhaseSec * MathF.Tau * (_flickerRefreshHz * 0.37f + 0.73f));
        var mixed = primary * 0.65f + secondary * 0.35f;

        _flickerMultiplier = 1f - _flickerAmount + mixed * _flickerAmount * 2f;
        _lastFlickerRefreshTick = tick;

        ForceRefresh();
        OnChanged();
    }

    private void UpdateWorldBounds()
    {
        WorldBounds = BoundsFromCenter(_centerWorldPx, _radiusWorldPx);
        OnChanged();
    }

    private void OnChanged()
    {
        Changed?.Invoke(this);
    }

    private static Rectangle BoundsFromCenter(PointF centerWorldPx, float radiusWorldPx)
    {
        var radius = Math.Max(1f, radiusWorldPx);
        var left = (int)MathF.Floor(centerWorldPx.X - radius);
        var top = (int)MathF.Floor(centerWorldPx.Y - radius);
        var right = (int)MathF.Ceiling(centerWorldPx.X + radius);
        var bottom = (int)MathF.Ceiling(centerWorldPx.Y + radius);

        return Rectangle.FromLTRB(left, top, right, bottom);
    }

    private static SKColor ToSkColor(Color color, float intensity)
    {
        var alpha = (byte)Math.Clamp(color.A * Clamp01(intensity), 0f, 255f);
        return new SKColor(color.R, color.G, color.B, alpha);
    }

    private static float Clamp01(float value) => Math.Clamp(value, 0f, 1f);

    private static bool ApproximatelyEqual(PointF left, PointF right)
    {
        return Math.Abs(left.X - right.X) < 0.001f &&
               Math.Abs(left.Y - right.Y) < 0.001f;
    }
}
