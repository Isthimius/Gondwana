using System.Drawing;
using System.Numerics;
using Gondwana.Drawing.Direct;
using Gondwana.Input.Keyboard;
using Gondwana.Rendering;
using Gondwana.Rendering.Views;

namespace Gondwana.Widgets.Menus;

/// <summary>
/// Provides a view-level menu bar with animated dropdown command lists.
/// </summary>
public sealed class MenuBarWidget : ContainerWidget, IWidgetKeyboardFallback
{
    private readonly View _view;
    private readonly Rectangle _bounds;
    private readonly MenuBarTheme _theme;
    private readonly DismissLayerWidget _dismissLayer;
    private readonly List<MenuBarMenu> _menus = new();

    private readonly Dictionary<string, MenuItemWidget> _itemsByKey = new(StringComparer.Ordinal);
    private readonly Dictionary<KeyGesture, MenuItemWidget> _shortcuts = new();
    internal bool IsAvailable => !_disposed && Visible && IsInputEnabled && IsKeyboardInputEnabled;

    /// <summary>Gets an item by its stable, case-sensitive key, including nested descendants.</summary>
    public MenuItemWidget GetItem(string key) => _itemsByKey[key];
    /// <summary>Gets an item by its stable key, or throws KeyNotFoundException.</summary>
    public MenuItemWidget this[string key] => GetItem(key);
    /// <summary>Looks up a stable key; returns false and null when absent.</summary>
    public bool TryGetItem(string key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out MenuItemWidget? item) =>
        _itemsByKey.TryGetValue(key, out item);

