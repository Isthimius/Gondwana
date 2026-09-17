using System.Drawing;
using System.Numerics;
using Gondwana.Drawing.Direct;
using Gondwana.Rendering;
using Gondwana.Rendering.Views;
using SkiaSharp;

namespace Gondwana.Widgets.Menus;

/// <summary>
/// Represents a popup command list owned by a header or submenu item.
/// </summary>
public sealed class MenuDropDownWidget : ContainerWidget
{
    private readonly MenuBarTheme _theme;
    private readonly MenuBarWidget _bar;
    private int _zOrder;
    private int _minimumWidth;
    internal MenuItemWidget? ParentItem { get; private set; }
    internal MenuDropDownWidget? ParentMenu { get; private set; }
    internal MenuDropDownWidget? OpenChild { get; private set; }
    internal MenuDropDownWidget ActiveMenu => OpenChild?.ActiveMenu ?? this;
    internal MenuItemWidget? SelectedItem => _selectedIndex >= 0 && _selectedIndex < _items.Count ? _items[_selectedIndex] : null;
    internal bool CanInvoke => !_disposed && _bar.IsAvailable && (ParentItem?.CanInvoke ?? true);
    private readonly List<Entry> _entries = new();
    private readonly List<MenuItemWidget> _items = new();

    private EventHandler<DirectDrawingBase>? _closeCompletionHandler;
    private int _nextChildNicknameSuffix;
    private int _selectedIndex = -1;
    private bool _disposed;

    internal MenuDropDownWidget(RenderSurfaceHostBase host,
                                View view,
                                Point location,
                                MenuBarTheme theme,
                                string? nickname,
                                MenuBarWidget bar)
        : base(host, DirectDrawingMode.View, location, nickname)
    {
        _bar = bar;
        _theme = theme ?? throw new ArgumentNullException(nameof(theme));
        _minimumWidth = Width = Math.Max(_theme.MinimumDropDownWidth, _theme.DefaultDropDownWidth);

        Panel = new DirectRectangle(
                _theme.DropDownBackgroundColor,
                host,
                view,
                new Rectangle(location.X, location.Y, Width, 1),
                $"{Nickname}.panel")
            .SetFilled(true)
            .SetBorderColor(_theme.DropDownBorderColor)
            .SetStrokeWidth(_theme.BorderWidth)
            .SetCornerRadius(_theme.CornerRadius);

        Panel.HideWhenFullyTransparent = false;
        Add(Panel);

        IsPointerInputEnabled = true;
        IsKeyboardInputEnabled = false;
        CanReceiveFocus = false;

        RecalculateLayout();
        Hide();
    }

    /// <summary>Gets the popup panel drawing.</summary>
    public DirectRectangle Panel { get; }

    /// <summary>Gets the actionable entries in insertion order.</summary>
    public IReadOnlyList<MenuItemWidget> Items => _items.AsReadOnly();

    /// <summary>Gets the current popup width.</summary>
    public int Width { get; private set; }

    /// <summary>Gets the current popup height.</summary>
    public int Height { get; private set; }

    /// <summary>Gets whether this dropdown accepts input. False while its close animation finishes.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>Gets the selected actionable-item index, or -1.</summary>
    public int SelectedIndex => _selectedIndex;

    /// <summary>Adds a command, preserving fluent construction. Gesture text overrides display-only shortcutText.</summary>
    public MenuDropDownWidget AddItem(string text, Action? action = null,
        string? shortcutText = null, bool enabled = true, string? key = null,
        KeyGesture? shortcut = null, char? mnemonic = null, SKImage? icon = null)
    {
        AddEntry(text, action, shortcutText, enabled, key, shortcut, mnemonic, icon);
        return this;
    }

    /// <summary>Adds a check item. Invocation toggles state before passing the resulting value to the callback.</summary>
    public MenuDropDownWidget AddCheckItem(string text, Action<bool>? action = null,
        bool isChecked = false, bool enabled = true, string? key = null,
        KeyGesture? shortcut = null, char? mnemonic = null, SKImage? icon = null)
    {
        AddEntry(text, null, null, enabled, key, shortcut, mnemonic, icon,
            checkable: true, checkedAction: action).SetChecked(isChecked);
        return this;
    }

