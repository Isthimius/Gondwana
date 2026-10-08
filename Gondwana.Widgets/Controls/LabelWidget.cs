using System.Drawing;
using System.Numerics;
using Gondwana.Drawing.Direct;
using Gondwana.Rendering;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;
using SkiaSharp;

namespace Gondwana.Widgets.Controls;

/// <summary>
/// Displays retained-mode text as a reusable Gondwana widget.
/// </summary>
public sealed class LabelWidget : WidgetBase
{
    private const int ScrollBarWidth = 12;
    private const int ScrollBarMargin = 3;
    private const int MinimumScrollBarThumbHeight = 18;

    private SKColor _foregroundColor = SKColors.White;
    private SKColor _backgroundColor = SKColors.Transparent;
    private string _text;
    private Size _size;
    private ScrollBarVisibility _verticalScrollBarVisibility = ScrollBarVisibility.Never;
    private int _mouseWheelScrollPixels = 48;
    private float _verticalScrollOffsetPx;
    private float _verticalScrollEndPaddingPx;
    private float _maximumVerticalScrollOffsetPx;
    private bool _isDraggingScrollBarThumb;
    private float _scrollBarDragOffset;
    private int? _scrollBarPointerId;

    /// <summary>
    /// Creates a view-level label.
    /// </summary>
    /// <param name="renderSurfaceHost">The render-surface host that owns the drawing.</param>
    /// <param name="view">The view used for presentation and coordinate conversion.</param>
    /// <param name="bounds">The widget bounds in view-local pixels.</param>
    /// <param name="text">The text to display.</param>
    /// <param name="nickname">An optional name used to identify the object.</param>
    public LabelWidget(RenderSurfaceHostBase renderSurfaceHost,
                       View view,
                       Rectangle bounds,
                       string text = "",
                       string? nickname = null)
        : base(renderSurfaceHost, DirectDrawingMode.View, ValidateBounds(bounds).Location, nickname)
    {
        ArgumentNullException.ThrowIfNull(view);

        _text = text ?? string.Empty;
        _size = bounds.Size;
        TextBlock = new TextBlock(renderSurfaceHost, view, bounds, $"{Nickname}.text")
            .SetText(_text)
            .SetColors(_foregroundColor, _backgroundColor);
        VerticalScrollBarTrack = CreateScrollBarTrack(renderSurfaceHost, view, bounds);
        VerticalScrollBarThumb = CreateScrollBarThumb(renderSurfaceHost, view, bounds);

        Add(TextBlock, keepCurrentOffset: false, explicitLocalOffsetPx: Vector2.Zero);
        Add(VerticalScrollBarTrack);
        Add(VerticalScrollBarThumb);
        CompleteInitialization();
    }

    /// <summary>
    /// Creates a scene-layer label.
    /// </summary>
    /// <param name="renderSurfaceHost">The render-surface host that owns the drawing.</param>
    /// <param name="sceneLayer">The scene layer that owns the content.</param>
    /// <param name="bounds">The widget bounds in world pixels.</param>
    /// <param name="text">The text to display.</param>
    /// <param name="nickname">An optional name used to identify the object.</param>
    public LabelWidget(RenderSurfaceHostBase renderSurfaceHost,
                       SceneLayer sceneLayer,
                       Rectangle bounds,
                       string text = "",
                       string? nickname = null)
        : base(renderSurfaceHost, DirectDrawingMode.SceneLayer, ValidateBounds(bounds).Location, nickname)
    {
        ArgumentNullException.ThrowIfNull(sceneLayer);

        _text = text ?? string.Empty;
        _size = bounds.Size;
        TextBlock = new TextBlock(renderSurfaceHost, sceneLayer, view: null, worldBounds: bounds, nickname: $"{Nickname}.text")
            .SetText(_text)
            .SetColors(_foregroundColor, _backgroundColor);
        VerticalScrollBarTrack = CreateScrollBarTrack(renderSurfaceHost, sceneLayer, bounds);
        VerticalScrollBarThumb = CreateScrollBarThumb(renderSurfaceHost, sceneLayer, bounds);

        Add(TextBlock, keepCurrentOffset: false, explicitLocalOffsetPx: Vector2.Zero);
        Add(VerticalScrollBarTrack);
        Add(VerticalScrollBarThumb);
        CompleteInitialization();
    }

    /// <summary>
    /// Gets the underlying text drawing.
    /// </summary>
    public TextBlock TextBlock { get; }

    /// <summary>Gets the vertical scrollbar track drawing.</summary>
    public DirectRectangle VerticalScrollBarTrack { get; }

