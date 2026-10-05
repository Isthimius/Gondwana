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
/// Provides a tabbed container whose pages own ordinary Gondwana widgets.
/// </summary>
/// <remarks>
/// Add pages with <see cref="AddTab(string,char?,string?)"/>. By default, all
/// tab headers share one row and resize to fit the control width. Set
/// <see cref="AutoExpandRows"/> to retain a preferred header width by adding
/// rows as more tabs are added, or set <see cref="TabRowCount"/> to reserve a
/// fixed number of rows. Pages can be selected with their headers or with an
/// <c>Alt</c> mnemonic.
/// </remarks>
public sealed class TabControlWidget : ContainerWidget, IWidgetKeyboardFallback
{
    private const int DefaultTabHeight = 32;
    private const int DefaultPreferredTabWidth = 132;
    private const float DefaultReorderDragThresholdPx = 6f;

    private readonly List<TabPageWidget> _tabs = [];
    private readonly Dictionary<TabPageWidget, TabHeaderWidget> _headers = [];

    private readonly View? _view;
    private readonly SceneLayer? _sceneLayer;

    private Size _size;
    private int _selectedIndex = -1;
    private int _tabRowCount = 1;
    private int _displayedRowCount = 1;
    private int _displayedColumnCount = 1;
    private int _tabHeight = DefaultTabHeight;
    private int _preferredTabWidth = DefaultPreferredTabWidth;
    private int _baseZOrder;
    private float _tabReorderDragThresholdPx = DefaultReorderDragThresholdPx;
    private bool _autoExpandRows;
    private bool _isTabReorderingEnabled;
    private bool _isShown;
    private bool _disposed;

    private Color _normalTabColor = Color.FromArgb(255, 60, 60, 68);
    private Color _hoverTabColor = Color.FromArgb(255, 78, 78, 88);
    private Color _pressedTabColor = Color.FromArgb(255, 42, 42, 48);
    private Color _selectedTabColor = Color.FromArgb(255, 48, 89, 142);
    private Color _tabTextColor = Color.White;

    /// <summary>
    /// Occurs after the selected page changes.
    /// </summary>
    public event Action<TabPageWidget>? SelectedTabChanged;

    /// <summary>
    /// Occurs after a page moves to a different tab index.
    /// </summary>
    public event Action<TabPageWidget, int, int>? TabReordered;

    /// <summary>
    /// Creates a view-level tab control.
    /// </summary>
    /// <param name="renderSurfaceHost">The render surface host that owns the control.</param>
    /// <param name="view">The view that renders the control.</param>
    /// <param name="bounds">The control bounds in view pixels.</param>
    /// <param name="nickname">The optional diagnostic nickname.</param>
    public TabControlWidget(RenderSurfaceHostBase renderSurfaceHost,
                            View view,
                            Rectangle bounds,
                            string? nickname = null)
        : base(renderSurfaceHost, DirectDrawingMode.View, ValidateBounds(bounds).Location, nickname)
    {
        ArgumentNullException.ThrowIfNull(view);

        _view = view;
        _size = bounds.Size;

        InitializeInput();
    }

    /// <summary>
    /// Creates a scene-layer tab control.
    /// </summary>
    /// <param name="renderSurfaceHost">The render surface host that owns the control.</param>
    /// <param name="sceneLayer">The scene layer that renders the control.</param>
    /// <param name="bounds">The control bounds in world pixels.</param>
    /// <param name="nickname">The optional diagnostic nickname.</param>
    public TabControlWidget(RenderSurfaceHostBase renderSurfaceHost,
                            SceneLayer sceneLayer,
                            Rectangle bounds,
                            string? nickname = null)
        : base(renderSurfaceHost, DirectDrawingMode.SceneLayer, ValidateBounds(bounds).Location, nickname)
    {
        ArgumentNullException.ThrowIfNull(sceneLayer);

        _sceneLayer = sceneLayer;
        _size = bounds.Size;

        InitializeInput();
    }

    /// <summary>
    /// Gets the current control bounds in its native coordinate space.
    /// </summary>
    public Rectangle Bounds
    {
        get
        {
            Vector2 position = GetPosition();
            return new Rectangle(
                (int)MathF.Round(position.X),
                (int)MathF.Round(position.Y),
                _size.Width,
                _size.Height);
        }
    }

