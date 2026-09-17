using System.Drawing;
using System.Numerics;
using Gondwana.Drawing.Direct;
using Gondwana.Input.Keyboard;
using Gondwana.Rendering;
using Gondwana.Rendering.Views;
using SkiaSharp;

namespace Gondwana.Widgets.Menus;

/// <summary>A command, check/radio item, or submenu owner in a menu dropdown.</summary>
public sealed class MenuItemWidget : WidgetBase
{
    private readonly MenuBarTheme _theme;
    private readonly MenuDropDownWidget _owner;
    private readonly Action? _action;
    private readonly Action<bool>? _checkedAction;
    private readonly MenuIndicatorDrawing _marker;
    private readonly MenuIndicatorDrawing _arrow;
    private bool _disposed;
    private bool _isPointerOver;
    private bool _isSelected;
    private bool _isEnabled = true;

    internal event Action<MenuItemWidget>? Hovered;
    /// <summary>Occurs after state changes and hierarchy closure, before the command callback.</summary>
    public event Action<MenuItemWidget>? Invoked;

    internal MenuItemWidget(RenderSurfaceHostBase host, View view, Rectangle bounds,
        string text, string? shortcutText, Action? action, MenuBarTheme theme,
        string nickname, MenuDropDownWidget owner, string? key, KeyGesture? shortcut,
        char? mnemonic, SKImage? icon, bool checkable, string? radioGroup,
        Action<bool>? checkedAction, MenuDropDownWidget? subMenu)
        : base(host, DirectDrawingMode.View, bounds.Location, nickname)
    {
        _theme = theme;
        _owner = owner;
        _action = action;
        _checkedAction = checkedAction;
        Text = text;
        Key = key;
        Shortcut = shortcut;
        ShortcutText = shortcut?.ToString() ?? shortcutText ?? string.Empty;
        Mnemonic = mnemonic;
        Icon = icon;
        IsCheckable = checkable;
        RadioGroup = radioGroup;
        SubMenu = subMenu;
        Background = new DirectRectangle(theme.ItemNormalColor, host, view, bounds, $"{Nickname}.background")
            .SetFilled(true).SetStrokeWidth(0f);
        Label = CreateText(host, view, bounds, "label", SKTextAlign.Left).SetText(Text);
        ShortcutLabel = CreateText(host, view, bounds, "shortcut", SKTextAlign.Right).SetText(ShortcutText);
        _marker = new MenuIndicatorDrawing(host, view, bounds,
            radioGroup is null ? MenuIndicatorDrawing.Shape.Check : MenuIndicatorDrawing.Shape.Radio,
            theme, $"{Nickname}.marker");
        _arrow = new MenuIndicatorDrawing(host, view, bounds, MenuIndicatorDrawing.Shape.Arrow,
            theme, $"{Nickname}.arrow");
        Add(Background);
        Add(Label);
        Add(ShortcutLabel);
        Add(Marker);
        Add(Arrow);
        if (icon is not null)
        {
            IconDrawing = new DirectImage(icon, host, view, bounds, $"{Nickname}.icon")
                .SetScaleMode(DirectImage.ScaleMode.Fit);
            Add(IconDrawing);
        }
        CanReceiveFocus = true;
        IsKeyboardInputEnabled = true;
        IsPointerInputEnabled = true;
        UpdateVisualState();
    }

