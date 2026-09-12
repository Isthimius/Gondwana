using System.Drawing;
using System.Numerics;
using Gondwana.Drawing.Direct;
using Gondwana.Rendering;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;
using SkiaSharp;

namespace Gondwana.Widgets.Controls;

/// <summary>
/// Provides a rectangular widget container with a configurable color or image background.
/// </summary>
public sealed class PanelWidget : ContainerWidget
{
    private int _panelZOrder;

    /// <summary>
    /// Creates a view-level panel.
    /// </summary>
    public PanelWidget(RenderSurfaceHostBase renderSurfaceHost,
                       View view,
                       Rectangle bounds,
                       Color backgroundColor,
                       string? nickname = null)
        : base(renderSurfaceHost, DirectDrawingMode.View, ValidateBounds(bounds).Location, nickname)
    {
        ArgumentNullException.ThrowIfNull(view);

        Background = new DirectRectangle(backgroundColor, renderSurfaceHost, view, bounds, $"{Nickname}.background")
            .SetFilled(true)
            .SetStrokeWidth(0f);

        Add(Background, keepCurrentOffset: false, explicitLocalOffsetPx: Vector2.Zero);
        DisableInput();
    }

    /// <summary>
    /// Creates a scene-layer panel.
    /// </summary>
    public PanelWidget(RenderSurfaceHostBase renderSurfaceHost,
                       SceneLayer sceneLayer,
                       Rectangle bounds,
                       Color backgroundColor,
                       string? nickname = null)
        : base(renderSurfaceHost, DirectDrawingMode.SceneLayer, ValidateBounds(bounds).Location, nickname)
    {
        ArgumentNullException.ThrowIfNull(sceneLayer);

        Background = new DirectRectangle(backgroundColor, renderSurfaceHost, sceneLayer, bounds, $"{Nickname}.background")
            .SetFilled(true)
            .SetStrokeWidth(0f);

        Add(Background, keepCurrentOffset: false, explicitLocalOffsetPx: Vector2.Zero);
        DisableInput();
    }

    /// <summary>
    /// Gets the panel background drawing.
    /// </summary>
    public DirectRectangle Background { get; }

    /// <summary>
    /// Gets the panel bounds in its native coordinate space.
    /// </summary>
    public Rectangle Bounds => Mode == DirectDrawingMode.View
        ? Background.ScreenBounds
        : Background.WorldBounds;

    /// <summary>
    /// Gets or sets the panel size while preserving its current position.
    /// </summary>
    public Size Size
    {
        get => Bounds.Size;
        set
        {
            ValidateSize(value);

            if (Mode == DirectDrawingMode.View)
                Background.ScreenBounds = new Rectangle(Background.ScreenBounds.Location, value);
            else
                Background.WorldBounds = new Rectangle(Background.WorldBounds.Location, value);
        }
    }

    /// <summary>
    /// Adds a child widget at the supplied local offset from the panel's upper-left corner.
    /// </summary>
    public PanelWidget AddWidget(WidgetBase widget, Point? offsetPx = null)
    {
        ArgumentNullException.ThrowIfNull(widget);

        Point offset = offsetPx ?? Point.Empty;
        Add(widget,
            keepCurrentOffset: false,
            explicitLocalOffsetPx: new Vector2(offset.X, offset.Y));
        widget.SetZOrder(_panelZOrder + 1);
        return this;
    }

    /// <summary>
    /// Removes a child widget without disposing it unless requested.
    /// </summary>
    public PanelWidget RemoveWidget(WidgetBase widget, bool dispose = false)
    {
        RemoveChild(widget, dispose);
        return this;
    }

    /// <summary>
    /// Sets the solid background color.
    /// </summary>
    public PanelWidget SetBackgroundColor(Color color)
    {
        Background.SetColor(color);
        RefreshBackground();
        return this;
    }

    /// <summary>
    /// Sets the panel border color.
    /// </summary>
    public PanelWidget SetBorderColor(Color color)
    {
        Background.SetBorderColor(color);
        RefreshBackground();
        return this;
    }

    /// <summary>
    /// Sets the panel border width.
    /// </summary>
    public PanelWidget SetStrokeWidth(float width)
    {
        Background.SetStrokeWidth(width);
        RefreshBackground();
        return this;
    }

    /// <summary>
    /// Sets the panel corner radius.
    /// </summary>
    public PanelWidget SetCornerRadius(float radius)
    {
        Background.SetCornerRadius(radius);
        RefreshBackground();
        return this;
    }

    /// <summary>
    /// Uses a bitmap as the panel background.
    /// </summary>
    public PanelWidget SetBackgroundImage(SKBitmap bitmap,
                                          DirectRectangle.ImageFillMode mode = DirectRectangle.ImageFillMode.Stretch,
                                          float scale = 1f,
                                          SKPoint? offsetPx = null,
                                          SKFilterQuality filterQuality = SKFilterQuality.Medium)
    {
        Background.SetFillImage(bitmap, mode, scale, offsetPx, filterQuality);
        return this;
    }

    /// <summary>
    /// Uses an image as the panel background.
    /// </summary>
    public PanelWidget SetBackgroundImage(SKImage image,
                                          DirectRectangle.ImageFillMode mode = DirectRectangle.ImageFillMode.Stretch,
                                          float scale = 1f,
                                          SKPoint? offsetPx = null,
                                          SKFilterQuality filterQuality = SKFilterQuality.Medium)
    {
        Background.SetFillImage(image, mode, scale, offsetPx, filterQuality);
        return this;
    }

    /// <summary>
    /// Clears an image background and returns to the configured solid fill.
    /// </summary>
    public PanelWidget ClearBackgroundImage()
    {
        Background.ClearFillImage();
        return this;
    }

    /// <summary>
    /// Places the background at the requested Z-order and child widgets immediately above it.
    /// </summary>
    public PanelWidget SetPanelZOrder(int zOrder)
    {
        _panelZOrder = zOrder;
        Background.ZOrder = zOrder;

        foreach (WidgetBase widget in ChildWidgets)
            widget.SetZOrder(zOrder + 1);

        return this;
    }

    private void RefreshBackground()
    {
        Background.SetPosition(Background.GetPosition());
    }

    private void DisableInput()
    {
        IsInputEnabled = false;
        IsPointerInputEnabled = false;
        IsKeyboardInputEnabled = false;
        CanReceiveFocus = false;
    }

    private static Rectangle ValidateBounds(Rectangle bounds)
    {
        ValidateSize(bounds.Size);
        return bounds;
    }

    private static void ValidateSize(Size size)
    {
        if (size.Width <= 0 || size.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(size), size, "Panel size must be positive.");
    }
}