    /// <summary>
    /// Gets or sets the overall control size while preserving its anchor position.
    /// </summary>
    public Size Size
    {
        get => _size;
        set
        {
            ValidateSize(value);

            if (_size == value)
                return;

            _size = value;
            Relayout();
        }
    }

    /// <summary>
    /// Gets the pages in their visible tab order.
    /// </summary>
    public IReadOnlyList<TabPageWidget> Tabs => _tabs.AsReadOnly();

    /// <summary>
    /// Gets the selected tab index, or <c>-1</c> when the control has no pages.
    /// </summary>
    public int SelectedIndex => _selectedIndex;

    /// <summary>
    /// Gets the selected page, or <see langword="null"/> when the control has no pages.
    /// </summary>
    public TabPageWidget? SelectedTab => _selectedIndex >= 0 && _selectedIndex < _tabs.Count
        ? _tabs[_selectedIndex]
        : null;

    /// <summary>
    /// Gets or sets the height of one tab header row in native pixels.
    /// </summary>
    public int TabHeight
    {
        get => _tabHeight;
        set
        {
            if (value < 20)
                throw new ArgumentOutOfRangeException(nameof(value), "The tab height must be at least 20 pixels.");

            if (_tabHeight == value)
                return;

            _tabHeight = value;
            Relayout();
        }
    }

    /// <summary>
    /// Gets or sets the number of header rows reserved when <see cref="AutoExpandRows"/> is disabled.
    /// </summary>
    /// <remarks>
    /// The default is one row. A fixed row count distributes tabs across that
    /// many rows and leaves unused cells empty when fewer tabs are present.
    /// </remarks>
    public int TabRowCount
    {
        get => _tabRowCount;
        set
        {
            if (value < 1)
                throw new ArgumentOutOfRangeException(nameof(value), "At least one tab row is required.");

            if (_tabRowCount == value)
                return;

            _tabRowCount = value;
            Relayout();
        }
    }

    /// <summary>
    /// Gets or sets whether the control adds rows as more tabs are added.
    /// </summary>
    /// <remarks>
    /// When enabled, <see cref="PreferredTabWidth"/> determines how many tabs
    /// fit in one row. When disabled, <see cref="TabRowCount"/> controls the
    /// number of rows.
    /// </remarks>
    public bool AutoExpandRows
    {
        get => _autoExpandRows;
        set
        {
            if (_autoExpandRows == value)
                return;

            _autoExpandRows = value;
            Relayout();
        }
    }

    /// <summary>
    /// Gets or sets the preferred header width used when rows expand automatically.
    /// </summary>
    public int PreferredTabWidth
    {
        get => _preferredTabWidth;
        set
        {
            if (value < 48)
                throw new ArgumentOutOfRangeException(nameof(value), "The preferred tab width must be at least 48 pixels.");

            if (_preferredTabWidth == value)
                return;

            _preferredTabWidth = value;
            Relayout();
        }
    }

    /// <summary>
    /// Gets the number of header rows currently occupied or reserved by the control.
    /// </summary>
    public int DisplayedTabRowCount => _displayedRowCount;

    /// <summary>
    /// Gets the number of tab cells displayed across each header row.
    /// </summary>
    public int DisplayedTabColumnCount => _displayedColumnCount;

    /// <summary>
    /// Gets or sets whether headers can be drag-reordered with a primary pointer.
    /// </summary>
    public bool IsTabReorderingEnabled
    {
        get => _isTabReorderingEnabled;
        set
        {
            if (_isTabReorderingEnabled == value)
                return;

            _isTabReorderingEnabled = value;

            foreach (TabHeaderWidget header in _headers.Values)
                header.IsDragEnabled = value;
        }
    }

    /// <summary>
    /// Gets or sets the pointer movement required to begin a tab reordering drag.
    /// </summary>
    public float TabReorderDragThresholdPx
    {
        get => _tabReorderDragThresholdPx;
        set
        {
            if (value < 0f)
                throw new ArgumentOutOfRangeException(nameof(value), "The tab reordering threshold cannot be negative.");

            if (Math.Abs(_tabReorderDragThresholdPx - value) < float.Epsilon)
                return;

            _tabReorderDragThresholdPx = value;

            foreach (TabHeaderWidget header in _headers.Values)
                header.DragThresholdPx = value;
        }
    }