    /// <summary>Gets the immutable optional key, unique within the menu bar.</summary>
    public string? Key { get; }
    /// <summary>Gets the command label.</summary>
    public string Text { get; }
    /// <summary>Gets the executable gesture, if any.</summary>
    public KeyGesture? Shortcut { get; }
    /// <summary>Gets gesture-generated text, or the legacy display-only shortcut text.</summary>
    public string ShortcutText { get; }
    /// <summary>Gets the explicit, case-insensitive access character.</summary>
    public char? Mnemonic { get; }
    /// <summary>Gets the caller-owned image. Keep it alive until the item is disposed.</summary>
    public SKImage? Icon { get; }
    /// <summary>Gets the retained icon drawing, if configured.</summary>
    public DirectImage? IconDrawing { get; }
    /// <summary>Gets whether this item has check or radio state.</summary>
    public bool IsCheckable { get; }
    /// <summary>Gets the current checked state.</summary>
    public bool IsChecked { get; private set; }
    /// <summary>Gets the radio group, scoped to the containing dropdown.</summary>
    public string? RadioGroup { get; }
    /// <summary>Gets the child dropdown, if this item owns a submenu.</summary>
    public MenuDropDownWidget? SubMenu { get; }
    /// <summary>Gets the background drawing.</summary>
    public DirectRectangle Background { get; }
    /// <summary>Gets the label drawing.</summary>
    public TextBlock Label { get; }
    /// <summary>Gets the shortcut drawing.</summary>
    public TextBlock ShortcutLabel { get; }
    /// <summary>Gets the check/radio indicator drawing.</summary>
    public DirectDrawingMovableBase Marker => _marker;
    /// <summary>Gets the submenu arrow drawing.</summary>
    public DirectDrawingMovableBase Arrow => _arrow;
    /// <summary>Gets whether this item is enabled.</summary>
    public bool IsEnabled => _isEnabled;
    /// <summary>Gets whether this item is selected by pointer or keyboard.</summary>
    public bool IsSelected => _isSelected;
    internal bool CanInvoke => !_disposed && _isEnabled && _owner.CanInvoke;

