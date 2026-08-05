using System.Drawing;
using System.Numerics;
using Gondwana.Drawing.Direct;
using Gondwana.Rendering;
using Gondwana.Rendering.Views;

namespace Gondwana.Widgets.Menus;

/// <summary>
/// Represents the popup command list owned by a menu-bar header.
/// </summary>
public sealed class MenuDropDownWidget : ContainerWidget
{
    private readonly MenuBarTheme _theme;
    private readonly List<Entry> _entries = new();
    private readonly List<MenuItemWidget> _items = new();

    private EventHandler<DirectDrawingBase>? _closeCompletionHandler;
    private int _selectedIndex = -1;
    private bool _disposed;

    internal event Action<MenuDropDownWidget, MenuItemWidget>? ItemInvoked;

    internal MenuDropDownWidget(RenderSurfaceHostBase host,
                                View view,
                                Point location,
                                MenuBarTheme theme,
                                string? nickname = null)
        : base(host, DirectDrawingMode.View, location, nickname)
    {
        _theme = theme ?? throw new ArgumentNullException(nameof(theme));
        Width = Math.Max(_theme.MinimumDropDownWidth, _theme.DefaultDropDownWidth);

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
    public IReadOnlyList<MenuItemWidget> Items => _items;

    /// <summary>Gets the current popup width.</summary>
    public int Width { get; private set; }

    /// <summary>Gets the current popup height.</summary>
    public int Height { get; private set; }

    /// <summary>Gets whether this dropdown is open or is completing its close animation.</summary>
    public bool IsOpen { get; private set; }

    /// <summary>Gets the selected actionable-item index, or -1.</summary>
    public int SelectedIndex => _selectedIndex;

    /// <summary>Adds an actionable command.</summary>
    public MenuDropDownWidget AddItem(string text,
                                      Action? action = null,
                                      string? shortcutText = null,
                                      bool enabled = true)
    {
        ArgumentNullException.ThrowIfNull(text);

        int requiredWidth = EstimateRequiredWidth(text, shortcutText);
        Width = Math.Max(Width, requiredWidth);

        Point anchor = Point.Round(new PointF(GetPosition().X, GetPosition().Y));
        var item = new MenuItemWidget(
            RenderSurfaceHost,
            View!,
            new Rectangle(anchor.X, anchor.Y, Width, _theme.ItemHeight),
            text,
            shortcutText,
            action,
            _theme,
            $"{Nickname}.item.{_items.Count}");

        item.SetEnabled(enabled);
        item.Hovered += OnItemHovered;
        item.Invoked += OnItemInvoked;

        _items.Add(item);
        _entries.Add(Entry.ForItem(item));
        Add(item, Vector2.Zero);

        if (!IsOpen)
        {
            item.ApplyInputState(false);
            item.Hide();
        }

        RecalculateLayout();
        return this;
    }

    /// <summary>Adds a visual separator.</summary>
    public MenuDropDownWidget AddSeparator()
    {
        Point anchor = Point.Round(new PointF(GetPosition().X, GetPosition().Y));
        var separator = new DirectRectangle(
                _theme.SeparatorColor,
                RenderSurfaceHost,
                View!,
                new Rectangle(anchor.X, anchor.Y, 1, 1),
                $"{Nickname}.separator.{_entries.Count}")
            .SetFilled(true)
            .SetStrokeWidth(0f);

        separator.HideWhenFullyTransparent = false;
        _entries.Add(Entry.ForSeparator(separator));
        Add(separator, keepCurrentOffset: false, explicitLocalOffsetPx: Vector2.Zero);
        separator.Visible = IsOpen;

        RecalculateLayout();
        return this;
    }

    /// <summary>Sets a fixed minimum width for this dropdown.</summary>
    public MenuDropDownWidget SetWidth(int width)
    {
        if (width < _theme.MinimumDropDownWidth)
        {
            throw new ArgumentOutOfRangeException(
                nameof(width),
                width,
                $"Menu dropdown width must be at least {_theme.MinimumDropDownWidth} pixels.");
        }

        Width = width;
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
        FadeIn(durationSec);

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
        IsOpen = false;
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
        FadeOut(durationSec);
    }

    internal void SetDropDownZOrder(int zOrder)
    {
        Panel.ZOrder = zOrder;

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
        CancelPendingClose();

        foreach (MenuItemWidget item in _items)
        {
            item.Hovered -= OnItemHovered;
            item.Invoked -= OnItemInvoked;
        }

        base.Dispose();
    }

    private void RecalculateLayout()
    {
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
                    _theme.ItemHeight));

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

            if (_items[index].IsEnabled)
                return index;
        }

        return -1;
    }

    private void OnItemHovered(MenuItemWidget item)
    {
        int index = _items.IndexOf(item);

        if (index >= 0)
            SetSelectedIndex(index);
    }

    private void OnItemInvoked(MenuItemWidget item)
    {
        ItemInvoked?.Invoke(this, item);
    }

    private int EstimateRequiredWidth(string text, string? shortcutText)
    {
        float labelWidth = text.Length * _theme.EstimatedGlyphWidth;
        float shortcutWidth = string.IsNullOrWhiteSpace(shortcutText)
            ? 0f
            : shortcutText.Length * _theme.EstimatedGlyphWidth + _theme.ShortcutGap;

        int padding = (_theme.DropDownHorizontalPadding * 2) +
                      (_theme.ItemHorizontalPadding * 2);

        return Math.Max(
            _theme.MinimumDropDownWidth,
            (int)Math.Ceiling(labelWidth + shortcutWidth + padding));
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
            visual.CancelFade();
    }

    private void SetRevealProgress(float progress)
    {
        foreach (DirectDrawingBase visual in EnumerateVisuals())
        {
            visual.SetRevealDirection(DirectDrawingBase.RevealDirection.TopToBottom)
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

            if (child is IDirectCompositeContainer nestedContainer)
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