    /// <summary>
    /// Adds a page to the end of the tab strip.
    /// </summary>
    /// <param name="title">The text displayed in the new tab header.</param>
    /// <param name="mnemonic">The optional <c>Alt</c>-key mnemonic that selects the new page.</param>
    /// <param name="nickname">The optional diagnostic nickname for the page.</param>
    /// <returns>The newly created page, which can own other widgets.</returns>
    public TabPageWidget AddTab(string title,
                                char? mnemonic = null,
                                string? nickname = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ValidateTitle(title);
        ValidateMnemonic(mnemonic);

        Rectangle initialHeaderBounds = new(Bounds.Location, new Size(1, TabHeight));
        Rectangle initialContentBounds = new(
            Bounds.Left,
            Bounds.Top + TabHeight,
            Bounds.Width,
            Math.Max(1, Bounds.Height - TabHeight));

        TabPageWidget page = Mode == DirectDrawingMode.View
            ? new TabPageWidget(RenderSurfaceHost, _view!, initialContentBounds, title, mnemonic, nickname)
            : new TabPageWidget(RenderSurfaceHost, _sceneLayer!, initialContentBounds, title, mnemonic, nickname);

        TabHeaderWidget header = Mode == DirectDrawingMode.View
            ? new TabHeaderWidget(RenderSurfaceHost, _view!, initialHeaderBounds, title, $"{page.Nickname}.header")
            : new TabHeaderWidget(RenderSurfaceHost, _sceneLayer!, initialHeaderBounds, title, $"{page.Nickname}.header");

        page.TitleChanged += OnPageTitleChanged;
        page.Disposing += OnPageDisposing;
        header.Invoked += OnHeaderInvoked;
        header.ReorderDragged += OnHeaderDragged;
        header.ReorderDragEnded += OnHeaderDragEnded;
        header.Disposing += OnHeaderDisposing;
        header.IsDragEnabled = IsTabReorderingEnabled;
        header.DragThresholdPx = TabReorderDragThresholdPx;

        _tabs.Add(page);
        _headers.Add(page, header);

        Add(page, Vector2.Zero);
        Add(header, Vector2.Zero);

        if (_selectedIndex < 0)
            _selectedIndex = 0;

        Relayout();
        UpdateSelectedTabVisuals();
        UpdatePageVisibility();

        return page;
    }

    /// <summary>
    /// Removes a page from the tab control.
    /// </summary>
    /// <param name="page">The page to remove.</param>
    /// <param name="dispose">Whether to dispose the page and its header after removal.</param>
    /// <returns><see langword="true"/> when the page was owned by this control; otherwise, <see langword="false"/>.</returns>
    public bool RemoveTab(TabPageWidget page,
                          bool dispose = false)
    {
        ArgumentNullException.ThrowIfNull(page);
        return RemoveTabCore(page, dispose, raiseSelectionChanged: true);
    }

    /// <summary>
    /// Selects the page at the supplied tab index.
    /// </summary>
    /// <param name="index">The zero-based tab index to select.</param>
    /// <returns>The current tab control.</returns>
    public TabControlWidget SelectTab(int index)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (index < 0 || index >= _tabs.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        if (_selectedIndex == index)
            return this;

        _selectedIndex = index;
        UpdateSelectedTabVisuals();
        UpdatePageVisibility();

        SelectedTabChanged?.Invoke(_tabs[index]);
        return this;
    }

    /// <summary>
    /// Selects the supplied page.
    /// </summary>
    /// <param name="page">The page to select.</param>
    /// <returns>The current tab control.</returns>
    public TabControlWidget SelectTab(TabPageWidget page)
    {
        ArgumentNullException.ThrowIfNull(page);

        int index = _tabs.IndexOf(page);
        if (index < 0)
            throw new ArgumentException("The page is not owned by this tab control.", nameof(page));

        return SelectTab(index);
    }

    /// <summary>
    /// Moves one page to a different tab index.
    /// </summary>
    /// <param name="fromIndex">The zero-based index of the page to move.</param>
    /// <param name="toIndex">The zero-based destination index.</param>
    /// <returns>The current tab control.</returns>
    public TabControlWidget MoveTab(int fromIndex,
                                    int toIndex)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (fromIndex < 0 || fromIndex >= _tabs.Count)
            throw new ArgumentOutOfRangeException(nameof(fromIndex));

        if (toIndex < 0 || toIndex >= _tabs.Count)
            throw new ArgumentOutOfRangeException(nameof(toIndex));

        if (fromIndex == toIndex)
            return this;

        TabPageWidget page = _tabs[fromIndex];
        TabPageWidget? selectedPage = SelectedTab;

