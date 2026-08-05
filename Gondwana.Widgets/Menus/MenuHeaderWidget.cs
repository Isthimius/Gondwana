using System.Drawing;
using Gondwana.Drawing.Direct;
using Gondwana.Input.Keyboard;
using Gondwana.Rendering;
using Gondwana.Rendering.Views;
using SkiaSharp;

namespace Gondwana.Widgets.Menus;

/// <summary>
/// Represents a top-level menu header such as File, Edit, or Help.
/// </summary>
public sealed class MenuHeaderWidget : WidgetBase
{
    private readonly MenuBarTheme _theme;
    private bool _isPointerOver;
    private bool _isOpen;
    private bool _isMenuBarActive;

    internal event Action<MenuHeaderWidget>? Invoked;
    internal event Action<MenuHeaderWidget>? Hovered;

    internal MenuHeaderWidget(RenderSurfaceHostBase host,
                              View view,
                              Rectangle bounds,
                              string text,
                              MenuBarTheme theme,
                              string? nickname = null)
        : base(host, DirectDrawingMode.View, bounds.Location, nickname)
    {
        _theme = theme ?? throw new ArgumentNullException(nameof(theme));
        Text = text ?? throw new ArgumentNullException(nameof(text));

        Background = new DirectRectangle(
                _theme.HeaderNormalColor,
                host,
                view,
                bounds,
                $"{Nickname}.background")
            .SetFilled(true)
            .SetStrokeWidth(0f);

        Label = new TextBlock(
                host,
                view,
                bounds,
                $"{Nickname}.label")
            .SetText(Text)
            .SetFont(SKTypeface.Default, _theme.FontSize, _theme.MinimumFontSize)
            .SetColors(ToSkColor(_theme.TextColor), SKColors.Transparent)
            .SetAlignment(SKTextAlign.Center, TextBlock.VerticalAlign.Center)
            .EnableWrapping(false);

        Add(Background);
        Add(Label);

        CanReceiveFocus = true;
        IsKeyboardInputEnabled = true;
        IsPointerInputEnabled = true;
    }

    /// <summary>Gets the displayed header text.</summary>
    public string Text { get; private set; }

    /// <summary>Gets the header background drawing.</summary>
    public DirectRectangle Background { get; }

    /// <summary>Gets the header label drawing.</summary>
    public TextBlock Label { get; }

    /// <summary>Gets whether this header currently owns the open dropdown.</summary>
    public bool IsOpen => _isOpen;

    /// <summary>Changes the displayed header text.</summary>
    public MenuHeaderWidget SetText(string text)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));
        Label.SetText(Text);
        return this;
    }

    internal void SetOpen(bool isOpen)
    {
        _isOpen = isOpen;
        UpdateVisualState();
    }

    internal void SetMenuBarActive(bool isActive)
    {
        _isMenuBarActive = isActive;
    }

    internal void SetHeaderZOrder(int zOrder)
    {
        Background.ZOrder = zOrder;
        Label.ZOrder = zOrder + 1;
    }

    internal void SetBounds(Rectangle bounds)
    {
        SetPosition(bounds.X, bounds.Y);

        Background.ScreenBounds = new Rectangle(
            Background.ScreenBounds.Location,
            bounds.Size);

        Label.ScreenBounds = new Rectangle(
            Label.ScreenBounds.Location,
            bounds.Size);
    }

    protected override void OnPointerEnter(WidgetPointerEventArgs args)
    {
        base.OnPointerEnter(args);
        _isPointerOver = true;
        UpdateVisualState();
        Hovered?.Invoke(this);
    }

    protected override void OnPointerLeave(WidgetPointerEventArgs args)
    {
        base.OnPointerLeave(args);
        _isPointerOver = false;
        UpdateVisualState();
    }

    protected override void OnPointerDown(WidgetPointerEventArgs args)
    {
        base.OnPointerDown(args);

        if (args.IsPrimaryButton)
            SetBackgroundColor(_theme.HeaderPressedColor);
    }

    protected override void OnPointerUp(WidgetPointerEventArgs args)
    {
        base.OnPointerUp(args);
        UpdateVisualState();
    }

    protected override void OnPointerClick(WidgetPointerEventArgs args)
    {
        base.OnPointerClick(args);

        if (!args.IsPrimaryButton)
            return;

        args.Handled = true;
        Invoked?.Invoke(this);
    }

    protected override void OnKeyboardInput(WidgetKeyboardEventArgs args)
    {
        base.OnKeyboardInput(args);

        if (args.KeyAction != KeyAction.Pressed)
            return;

        // Once any dropdown is open, leave menu-navigation keys unhandled so
        // they bubble through ContainerWidget to MenuBarWidget.
        if (_isMenuBarActive)
            return;

        // Enter, Space, or Down Arrow opens a closed menu header.
        if (args.Key is not 13 and not 32 and not 40)
            return;

        args.Handled = true;
        Invoked?.Invoke(this);
    }

    private void UpdateVisualState()
    {
        Color color = _isOpen
            ? _theme.HeaderOpenColor
            : _isPointerOver
                ? _theme.HeaderHoverColor
                : _theme.HeaderNormalColor;

        SetBackgroundColor(color);
    }

    private void SetBackgroundColor(Color color)
    {
        Background.SetColor(color);
        Background.SetPosition(Background.GetPosition());
    }

    private static SKColor ToSkColor(Color color)
    {
        return new SKColor(color.R, color.G, color.B, color.A);
    }
}
