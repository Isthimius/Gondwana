using System.Drawing;
using System.Numerics;
using Gondwana.Drawing.Direct;
using Gondwana.Rendering;
using SkiaSharp;

namespace Gondwana.Widgets.Controls;

/// <summary>
/// Represents one selectable page owned by a <see cref="TabControlWidget"/>.
/// </summary>
/// <remarks>
/// Create pages with <see cref="TabControlWidget.AddTab(string,char?,string?)"/>.
/// The page owns the widgets placed in its content area and is shown only while
/// it is the selected page of its owning tab control.
/// </remarks>
public sealed class TabPageWidget : ContainerWidget
{
    private int _pageZOrder;
    private string _title;

    /// <summary>
    /// Initializes a view-level page for an owning tab control.
    /// </summary>
    /// <param name="renderSurfaceHost">The render surface host that owns the page.</param>
    /// <param name="view">The view that renders the page.</param>
    /// <param name="contentBounds">The page content bounds in view pixels.</param>
    /// <param name="title">The tab title.</param>
    /// <param name="mnemonic">The optional Alt-key mnemonic.</param>
    /// <param name="nickname">The optional diagnostic nickname.</param>
    internal TabPageWidget(RenderSurfaceHostBase renderSurfaceHost,
                           Gondwana.Rendering.Views.View view,
                           Rectangle contentBounds,
                           string title,
                           char? mnemonic,
                           string? nickname)
        : base(renderSurfaceHost, DirectDrawingMode.View, ValidateBounds(contentBounds).Location, nickname)
    {
        ArgumentNullException.ThrowIfNull(view);

        _title = ValidateTitle(title);
        Mnemonic = mnemonic;

        Background = new DirectRectangle(DefaultBackgroundColor,
                                         renderSurfaceHost,
                                         view,
                                         contentBounds,
                                         $"{Nickname}.background")
            .SetFilled(true)
            .SetBorderColor(DefaultBorderColor)
            .SetStrokeWidth(1f);

        Add(Background);

        DisablePageInput();
    }

    /// <summary>
    /// Initializes a scene-layer page for an owning tab control.
    /// </summary>
    /// <param name="renderSurfaceHost">The render surface host that owns the page.</param>
    /// <param name="sceneLayer">The scene layer that renders the page.</param>
    /// <param name="contentBounds">The page content bounds in world pixels.</param>
    /// <param name="title">The tab title.</param>
    /// <param name="mnemonic">The optional Alt-key mnemonic.</param>
    /// <param name="nickname">The optional diagnostic nickname.</param>
    internal TabPageWidget(RenderSurfaceHostBase renderSurfaceHost,
                           Gondwana.Scenes.SceneLayer sceneLayer,
                           Rectangle contentBounds,
                           string title,
                           char? mnemonic,
                           string? nickname)
        : base(renderSurfaceHost, DirectDrawingMode.SceneLayer, ValidateBounds(contentBounds).Location, nickname)
    {
        ArgumentNullException.ThrowIfNull(sceneLayer);

        _title = ValidateTitle(title);
        Mnemonic = mnemonic;

        Background = new DirectRectangle(DefaultBackgroundColor,
                                         renderSurfaceHost,
                                         sceneLayer,
                                         contentBounds,
                                         $"{Nickname}.background")
            .SetFilled(true)
            .SetBorderColor(DefaultBorderColor)
            .SetStrokeWidth(1f);

        Add(Background);

        DisablePageInput();
    }

    /// <summary>
    /// Gets the direct rectangle used for this page's content background.
    /// </summary>
    public DirectRectangle Background { get; }

    /// <summary>
    /// Gets the title displayed by the page's tab header.
    /// </summary>
    public string Title => _title;

    /// <summary>
    /// Gets the optional Alt-key mnemonic that selects this page.
    /// </summary>
    public char? Mnemonic { get; }

    /// <summary>
    /// Gets the page's content bounds in its native coordinate space.
    /// </summary>
    public Rectangle ContentBounds => Mode == DirectDrawingMode.View
        ? Background.ScreenBounds
        : Background.WorldBounds;

    /// <summary>
    /// Gets the bounds occupied by this page's tab header in its native coordinate space.
    /// </summary>
    public Rectangle HeaderBounds { get; internal set; }

    /// <summary>
    /// Adds a widget at an offset from the page content area's upper-left corner.
    /// </summary>
    /// <param name="widget">The widget to add.</param>
    /// <param name="offsetPx">The local content offset, or <see langword="null"/> for the origin.</param>
    /// <returns>The current page.</returns>
    public TabPageWidget AddWidget(WidgetBase widget,
                                   Point? offsetPx = null)
    {
        ArgumentNullException.ThrowIfNull(widget);

        Point offset = offsetPx ?? Point.Empty;
        Add(widget,
            keepCurrentOffset: false,
            explicitLocalOffsetPx: new Vector2(offset.X, offset.Y));
        SetWidgetZOrder(widget, _pageZOrder + 1);

        return this;
    }

    /// <summary>
    /// Removes a child widget from the page.
    /// </summary>
    /// <param name="widget">The widget to remove.</param>
    /// <param name="dispose">Whether to dispose the widget after removing it.</param>
    /// <returns>The current page.</returns>
    public TabPageWidget RemoveWidget(WidgetBase widget,
                                      bool dispose = false)
    {
        RemoveChild(widget, dispose);
        return this;
    }

    /// <summary>
    /// Changes the title displayed by the owning tab control.
    /// </summary>
    /// <param name="title">The new non-empty title.</param>
    /// <returns>The current page.</returns>
    public TabPageWidget SetTitle(string title)
    {
        string validatedTitle = ValidateTitle(title);
        if (string.Equals(_title, validatedTitle, StringComparison.Ordinal))
            return this;

        _title = validatedTitle;
        TitleChanged?.Invoke(this);
        return this;
    }

