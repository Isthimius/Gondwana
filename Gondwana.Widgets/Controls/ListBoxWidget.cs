using System.Drawing;
using System.Numerics;
using Gondwana.Drawing.Direct;
using Gondwana.Input.Keyboard;
using Gondwana.Rendering;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;
using SkiaSharp;

namespace Gondwana.Widgets.Controls;

/// <summary>
/// Provides a selectable, vertically scrolling list of text items.
/// </summary>
public sealed class ListBoxWidget : WidgetBase
{
    private const int ContentPadding = 2;
    private const int DefaultItemHeight = 24;

    private readonly List<string> _items = [];
    private readonly List<TextBlock> _rowTextBlocks = [];

    private int _selectedIndex = -1;
    private int _topIndex;
    private int _itemHeight = DefaultItemHeight;
    private int _baseZOrder;

    /// <summary>
    /// Occurs when <see cref="SelectedIndex"/> changes.
    /// </summary>
    public event Action<int>? SelectedIndexChanged;

    /// <summary>
    /// Creates a view-level list box.
    /// </summary>
    public ListBoxWidget(RenderSurfaceHostBase renderSurfaceHost,
                         View view,
                         Rectangle bounds,
                         IEnumerable<string>? items = null,
                         string? nickname = null)
        : base(renderSurfaceHost, DirectDrawingMode.View, ValidateBounds(bounds).Location, nickname)
    {
        ArgumentNullException.ThrowIfNull(view);

        Background = CreateBackground(renderSurfaceHost, view, bounds);
        SelectionHighlight = CreateSelectionHighlight(renderSurfaceHost, view, GetInitialSelectionBounds(bounds));

        Add(Background);
        Add(SelectionHighlight);

        CompleteInitialization(items);
    }

    /// <summary>
    /// Creates a scene-layer list box.
    /// </summary>
    public ListBoxWidget(RenderSurfaceHostBase renderSurfaceHost,
                         SceneLayer sceneLayer,
                         Rectangle bounds,
                         IEnumerable<string>? items = null,
                         string? nickname = null)
        : base(renderSurfaceHost, DirectDrawingMode.SceneLayer, ValidateBounds(bounds).Location, nickname)
    {
        ArgumentNullException.ThrowIfNull(sceneLayer);

        Background = CreateBackground(renderSurfaceHost, sceneLayer, bounds);
        SelectionHighlight = CreateSelectionHighlight(renderSurfaceHost, sceneLayer, GetInitialSelectionBounds(bounds));

        Add(Background);
        Add(SelectionHighlight);

        CompleteInitialization(items);
    }

    /// <summary>
    /// Gets the list background.
    /// </summary>
    public DirectRectangle Background { get; }

    /// <summary>
    /// Gets the rectangle used to highlight the selected row.
    /// </summary>
    public DirectRectangle SelectionHighlight { get; }

    /// <summary>
    /// Gets the current items.
    /// </summary>
    public IReadOnlyList<string> Items => _items;

    /// <summary>
    /// Gets the list-box bounds in its native coordinate space.
    /// </summary>
    public Rectangle Bounds => Mode == DirectDrawingMode.View
        ? Background.ScreenBounds
        : Background.WorldBounds;

    /// <summary>
    /// Gets or sets the height of each row in native pixels.
    /// </summary>
    public int ItemHeight
    {
        get => _itemHeight;
        set
        {
            if (value < 12)
                throw new ArgumentOutOfRangeException(nameof(value), "Item height must be at least 12 pixels.");

            if (_itemHeight == value)
                return;

            _itemHeight = value;
            ClampTopIndex();
            EnsureSelectionVisible();
            RefreshRows();
        }
    }

    /// <summary>
    /// Gets the number of complete rows that can currently be displayed.
    /// </summary>
    public int VisibleItemCount => Math.Max(1, (Bounds.Height - ContentPadding * 2) / ItemHeight);

    /// <summary>
    /// Gets or sets the index of the first visible item.
    /// </summary>
    public int TopIndex
    {
        get => _topIndex;
        set
        {
            int clamped = Math.Clamp(value, 0, GetMaximumTopIndex());
            if (_topIndex == clamped)
                return;

            _topIndex = clamped;
            RefreshRows();
        }
    }