    internal void ValidateRegistration(string? key, KeyGesture? shortcut)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (key is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            if (_itemsByKey.ContainsKey(key)) throw new ArgumentException($"Duplicate menu item key '{key}'.", nameof(key));
        }
        if (shortcut is { } gesture)
        {
            if (gesture.Key <= 0) throw new ArgumentException("A shortcut requires a nonzero key.", nameof(shortcut));
            if (_shortcuts.ContainsKey(gesture)) throw new ArgumentException($"Duplicate menu shortcut '{gesture}'.", nameof(shortcut));
        }
    }

    internal void RegisterItem(MenuItemWidget item)
    {
        if (item.Key is not null) _itemsByKey.Add(item.Key, item);
        if (item.Shortcut is { } gesture) _shortcuts.Add(gesture, item);
    }

    internal void UnregisterItem(MenuItemWidget item)
    {
        if (item.Key is not null && _itemsByKey.GetValueOrDefault(item.Key) == item) _itemsByKey.Remove(item.Key);
        if (item.Shortcut is { } gesture && _shortcuts.GetValueOrDefault(gesture) == item) _shortcuts.Remove(gesture);
    }

    private WidgetBase? _previousFocus;
    private int _openMenuIndex = -1;
    private int _nextHeaderX;
    private int _menuZOrder = 20_000;
    private bool _disposed;

    /// <summary>Occurs after a top-level menu opens.</summary>
    public event Action<MenuBarMenu>? MenuOpened;

    /// <summary>Occurs after a top-level menu begins closing.</summary>
    public event Action<MenuBarMenu>? MenuClosed;

    /// <summary>Occurs when a command item is invoked.</summary>
    public event Action<MenuItemWidget>? ItemInvoked;

    /// <summary>
    /// Creates a menu bar attached to a view.
    /// </summary>
    public MenuBarWidget(RenderSurfaceHostBase host,
                         View view,
                         Rectangle bounds,
                         MenuBarTheme? theme = null,
                         string? nickname = null)
        : base(host, DirectDrawingMode.View, bounds.Location, nickname)
    {
        ArgumentNullException.ThrowIfNull(view);

        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bounds),
                bounds,
                "Menu bar bounds must have positive width and height.");
        }

        _view = view;
        _bounds = bounds;
        _theme = theme ?? MenuBarTheme.Default;
        _nextHeaderX = bounds.X;

        BarBackground = new DirectRectangle(
                _theme.BarBackgroundColor,
                host,
                view,
                bounds,
                $"{Nickname}.background")
            .SetFilled(true)
            .SetBorderColor(_theme.BarBorderColor)
            .SetStrokeWidth(_theme.BorderWidth);

        Rectangle dismissBounds = view.Viewport.TargetRectPx;
        _dismissLayer = new DismissLayerWidget(
            host,
            view,
            dismissBounds,
            $"{Nickname}.dismiss");

        _dismissLayer.DismissRequested += OnDismissRequested;
        _dismissLayer.SetDismissEnabled(false);

        Add(
            _dismissLayer,
            new Vector2(
                dismissBounds.X - bounds.X,
                dismissBounds.Y - bounds.Y));

        Add(BarBackground);

        CanReceiveFocus = false;
        IsKeyboardInputEnabled = true;
        IsPointerInputEnabled = true;

        SetMenuZOrder(_menuZOrder);
    }

    /// <summary>Gets the menu bar background drawing.</summary>
    public DirectRectangle BarBackground { get; }

    /// <summary>Gets the configured top-level menus.</summary>
    public IReadOnlyList<MenuBarMenu> Menus => _menus.AsReadOnly();

    /// <summary>Gets the zero-based open-menu index, or -1.</summary>
    public int OpenMenuIndex => _openMenuIndex;

    /// <summary>Gets the currently open menu, or null.</summary>
    public MenuBarMenu? OpenMenu =>
        _openMenuIndex >= 0 && _openMenuIndex < _menus.Count
            ? _menus[_openMenuIndex]
            : null;

    /// <summary>Gets or sets the dropdown animation.</summary>
    public MenuDropDownAnimation DropDownAnimation { get; set; } =
        MenuDropDownAnimation.FadeAndReveal;

    /// <summary>Gets or sets the dropdown animation duration, in seconds.</summary>
    public float DropDownAnimationDurationSec { get; set; } = 0.13f;

    /// <summary>
    /// Adds a top-level menu and optionally configures its dropdown.
    /// </summary>
    public MenuBarWidget AddMenu(string text,
                                 Action<MenuDropDownWidget>? configure = null,
                                 int? headerWidth = null,
                                 char? mnemonic = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(text);
        if (mnemonic is char character && _menus.Any(menu => menu.Header.Mnemonic is char existing &&
            char.ToUpperInvariant(existing) == char.ToUpperInvariant(character)))
            throw new ArgumentException($"Duplicate top-level mnemonic '{character}'.", nameof(mnemonic));

        int resolvedHeaderWidth = headerWidth ?? EstimateHeaderWidth(text);

        if (resolvedHeaderWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(headerWidth),
                resolvedHeaderWidth,
                "Menu header width must be positive.");
        }

        var headerBounds = new Rectangle(
            _nextHeaderX,
            _bounds.Y,
            resolvedHeaderWidth,
            _bounds.Height);

        int menuIndex = _menus.Count;

        var header = new MenuHeaderWidget(
            RenderSurfaceHost,
            _view,
            headerBounds,
            text,
            _theme,
            $"{Nickname}.header.{menuIndex}", mnemonic);

        var dropDown = new MenuDropDownWidget(
            RenderSurfaceHost,
            _view,
            new Point(headerBounds.X, _bounds.Bottom),
            _theme,
            $"{Nickname}.dropdown.{menuIndex}", this);

        try
        {
            configure?.Invoke(dropDown);
        }
        catch
        {
            dropDown.Dispose();
            header.Dispose();
            throw;
        }
        PositionDropDown(headerBounds, dropDown);

        var menu = new MenuBarMenu(header, dropDown);
        _menus.Add(menu);

        header.Invoked += OnHeaderInvoked;
        header.Hovered += OnHeaderHovered;

        Add(
            header,
            new Vector2(
                headerBounds.X - _bounds.X,
                headerBounds.Y - _bounds.Y));

        Add(
            dropDown,
            new Vector2(
                dropDown.GetPosition().X - _bounds.X,
                dropDown.GetPosition().Y - _bounds.Y));

        // Dropdown drawings are visible immediately after construction; menus start closed.
        dropDown.CloseAnimated(MenuDropDownAnimation.None, 0f, immediate: true);

        _nextHeaderX += resolvedHeaderWidth;
        ApplyMenuZOrder(_menuZOrder);

        return this;
    }

    /// <summary>Opens the specified top-level menu.</summary>
    public void OpenMenuAt(int index)
    {
        if (index < 0 || index >= _menus.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        if (!IsAvailable || _openMenuIndex == index)
            return;

        CloseMenuInternal(immediate: true);

        _previousFocus = WidgetInputRouterRegistry.GetFocusedWidget(this);
        _openMenuIndex = index;
        MenuBarMenu menu = _menus[index];

        menu.Header.SetOpen(true);

        foreach (MenuBarMenu candidate in _menus)
            candidate.Header.SetMenuBarActive(true);

        _dismissLayer.SetDismissEnabled(true);

        // Input routing is registration ordered rather than drawing-Z ordered.
        _dismissLayer.Activate();

        foreach (MenuBarMenu candidate in _menus)
            candidate.Header.Activate();

        PositionDropDown(menu.Header.Background.ScreenBounds, menu.DropDown);
        WidgetInputRouterRegistry.TryFocus(menu.Header);

        menu.DropDown.OpenAnimated(
            DropDownAnimation,
            Math.Max(0f, DropDownAnimationDurationSec));

        menu.DropDown.Activate();
        menu.DropDown.SelectFirstEnabled();

        MenuOpened?.Invoke(menu);
    }

    /// <summary>Closes the open menu, if any.</summary>
    public void CloseMenu()
    {
        CloseMenuInternal(immediate: false);
    }

    /// <summary>Toggles the specified top-level menu.</summary>
    public void ToggleMenu(int index)
    {
        if (_openMenuIndex == index)
            CloseMenu();
        else
            OpenMenuAt(index);
    }

    /// <summary>Applies a drawing Z-order to the menu-bar family.</summary>
    public MenuBarWidget SetMenuZOrder(int zOrder)
    {
        _menuZOrder = zOrder;
        ApplyMenuZOrder(_menuZOrder);
        return this;
    }

    /// <inheritdoc/>
    protected override void ProcessShown()
    {
        base.ProcessShown();

        _dismissLayer.SetDismissEnabled(false);
        _openMenuIndex = -1;

        foreach (MenuBarMenu menu in _menus)
        {
            menu.Header.SetOpen(false);
            menu.Header.SetMenuBarActive(false);
            menu.DropDown.CloseAnimated(MenuDropDownAnimation.None, 0f, immediate: true);
        }
    }

    /// <inheritdoc/>
    protected override void ProcessHidden()
    {
        CloseMenuInternal(immediate: true);
        _dismissLayer.SetDismissEnabled(false);
        base.ProcessHidden();
    }

    /// <inheritdoc/>
    protected override void OnKeyboardInput(WidgetKeyboardEventArgs args)
    {
        base.OnKeyboardInput(args);
        HandleKeyboard(args);
    }

    void IWidgetKeyboardFallback.HandleUnhandledKeyboardInput(WidgetKeyboardEventArgs args) => HandleKeyboard(args);

    private void HandleKeyboard(WidgetKeyboardEventArgs args)
    {
        if (args.Handled || args.KeyAction != KeyAction.Pressed || !IsAvailable) return;
        if (args.Key > 0 && _shortcuts.TryGetValue(new KeyGesture(args.Key, args.Modifiers), out var command) && command.CanInvoke)
        {
            args.Handled = true;
            command.PerformClick();
            return;
        }
        if (args.Modifiers == KeyboardModifierState.Alt)
        {
            int index = _menus.FindIndex(menu => MatchesMnemonic(menu.Header.Mnemonic, args.Key));
            if (index >= 0)
            {
                args.Handled = true;
                OpenMenuAt(index);
                _menus[index].DropDown.SelectFirstEnabled();
                return;
            }
        }
        if (_openMenuIndex < 0 || args.Modifiers != KeyboardModifierState.None) return;
        var dropDown = _menus[_openMenuIndex].DropDown.ActiveMenu;
        switch (args.Key)
        {
            case 27:
                if (dropDown.ParentMenu is { } parent) parent.CloseChild();
                else CloseMenu();
                break;
            case 37:
                if (dropDown.ParentMenu is { } previous) previous.CloseChild();
                else OpenMenuAt((_openMenuIndex - 1 + _menus.Count) % _menus.Count);
                break;
            case 39:
                if (dropDown.SelectedItem is { SubMenu: not null } owner)
                    dropDown.OpenSubMenu(owner, selectFirst: true);
                else if (dropDown.ParentMenu is null)
                    OpenMenuAt((_openMenuIndex + 1) % _menus.Count);
                break;
            case 38: dropDown.SelectPreviousEnabled(); break;
            case 40: dropDown.SelectNextEnabled(); break;
            case 13:
            case 32: dropDown.InvokeSelectedItem(); break;
            default:
                var item = dropDown.Items.FirstOrDefault(item => item.CanInvoke && MatchesMnemonic(item.Mnemonic, args.Key));
                if (item is null) return;
                item.PerformClick();
                break;
        }
        args.Handled = true;
    }

    private static bool MatchesMnemonic(char? mnemonic, int key) => mnemonic is char character &&
        key <= char.MaxValue && key >= 0 && char.ToUpperInvariant(character) == char.ToUpperInvariant((char)key);

    /// <inheritdoc/>
    public override void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        CloseMenuInternal(immediate: true);

        _dismissLayer.DismissRequested -= OnDismissRequested;

        foreach (MenuBarMenu menu in _menus)
        {
            menu.Header.Invoked -= OnHeaderInvoked;
            menu.Header.Hovered -= OnHeaderHovered;
        }

        base.Dispose();
        MenuOpened = null;
        MenuClosed = null;
        ItemInvoked = null;
    }

    private void OnHeaderInvoked(MenuHeaderWidget header)
    {
        int index = _menus.FindIndex(menu => ReferenceEquals(menu.Header, header));

        if (index >= 0)
            ToggleMenu(index);
    }

    private void OnHeaderHovered(MenuHeaderWidget header)
    {
        if (_openMenuIndex < 0)
            return;

        int index = _menus.FindIndex(menu => ReferenceEquals(menu.Header, header));

        if (index >= 0 && index != _openMenuIndex)
            OpenMenuAt(index);
    }

    internal void OnItemInvoked(MenuItemWidget item)
    {
        CloseMenuInternal(immediate: true);
        ItemInvoked?.Invoke(item);
    }

    private void OnDismissRequested()
    {
        CloseMenu();
    }

    private void CloseMenuInternal(bool immediate)
    {
        if (_openMenuIndex < 0 || _openMenuIndex >= _menus.Count)
        {
            _dismissLayer.SetDismissEnabled(false);
            _openMenuIndex = -1;
            return;
        }

        MenuBarMenu menu = _menus[_openMenuIndex];
        _openMenuIndex = -1;

        menu.Header.SetOpen(false);

        foreach (MenuBarMenu candidate in _menus)
            candidate.Header.SetMenuBarActive(false);

        _dismissLayer.SetDismissEnabled(false);

        menu.DropDown.CloseAnimated(
            DropDownAnimation,
            Math.Max(0f, DropDownAnimationDurationSec),
            immediate);

        var focused = WidgetInputRouterRegistry.GetFocusedWidget(this);
        if (focused is null || ContainsWidget(this, focused))
            WidgetInputRouterRegistry.RestoreFocus(this, _previousFocus);
        _previousFocus = null;
        MenuClosed?.Invoke(menu);
    }

    private static bool ContainsWidget(ContainerWidget container, WidgetBase widget) =>
        container.ChildWidgets.Any(child => ReferenceEquals(child, widget) ||
            child is ContainerWidget nested && ContainsWidget(nested, widget));

    private void PositionDropDown(Rectangle headerBounds,
                                  MenuDropDownWidget dropDown)
    {
        Rectangle viewport = _view.Viewport.TargetRectPx;
        int x = Math.Clamp(
            headerBounds.X,
            viewport.Left,
            Math.Max(viewport.Left, viewport.Right - dropDown.Width));

        int y = Math.Clamp(_bounds.Bottom, viewport.Top, Math.Max(viewport.Top, viewport.Bottom - dropDown.Height));
        if (Children.Contains(dropDown))
            SetLocalOffset(dropDown, new Vector2(x - GetPosition().X, y - GetPosition().Y));
        else
            dropDown.SetPosition(x, y);
    }

    private int EstimateHeaderWidth(string text)
    {
        int estimatedTextWidth = (int)Math.Ceiling(
            text.Length * _theme.EstimatedGlyphWidth);

        return Math.Max(
            _theme.MinimumHeaderWidth,
            estimatedTextWidth + (_theme.HeaderHorizontalPadding * 2));
    }

    private void ApplyMenuZOrder(int zOrder)
    {
        _dismissLayer.SetLayerZOrder(zOrder);
        BarBackground.ZOrder = zOrder + 1;

        foreach (MenuBarMenu menu in _menus)
        {
            menu.Header.SetHeaderZOrder(zOrder + 2);
            menu.DropDown.SetDropDownZOrder(zOrder + 10);
        }
    }

    private sealed class DismissLayerWidget : WidgetBase
    {
        internal event Action? DismissRequested;

        internal DismissLayerWidget(RenderSurfaceHostBase host,
                                    View view,
                                    Rectangle bounds,
                                    string? nickname)
            : base(host, DirectDrawingMode.View, bounds.Location, nickname)
        {
            HitArea = new DirectRectangle(
                    Color.Transparent,
                    host,
                    view,
                    bounds,
                    $"{Nickname}.hit-area")
                .SetFilled(true)
                .SetStrokeWidth(0f);

            Add(HitArea);

            CanReceiveFocus = false;
            IsKeyboardInputEnabled = false;
        }

        internal DirectRectangle HitArea { get; }

        internal void SetDismissEnabled(bool enabled)
        {
            IsInputEnabled = enabled;
            IsPointerInputEnabled = enabled;

            if (enabled && !Visible)
                Show();
            else if (!enabled && Visible)
                Hide();
        }

        internal void SetLayerZOrder(int zOrder)
        {
            HitArea.ZOrder = zOrder;
        }

        protected override void OnPointerClick(WidgetPointerEventArgs args)
        {
            base.OnPointerClick(args);

            if (!args.IsPrimaryButton)
                return;

            args.Handled = true;
            DismissRequested?.Invoke();
        }
    }
}
