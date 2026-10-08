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
    /// <param name="renderSurfaceHost">The render-surface host that owns the drawing.</param>
    /// <param name="view">The view used for presentation and coordinate conversion.</param>
    /// <param name="bounds">The widget bounds in view-local pixels.</param>
    /// <param name="backgroundColor">The background color.</param>
    /// <param name="nickname">An optional name used to identify the object.</param>
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
    /// <param name="renderSurfaceHost">The render-surface host that owns the drawing.</param>
    /// <param name="sceneLayer">The scene layer that owns the content.</param>
    /// <param name="bounds">The widget bounds in world pixels.</param>
    /// <param name="backgroundColor">The background color.</param>
    /// <param name="nickname">An optional name used to identify the object.</param>
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
    /// <param name="widget">The widget.</param>
    /// <param name="offsetPx">The offset in pixels.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public PanelWidget AddWidget(WidgetBase widget, Point? offsetPx = null)
    {
        ArgumentNullException.ThrowIfNull(widget);

        Point offset = offsetPx ?? Point.Empty;
        Add(widget,
            keepCurrentOffset: false,
            explicitLocalOffsetPx: new Vector2(offset.X, offset.Y));
        SetWidgetZOrder(widget, _panelZOrder + 1);
        return this;
    }

    /// <summary>
    /// Removes a child widget without disposing it unless requested.
    /// </summary>
    /// <param name="widget">The widget.</param>
    /// <param name="dispose">Whether to dispose the removed resource.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public PanelWidget RemoveWidget(WidgetBase widget, bool dispose = false)
    {
        RemoveChild(widget, dispose);
        return this;
    }

    /// <summary>
    /// Sets the solid background color.
    /// </summary>
    /// <param name="color">The color to apply.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public PanelWidget SetBackgroundColor(Color color)
    {
        Background.SetColor(color);
        RefreshBackground();
        return this;
    }

    /// <summary>
    /// Sets the panel border color.
    /// </summary>
    /// <param name="color">The color to apply.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public PanelWidget SetBorderColor(Color color)
    {
        Background.SetBorderColor(color);
        RefreshBackground();
        return this;
    }

    /// <summary>
    /// Sets the panel border width.
    /// </summary>
    /// <param name="width">The width.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public PanelWidget SetStrokeWidth(float width)
    {
        Background.SetStrokeWidth(width);
        RefreshBackground();
        return this;
    }

    /// <summary>
    /// Sets the panel corner radius.
    /// </summary>
    /// <param name="radius">The radius.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public PanelWidget SetCornerRadius(float radius)
    {
        Background.SetCornerRadius(radius);
        RefreshBackground();
        return this;
    }

    /// <summary>
    /// Uses a bitmap as the panel background.
    /// </summary>
    /// <param name="bitmap">The bitmap.</param>
    /// <param name="mode">The mode.</param>
    /// <param name="scale">The scale.</param>
    /// <param name="offsetPx">The offset in pixels.</param>
    /// <param name="filterQuality">The filter quality.</param>
    /// <returns>This instance for fluent chaining.</returns>
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
    /// <param name="image">The image.</param>
    /// <param name="mode">The mode.</param>
    /// <param name="scale">The scale.</param>
    /// <param name="offsetPx">The offset in pixels.</param>
    /// <param name="filterQuality">The filter quality.</param>
    /// <returns>This instance for fluent chaining.</returns>
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
    /// <returns>This instance for fluent chaining.</returns>
    public PanelWidget ClearBackgroundImage()
    {
        Background.ClearFillImage();
        return this;
    }

    /// <summary>
    /// Places the background at the requested Z-order and child widgets immediately above it.
    /// </summary>
    /// <param name="zOrder">The drawing order relative to other content.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public PanelWidget SetPanelZOrder(int zOrder)
    {
        _panelZOrder = zOrder;
        Background.ZOrder = zOrder;

        foreach (WidgetBase widget in ChildWidgets)
            SetWidgetZOrder(widget, zOrder + 1);

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

    private static void SetWidgetZOrder(WidgetBase widget, int zOrder)
    {
        DirectDrawingBase[] visuals = EnumerateVisuals(widget).ToArray();
        if (visuals.Length == 0)
            return;

        int currentMin = visuals.Min(static visual => visual.ZOrder);
        int delta = zOrder - currentMin;

        foreach (DirectDrawingBase visual in visuals)
            visual.ZOrder += delta;
    }

    private static IEnumerable<DirectDrawingBase> EnumerateVisuals(IDirectCompositeContainer container)
    {
        foreach (IDirectCompositeChild child in container.Children)
        {
            if (child is DirectDrawingBase drawing)
                yield return drawing;

            if (child is IDirectCompositeContainer nestedContainer)
            {
                foreach (DirectDrawingBase nestedDrawing in EnumerateVisuals(nestedContainer))
                    yield return nestedDrawing;
            }
        }
    }
}