    /// <summary>
    /// Gets or sets the selected item index. Use -1 to clear selection.
    /// </summary>
    public int SelectedIndex
    {
        get => _selectedIndex;
        set => SetSelectedIndex(value);
    }

    /// <summary>
    /// Gets the selected item text, or <see langword="null"/> when no item is selected.
    /// </summary>
    public string? SelectedItem => _selectedIndex >= 0 && _selectedIndex < _items.Count
        ? _items[_selectedIndex]
        : null;

    /// <summary>
    /// Replaces all list items and clears selection.
    /// </summary>
    public ListBoxWidget SetItems(IEnumerable<string> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        bool hadSelection = _selectedIndex >= 0;
        _items.Clear();
        _items.AddRange(items.Select(static item => item ?? string.Empty));
        _selectedIndex = -1;
        _topIndex = 0;
        RefreshRows();

        if (hadSelection)
            SelectedIndexChanged?.Invoke(-1);

        return this;
    }

    /// <summary>
    /// Adds an item to the end of the list.
    /// </summary>
    public ListBoxWidget AddItem(string item)
    {
        _items.Add(item ?? string.Empty);
        ClampTopIndex();
        RefreshRows();
        return this;
    }

    /// <summary>
    /// Removes an item by index.
    /// </summary>
    public ListBoxWidget RemoveAt(int index)
    {
        if (index < 0 || index >= _items.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        int oldSelectedIndex = _selectedIndex;
        bool selectedItemWasRemoved = index == oldSelectedIndex;

        _items.RemoveAt(index);

        if (_items.Count == 0)
        {
            _selectedIndex = -1;
        }
        else if (index < oldSelectedIndex)
        {
            _selectedIndex = oldSelectedIndex - 1;
        }
        else if (selectedItemWasRemoved)
        {
            _selectedIndex = Math.Min(index, _items.Count - 1);
        }

        ClampTopIndex();
        EnsureSelectionVisible();
        RefreshRows();

        if (_selectedIndex != oldSelectedIndex || selectedItemWasRemoved)
            SelectedIndexChanged?.Invoke(_selectedIndex);

        return this;
    }

    /// <summary>
    /// Removes all items and clears selection.
    /// </summary>
    public ListBoxWidget ClearItems()
    {
        bool hadSelection = _selectedIndex >= 0;
        _items.Clear();
        _selectedIndex = -1;
        _topIndex = 0;
        RefreshRows();

        if (hadSelection)
            SelectedIndexChanged?.Invoke(-1);

        return this;
    }

    /// <summary>
    /// Selects the requested item index. Use -1 to clear selection.
    /// </summary>
    public ListBoxWidget SetSelectedIndex(int index)
    {
        if (index < -1 || index >= _items.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        if (_selectedIndex == index)
            return this;

        _selectedIndex = index;
        EnsureSelectionVisible();
        RefreshSelectionHighlight();
        SelectedIndexChanged?.Invoke(index);
        return this;
    }

    /// <summary>
    /// Selects the next item when possible.
    /// </summary>
    public ListBoxWidget SelectNext()
    {
        if (_items.Count == 0)
            return this;

        int next = _selectedIndex < 0 ? 0 : Math.Min(_selectedIndex + 1, _items.Count - 1);
        return SetSelectedIndex(next);
    }

    /// <summary>
    /// Selects the previous item when possible.
    /// </summary>
    public ListBoxWidget SelectPrevious()
    {
        if (_items.Count == 0)
            return this;

        int previous = _selectedIndex < 0 ? 0 : Math.Max(_selectedIndex - 1, 0);
        return SetSelectedIndex(previous);
    }

    /// <summary>
    /// Sets the base Z-order used by the list-box visuals.
    /// </summary>
    public ListBoxWidget SetListBoxZOrder(int zOrder)
    {
        _baseZOrder = zOrder;
        Background.ZOrder = zOrder;
        SelectionHighlight.ZOrder = zOrder + 1;

        foreach (TextBlock row in _rowTextBlocks)
            row.ZOrder = zOrder + 2;

        return this;
    }

    /// <summary>
    /// Sets the selected-row highlight color.
    /// </summary>
    public ListBoxWidget SetSelectionColor(Color color)
    {
        SelectionHighlight.SetColor(color);
        Refresh(SelectionHighlight);
        return this;
    }

    /// <inheritdoc/>
    protected override void OnPointerClick(WidgetPointerEventArgs args)
    {
        base.OnPointerClick(args);

        if (!args.IsPrimaryButton || _items.Count == 0)
            return;

        RectangleF screenBounds = Background.GetDrawLocationScreen(args.View);
        if (screenBounds.Height <= 0f || Bounds.Height <= 0)
            return;

        float scaleY = screenBounds.Height / Bounds.Height;
        float localY = args.ScreenPositionPx.Y - screenBounds.Top - ContentPadding * scaleY;
        float rowHeightScreen = ItemHeight * scaleY;

        if (localY < 0f || rowHeightScreen <= 0f)
            return;

        int row = (int)(localY / rowHeightScreen);
        int index = TopIndex + row;

        if (row < 0 || row >= VisibleItemCount || index >= _items.Count)
            return;

        args.Handled = true;
        SetSelectedIndex(index);
    }

    /// <inheritdoc/>
    protected override void OnKeyboardInput(WidgetKeyboardEventArgs args)
    {
        base.OnKeyboardInput(args);

        if (args.KeyAction is not KeyAction.Pressed and not KeyAction.Repeated || _items.Count == 0)
            return;

        switch (args.Key)
        {
            case 38: // Up
                SelectPrevious();
                break;
            case 40: // Down
                SelectNext();
                break;
            case 36: // Home
                SetSelectedIndex(0);
                break;
            case 35: // End
                SetSelectedIndex(_items.Count - 1);
                break;
            case 33: // Page Up
                SetSelectedIndex(Math.Max(0, (_selectedIndex < 0 ? 0 : _selectedIndex) - VisibleItemCount));
                break;
            case 34: // Page Down
                SetSelectedIndex(Math.Min(_items.Count - 1, (_selectedIndex < 0 ? 0 : _selectedIndex) + VisibleItemCount));
                break;
            default:
                return;
        }

        args.Handled = true;
    }

    private void CompleteInitialization(IEnumerable<string>? items)
    {
        SelectionHighlight.Visible = false;
        CanReceiveFocus = true;
        IsKeyboardInputEnabled = true;
        SetListBoxZOrder(0);

        if (items is not null)
            _items.AddRange(items.Select(static item => item ?? string.Empty));

        RefreshRows();
    }

    private void EnsureSelectionVisible()
    {
        if (_selectedIndex < 0)
            return;

        if (_selectedIndex < _topIndex)
        {
            _topIndex = _selectedIndex;
            RefreshRows();
            return;
        }

        int lastVisible = _topIndex + VisibleItemCount - 1;
        if (_selectedIndex > lastVisible)
        {
            _topIndex = Math.Min(GetMaximumTopIndex(), _selectedIndex - VisibleItemCount + 1);
            RefreshRows();
        }
    }

    private void ClampTopIndex()
    {
        _topIndex = Math.Clamp(_topIndex, 0, GetMaximumTopIndex());
    }

    private int GetMaximumTopIndex()
    {
        return Math.Max(0, _items.Count - VisibleItemCount);
    }

    private void RefreshRows()
    {
        foreach (TextBlock row in _rowTextBlocks)
        {
            Remove(row);
            row.Dispose();
        }

        _rowTextBlocks.Clear();

        Rectangle bounds = Bounds;
        int count = Math.Min(VisibleItemCount, Math.Max(0, _items.Count - TopIndex));

        for (int rowIndex = 0; rowIndex < count; rowIndex++)
        {
            int itemIndex = TopIndex + rowIndex;
            Rectangle rowBounds = GetRowBounds(bounds, rowIndex);
            TextBlock row = CreateRowText(rowBounds, _items[itemIndex]);
            row.ZOrder = _baseZOrder + 2;
            Add(row);
            _rowTextBlocks.Add(row);
        }

        RefreshSelectionHighlight();
    }

    private TextBlock CreateRowText(Rectangle bounds, string text)
    {
        TextBlock row = Mode == DirectDrawingMode.View
            ? new TextBlock(RenderSurfaceHost, View!, bounds)
            : new TextBlock(RenderSurfaceHost, SceneLayer!, view: null, worldBounds: bounds);

        return row.SetText(text)
                  .SetFont(SKTypeface.Default, 15f, minSize: 9f)
                  .SetColors(SKColors.White, SKColors.Transparent)
                  .SetAlignment(SKTextAlign.Left, TextBlock.VerticalAlign.Center)
                  .SetPadding(6f, 0f)
                  .EnableWrapping(false);
    }

    private void RefreshSelectionHighlight()
    {
        if (_selectedIndex < TopIndex || _selectedIndex >= TopIndex + VisibleItemCount || _selectedIndex >= _items.Count)
        {
            SelectionHighlight.Visible = false;
            return;
        }

        int rowIndex = _selectedIndex - TopIndex;
        Rectangle selectionBounds = GetRowBounds(Bounds, rowIndex);
        Rectangle currentBounds = Mode == DirectDrawingMode.View
            ? SelectionHighlight.ScreenBounds
            : SelectionHighlight.WorldBounds;
        Rectangle resizedBounds = new(currentBounds.Location, selectionBounds.Size);

        if (Mode == DirectDrawingMode.View)
            SelectionHighlight.ScreenBounds = resizedBounds;
        else
            SelectionHighlight.WorldBounds = resizedBounds;

        SetLocalOffset(
            SelectionHighlight,
            new Vector2(ContentPadding, ContentPadding + rowIndex * ItemHeight));

        SelectionHighlight.Visible = true;
    }

    private Rectangle GetRowBounds(Rectangle bounds, int rowIndex)
    {
        int y = bounds.Y + ContentPadding + rowIndex * ItemHeight;
        return new Rectangle(bounds.X + ContentPadding,
                             y,
                             Math.Max(1, bounds.Width - ContentPadding * 2),
                             ItemHeight);
    }

    private static DirectRectangle CreateBackground(RenderSurfaceHostBase host,
                                                    View view,
                                                    Rectangle bounds)
    {
        return ConfigureBackground(new DirectRectangle(Color.FromArgb(255, 36, 36, 44), host, view, bounds));
    }

    private static DirectRectangle CreateBackground(RenderSurfaceHostBase host,
                                                    SceneLayer layer,
                                                    Rectangle bounds)
    {
        return ConfigureBackground(new DirectRectangle(Color.FromArgb(255, 36, 36, 44), host, layer, bounds));
    }

    private static DirectRectangle ConfigureBackground(DirectRectangle background)
    {
        return background.SetFilled(true)
                         .SetBorderColor(Color.FromArgb(255, 140, 140, 155))
                         .SetStrokeWidth(1.5f)
                         .SetCornerRadius(4f);
    }

    private static DirectRectangle CreateSelectionHighlight(RenderSurfaceHostBase host,
                                                            View view,
                                                            Rectangle bounds)
    {
        return ConfigureSelectionHighlight(new DirectRectangle(Color.FromArgb(220, 63, 91, 138), host, view, bounds));
    }

    private static DirectRectangle CreateSelectionHighlight(RenderSurfaceHostBase host,
                                                            SceneLayer layer,
                                                            Rectangle bounds)
    {
        return ConfigureSelectionHighlight(new DirectRectangle(Color.FromArgb(220, 63, 91, 138), host, layer, bounds));
    }

    private static DirectRectangle ConfigureSelectionHighlight(DirectRectangle highlight)
    {
        return highlight.SetFilled(true)
                        .SetStrokeWidth(0f)
                        .SetCornerRadius(2f);
    }

    private static Rectangle GetInitialSelectionBounds(Rectangle bounds)
    {
        return new Rectangle(bounds.X + ContentPadding,
                             bounds.Y + ContentPadding,
                             Math.Max(1, bounds.Width - ContentPadding * 2),
                             DefaultItemHeight);
    }

    private static void Refresh(DirectRectangle rectangle)
    {
        rectangle.SetPosition(rectangle.GetPosition());
    }

    private static Rectangle ValidateBounds(Rectangle bounds)
    {
        if (bounds.Width < 24 || bounds.Height < DefaultItemHeight + ContentPadding * 2)
            throw new ArgumentOutOfRangeException(nameof(bounds), bounds, "List-box bounds are too small.");

        return bounds;
    }
}
