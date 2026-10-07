using System.Drawing;
using System.Numerics;
using Gondwana.Drawing.Direct;
using Gondwana.Rendering;
using Gondwana.Rendering.Views;
using SkiaSharp;

namespace Gondwana.Widgets.Overlays;

/// <summary>
/// Displays a lightweight view-level text tooltip near a screen position.
/// </summary>
/// <remarks>
/// Tooltips are non-interactive and therefore do not intercept pointer input from the
/// widget or game content beneath them. <see cref="ShowTooltip"/> clamps the tooltip
/// to the owning view and flips it to the opposite side of the pointer when needed.
/// </remarks>
public sealed class TooltipWidget : WidgetBase
{
    private readonly View _view;
    private Size _size;
    private int _pointerOffsetPx = 12;

    /// <summary>
    /// Creates a hidden view-level tooltip.
    /// </summary>
    /// <param name="renderSurfaceHost">The render surface host that owns the tooltip.</param>
    /// <param name="view">The view in which the tooltip is displayed.</param>
    /// <param name="size">The tooltip size in screen pixels.</param>
    /// <param name="nickname">An optional diagnostic nickname.</param>
    public TooltipWidget(
        RenderSurfaceHostBase renderSurfaceHost,
        View view,
        Size size,
        string? nickname = null)
        : base(renderSurfaceHost, DirectDrawingMode.View, PointF.Empty, nickname)
    {
        ArgumentNullException.ThrowIfNull(view);
        ValidateSize(size);

        _view = view;
        _size = size;
        Rectangle bounds = new(Point.Empty, size);

        Background = new DirectRectangle(
                Color.FromArgb(238, 30, 30, 36),
                renderSurfaceHost,
                view,
                bounds,
                $"{Nickname}.background")
            .SetFilled(true)
            .SetBorderColor(Color.FromArgb(255, 168, 168, 180))
            .SetStrokeWidth(1f)
            .SetStrokeAlign(DirectRectangle.StrokeAlign.Inside)
            .SetCornerRadius(4f);

        Label = new TextBlock(
                renderSurfaceHost,
                view,
                bounds,
                $"{Nickname}.label")
            .SetFont(SKTypeface.Default, 14f, minSize: 11f)
            .SetColors(SKColors.White, SKColors.Transparent)
            .SetAlignment(SKTextAlign.Left, TextBlock.VerticalAlign.Top)
            .SetPadding(10f, 8f)
            .EnableWrapping(true);

        Add(Background, keepCurrentOffset: false, explicitLocalOffsetPx: Vector2.Zero);
        Add(Label, keepCurrentOffset: false, explicitLocalOffsetPx: Vector2.Zero);

        IsInputEnabled = false;
        IsPointerInputEnabled = false;
        IsKeyboardInputEnabled = false;
        CanReceiveFocus = false;

        SetTooltipZOrder(int.MaxValue - 64);
        SetIsVisible(false);
    }

    /// <summary>Gets the tooltip background drawing.</summary>
    public DirectRectangle Background { get; }

    /// <summary>Gets the tooltip text drawing.</summary>
    public TextBlock Label { get; }

    /// <summary>Gets the current tooltip bounds in screen pixels.</summary>
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

    /// <summary>Gets or sets the tooltip size in screen pixels.</summary>
    public Size Size
    {
        get => _size;
        set
        {
            ValidateSize(value);
            _size = value;
            ApplyBounds(new Rectangle(Bounds.Location, value));
        }
    }

    /// <summary>Gets or sets the gap between the pointer and tooltip.</summary>
    public int PointerOffsetPx
    {
        get => _pointerOffsetPx;
        set => _pointerOffsetPx = value >= 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value));
    }

    /// <summary>
    /// Shows text near the supplied pointer position, clamped to the owning view.
    /// </summary>
    /// <param name="text">Tooltip text.</param>
    /// <param name="screenPositionPx">Pointer position in screen pixels.</param>
    /// <returns>The current tooltip.</returns>
    public TooltipWidget ShowTooltip(string text, Point screenPositionPx)
    {
        ArgumentNullException.ThrowIfNull(text);

        Label.SetText(text);
        ApplyBounds(ResolveBounds(screenPositionPx));

        if (!Visible)
            Show();

        return this;
    }

    /// <summary>Hides the tooltip.</summary>
    /// <returns>The current tooltip.</returns>
    public TooltipWidget HideTooltip()
    {
        if (Visible)
            Hide();

        return this;
    }

    /// <summary>Sets the drawing Z-order for the tooltip.</summary>
    /// <param name="zOrder">Background Z-order.</param>
    /// <returns>The current tooltip.</returns>
    public TooltipWidget SetTooltipZOrder(int zOrder)
    {
        Background.ZOrder = zOrder;
        Label.ZOrder = zOrder + 1;
        return this;
    }

    private Rectangle ResolveBounds(Point pointer)
    {
        Rectangle viewport = _view.Viewport.TargetRectPx;

        int x = pointer.X + PointerOffsetPx;
        int y = pointer.Y + PointerOffsetPx;

        if (x + _size.Width > viewport.Right)
            x = pointer.X - PointerOffsetPx - _size.Width;
        if (y + _size.Height > viewport.Bottom)
            y = pointer.Y - PointerOffsetPx - _size.Height;

        x = Math.Clamp(
            x,
            viewport.Left,
            Math.Max(viewport.Left, viewport.Right - _size.Width));
        y = Math.Clamp(
            y,
            viewport.Top,
            Math.Max(viewport.Top, viewport.Bottom - _size.Height));

        return new Rectangle(x, y, _size.Width, _size.Height);
    }

    private void ApplyBounds(Rectangle bounds)
    {
        SetPosition(bounds.X, bounds.Y);
        Background.ScreenBounds = bounds;
        Label.ScreenBounds = bounds;
    }

    private static void ValidateSize(Size size)
    {
        if (size.Width <= 0 || size.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(size), size, "Tooltip size must be positive.");
    }
}