    /// <summary>Gets the vertical scrollbar thumb drawing.</summary>
    public DirectRectangle VerticalScrollBarThumb { get; }

    /// <summary>
    /// Gets the current label text.
    /// </summary>
    public string Text => _text;

    /// <summary>
    /// Gets the label bounds in its native coordinate space.
    /// </summary>
    public Rectangle Bounds
    {
        get
        {
            Vector2 position = GetPosition();
            return new Rectangle(
                (int)MathF.Round(position.X),
                (int)MathF.Round(position.Y),
                _size.Width,
                _size.Height);
        }
    }

    /// <summary>
    /// Gets or sets the label size while preserving its current position.
    /// </summary>
    public Size Size
    {
        get => Bounds.Size;
        set
        {
            ValidateSize(value);
            _size = value;
            RefreshScrollState();
        }
    }

    /// <summary>Gets or sets when the label displays its vertical scrollbar.</summary>
    /// <remarks>
    /// The default is <see cref="ScrollBarVisibility.Never"/>, preserving the traditional
    /// non-interactive label behavior. Set this to <see cref="ScrollBarVisibility.Auto"/> or
    /// <see cref="ScrollBarVisibility.Always"/> to enable pointer scrolling.
    /// </remarks>
    public ScrollBarVisibility VerticalScrollBarVisibility
    {
        get => _verticalScrollBarVisibility;
        set
        {
            if (!Enum.IsDefined(value))
                throw new ArgumentOutOfRangeException(nameof(value));

            if (_verticalScrollBarVisibility == value)
                return;

            _verticalScrollBarVisibility = value;
            RefreshScrollState();
        }
    }

    /// <summary>Gets whether the vertical scrollbar is currently visible.</summary>
    public bool IsVerticalScrollBarVisible => Visible && NeedsVerticalScrollBar;