        _tabs.RemoveAt(fromIndex);
        _tabs.Insert(toIndex, page);
        _selectedIndex = selectedPage is null ? -1 : _tabs.IndexOf(selectedPage);

        Relayout();
        TabReordered?.Invoke(page, fromIndex, toIndex);

        return this;
    }

    /// <summary>
    /// Moves the supplied page to a different tab index.
    /// </summary>
    /// <param name="page">The page to move.</param>
    /// <param name="toIndex">The zero-based destination index.</param>
    /// <returns>The current tab control.</returns>
    public TabControlWidget MoveTab(TabPageWidget page,
                                    int toIndex)
    {
        ArgumentNullException.ThrowIfNull(page);

        int fromIndex = _tabs.IndexOf(page);
        if (fromIndex < 0)
            throw new ArgumentException("The page is not owned by this tab control.", nameof(page));

        return MoveTab(fromIndex, toIndex);
    }

    /// <summary>
    /// Recomputes header and page layout after changing a size or tab-layout setting.
    /// </summary>
    /// <returns>The current tab control.</returns>
    public TabControlWidget Relayout()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        Rectangle bounds = Bounds;
        (_displayedRowCount, _displayedColumnCount) = ResolveGrid(bounds);
        Rectangle contentBounds = CreateContentBounds(bounds, _displayedRowCount);

        for (int index = 0; index < _tabs.Count; index++)
        {
            TabPageWidget page = _tabs[index];
            TabHeaderWidget header = _headers[page];
            Rectangle headerBounds = CreateHeaderBounds(bounds, index, _displayedColumnCount);

            header.SetBounds(headerBounds);
            page.HeaderBounds = headerBounds;
            page.SetContentBounds(contentBounds);

            SetLocalOffset(header, new Vector2(headerBounds.Left - bounds.Left, headerBounds.Top - bounds.Top));
            SetLocalOffset(page, new Vector2(contentBounds.Left - bounds.Left, contentBounds.Top - bounds.Top));
        }

        ApplyZOrder();
        return this;
    }

    /// <summary>
    /// Sets the tab header colors.
    /// </summary>
    /// <param name="normal">The normal unselected color.</param>
    /// <param name="hover">The unselected hover color.</param>
    /// <param name="pressed">The unselected pressed color.</param>
    /// <param name="selected">The selected tab color.</param>
    /// <returns>The current tab control.</returns>
    public TabControlWidget SetTabColors(Color normal,
                                         Color hover,
                                         Color pressed,
                                         Color selected)
    {
        _normalTabColor = normal;
        _hoverTabColor = hover;
        _pressedTabColor = pressed;
        _selectedTabColor = selected;
        UpdateSelectedTabVisuals();

        return this;
    }

    /// <summary>
    /// Sets the color used by tab-header labels.
    /// </summary>
    /// <param name="color">The new label color.</param>
    /// <returns>The current tab control.</returns>
    public TabControlWidget SetTabTextColor(Color color)
    {
        _tabTextColor = color;

        foreach (TabHeaderWidget header in _headers.Values)
            header.SetTextColor(color);

        return this;
    }

    /// <summary>
    /// Sets the base Z-order for all page and header visuals.
    /// </summary>
    /// <param name="zOrder">The Z-order applied to page backgrounds.</param>
    /// <returns>The current tab control.</returns>
    public TabControlWidget SetTabControlZOrder(int zOrder)
    {
        _baseZOrder = zOrder;
        ApplyZOrder();

        return this;
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        foreach (TabPageWidget page in _tabs)
        {
            page.TitleChanged -= OnPageTitleChanged;
            page.Disposing -= OnPageDisposing;

            TabHeaderWidget header = _headers[page];
            header.Invoked -= OnHeaderInvoked;
            header.ReorderDragged -= OnHeaderDragged;
            header.ReorderDragEnded -= OnHeaderDragEnded;
            header.Disposing -= OnHeaderDisposing;
        }

        base.Dispose();

        _tabs.Clear();
        _headers.Clear();
        SelectedTabChanged = null;
        TabReordered = null;
    }

    /// <inheritdoc/>
    protected override void ProcessShown()
    {
        base.ProcessShown();
        _isShown = true;
        UpdatePageVisibility();
    }

    /// <inheritdoc/>
    protected override void ProcessHidden()
    {
        _isShown = false;
        base.ProcessHidden();
    }

    /// <inheritdoc/>
    protected override void ProcessActivated()
    {
        base.ProcessActivated();

        SelectedTab?.Activate();
    }

    /// <inheritdoc/>
    protected override void OnKeyboardInput(WidgetKeyboardEventArgs args)
    {
        base.OnKeyboardInput(args);
        HandleKeyboardInput(args);
    }

    /// <inheritdoc/>
    void IWidgetKeyboardFallback.HandleUnhandledKeyboardInput(WidgetKeyboardEventArgs args)
    {
        HandleKeyboardInput(args);
    }

    private void InitializeInput()
    {
        IsPointerInputEnabled = false;
        IsKeyboardInputEnabled = true;
        CanReceiveFocus = false;
    }

    private bool RemoveTabCore(TabPageWidget page,
                               bool dispose,
                               bool raiseSelectionChanged)
    {
        int removedIndex = _tabs.IndexOf(page);
        if (removedIndex < 0)
            return false;

        TabPageWidget? previouslySelected = SelectedTab;
        TabHeaderWidget header = _headers[page];

        page.TitleChanged -= OnPageTitleChanged;
        page.Disposing -= OnPageDisposing;
        header.Invoked -= OnHeaderInvoked;
        header.ReorderDragged -= OnHeaderDragged;
        header.ReorderDragEnded -= OnHeaderDragEnded;
        header.Disposing -= OnHeaderDisposing;

        _tabs.RemoveAt(removedIndex);
        _headers.Remove(page);

        Remove(header);
        Remove(page);

        if (dispose)
        {
            header.Dispose();
            page.Dispose();
        }

        if (_tabs.Count == 0)
        {
            _selectedIndex = -1;
        }
        else if (ReferenceEquals(previouslySelected, page))
        {
            _selectedIndex = Math.Min(removedIndex, _tabs.Count - 1);
        }
        else
        {
            _selectedIndex = previouslySelected is null ? 0 : _tabs.IndexOf(previouslySelected);
        }

        Relayout();
        UpdateSelectedTabVisuals();
        UpdatePageVisibility();

        if (raiseSelectionChanged && SelectedTab is not null && !ReferenceEquals(previouslySelected, SelectedTab))
            SelectedTabChanged?.Invoke(SelectedTab);

        return true;
    }

    private void OnPageTitleChanged(TabPageWidget page)
    {
        if (_headers.TryGetValue(page, out TabHeaderWidget? header))
            header.SetText(page.Title);
    }

    private void OnPageDisposing(object? sender,
                                 IDirectDrawable _)
    {
        if (!_disposed && sender is TabPageWidget page)
            RemoveTabCore(page, dispose: true, raiseSelectionChanged: true);
    }

    private void OnHeaderDisposing(object? sender,
                                   IDirectDrawable _)
    {
        if (_disposed || sender is not TabHeaderWidget header)
            return;

        TabPageWidget? page = _headers.FirstOrDefault(pair => ReferenceEquals(pair.Value, header)).Key;
        if (page is not null)
            RemoveTabCore(page, dispose: true, raiseSelectionChanged: true);
    }

    private void OnHeaderInvoked(TabHeaderWidget header)
    {
        TabPageWidget? page = _headers.FirstOrDefault(pair => ReferenceEquals(pair.Value, header)).Key;
        if (page is not null)
            SelectTab(page);
    }

    private void OnHeaderDragged(TabHeaderWidget header,
                                 WidgetDragEventArgs args,
                                 View view)
    {
        if (!IsTabReorderingEnabled)
            return;

        TabPageWidget? page = _headers.FirstOrDefault(pair => ReferenceEquals(pair.Value, header)).Key;
        if (page is null)
            return;

        int fromIndex = _tabs.IndexOf(page);
        int targetIndex = GetHeaderIndexAt(args.CurrentScreenPositionPx, view);

        if (targetIndex >= 0 && targetIndex != fromIndex)
            MoveTab(fromIndex, targetIndex);
    }

    private void OnHeaderDragEnded(TabHeaderWidget _)
    {
        Relayout();
    }

    private int GetHeaderIndexAt(PointF screenPositionPx,
                                 View view)
    {
        Point screenPosition = Point.Round(screenPositionPx);

        for (int index = 0; index < _tabs.Count; index++)
        {
            TabHeaderWidget header = _headers[_tabs[index]];
            if (Mode == DirectDrawingMode.View
                ? header.Bounds.Contains(screenPosition)
                : header.HitTest(view, screenPosition))
            {
                return index;
            }
        }

        return -1;
    }

    private void HandleKeyboardInput(WidgetKeyboardEventArgs args)
    {
        if (args.Handled || args.KeyAction != KeyAction.Pressed || args.Modifiers != KeyboardModifierState.Alt)
            return;

        for (int index = 0; index < _tabs.Count; index++)
        {
            if (!MatchesMnemonic(_tabs[index].Mnemonic, args.Key))
                continue;

            args.Handled = true;
            SelectTab(index);
            return;
        }
    }

    private void UpdateSelectedTabVisuals()
    {
        for (int index = 0; index < _tabs.Count; index++)
        {
            TabHeaderWidget header = _headers[_tabs[index]];
            bool selected = index == _selectedIndex;

            header.SetColors(
                selected ? _selectedTabColor : _normalTabColor,
                selected ? _selectedTabColor : _hoverTabColor,
                selected ? _selectedTabColor : _pressedTabColor);
            header.SetTextColor(_tabTextColor);
        }
    }

    private void UpdatePageVisibility()
    {
        for (int index = 0; index < _tabs.Count; index++)
        {
            TabPageWidget page = _tabs[index];
            bool isSelected = index == _selectedIndex;

            if (_isShown && isSelected)
            {
                page.Show();
                page.Activate();
            }
            else if (!isSelected || _isShown)
            {
                page.Hide();
            }
        }
    }

    private void ApplyZOrder()
    {
        foreach (TabPageWidget page in _tabs)
        {
            page.SetPageZOrder(_baseZOrder);
            _headers[page].SetHeaderZOrder(_baseZOrder + 100);
        }
    }

    private (int Rows, int Columns) ResolveGrid(Rectangle bounds)
    {
        if (!AutoExpandRows)
        {
            int columns = Math.Max(1, (int)Math.Ceiling(_tabs.Count / (double)TabRowCount));
            return (TabRowCount, columns);
        }

        int autoColumns = _tabs.Count == 0
            ? 1
            : Math.Min(_tabs.Count, Math.Max(1, bounds.Width / PreferredTabWidth));
        int autoRows = Math.Max(1, (int)Math.Ceiling(_tabs.Count / (double)autoColumns));

        return (autoRows, autoColumns);
    }

    private Rectangle CreateHeaderBounds(Rectangle bounds,
                                         int index,
                                         int columns)
    {
        int row = index / columns;
        int column = index % columns;
        int left = bounds.Left + column * bounds.Width / columns;
        int right = bounds.Left + (column + 1) * bounds.Width / columns;

        return Rectangle.FromLTRB(
            left,
            bounds.Top + row * TabHeight,
            right,
            bounds.Top + (row + 1) * TabHeight);
    }

    private Rectangle CreateContentBounds(Rectangle bounds,
                                          int rowCount)
    {
        int contentTop = bounds.Top + rowCount * TabHeight;

        return new Rectangle(
            bounds.Left,
            contentTop,
            bounds.Width,
            Math.Max(1, bounds.Bottom - contentTop));
    }

    private static Rectangle ValidateBounds(Rectangle bounds)
    {
        ValidateSize(bounds.Size);
        return bounds;
    }

    private static void ValidateSize(Size size)
    {
        if (size.Width <= 0 || size.Height <= DefaultTabHeight)
        {
            throw new ArgumentOutOfRangeException(
                nameof(size),
                size,
                "Tab-control bounds must be positive and taller than one tab row.");
        }
    }

    private void ValidateMnemonic(char? mnemonic)
    {
        if (mnemonic is not char value)
            return;

        if (_tabs.Any(tab => tab.Mnemonic is char existing &&
                             char.ToUpperInvariant(existing) == char.ToUpperInvariant(value)))
        {
            throw new ArgumentException($"Duplicate tab mnemonic '{value}'.", nameof(mnemonic));
        }
    }

    private static void ValidateTitle(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
    }

    private static bool MatchesMnemonic(char? mnemonic,
                                        int key)
    {
        return mnemonic is char character &&
               key >= 0 &&
               key <= char.MaxValue &&
               char.ToUpperInvariant(character) == char.ToUpperInvariant((char)key);
    }

    private sealed class TabHeaderWidget : DraggableWidgetBase
    {
        private Color _normalColor = Color.FromArgb(255, 60, 60, 68);
        private Color _hoverColor = Color.FromArgb(255, 78, 78, 88);
        private Color _pressedColor = Color.FromArgb(255, 42, 42, 48);

        /// <summary>
        /// Occurs when a header is selected by pointer or keyboard input.
        /// </summary>
        internal event Action<TabHeaderWidget>? Invoked;

        /// <summary>
        /// Occurs while a header is being dragged for tab reordering.
        /// </summary>
        internal event Action<TabHeaderWidget, WidgetDragEventArgs, View>? ReorderDragged;

        /// <summary>
        /// Occurs after a header drag has ended.
        /// </summary>
        internal event Action<TabHeaderWidget>? ReorderDragEnded;

        private View? _dragView;
        private bool _disposed;

        /// <summary>
        /// Initializes a view-level tab header.
        /// </summary>
        /// <param name="renderSurfaceHost">The render surface host that owns the header.</param>
        /// <param name="view">The view that renders the header.</param>
        /// <param name="bounds">The header bounds in view pixels.</param>
        /// <param name="text">The tab title.</param>
        /// <param name="nickname">The diagnostic nickname.</param>
        internal TabHeaderWidget(RenderSurfaceHostBase renderSurfaceHost,
                                 View view,
                                 Rectangle bounds,
                                 string text,
                                 string nickname)
            : base(renderSurfaceHost, DirectDrawingMode.View, bounds.Location, nickname)
        {
            ArgumentNullException.ThrowIfNull(view);

            Background = CreateBackground(renderSurfaceHost, view, bounds, nickname);
            Label = CreateLabel(renderSurfaceHost, view, bounds, text);
            CompleteInitialization();
        }

        /// <summary>
        /// Initializes a scene-layer tab header.
        /// </summary>
        /// <param name="renderSurfaceHost">The render surface host that owns the header.</param>
        /// <param name="sceneLayer">The scene layer that renders the header.</param>
        /// <param name="bounds">The header bounds in world pixels.</param>
        /// <param name="text">The tab title.</param>
        /// <param name="nickname">The diagnostic nickname.</param>
        internal TabHeaderWidget(RenderSurfaceHostBase renderSurfaceHost,
                                 SceneLayer sceneLayer,
                                 Rectangle bounds,
                                 string text,
                                 string nickname)
            : base(renderSurfaceHost, DirectDrawingMode.SceneLayer, bounds.Location, nickname)
        {
            ArgumentNullException.ThrowIfNull(sceneLayer);

            Background = CreateBackground(renderSurfaceHost, sceneLayer, bounds, nickname);
            Label = CreateLabel(renderSurfaceHost, sceneLayer, bounds, text);
            CompleteInitialization();
        }

        /// <summary>
        /// Gets the header background drawing.
        /// </summary>
        internal DirectRectangle Background { get; }

        /// <summary>
        /// Gets the header label drawing.
        /// </summary>
        internal TextBlock Label { get; }

        /// <summary>
        /// Gets the header bounds in its native coordinate space.
        /// </summary>
        internal Rectangle Bounds => Mode == DirectDrawingMode.View
            ? Background.ScreenBounds
            : Background.WorldBounds;

        /// <summary>
        /// Updates the header bounds.
        /// </summary>
        /// <param name="bounds">The new bounds in the header's native coordinate space.</param>
        internal void SetBounds(Rectangle bounds)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
                throw new ArgumentOutOfRangeException(nameof(bounds));

            if (Mode == DirectDrawingMode.View)
            {
                Background.ScreenBounds = bounds;
                Label.ScreenBounds = bounds;
            }
            else
            {
                Background.WorldBounds = bounds;
                Label.WorldBounds = bounds;
            }

            SetPosition(bounds.Location.X, bounds.Location.Y);
        }

        /// <summary>
        /// Updates the header label text.
        /// </summary>
        /// <param name="text">The new tab title.</param>
        internal void SetText(string text)
        {
            Label.SetText(text);
        }

        /// <summary>
        /// Updates the normal, hover, and pressed header colors.
        /// </summary>
        /// <param name="normal">The normal color.</param>
        /// <param name="hover">The hover color.</param>
        /// <param name="pressed">The pressed color.</param>
        internal void SetColors(Color normal,
                                Color hover,
                                Color pressed)
        {
            _normalColor = normal;
            _hoverColor = hover;
            _pressedColor = pressed;
            UpdateBackgroundColor(normal);
        }

        /// <summary>
        /// Updates the header label color.
        /// </summary>
        /// <param name="color">The new text color.</param>
        internal void SetTextColor(Color color)
        {
            Label.SetColors(color, Color.Transparent);
        }

        /// <summary>
        /// Sets the header background Z-order.
        /// </summary>
        /// <param name="zOrder">The background Z-order.</param>
        internal void SetHeaderZOrder(int zOrder)
        {
            Background.ZOrder = zOrder;
            Label.ZOrder = zOrder + 1;
        }

        /// <inheritdoc/>
        protected override Vector2 ConvertScreenDeltaToPositionDelta(Vector2 totalScreenDeltaPx,
                                                                       View view)
        {
            return Vector2.Zero;
        }

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
            {
                _dragView = args.View;
                UpdateBackgroundColor(_pressedColor);
            }
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
            Invoked?.Invoke(this);
        }

        /// <inheritdoc/>
        protected override void OnKeyboardInput(WidgetKeyboardEventArgs args)
        {
            base.OnKeyboardInput(args);

            if (args.KeyAction != KeyAction.Pressed || args.Key is not 13 and not 32)
                return;

            args.Handled = true;
            Invoked?.Invoke(this);
        }

        /// <inheritdoc/>
        protected override void OnDragged(WidgetDragEventArgs args)
        {
            base.OnDragged(args);
            if (_dragView is not null)
                ReorderDragged?.Invoke(this, args, _dragView);
        }

        /// <inheritdoc/>
        protected override void OnDragEnded(WidgetDragEventArgs args)
        {
            base.OnDragEnded(args);
            ReorderDragEnded?.Invoke(this);
            _dragView = null;
        }

        /// <inheritdoc/>
        public override void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            base.Dispose();
            Invoked = null;
            ReorderDragged = null;
            ReorderDragEnded = null;
        }

        private void CompleteInitialization()
        {
            Add(Background);
            Add(Label);

            SetHeaderZOrder(0);
            CanReceiveFocus = true;
            IsKeyboardInputEnabled = true;
            IsDragEnabled = false;
        }

        private void UpdateBackgroundColor(Color color)
        {
            Background.SetColor(color);
            Background.SetPosition(Background.GetPosition());
        }

        private static DirectRectangle CreateBackground(RenderSurfaceHostBase renderSurfaceHost,
                                                        View view,
                                                        Rectangle bounds,
                                                        string nickname)
        {
            return new DirectRectangle(Color.FromArgb(255, 60, 60, 68),
                                       renderSurfaceHost,
                                       view,
                                       bounds,
                                       $"{nickname}.background")
                .SetFilled(true)
                .SetBorderColor(Color.FromArgb(255, 150, 150, 160))
                .SetStrokeWidth(1.5f)
                .SetCornerRadius(5f);
        }

        private static DirectRectangle CreateBackground(RenderSurfaceHostBase renderSurfaceHost,
                                                        SceneLayer sceneLayer,
                                                        Rectangle bounds,
                                                        string nickname)
        {
            return new DirectRectangle(Color.FromArgb(255, 60, 60, 68),
                                       renderSurfaceHost,
                                       sceneLayer,
                                       bounds,
                                       $"{nickname}.background")
                .SetFilled(true)
                .SetBorderColor(Color.FromArgb(255, 150, 150, 160))
                .SetStrokeWidth(1.5f)
                .SetCornerRadius(5f);
        }

        private static TextBlock CreateLabel(RenderSurfaceHostBase renderSurfaceHost,
                                             View view,
                                             Rectangle bounds,
                                             string text)
        {
            return new TextBlock(renderSurfaceHost, view, bounds)
                .SetText(text)
                .SetFont(SKTypeface.Default, 16f, minSize: 10f)
                .SetColors(SKColors.White, SKColors.Transparent)
                .SetAlignment(SKTextAlign.Center, TextBlock.VerticalAlign.Center)
                .EnableWrapping(false);
        }

        private static TextBlock CreateLabel(RenderSurfaceHostBase renderSurfaceHost,
                                             SceneLayer sceneLayer,
                                             Rectangle bounds,
                                             string text)
        {
            return new TextBlock(renderSurfaceHost, sceneLayer, view: null, worldBounds: bounds)
                .SetText(text)
                .SetFont(SKTypeface.Default, 16f, minSize: 10f)
                .SetColors(SKColors.White, SKColors.Transparent)
                .SetAlignment(SKTextAlign.Center, TextBlock.VerticalAlign.Center)
                .EnableWrapping(false);
        }
    }
}