    /// <summary>
    /// Sets the page background fill color.
    /// </summary>
    /// <param name="color">The new background color.</param>
    /// <returns>The current page.</returns>
    public TabPageWidget SetBackgroundColor(Color color)
    {
        Background.SetColor(color);
        RefreshBackground();
        return this;
    }

    /// <summary>
    /// Sets the page border color.
    /// </summary>
    /// <param name="color">The new border color.</param>
    /// <returns>The current page.</returns>
    public TabPageWidget SetBorderColor(Color color)
    {
        Background.SetBorderColor(color);
        RefreshBackground();
        return this;
    }

    /// <summary>
    /// Sets the page border width.
    /// </summary>
    /// <param name="width">The non-negative border width.</param>
    /// <returns>The current page.</returns>
    public TabPageWidget SetStrokeWidth(float width)
    {
        if (width < 0f)
            throw new ArgumentOutOfRangeException(nameof(width), "The page border width cannot be negative.");

        Background.SetStrokeWidth(width);
        RefreshBackground();
        return this;
    }

    /// <summary>
    /// Sets the page corner radius.
    /// </summary>
    /// <param name="radius">The non-negative corner radius.</param>
    /// <returns>The current page.</returns>
    public TabPageWidget SetCornerRadius(float radius)
    {
        if (radius < 0f)
            throw new ArgumentOutOfRangeException(nameof(radius), "The page corner radius cannot be negative.");

        Background.SetCornerRadius(radius);
        RefreshBackground();
        return this;
    }

    /// <summary>
    /// Uses a bitmap as the page background.
    /// </summary>
    /// <param name="bitmap">The source bitmap.</param>
    /// <param name="mode">How the bitmap fills the page.</param>
    /// <param name="scale">The bitmap scale.</param>
    /// <param name="offsetPx">The optional bitmap offset.</param>
    /// <param name="filterQuality">The sampling quality.</param>
    /// <returns>The current page.</returns>
    public TabPageWidget SetBackgroundImage(SKBitmap bitmap,
                                            DirectRectangle.ImageFillMode mode = DirectRectangle.ImageFillMode.Stretch,
                                            float scale = 1f,
                                            SKPoint? offsetPx = null,
                                            SKFilterQuality filterQuality = SKFilterQuality.Medium)
    {
        Background.SetFillImage(bitmap, mode, scale, offsetPx, filterQuality);
        return this;
    }

    /// <summary>
    /// Uses an image as the page background.
    /// </summary>
    /// <param name="image">The source image.</param>
    /// <param name="mode">How the image fills the page.</param>
    /// <param name="scale">The image scale.</param>
    /// <param name="offsetPx">The optional image offset.</param>
    /// <param name="filterQuality">The sampling quality.</param>
    /// <returns>The current page.</returns>
    public TabPageWidget SetBackgroundImage(SKImage image,
                                            DirectRectangle.ImageFillMode mode = DirectRectangle.ImageFillMode.Stretch,
                                            float scale = 1f,
                                            SKPoint? offsetPx = null,
                                            SKFilterQuality filterQuality = SKFilterQuality.Medium)
    {
        Background.SetFillImage(image, mode, scale, offsetPx, filterQuality);
        return this;
    }

    /// <summary>
    /// Removes an image background and restores the configured color fill.
    /// </summary>
    /// <returns>The current page.</returns>
    public TabPageWidget ClearBackgroundImage()
    {
        Background.ClearFillImage();
        return this;
    }

    /// <summary>
    /// Occurs after <see cref="Title"/> changes.
    /// </summary>
    internal event Action<TabPageWidget>? TitleChanged;

    /// <summary>
    /// Updates the page content size while preserving its current anchor.
    /// </summary>
    /// <param name="bounds">The new content bounds.</param>
    internal void SetContentBounds(Rectangle bounds)
    {
        ValidateBounds(bounds);

        if (Mode == DirectDrawingMode.View)
            Background.ScreenBounds = new Rectangle(Background.ScreenBounds.Location, bounds.Size);
        else
            Background.WorldBounds = new Rectangle(Background.WorldBounds.Location, bounds.Size);
    }

    /// <summary>
    /// Sets the base Z-order for the page background and child widgets.
    /// </summary>
    /// <param name="zOrder">The Z-order applied to the background.</param>
    internal void SetPageZOrder(int zOrder)
    {
        _pageZOrder = zOrder;
        Background.ZOrder = zOrder;

        foreach (WidgetBase widget in ChildWidgets)
            SetWidgetZOrder(widget, zOrder + 1);
    }

    private const int DefaultBackgroundAlpha = 235;

    private static readonly Color DefaultBackgroundColor = Color.FromArgb(DefaultBackgroundAlpha, 25, 28, 36);
    private static readonly Color DefaultBorderColor = Color.FromArgb(255, 110, 120, 140);

    private void DisablePageInput()
    {
        IsInputEnabled = false;
        IsPointerInputEnabled = false;
        IsKeyboardInputEnabled = false;
        CanReceiveFocus = false;
    }

    private void RefreshBackground()
    {
        Background.SetPosition(Background.GetPosition());
    }

    private static Rectangle ValidateBounds(Rectangle bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bounds),
                bounds,
                "Tab-page bounds must have positive width and height.");
        }

        return bounds;
    }

    private static string ValidateTitle(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        return title;
    }

    private static void SetWidgetZOrder(WidgetBase widget,
                                        int zOrder)
    {
        DirectDrawingBase[] visuals = EnumerateVisuals(widget).ToArray();
        if (visuals.Length == 0)
            return;

        int currentMinimum = visuals.Min(static visual => visual.ZOrder);
        int delta = zOrder - currentMinimum;

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