    /// <summary>Gets or sets the number of native pixels scrolled for one mouse-wheel notch.</summary>
    public int MouseWheelScrollPixels
    {
        get => _mouseWheelScrollPixels;
        set => _mouseWheelScrollPixels = value > 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), "Mouse-wheel scrolling must advance at least one pixel.");
    }

    /// <summary>
    /// Gets or sets additional space exposed after the last line when scrolled fully down.
    /// </summary>
    /// <remarks>
    /// The default is zero. A small positive value is useful for dense diagnostic/log views
    /// so font descent and rasterization rounding never leave the final line touching the clip edge.
    /// It does not create a scrollbar when the text otherwise fits.
    /// </remarks>
    public float VerticalScrollEndPaddingPx
    {
        get => _verticalScrollEndPaddingPx;
        set
        {
            if (!float.IsFinite(value) || value < 0f)
                throw new ArgumentOutOfRangeException(nameof(value));

            if (Math.Abs(_verticalScrollEndPaddingPx - value) < 0.001f)
                return;

            _verticalScrollEndPaddingPx = value;
            TextBlock.VerticalScrollEndPaddingPx = value;
            RefreshScrollState();
        }
    }

    /// <summary>Gets or sets the current vertical text-content scroll offset.</summary>
    public float VerticalScrollOffsetPx
    {
        get => _verticalScrollOffsetPx;
        set
        {
            if (!float.IsFinite(value) || value < 0f)
                throw new ArgumentOutOfRangeException(nameof(value));

            SetVerticalScrollOffset(value);
        }
    }

    /// <summary>Gets the maximum vertical text-content scroll offset for the current layout.</summary>
    public float MaximumVerticalScrollOffsetPx => _maximumVerticalScrollOffsetPx;

    /// <summary>
    /// Changes the displayed text.
    /// </summary>
    /// <param name="text">The text to display.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public LabelWidget SetText(string text)
    {
        _text = text ?? string.Empty;
        TextBlock.SetText(_text);
        RefreshScrollState();
        return this;
    }

    /// <summary>
    /// Configures the label typeface and size.
    /// </summary>
    /// <param name="typeface">The typeface used to render text.</param>
    /// <param name="size">The size.</param>
    /// <param name="minSize">The min size.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public LabelWidget SetFont(SKTypeface typeface, float size, float? minSize = null)
    {
        ArgumentNullException.ThrowIfNull(typeface);
        TextBlock.SetFont(typeface, size, minSize);
        RefreshScrollState();
        return this;
    }

    /// <summary>
    /// Sets foreground and background colors.
    /// </summary>
    /// <param name="foreground">The foreground.</param>
    /// <param name="background">The background.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public LabelWidget SetColors(SKColor foreground, SKColor background)
    {
        _foregroundColor = foreground;
        _backgroundColor = background;
        TextBlock.SetColors(_foregroundColor, _backgroundColor);
        return this;
    }

    /// <summary>
    /// Sets the foreground text color while preserving the background color.
    /// </summary>
    /// <param name="color">The color to apply.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public LabelWidget SetTextColor(SKColor color)
    {
        _foregroundColor = color;
        TextBlock.SetColors(_foregroundColor, _backgroundColor);
        return this;
    }

    /// <summary>
    /// Sets the text-block background color while preserving the foreground color.
    /// </summary>
    /// <param name="color">The color to apply.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public LabelWidget SetBackgroundColor(SKColor color)
    {
        _backgroundColor = color;
        TextBlock.SetColors(_foregroundColor, _backgroundColor);
        return this;
    }

    /// <summary>
    /// Configures horizontal and vertical text alignment.
    /// </summary>
    /// <param name="horizontal">The horizontal.</param>
    /// <param name="vertical">The vertical.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public LabelWidget SetAlignment(SKTextAlign horizontal, TextBlock.VerticalAlign vertical)
    {
        TextBlock.SetAlignment(horizontal, vertical);
        return this;
    }

    /// <summary>
    /// Enables or disables wrapping.
    /// </summary>
    /// <param name="enabled">Whether the feature is enabled.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public LabelWidget EnableWrapping(bool enabled = true)
    {
        TextBlock.EnableWrapping(enabled);
        RefreshScrollState();
        return this;
    }

    /// <summary>
    /// Sets symmetric horizontal and vertical padding.
    /// </summary>
    /// <param name="horizontal">The horizontal.</param>
    /// <param name="vertical">The vertical.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public LabelWidget SetPadding(float horizontal, float vertical)
    {
        if (!float.IsFinite(horizontal) || horizontal < 0f)
            throw new ArgumentOutOfRangeException(nameof(horizontal));
        if (!float.IsFinite(vertical) || vertical < 0f)
            throw new ArgumentOutOfRangeException(nameof(vertical));

        TextBlock.SetPadding(horizontal, vertical);
        RefreshScrollState();
        return this;
    }

    /// <summary>Sets the base Z-order used by the label text and optional scrollbar.</summary>
    /// <param name="zOrder">The drawing order relative to other content.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public LabelWidget SetLabelZOrder(int zOrder)
    {
        TextBlock.ZOrder = zOrder;
        VerticalScrollBarTrack.ZOrder = zOrder + 1;
        VerticalScrollBarThumb.ZOrder = zOrder + 2;
        return this;
    }

    /// <inheritdoc/>
    protected override void ProcessShown()
    {
        base.ProcessShown();
        RefreshScrollState();
    }

    /// <inheritdoc/>
    protected override void ProcessHidden()
    {
        _isDraggingScrollBarThumb = false;
        _scrollBarPointerId = null;
        base.ProcessHidden();
    }

    /// <inheritdoc/>
    protected override void OnPointerDown(WidgetPointerEventArgs args)
    {
        base.OnPointerDown(args);

        if (_scrollBarPointerId.HasValue || !args.IsPrimaryButton || !IsVerticalScrollBarVisible)
            return;

        PointF local = GetLocalPointerPosition(args);
        Rectangle trackBounds = GetScrollBarTrackBounds();
        Rectangle thumbBounds = GetScrollBarThumbBounds();

        if (!trackBounds.Contains(Point.Round(local)))
            return;

        args.Handled = true;
        _scrollBarPointerId = args.PointerId;

        if (thumbBounds.Contains(Point.Round(local)))
        {
            _isDraggingScrollBarThumb = true;
            _scrollBarDragOffset = local.Y - thumbBounds.Top;
            return;
        }

        float page = Math.Max(1f, GetViewportContentHeight());
        SetVerticalScrollOffset(_verticalScrollOffsetPx + (local.Y < thumbBounds.Top ? -page : page));
    }

    /// <inheritdoc/>
    protected override void OnPointerMove(WidgetPointerEventArgs args)
    {
        base.OnPointerMove(args);

        if (!_isDraggingScrollBarThumb ||
            _scrollBarPointerId != args.PointerId ||
            !IsVerticalScrollBarVisible)
        {
            return;
        }

        Rectangle trackBounds = GetScrollBarTrackBounds();
        Rectangle thumbBounds = GetScrollBarThumbBounds();
        float travel = trackBounds.Height - thumbBounds.Height;
        float maximumScroll = _maximumVerticalScrollOffsetPx;

        if (travel <= 0f || maximumScroll <= 0f)
            return;

        float desiredTop = GetLocalPointerPosition(args).Y - _scrollBarDragOffset;
        float fraction = Math.Clamp((desiredTop - trackBounds.Top) / travel, 0f, 1f);
        SetVerticalScrollOffset(fraction * maximumScroll);
        args.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnPointerUp(WidgetPointerEventArgs args)
    {
        base.OnPointerUp(args);

        if (_scrollBarPointerId != args.PointerId)
            return;

        _isDraggingScrollBarThumb = false;
        _scrollBarPointerId = null;
        args.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnMouseWheel(WidgetMouseWheelEventArgs args)
    {
        base.OnMouseWheel(args);

        float maximumScroll = _maximumVerticalScrollOffsetPx;
        if (args.Delta == 0 || maximumScroll <= 0f)
            return;

        long notches = Math.Max(1, Math.Abs((long)args.Delta) / 120);
        float change = notches * MouseWheelScrollPixels * (args.Delta > 0 ? -1f : 1f);
        SetVerticalScrollOffset(_verticalScrollOffsetPx + change);
        args.Handled = true;
    }

    private bool NeedsVerticalScrollBar =>
        VerticalScrollBarVisibility != ScrollBarVisibility.Never &&
        (VerticalScrollBarVisibility == ScrollBarVisibility.Always ||
         _maximumVerticalScrollOffsetPx > 0.5f);

    private void CompleteInitialization()
    {
        VerticalScrollBarTrack.Visible = false;
        VerticalScrollBarThumb.Visible = false;
        SetLabelZOrder(0);
        RefreshScrollState();
    }

    private void RefreshScrollState()
    {
        bool scrollingEnabled = VerticalScrollBarVisibility != ScrollBarVisibility.Never;

        Rectangle bounds = Bounds;
        SetTextBlockBounds(bounds);

        if (!scrollingEnabled)
        {
            _verticalScrollOffsetPx = 0f;
            TextBlock.VerticalScrollOffsetPx = 0f;
        }

        _maximumVerticalScrollOffsetPx = TextBlock.MeasureMaximumVerticalScrollOffsetPx();

        bool showScrollBar = VerticalScrollBarVisibility == ScrollBarVisibility.Always ||
            (VerticalScrollBarVisibility == ScrollBarVisibility.Auto &&
             _maximumVerticalScrollOffsetPx > 0.5f);

        if (showScrollBar)
        {
            Rectangle contentBounds = new(
                bounds.Left,
                bounds.Top,
                Math.Max(1, bounds.Width - ScrollBarWidth - ScrollBarMargin * 2),
                bounds.Height);
            SetTextBlockBounds(contentBounds);
            _maximumVerticalScrollOffsetPx = TextBlock.MeasureMaximumVerticalScrollOffsetPx();
        }

        IsInputEnabled = showScrollBar;
        IsPointerInputEnabled = showScrollBar;
        IsKeyboardInputEnabled = false;
        CanReceiveFocus = false;

        SetVerticalScrollOffset(_verticalScrollOffsetPx);

        bool visible = Visible && showScrollBar;
        VerticalScrollBarTrack.Visible = visible;
        VerticalScrollBarThumb.Visible = visible;

        if (visible)
            RefreshScrollBarBounds();
    }

    private void SetVerticalScrollOffset(float value)
    {
        _verticalScrollOffsetPx = Math.Clamp(value, 0f, _maximumVerticalScrollOffsetPx);
        TextBlock.VerticalScrollOffsetPx = _verticalScrollOffsetPx;
        RefreshScrollBarBounds();
    }

    private float GetViewportContentHeight()
    {
        Rectangle textBounds = Mode == DirectDrawingMode.View
            ? TextBlock.ScreenBounds
            : TextBlock.WorldBounds;

        return Math.Max(0f, textBounds.Height - TextBlock.VerticalPadding * 2f);
    }

    private void RefreshScrollBarBounds()
    {
        if (!IsVerticalScrollBarVisible)
            return;

        SetRectangleBounds(VerticalScrollBarTrack, GetScrollBarTrackBounds());
        SetRectangleBounds(VerticalScrollBarThumb, GetScrollBarThumbBounds());
    }

    private Rectangle GetScrollBarTrackBounds()
    {
        return new Rectangle(
            Bounds.Right - ScrollBarMargin - ScrollBarWidth,
            Bounds.Top + ScrollBarMargin,
            ScrollBarWidth,
            Math.Max(1, Bounds.Height - ScrollBarMargin * 2));
    }

    private Rectangle GetScrollBarThumbBounds()
    {
        Rectangle trackBounds = GetScrollBarTrackBounds();
        float maximumScroll = _maximumVerticalScrollOffsetPx;
        float viewportHeight = GetViewportContentHeight();

        if (maximumScroll <= 0f || viewportHeight <= 0f)
            return trackBounds;

        float contentHeight = viewportHeight + maximumScroll;
        int thumbHeight = Math.Clamp(
            (int)MathF.Round(trackBounds.Height * Math.Min(1f, viewportHeight / contentHeight)),
            Math.Min(MinimumScrollBarThumbHeight, trackBounds.Height),
            trackBounds.Height);
        int travel = trackBounds.Height - thumbHeight;
        int offset = maximumScroll <= 0f
            ? 0
            : (int)MathF.Round(travel * (_verticalScrollOffsetPx / maximumScroll));

        return new Rectangle(
            trackBounds.Left,
            trackBounds.Top + offset,
            trackBounds.Width,
            thumbHeight);
    }

    private PointF GetLocalPointerPosition(WidgetPointerEventArgs args)
    {
        RectangleF screenBounds = TextBlock.GetDrawLocationScreen(args.View);
        screenBounds.Offset(
            args.WrappedOffsetWorldPx.X * args.View.Viewport.Zoom,
            args.WrappedOffsetWorldPx.Y * args.View.Viewport.Zoom);

        Rectangle textBounds = Mode == DirectDrawingMode.View
            ? TextBlock.ScreenBounds
            : TextBlock.WorldBounds;

        float scaleX = screenBounds.Width / textBounds.Width;
        float scaleY = screenBounds.Height / textBounds.Height;

        return new PointF(
            Bounds.Left + (args.ScreenPositionPx.X - screenBounds.Left) / scaleX,
            Bounds.Top + (args.ScreenPositionPx.Y - screenBounds.Top) / scaleY);
    }

    private void SetTextBlockBounds(Rectangle bounds)
    {
        // Use TextBlock's size API so width changes invalidate wrapping/layout.
        TextBlock.SetSize(bounds.Size);
        TextBlock.SetPosition(new Vector2(bounds.X, bounds.Y));

        Rectangle overallBounds = Bounds;
        SetLocalOffset(
            TextBlock,
            new Vector2(bounds.X - overallBounds.X, bounds.Y - overallBounds.Y));
    }

    private void SetRectangleBounds(DirectRectangle rectangle, Rectangle bounds)
    {
        if (Mode == DirectDrawingMode.View)
            rectangle.ScreenBounds = bounds;
        else
            rectangle.WorldBounds = bounds;

        SetLocalOffset(
            rectangle,
            new Vector2(bounds.X - Bounds.X, bounds.Y - Bounds.Y));
    }

    private static DirectRectangle CreateScrollBarTrack(
        RenderSurfaceHostBase host,
        View view,
        Rectangle bounds)
        => ConfigureScrollBarTrack(new DirectRectangle(Color.FromArgb(255, 26, 26, 32), host, view, bounds));

    private static DirectRectangle CreateScrollBarTrack(
        RenderSurfaceHostBase host,
        SceneLayer sceneLayer,
        Rectangle bounds)
        => ConfigureScrollBarTrack(new DirectRectangle(Color.FromArgb(255, 26, 26, 32), host, sceneLayer, bounds));

    private static DirectRectangle ConfigureScrollBarTrack(DirectRectangle track)
        => track.SetFilled(true).SetStrokeWidth(0f).SetCornerRadius(3f);

    private static DirectRectangle CreateScrollBarThumb(
        RenderSurfaceHostBase host,
        View view,
        Rectangle bounds)
        => ConfigureScrollBarThumb(new DirectRectangle(Color.FromArgb(255, 126, 126, 142), host, view, bounds));

    private static DirectRectangle CreateScrollBarThumb(
        RenderSurfaceHostBase host,
        SceneLayer sceneLayer,
        Rectangle bounds)
        => ConfigureScrollBarThumb(new DirectRectangle(Color.FromArgb(255, 126, 126, 142), host, sceneLayer, bounds));

    private static DirectRectangle ConfigureScrollBarThumb(DirectRectangle thumb)
        => thumb.SetFilled(true).SetStrokeWidth(0f).SetCornerRadius(3f);

    private static Rectangle ValidateBounds(Rectangle bounds)
    {
        ValidateSize(bounds.Size);
        return bounds;
    }

    private static void ValidateSize(Size size)
    {
        if (size.Width <= 0 || size.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(size), size, "Label size must be positive.");
    }
}