    /// <summary>Enables or disables the item.</summary>
    public MenuItemWidget SetEnabled(bool enabled)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _isEnabled = enabled;
        ApplyInputState(_owner.IsOpen);
        if (!enabled) _owner.OnItemDisabled(this);
        UpdateVisualState();
        return this;
    }

    /// <summary>Changes state without invoking callbacks. Checking a radio item clears its group peers.</summary>
    public MenuItemWidget SetChecked(bool isChecked)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsCheckable) throw new InvalidOperationException("This menu item is not checkable.");
        if (isChecked && RadioGroup is not null) _owner.UncheckRadioPeers(this);
        IsChecked = isChecked;
        UpdateVisualState();
        return this;
    }

    /// <summary>Invokes an enabled command, even while its dropdown is closed, or opens its submenu.</summary>
    /// <remarks>The bar and ancestor items must be available. State changes and menu closure precede callbacks.</remarks>
    public void PerformClick()
    {
        if (!CanInvoke) return;
        if (SubMenu is not null)
        {
            _owner.OpenSubMenu(this, selectFirst: true);
            return;
        }
        if (IsCheckable) SetChecked(RadioGroup is not null || !IsChecked);
        bool checkedState = IsChecked;
        _owner.NotifyInvoked(this);
        Invoked?.Invoke(this);
        _checkedAction?.Invoke(checkedState);
        _action?.Invoke();
    }

    internal void SetSelected(bool selected)
    {
        _isSelected = selected;
        if (!selected) _isPointerOver = false;
        UpdateVisualState();
    }

    internal void ApplyInputState(bool menuAcceptsInput)
    {
        bool enabled = menuAcceptsInput && _isEnabled && !_disposed;
        IsInputEnabled = enabled;
        IsPointerInputEnabled = enabled;
        IsKeyboardInputEnabled = enabled;
        CanReceiveFocus = enabled;
    }

    internal void SetItemZOrder(int zOrder)
    {
        Background.ZOrder = zOrder;
        foreach (var drawing in new DirectDrawingBase[] { Label, ShortcutLabel, Marker, Arrow })
            drawing.ZOrder = zOrder + 1;
        if (IconDrawing is not null) IconDrawing.ZOrder = zOrder + 1;
    }

    internal void SetBounds(Rectangle bounds, int markerWidth, int iconWidth, int shortcutWidth, int arrowWidth)
    {
        SetPosition(bounds.X, bounds.Y);
        Background.ScreenBounds = bounds;
        int x = bounds.X + _theme.ItemHorizontalPadding;
        Place(Marker, new Rectangle(x, bounds.Y, Math.Max(1, markerWidth), bounds.Height));
        x += markerWidth;
        if (IconDrawing is not null)
            Place(IconDrawing, new Rectangle(x, bounds.Y + (bounds.Height - _theme.IconSize) / 2, _theme.IconSize, _theme.IconSize));
        x += iconWidth;
        int right = bounds.Right - _theme.ItemHorizontalPadding;
        Place(Arrow, new Rectangle(right - arrowWidth, bounds.Y, Math.Max(1, arrowWidth), bounds.Height));
        right -= arrowWidth;
        Place(ShortcutLabel, new Rectangle(right - shortcutWidth, bounds.Y, Math.Max(1, shortcutWidth), bounds.Height));
        if (shortcutWidth > 0) right -= shortcutWidth + _theme.ShortcutGap;
        Place(Label, new Rectangle(x, bounds.Y, Math.Max(1, right - x), bounds.Height));
        Label.HorizontalPadding = ShortcutLabel.HorizontalPadding = 0;
    }

    private void Place(DirectDrawingBase drawing, Rectangle bounds)
    {
        SetLocalOffset((IDirectCompositeChild)drawing, new Vector2(bounds.X - GetPosition().X, bounds.Y - GetPosition().Y));
        drawing.ScreenBounds = bounds;
    }

    private TextBlock CreateText(RenderSurfaceHostBase host, View view, Rectangle bounds, string name, SKTextAlign alignment) =>
        new TextBlock(host, view, bounds, $"{Nickname}.{name}")
            .SetFont(SKTypeface.Default, _theme.FontSize, _theme.MinimumFontSize)
            .SetAlignment(alignment, TextBlock.VerticalAlign.Center).EnableWrapping(false);

    /// <inheritdoc/>
    public override void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _owner.UnregisterItem(this);
        Hovered = null;
        Invoked = null;
        base.Dispose();
    }

    /// <inheritdoc/>
    protected override void OnPointerEnter(WidgetPointerEventArgs args)
    {
        base.OnPointerEnter(args);
        if (!_isEnabled) return;
        _isPointerOver = true;
        Hovered?.Invoke(this);
        UpdateVisualState();
    }

    /// <inheritdoc/>
    protected override void OnPointerLeave(WidgetPointerEventArgs args)
    {
        base.OnPointerLeave(args);
        _isPointerOver = false;
        UpdateVisualState();
    }

    /// <inheritdoc/>
    protected override void OnPointerDown(WidgetPointerEventArgs args)
    {
        base.OnPointerDown(args);
        if (_isEnabled && args.IsPrimaryButton) SetBackgroundColor(_theme.ItemPressedColor);
    }

    /// <inheritdoc/>
    protected override void OnPointerUp(WidgetPointerEventArgs args)
    {
        base.OnPointerUp(args);
        UpdateVisualState();
    }

    /// <inheritdoc/>
    protected override void OnPointerClick(WidgetPointerEventArgs args)
    {
        base.OnPointerClick(args);
        if (!_isEnabled || !args.IsPrimaryButton) return;
        args.Handled = true;
        PerformClick();
    }

    /// <inheritdoc/>
    protected override void OnKeyboardInput(WidgetKeyboardEventArgs args)
    {
        base.OnKeyboardInput(args);
        // Open-menu navigation bubbles to the bar, which knows the active depth.
        if (_owner.IsOpen || args.Handled || args.KeyAction != KeyAction.Pressed || args.Key is not 13 and not 32) return;
        if (!CanInvoke) return;
        args.Handled = true;
        PerformClick();
    }

    private void UpdateVisualState()
    {
        SetBackgroundColor(_isEnabled && (_isPointerOver || _isSelected) ? _theme.ItemHoverColor : _theme.ItemNormalColor);
        Label.SetColors(ToSkColor(_isEnabled ? _theme.TextColor : _theme.DisabledTextColor), SKColors.Transparent);
        ShortcutLabel.SetColors(ToSkColor(_isEnabled ? _theme.ShortcutTextColor : _theme.DisabledTextColor), SKColors.Transparent);
        var indicator = ToSkColor(_isEnabled ? _theme.IndicatorColor : _theme.DisabledTextColor);
        _marker.SetState(IsChecked, indicator);
        _arrow.SetState(SubMenu is not null, indicator);
        IconDrawing?.SetTint(_isEnabled ? SKColors.White : ToSkColor(_theme.DisabledTextColor));
    }

    private void SetBackgroundColor(Color color)
    {
        Background.SetColor(color);
        Background.SetPosition(Background.GetPosition());
    }

    private static SKColor ToSkColor(Color color) => new(color.R, color.G, color.B, color.A);
}
