using System.Drawing;
using System.Numerics;
using Gondwana.Drawing.Direct;
using Gondwana.Drawing.Sprites;
using Gondwana.Rendering;

namespace Gondwana.Widgets.Hud;

/// <summary>
/// Displays a world-space health bar that follows a sprite.
/// </summary>
/// <remarks>
/// The widget tracks sprite movement automatically. Set <see cref="Value"/>
/// when gameplay changes the target's health.
/// </remarks>
public sealed class HealthBarWidget : WidgetBase
{
    private const int InnerPadding = 2;

    private readonly DirectRectangle _track;
    private readonly DirectRectangle _fill;

    private bool _disposed;
    private float _maximum;
    private float _value;
    private Size _size;
    private Point _offsetPx;

    /// <summary>
    /// Initializes a health bar that follows <paramref name="target"/>.
    /// </summary>
    /// <param name="renderSurfaceHost">The render surface hosting the target scene.</param>
    /// <param name="target">The sprite followed by the bar.</param>
    /// <param name="maximum">The maximum health value. Must be greater than zero.</param>
    /// <param name="size">The outer bar size in world pixels.</param>
    /// <param name="offsetPx">An additional world-pixel offset from the centered position above the sprite.</param>
    /// <param name="nickname">An optional diagnostic nickname.</param>
    public HealthBarWidget(
        RenderSurfaceHostBase renderSurfaceHost,
        Sprite target,
        float maximum,
        Size? size = null,
        Point? offsetPx = null,
        string? nickname = null)
        : base(
            renderSurfaceHost,
            DirectDrawingMode.SceneLayer,
            nickname: nickname)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));

        if (maximum <= 0f)
            throw new ArgumentOutOfRangeException(nameof(maximum), "Maximum health must be greater than zero.");

        _maximum = maximum;
        _value = maximum;
        _size = ValidateSize(size ?? new Size(64, 9));
        _offsetPx = offsetPx ?? Point.Empty;

        IsInputEnabled = false;
        IsPointerInputEnabled = false;

        _track = new DirectRectangle(
                Color.FromArgb(220, 20, 24, 31),
                renderSurfaceHost,
                Target.SceneLayer,
                new Rectangle(Point.Empty, _size),
                $"{nickname ?? Target.Nickname ?? "sprite"}-health-track")
            .SetFilled(true)
            .SetBorderColor(Color.FromArgb(235, 235, 241, 247))
            .SetStrokeWidth(1f)
            .SetStrokeAlign(DirectRectangle.StrokeAlign.Inside)
            .SetCornerRadius(2f);

        _fill = new DirectRectangle(
                Color.FromArgb(245, 55, 210, 105),
                renderSurfaceHost,
                Target.SceneLayer,
                GetFillBounds(Point.Empty),
                $"{nickname ?? Target.Nickname ?? "sprite"}-health-fill")
            .SetFilled(true)
            .SetStrokeWidth(0f)
            .SetCornerRadius(1f);

        Add(
            _track,
            keepCurrentOffset: false,
            explicitLocalOffsetPx: Vector2.Zero);

        Add(
            _fill,
            keepCurrentOffset: false,
            explicitLocalOffsetPx: new Vector2(InnerPadding, InnerPadding));

        Target.SpriteMoved += OnTargetMoved;
        Target.Disposing += OnTargetDisposing;

        RefreshPosition();
        UpdateFillBounds();
    }

    /// <summary>
    /// Gets the sprite followed by this health bar.
    /// </summary>
    public Sprite Target { get; }

    /// <summary>
    /// Gets or sets the maximum health value.
    /// </summary>
    public float Maximum
    {
        get => _maximum;
        set
        {
            if (value <= 0f)
                throw new ArgumentOutOfRangeException(nameof(value), "Maximum health must be greater than zero.");

            _maximum = value;
            _value = Math.Clamp(_value, 0f, _maximum);
            UpdateFillBounds();
        }
    }

    /// <summary>
    /// Gets or sets the current health value. Values are clamped to zero through <see cref="Maximum"/>.
    /// </summary>
    public float Value
    {
        get => _value;
        set
        {
            float clamped = Math.Clamp(value, 0f, Maximum);

            if (_value.Equals(clamped))
                return;

            _value = clamped;
            UpdateFillBounds();
        }
    }

    /// <summary>
    /// Gets the filled fraction in the range zero through one.
    /// </summary>
    public float Fraction => Value / Maximum;

    /// <summary>
    /// Gets or sets the outer bar size in world pixels.
    /// </summary>
    public Size Size
    {
        get => _size;
        set
        {
            Size validated = ValidateSize(value);

            if (_size == validated)
                return;

            _size = validated;
            _track.WorldBounds = new Rectangle(_track.WorldBounds.Location, _size);
            RefreshPosition();
            UpdateFillBounds();
        }
    }

    /// <summary>
    /// Gets or sets an additional world-pixel offset from the centered position above the target.
    /// </summary>
    public Point OffsetPx
    {
        get => _offsetPx;
        set
        {
            _offsetPx = value;
            RefreshPosition();
        }
    }

    /// <summary>
    /// Gets the current world bounds of the health-bar track.
    /// </summary>
    public Rectangle TrackBoundsWorld => _track.WorldBounds;

    /// <summary>
    /// Gets the current world bounds of the filled portion.
    /// </summary>
    public Rectangle FillBoundsWorld => _fill.WorldBounds;

    /// <summary>
    /// Sets the current health and returns this widget for fluent setup.
    /// </summary>
    public HealthBarWidget SetValue(float value)
    {
        Value = value;
        return this;
    }

    /// <summary>
    /// Sets the fill color and returns this widget.
    /// </summary>
    public HealthBarWidget SetFillColor(Color color)
    {
        _fill.SetColor(color);
        return this;
    }

    /// <summary>
    /// Repositions the bar from the target's current render bounds.
    /// </summary>
    public void RefreshPosition()
    {
        Rectangle targetBounds = Target.DrawLocationWorld;
        int x = targetBounds.Left + (targetBounds.Width - Size.Width) / 2 + OffsetPx.X;
        int y = targetBounds.Top - Size.Height - 6 + OffsetPx.Y;

        SetPosition(x, y);
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Target.SpriteMoved -= OnTargetMoved;
        Target.Disposing -= OnTargetDisposing;
        base.Dispose();
    }

    /// <inheritdoc/>
    protected override void ProcessShown()
    {
        base.ProcessShown();
        _fill.Visible = Value > 0f;
    }

    private void OnTargetMoved(SpriteMovedEventArgs args)
    {
        RefreshPosition();
    }

    private void OnTargetDisposing(Sprite sprite)
    {
        Dispose();
    }

    private void UpdateFillBounds()
    {
        Point location = _fill.WorldBounds.Location;
        Rectangle fillBounds = GetFillBounds(location);

        _fill.WorldBounds = fillBounds;
        _fill.Visible = Value > 0f && _track.Visible;
    }

    private Rectangle GetFillBounds(Point location)
    {
        int availableWidth = Math.Max(0, Size.Width - InnerPadding * 2);
        int availableHeight = Math.Max(1, Size.Height - InnerPadding * 2);
        int fillWidth = (int)MathF.Round(availableWidth * Fraction);

        return new Rectangle(location, new Size(fillWidth, availableHeight));
    }

    private static Size ValidateSize(Size value)
    {
        if (value.Width < InnerPadding * 2 + 1)
            throw new ArgumentOutOfRangeException(nameof(value), "Health-bar width is too small.");

        if (value.Height < InnerPadding * 2 + 1)
            throw new ArgumentOutOfRangeException(nameof(value), "Health-bar height is too small.");

        return value;
    }
}