    /// <summary>Adds a radio command. Group names are scoped to this dropdown; the last checked item wins.</summary>
    public MenuDropDownWidget AddRadioItem(string text, Action? action, string group,
        bool isChecked = false, bool enabled = true, string? key = null,
        KeyGesture? shortcut = null, char? mnemonic = null, SKImage? icon = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(group);
        AddEntry(text, action, null, enabled, key, shortcut, mnemonic, icon,
            checkable: true, radioGroup: group).SetChecked(isChecked);
        return this;
    }

    /// <summary>Adds an owned submenu. Submenus may themselves contain submenus to any depth.</summary>
    public MenuDropDownWidget AddSubMenu(string text, Action<MenuDropDownWidget> configure,
        bool enabled = true, string? key = null, char? mnemonic = null, SKImage? icon = null)
    {
        ArgumentNullException.ThrowIfNull(configure);
        ValidateEntry(text, key, null, mnemonic);
        var child = new MenuDropDownWidget(RenderSurfaceHost, View!, Point.Empty, _theme,
            CreateChildNickname("submenu"), _bar);
        try
        {
            configure(child);
            var item = AddEntry(text, null, null, enabled, key, null, mnemonic, icon, subMenu: child);
            child.ParentMenu = this;
            child.ParentItem = item;
            Add(child, Vector2.Zero);
            child.CloseAnimated(MenuDropDownAnimation.None, 0, immediate: true);
            child.SetDropDownZOrder(_zOrder + 10);
        }
        catch
        {
            child.Dispose();
            throw;
        }
        return this;
    }

