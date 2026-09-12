using System.Drawing;
using System.Numerics;
using Gondwana.Drawing.Direct;
using Gondwana.Rendering;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;
using Gondwana.Widgets.Layout;

namespace Gondwana.Widgets.Controls;

/// <summary>
/// Displays a normalized progress value as a horizontal or vertical filled bar.
/// </summary>
public sealed class ProgressBarWidget : WidgetBase
{
    private float _value;
    private WidgetOrientation _orientation;
    private int _padding = 2;
    private Size _size;

    /// <summary>
    /// Creates a view-level progress bar.
    /// </summary>
    public ProgressBarWidget(RenderSurfaceHostBase renderSurfaceHost,
                             View view,
                             Rectangle bounds,
                             float value = 0f,
                             WidgetOrientation orientation = WidgetOrientation.Horizontal,
                             string? nickname = null)
        : base(renderSurfaceHost, DirectDrawingMode.View, ValidateBounds(bounds).Location, nickname)
    {
        ArgumentNullException.ThrowIfNull(view);

        _size = bounds.Size;
        _orientation = orientation;

        Track = CreateTrack(renderSurfaceHost, view, bounds);
        Fill = CreateFill(renderSurfaceHost, view, bounds);
        CompleteInitialization(value);
    }

    /// <summary>
    /// Creates a scene-layer progress bar.
    /// </summary>
    public ProgressBarWidget(RenderSurfaceHostBase renderSurfaceHost,
                             SceneLayer sceneLayer,
                             Rectangle bounds,
                             float value = 0f,
                             WidgetOrientation orientation = WidgetOrientation.Horizontal,
                             string? nickname = null)
        : base(renderSurfaceHost, DirectDrawingMode.SceneLayer, ValidateBounds(bounds).Location, nickname)
    {
        ArgumentNullException.ThrowIfNull(sceneLayer);

        _size = bounds.Size;
        _orientation = orientation;

        Track = CreateTrack(renderSurfaceHost, sceneLayer, bounds);
        Fill = CreateFill(renderSurfaceHost, sceneLayer, bounds);
        CompleteInitialization(value);
    }

    /// <summary>
    /// Gets the background/track drawing.
    /// </summary>
    public DirectRectangle Track { get; }

    /// <summary>
    /// Gets the foreground/fill drawing.
    /// </summary>
    public DirectRectangle Fill { get; }

    /// <summary>
    /// Gets or sets the normalized progress value in the range 0 through 1.
    /// </summary>
    public float Value
    {
        get => _value;
        set
        {
            if (!float.IsFinite(value))
                throw new ArgumentOutOfRangeException(nameof(value));

            float clamped = Math.Clamp(value, 0f, 1f);
            if (_value.Equals(clamped))
                return;

            _value = clamped;
            UpdateFillBounds();
        }
    }

    /// <summary>
    /// Gets or sets the fill orientation. Vertical bars fill from bottom to top.
    /// </summary>
    public WidgetOrientation Orientation
    {
        get => _orientation;
        set
        {
            if (_orientation == value)
                return;

            _orientation = value;
            UpdateFillBounds();
        }
    }

