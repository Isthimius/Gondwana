using System.Drawing;
using SkiaSharp;
using Gondwana.Drawing.Direct;
using Gondwana.Input.Keyboard;
using Gondwana.Rendering;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;

namespace Gondwana.Widgets.Controls;

/// <summary>
/// Represents a basic text button composed from a rectangle and text block.
/// </summary>
public sealed class ButtonWidget : WidgetBase
{
    private Color _normalColor = Color.FromArgb(255, 60, 60, 68);
    private Color _hoverColor = Color.FromArgb(255, 78, 78, 88);
    private Color _pressedColor = Color.FromArgb(255, 42, 42, 48);

    /// <summary>
    /// Occurs when the button is activated by pointer or keyboard.
    /// </summary>
    public event Action? Clicked;

    #region constructors

    /// <summary>
    /// Initializes a view-level button.
    /// </summary>
    public ButtonWidget(RenderSurfaceHostBase renderSurfaceHost,
                        View view,
                        Rectangle bounds,
                        string text,
                        string? nickname = null)
        : base(renderSurfaceHost, DirectDrawingMode.View, bounds.Location, nickname)
    {
        Background = CreateBackground(renderSurfaceHost, view, bounds);
        Label = CreateLabel(renderSurfaceHost, view, bounds, text);

        CompleteInitialization();
    }

    /// <summary>
    /// Initializes a scene-layer button.
    /// </summary>
    public ButtonWidget(RenderSurfaceHostBase renderSurfaceHost,
                        SceneLayer sceneLayer,
                        Rectangle bounds,
                        string text,
                        string? nickname = null)
        : base(renderSurfaceHost, DirectDrawingMode.SceneLayer, bounds.Location, nickname)
    {
        Background = CreateBackground(renderSurfaceHost, sceneLayer, bounds);
        Label = CreateLabel(renderSurfaceHost, sceneLayer, bounds, text);

        CompleteInitialization();
    }

    #endregion constructors

    #region public properties

    /// <summary>
    /// Gets the rectangle used as the button background and border.
    /// </summary>
    public DirectRectangle Background { get; }

    /// <summary>
    /// Gets the text block used as the button label.
    /// </summary>
    public TextBlock Label { get; }

    #endregion public properties

    #region public methods

    /// <summary>
    /// Sets the button text.
    /// </summary>
    public ButtonWidget SetText(string text)
    {
        Label.SetText(text);
        return this;
    }

    /// <summary>
    /// Sets the normal, hover, and pressed background colors.
    /// </summary>
    public ButtonWidget SetBackgroundColors(Color normal,
                                            Color hover,
                                            Color pressed)
    {
        _normalColor = normal;
        _hoverColor = hover;
        _pressedColor = pressed;

        UpdateBackgroundColor(normal);

        return this;
    }

    /// <summary>
    /// Sets the button text color.
    /// </summary>
    public ButtonWidget SetTextColor(Color color)
    {
        Label.SetColors(color, Color.Transparent);

        return this;
    }

    /// <summary>
    /// Sets the background Z-order and places the label immediately above it.
    /// </summary>
    public ButtonWidget SetButtonZOrder(int zOrder)
    {
        Background.ZOrder = zOrder;
        Label.ZOrder = zOrder + 1;

        return this;
    }

    /// <summary>
    /// Programmatically activates the button.
    /// </summary>
    public void PerformClick()
    {
        if (!IsInputEnabled)
            return;

        Clicked?.Invoke();
    }

    #endregion public methods
    
    #region protected methods

    /// <inheritdoc/>
    protected override void OnPointerEnter(WidgetPointerEventArgs args)
    {
        base.OnPointerEnter(args);
        UpdateBackgroundColor(_hoverColor);
    }

    /// <inheritdoc/>
    protected override void OnPointerLeave(WidgetPointerEventArgs args)
    {
        base.OnPointerLeave(args);
        UpdateBackgroundColor(_normalColor);
    }

    /// <inheritdoc/>
    protected override void OnPointerDown(WidgetPointerEventArgs args)
    {
        base.OnPointerDown(args);

        if (args.IsPrimaryButton)
            UpdateBackgroundColor(_pressedColor);
    }

    /// <inheritdoc/>
    protected override void OnPointerUp(WidgetPointerEventArgs args)
    {
        base.OnPointerUp(args);
        UpdateBackgroundColor(_hoverColor);
    }

    /// <inheritdoc/>
    protected override void OnPointerClick(WidgetPointerEventArgs args)
    {
        base.OnPointerClick(args);

        if (!args.IsPrimaryButton)
            return;

        args.Handled = true;
        PerformClick();
    }

    /// <inheritdoc/>
    protected override void OnKeyboardInput(WidgetKeyboardEventArgs args)
    {
        base.OnKeyboardInput(args);

        if (args.KeyAction != KeyAction.Pressed)
        {
            return;
        }

        // Standard Enter and Space virtual-key values.
        if (args.Key is not 13 and not 32)
            return;

        args.Handled = true;
        PerformClick();
    }

    #endregion protected methods

    #region private methods

    private void UpdateBackgroundColor(Color color)
    {
        Background.SetColor(color);

        // DirectRectangle's current color setter rebuilds its paint but does not
        // enqueue a dirty region by itself. Reapplying the existing position
        // performs the required old/new refresh without moving the button.
        Background.SetPosition(Background.GetPosition());
    }

    private void CompleteInitialization()
    {
        Add(Background);
        Add(Label);

        SetButtonZOrder(0);

        CanReceiveFocus = true;
        IsKeyboardInputEnabled = true;
    }

    private DirectRectangle CreateBackground(RenderSurfaceHostBase host, View view, Rectangle bounds)
    {
        return new DirectRectangle(_normalColor,
                                   host,
                                   view,
                                   bounds,
                                   $"{Nickname}.background").SetFilled(true)
                                                            .SetBorderColor(Color.FromArgb(255, 150, 150, 160))
                                                            .SetStrokeWidth(1.5f)
                                                            .SetCornerRadius(5f);
    }

    private DirectRectangle CreateBackground(RenderSurfaceHostBase host, SceneLayer layer, Rectangle bounds)
    {
        return new DirectRectangle(_normalColor,
                                   host,
                                   layer,
                                   bounds,
                                   $"{Nickname}.background").SetFilled(true)
                                                            .SetBorderColor(Color.FromArgb(255, 150, 150, 160))
                                                            .SetStrokeWidth(1.5f)
                                                            .SetCornerRadius(5f);
    }

    private static TextBlock CreateLabel(RenderSurfaceHostBase host, View view, Rectangle bounds, string text)
    {
        return new TextBlock(host, view, bounds).SetText(text)
                                                .SetFont(SKTypeface.Default, 16f, minSize: 10f)
                                                .SetColors(SKColors.White, SKColors.Transparent)
                                                .SetAlignment(SKTextAlign.Center, TextBlock.VerticalAlign.Center)
                                                .EnableWrapping(false);
    }

    private static TextBlock CreateLabel(RenderSurfaceHostBase host, SceneLayer layer, Rectangle bounds, string text)
    {
        return new TextBlock(host,
                             layer,
                             view: null,
                             worldBounds: bounds).SetText(text)
                                                 .SetFont(SKTypeface.Default, 16f, minSize: 10f)
                                                 .SetColors(SKColors.White, SKColors.Transparent)
                                                 .SetAlignment(SKTextAlign.Center, TextBlock.VerticalAlign.Center)
                                                 .EnableWrapping(false);
    }

    #endregion private methods
}