    private void ValidateEntry(string text, string? key, KeyGesture? shortcut, char? mnemonic)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(text);
        _bar.ValidateRegistration(key, shortcut);
        if (mnemonic is char character && _items.Any(item =>
            item.Mnemonic is char existing && char.ToUpperInvariant(existing) == char.ToUpperInvariant(character)))
            throw new ArgumentException($"Duplicate menu mnemonic '{character}'.", nameof(mnemonic));
    }

    private MenuItemWidget AddEntry(string text, Action? action, string? shortcutText,
        bool enabled, string? key, KeyGesture? shortcut, char? mnemonic, SKImage? icon,
        bool checkable = false, string? radioGroup = null, Action<bool>? checkedAction = null,
        MenuDropDownWidget? subMenu = null)
    {
        ValidateEntry(text, key, shortcut, mnemonic);
        var anchor = Point.Round(new PointF(GetPosition().X, GetPosition().Y));
        var item = new MenuItemWidget(RenderSurfaceHost, View!,
            new Rectangle(anchor.X, anchor.Y, Width, _theme.ItemHeight), text, shortcutText,
            action, _theme, CreateChildNickname("item"), this, key, shortcut, mnemonic,
            icon, checkable, radioGroup, checkedAction, subMenu);
        item.SetEnabled(enabled);
        item.Hovered += OnItemHovered;
        _items.Add(item);
        _entries.Add(Entry.ForItem(item));
        Add(item, Vector2.Zero);
        _bar.RegisterItem(item);
        if (!IsOpen)
        {
            item.ApplyInputState(false);
            item.Hide();
        }
        RecalculateLayout();
        SetDropDownZOrder(_zOrder);
        return item;
    }

    internal void UncheckRadioPeers(MenuItemWidget selected)
    {
        foreach (var item in _items)
            if (!ReferenceEquals(item, selected) && item.RadioGroup == selected.RadioGroup && item.IsChecked)
                item.SetChecked(false);
    }

    private string CreateChildNickname(string kind) => $"{Nickname}.{kind}.{_nextChildNicknameSuffix++}";

    internal void UnregisterItem(MenuItemWidget item)
    {
        _bar.UnregisterItem(item);
        if (_disposed) return;
        OnItemDisabled(item);
        item.SubMenu?.Dispose();
        item.Hovered -= OnItemHovered;
        _items.Remove(item);
        _entries.RemoveAll(entry => ReferenceEquals(entry.Item, item));
        _selectedIndex = -1;
        foreach (var remaining in _items) remaining.SetSelected(false);
    }

    internal void OnItemDisabled(MenuItemWidget item)
    {
        if (item.SubMenu is not null && ReferenceEquals(OpenChild, item.SubMenu)) CloseChild();
        if (ReferenceEquals(SelectedItem, item)) SetSelectedIndex(-1);
    }

    internal void NotifyInvoked(MenuItemWidget item) => _bar.OnItemInvoked(item);

    internal void OpenSubMenu(MenuItemWidget item, bool selectFirst)
    {
        if (!IsOpen || !item.CanInvoke || item.SubMenu is not { } child) return;
        SetSelectedIndex(_items.IndexOf(item));
        if (!ReferenceEquals(OpenChild, child))
        {
            CloseChild();
            var viewport = View!.Viewport.TargetRectPx;
            var row = item.Background.ScreenBounds;
            int x = Panel.ScreenBounds.Right + _theme.SubMenuGap;
            if (x + child.Width > viewport.Right)
                x = Panel.ScreenBounds.Left - child.Width - _theme.SubMenuGap;
            x = Math.Clamp(x, viewport.Left, Math.Max(viewport.Left, viewport.Right - child.Width));
            int y = Math.Clamp(row.Top, viewport.Top, Math.Max(viewport.Top, viewport.Bottom - child.Height));
            SetLocalOffset(child, new Vector2(x - GetPosition().X, y - GetPosition().Y));
            OpenChild = child;
            child.OpenAnimated(_bar.DropDownAnimation, Math.Max(0, _bar.DropDownAnimationDurationSec));
            child.Activate();
        }
        if (selectFirst) child.SelectFirstEnabled();
    }

    internal void CloseChild()
    {
        var child = OpenChild;
        OpenChild = null;
        child?.CloseAnimated(MenuDropDownAnimation.None, 0, immediate: true);
    }

    /// <inheritdoc/>
    protected override void ProcessShown()
    {
        base.ProcessShown();
        foreach (var item in _items)
            item.SubMenu?.CloseAnimated(MenuDropDownAnimation.None, 0, immediate: true);
    }

    /// <inheritdoc/>
    protected override void ProcessHidden()
    {
        CloseChild();
        IsOpen = false;
        SetEntryInputEnabled(false);
        CancelPendingClose();
        CancelVisualFades();
        base.ProcessHidden();
    }

    /// <summary>Adds a visual separator.</summary>
    public MenuDropDownWidget AddSeparator()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Point anchor = Point.Round(new PointF(GetPosition().X, GetPosition().Y));
        var separator = new DirectRectangle(
                _theme.SeparatorColor,
                RenderSurfaceHost,
                View!,
                new Rectangle(anchor.X, anchor.Y, 1, 1),
                CreateChildNickname("separator"))
            .SetFilled(true)
            .SetStrokeWidth(0f);

        separator.HideWhenFullyTransparent = false;
        _entries.Add(Entry.ForSeparator(separator));
        Add(separator, keepCurrentOffset: false, explicitLocalOffsetPx: Vector2.Zero);
        separator.Visible = IsOpen;
        separator.ZOrder = _zOrder + 2;

        RecalculateLayout();
        return this;
    }

    /// <summary>Sets a fixed minimum width for this dropdown.</summary>
    public MenuDropDownWidget SetWidth(int width)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (width < _theme.MinimumDropDownWidth)
        {
            throw new ArgumentOutOfRangeException(
                nameof(width),
                width,
                $"Menu dropdown width must be at least {_theme.MinimumDropDownWidth} pixels.");
        }

        _minimumWidth = width;
        RecalculateLayout();
        return this;
    }

    internal void OpenAnimated(MenuDropDownAnimation animation,
                               float durationSec)
    {
        CancelPendingClose();

        if (!Visible)
            Show();

        IsPointerInputEnabled = true;
        IsOpen = true;
        SetEntryInputEnabled(true);

        PrepareVisualsForAnimation();

        if (animation == MenuDropDownAnimation.None || durationSec <= 0f)
        {
            SetOpacity(1f);
            SetRevealProgress(1f);
            return;
        }

        SetOpacity(0f);
        SetRevealProgress(animation == MenuDropDownAnimation.FadeAndReveal ? 0f : 1f);
        foreach (var visual in EnumerateVisuals()) visual.FadeIn(durationSec);

        if (animation == MenuDropDownAnimation.FadeAndReveal)
        {
            foreach (DirectDrawingBase visual in EnumerateVisuals())
            {
                visual.SetRevealDirection(DirectDrawingBase.RevealDirection.TopToBottom)
                      .RevealTo(1f, durationSec, EaseOutCubic);
            }
        }
    }

    internal void CloseAnimated(MenuDropDownAnimation animation,
                                float durationSec,
                                bool immediate = false)
    {
        CloseChild();
        IsOpen = false;
        IsPointerInputEnabled = false;
        SetEntryInputEnabled(false);
        SetSelectedIndex(-1);
        CancelPendingClose();

        if (immediate || animation == MenuDropDownAnimation.None || durationSec <= 0f)
        {
            CancelVisualFades();
            SetOpacity(1f);
            SetRevealProgress(1f);

            if (Visible)
                Hide();

            return;
        }

        _closeCompletionHandler = (_, _) =>
        {
            CancelPendingClose();

            if (Visible)
                Hide();
        };

        Panel.FadeToCompleted += _closeCompletionHandler;

        // Closing is a clean fade. Any in-progress opening reveal is snapped
        // fully open so it cannot continue unfolding while disappearing.
        SetRevealProgress(1f);
        foreach (var visual in EnumerateVisuals()) visual.FadeOut(durationSec);
    }

    internal void SetDropDownZOrder(int zOrder)
    {
        _zOrder = zOrder;
        Panel.ZOrder = zOrder;
        foreach (var item in _items) item.SubMenu?.SetDropDownZOrder(zOrder + 10);

        foreach (Entry entry in _entries)
        {
            if (entry.Item is not null)
                entry.Item.SetItemZOrder(zOrder + 1);
            else if (entry.Separator is not null)
                entry.Separator.ZOrder = zOrder + 2;
        }
    }

    internal void SelectFirstEnabled()
    {
        SetSelectedIndex(FindNextEnabledIndex(-1, 1));
    }

    internal void SelectNextEnabled()
    {
        SetSelectedIndex(FindNextEnabledIndex(_selectedIndex, 1));
    }

    internal void SelectPreviousEnabled()
    {
        int start = _selectedIndex < 0 ? _items.Count : _selectedIndex;
        SetSelectedIndex(FindNextEnabledIndex(start, -1));
    }

    internal void InvokeSelectedItem()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _items.Count)
            return;

        _items[_selectedIndex].PerformClick();
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        CloseChild();
        CancelPendingClose();
        CancelVisualFades();
        IsOpen = false;

        foreach (MenuItemWidget item in _items)
        {
            item.Hovered -= OnItemHovered;
        }

        base.Dispose();
    }

    private void RecalculateLayout()
    {
        int markerWidth = _items.Any(item => item.IsCheckable) ? _theme.MarkerColumnWidth : 0;
        int iconWidth = _items.Any(item => item.Icon is not null) ? _theme.IconSize + _theme.IconGap : 0;
        int arrowWidth = _items.Any(item => item.SubMenu is not null) ? _theme.SubMenuArrowWidth : 0;
        int shortcutWidth = _items.Select(item => MeasureText(item.ShortcutText)).DefaultIfEmpty().Max();
        int labelWidth = _items.Select(item => MeasureText(item.Text)).DefaultIfEmpty().Max();
        Width = Math.Max(_minimumWidth, markerWidth + iconWidth + arrowWidth + shortcutWidth + labelWidth +
            (shortcutWidth > 0 ? _theme.ShortcutGap : 0) + 2 * (_theme.DropDownHorizontalPadding + _theme.ItemHorizontalPadding));
        int y = _theme.DropDownVerticalPadding;

        foreach (Entry entry in _entries)
        {
            if (entry.Item is not null)
            {
                var offset = new Vector2(_theme.DropDownHorizontalPadding, y);
                SetLocalOffset(entry.Item, offset);

                Point position = Point.Round(new PointF(
                    GetPosition().X + offset.X,
                    GetPosition().Y + offset.Y));

                entry.Item.SetBounds(new Rectangle(
                    position.X,
                    position.Y,
                    Width - (_theme.DropDownHorizontalPadding * 2),
                    _theme.ItemHeight), markerWidth, iconWidth, shortcutWidth, arrowWidth);

                y += _theme.ItemHeight;
                continue;
            }

            if (entry.Separator is not null)
            {
                int lineX = _theme.DropDownHorizontalPadding + 5;
                int lineY = y + (_theme.SeparatorHeight / 2);
                var offset = new Vector2(lineX, lineY);

                SetLocalOffset(entry.Separator, offset);

                Point position = Point.Round(new PointF(
                    GetPosition().X + offset.X,
                    GetPosition().Y + offset.Y));

                entry.Separator.ScreenBounds = new Rectangle(
                    position.X,
                    position.Y,
                    Math.Max(1, Width - (lineX * 2)),
                    1);

                y += _theme.SeparatorHeight;
            }
        }

        Height = Math.Max(1, y + _theme.DropDownVerticalPadding);

        Point panelPosition = Point.Round(new PointF(GetPosition().X, GetPosition().Y));
        Panel.ScreenBounds = new Rectangle(panelPosition.X, panelPosition.Y, Width, Height);
    }

    private void SetEntryInputEnabled(bool menuAcceptsInput)
    {
        foreach (MenuItemWidget item in _items)
            item.ApplyInputState(menuAcceptsInput);
    }

    private void SetSelectedIndex(int index)
    {
        if (index < -1 || index >= _items.Count)
            index = -1;

        if (_selectedIndex == index)
            return;

        if (_selectedIndex >= 0 && _selectedIndex < _items.Count)
            _items[_selectedIndex].SetSelected(false);

        CloseChild();
        _selectedIndex = index;

        if (_selectedIndex >= 0)
            _items[_selectedIndex].SetSelected(true);
    }

    private int FindNextEnabledIndex(int startIndex, int direction)
    {
        if (_items.Count == 0)
            return -1;

        int index = startIndex;

        for (int attempt = 0; attempt < _items.Count; attempt++)
        {
            index = (index + direction + _items.Count) % _items.Count;

            if (_items[index].CanInvoke)
                return index;
        }

        return -1;
    }

    private void OnItemHovered(MenuItemWidget item)
    {
        int index = _items.IndexOf(item);

        if (index >= 0)
        {
            SetSelectedIndex(index);
            if (item.SubMenu is not null) OpenSubMenu(item, selectFirst: false);
        }
    }

    private int MeasureText(string text)
    {
        using var font = new SKFont(SKTypeface.Default, _theme.FontSize);
        return (int)Math.Ceiling(font.MeasureText(text));
    }

    private void PrepareVisualsForAnimation()
    {
        foreach (DirectDrawingBase visual in EnumerateVisuals())
        {
            visual.CancelFade();
            visual.HideWhenFullyTransparent = false;
        }
    }

    private void CancelVisualFades()
    {
        foreach (DirectDrawingBase visual in EnumerateVisuals())
            visual.CancelFade().CancelReveal();
    }

    private void SetRevealProgress(float progress)
    {
        foreach (DirectDrawingBase visual in EnumerateVisuals())
        {
            visual.CancelReveal()
                  .SetRevealDirection(DirectDrawingBase.RevealDirection.TopToBottom)
                  .SetReveal(progress);
        }
    }

    private IEnumerable<DirectDrawingBase> EnumerateVisuals()
    {
        return EnumerateVisuals(this);
    }

    private static IEnumerable<DirectDrawingBase> EnumerateVisuals(IDirectCompositeContainer container)
    {
        foreach (IDirectCompositeChild child in container.Children)
        {
            if (child is DirectDrawingBase drawing)
                yield return drawing;

            if (child is IDirectCompositeContainer nestedContainer && child is not MenuDropDownWidget)
            {
                foreach (DirectDrawingBase nestedDrawing in EnumerateVisuals(nestedContainer))
                    yield return nestedDrawing;
            }
        }
    }

    private void CancelPendingClose()
    {
        if (_closeCompletionHandler is null)
            return;

        Panel.FadeToCompleted -= _closeCompletionHandler;
        _closeCompletionHandler = null;
    }

    private static float EaseOutCubic(float t)
    {
        float inverse = 1f - t;
        return 1f - (inverse * inverse * inverse);
    }

    private sealed class Entry
    {
        private Entry(MenuItemWidget? item,
                      DirectRectangle? separator)
        {
            Item = item;
            Separator = separator;
        }

        internal MenuItemWidget? Item { get; }
        internal DirectRectangle? Separator { get; }

        internal static Entry ForItem(MenuItemWidget item) => new(item, null);
        internal static Entry ForSeparator(DirectRectangle separator) => new(null, separator);
    }
}