    /// <summary>
    /// Gets or sets the inner padding between the track and fill.
    /// </summary>
    public int Padding
    {
        get => _padding;
        set
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value));

            _padding = value;
            UpdateFillBounds();
        }
    }

    /// <summary>
    /// Gets or sets the outer progress-bar size.
    /// </summary>
    public Size Size
    {
        get => _size;
        set
        {
            ValidateSize(value);
            _size = value;

            if (Mode == DirectDrawingMode.View)
                Track.ScreenBounds = new Rectangle(Track.ScreenBounds.Location, value);
            else
                Track.WorldBounds = new Rectangle(Track.WorldBounds.Location, value);

            UpdateFillBounds();
        }
    }

    /// <summary>
    /// Gets the track bounds in the widget's native coordinate space.
    /// </summary>
    public Rectangle TrackBounds => Mode == DirectDrawingMode.View
        ? Track.ScreenBounds
        : Track.WorldBounds;

    /// <summary>
    /// Gets the fill bounds in the widget's native coordinate space.
    /// </summary>
    public Rectangle FillBounds => Mode == DirectDrawingMode.View
        ? Fill.ScreenBounds
        : Fill.WorldBounds;

    /// <summary>
    /// Sets the progress value and returns this widget for fluent setup.
    /// </summary>
    public ProgressBarWidget SetValue(float value)
    {
        Value = value;
        return this;
    }

    /// <summary>
    /// Sets the fill color.
    /// </summary>
    public ProgressBarWidget SetFillColor(Color color)
    {
        Fill.SetColor(color);
        Fill.SetPosition(Fill.GetPosition());
        return this;
    }

    /// <summary>
    /// Sets the track color.
    /// </summary>
    public ProgressBarWidget SetTrackColor(Color color)
    {
        Track.SetColor(color);
        Track.SetPosition(Track.GetPosition());
        return this;
    }

    /// <summary>
    /// Places the track at the requested Z-order and the fill directly above it.
    /// </summary>
    public ProgressBarWidget SetProgressZOrder(int zOrder)
    {
        Track.ZOrder = zOrder;
        Fill.ZOrder = zOrder + 1;
        return this;
    }

    /// <inheritdoc/>
    protected override void ProcessShown()
    {
        base.ProcessShown();
        Fill.Visible = Value > 0f;
    }

    private void CompleteInitialization(float value)
    {
        Add(Track, keepCurrentOffset: false, explicitLocalOffsetPx: Vector2.Zero);
        Add(Fill, keepCurrentOffset: false, explicitLocalOffsetPx: new Vector2(_padding, _padding));

        IsInputEnabled = false;
        IsPointerInputEnabled = false;
        IsKeyboardInputEnabled = false;
        CanReceiveFocus = false;

        SetProgressZOrder(0);
        _value = Math.Clamp(float.IsFinite(value) ? value : throw new ArgumentOutOfRangeException(nameof(value)), 0f, 1f);
        UpdateFillBounds();
    }

    private void UpdateFillBounds()
    {
        int innerWidth = Math.Max(0, _size.Width - _padding * 2);
        int innerHeight = Math.Max(0, _size.Height - _padding * 2);

        Size fillSize;
        Vector2 offset;

        if (_orientation == WidgetOrientation.Horizontal)
        {
            int fillWidth = (int)MathF.Round(innerWidth * _value);
            fillSize = new Size(fillWidth, innerHeight);
            offset = new Vector2(_padding, _padding);
        }
        else
        {
            int fillHeight = (int)MathF.Round(innerHeight * _value);
            fillSize = new Size(innerWidth, fillHeight);
            offset = new Vector2(_padding, _padding + innerHeight - fillHeight);
        }

        Rectangle current = FillBounds;
        Rectangle resized = new(current.Location, fillSize);

        if (Mode == DirectDrawingMode.View)
            Fill.ScreenBounds = resized;
        else
            Fill.WorldBounds = resized;

        SetLocalOffset(Fill, offset);
        Fill.Visible = _value > 0f && Track.Visible;
    }

    private DirectRectangle CreateTrack(RenderSurfaceHostBase host, View view, Rectangle bounds)
    {
        return new DirectRectangle(Color.FromArgb(220, 28, 30, 36), host, view, bounds, $"{Nickname}.track")
            .SetFilled(true)
            .SetBorderColor(Color.FromArgb(235, 225, 228, 234))
            .SetStrokeWidth(1f)
            .SetStrokeAlign(DirectRectangle.StrokeAlign.Inside)
            .SetCornerRadius(2f);
    }

    private DirectRectangle CreateTrack(RenderSurfaceHostBase host, SceneLayer layer, Rectangle bounds)
    {
        return new DirectRectangle(Color.FromArgb(220, 28, 30, 36), host, layer, bounds, $"{Nickname}.track")
            .SetFilled(true)
            .SetBorderColor(Color.FromArgb(235, 225, 228, 234))
            .SetStrokeWidth(1f)
            .SetStrokeAlign(DirectRectangle.StrokeAlign.Inside)
            .SetCornerRadius(2f);
    }

    private DirectRectangle CreateFill(RenderSurfaceHostBase host, View view, Rectangle bounds)
    {
        return new DirectRectangle(Color.FromArgb(245, 55, 210, 105), host, view, bounds, $"{Nickname}.fill")
            .SetFilled(true)
            .SetStrokeWidth(0f)
            .SetCornerRadius(1f);
    }

    private DirectRectangle CreateFill(RenderSurfaceHostBase host, SceneLayer layer, Rectangle bounds)
    {
        return new DirectRectangle(Color.FromArgb(245, 55, 210, 105), host, layer, bounds, $"{Nickname}.fill")
            .SetFilled(true)
            .SetStrokeWidth(0f)
            .SetCornerRadius(1f);
    }

    private static Rectangle ValidateBounds(Rectangle bounds)
    {
        ValidateSize(bounds.Size);
        return bounds;
    }

    private static void ValidateSize(Size size)
    {
        if (size.Width <= 0 || size.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(size), size, "Progress-bar size must be positive.");
    }
}
