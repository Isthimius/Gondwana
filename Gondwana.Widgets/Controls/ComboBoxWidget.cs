using System.Drawing;
using System.Numerics;
using Gondwana.Drawing.Direct;
using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;
using SkiaSharp;

namespace Gondwana.Widgets.Controls;

/// <summary>
/// Provides a compact selection control that expands into a <see cref="ListBoxWidget"/>.
/// </summary>
public sealed class ComboBoxWidget : ContainerWidget
{
    private const int DefaultDropDownHeight = 144;

    private readonly DropDownChevronDrawing _chevron;
    private bool _isDropDownOpen;
    private bool _disposed;
    private string _placeholder = "Select...";

    /// <summary>
    /// Occurs when <see cref="SelectedIndex"/> changes.
    /// </summary>
    public event Action<int>? SelectedIndexChanged;

    /// <summary>
    /// Creates a view-level combo box.
    /// </summary>
    /// <param name="renderSurfaceHost">The render-surface host that owns the drawing.</param>
    /// <param name="view">The view used for presentation and coordinate conversion.</param>
    /// <param name="bounds">The widget bounds in view-local pixels.</param>
    /// <param name="items">The items.</param>
    /// <param name="dropDownHeight">The drop down height.</param>
    /// <param name="nickname">An optional name used to identify the object.</param>
    public ComboBoxWidget(RenderSurfaceHostBase renderSurfaceHost,
                          View view,
                          Rectangle bounds,
                          IEnumerable<string>? items = null,
                          int dropDownHeight = DefaultDropDownHeight,
                          string? nickname = null)
        : base(renderSurfaceHost, DirectDrawingMode.View, ValidateBounds(bounds).Location, nickname)
    {
        ArgumentNullException.ThrowIfNull(view);
        ValidateDropDownHeight(dropDownHeight);

        Header = new ButtonWidget(renderSurfaceHost, view, bounds, string.Empty, $"{Nickname}.header");
        DropDown = new ListBoxWidget(renderSurfaceHost,
                                     view,
                                     new Rectangle(bounds.X, bounds.Bottom, bounds.Width, dropDownHeight),
                                     items,
                                     $"{Nickname}.list");
        _chevron = new DropDownChevronDrawing(
            renderSurfaceHost,
            view,
            ResolveChevronBounds(bounds),
            $"{Nickname}.chevron");

        CompleteInitialization(bounds);
    }

    /// <summary>
    /// Creates a scene-layer combo box.
    /// </summary>
    /// <param name="renderSurfaceHost">The render-surface host that owns the drawing.</param>
    /// <param name="sceneLayer">The scene layer that owns the content.</param>
    /// <param name="bounds">The widget bounds in world pixels.</param>
    /// <param name="items">The items.</param>
    /// <param name="dropDownHeight">The drop down height.</param>
    /// <param name="nickname">An optional name used to identify the object.</param>
    public ComboBoxWidget(RenderSurfaceHostBase renderSurfaceHost,
                          SceneLayer sceneLayer,
                          Rectangle bounds,
                          IEnumerable<string>? items = null,
                          int dropDownHeight = DefaultDropDownHeight,
                          string? nickname = null)
        : base(renderSurfaceHost, DirectDrawingMode.SceneLayer, ValidateBounds(bounds).Location, nickname)
    {
        ArgumentNullException.ThrowIfNull(sceneLayer);
        ValidateDropDownHeight(dropDownHeight);

        Header = new ButtonWidget(renderSurfaceHost, sceneLayer, bounds, string.Empty, $"{Nickname}.header");
        DropDown = new ListBoxWidget(renderSurfaceHost,
                                     sceneLayer,
                                     new Rectangle(bounds.X, bounds.Bottom, bounds.Width, dropDownHeight),
                                     items,
                                     $"{Nickname}.list");
        _chevron = new DropDownChevronDrawing(
            renderSurfaceHost,
            sceneLayer,
            ResolveChevronBounds(bounds),
            $"{Nickname}.chevron");

        CompleteInitialization(bounds);
    }

    /// <summary>
    /// Gets the collapsed button portion of the combo box.
    /// </summary>
    public ButtonWidget Header { get; }

    /// <summary>
    /// Gets the list displayed while the combo box is expanded.
    /// </summary>
    public ListBoxWidget DropDown { get; }

    /// <summary>
    /// Gets the available items.
    /// </summary>
    public IReadOnlyList<string> Items => DropDown.Items;

    /// <summary>
    /// Gets whether the drop-down list is currently open.
    /// </summary>
    public bool IsDropDownOpen => _isDropDownOpen;

    /// <summary>
    /// Gets or sets the selected item index. Use -1 to clear selection.
    /// </summary>
    public int SelectedIndex
    {
        get => DropDown.SelectedIndex;
        set => DropDown.SelectedIndex = value;
    }

    /// <summary>
    /// Gets the selected item text, or <see langword="null"/>.
    /// </summary>
    public string? SelectedItem => DropDown.SelectedItem;

    /// <summary>
    /// Gets or sets the text displayed while no item is selected.
    /// </summary>
    public string Placeholder
    {
        get => _placeholder;
        set
        {
            _placeholder = value ?? string.Empty;
            RefreshHeaderText();
        }
    }

    /// <summary>
    /// Replaces all available items and clears the current selection.
    /// </summary>
    /// <param name="items">The items.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public ComboBoxWidget SetItems(IEnumerable<string> items)
    {
        DropDown.SetItems(items);
        RefreshHeaderText();
        return this;
    }

    /// <summary>
    /// Adds an item to the end of the combo box.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public ComboBoxWidget AddItem(string item)
    {
        DropDown.AddItem(item);
        return this;
    }

    /// <summary>
    /// Opens the drop-down list when items are available.
    /// </summary>
    /// <returns>This instance for fluent chaining.</returns>
    public ComboBoxWidget OpenDropDown()
    {
        if (_isDropDownOpen || Items.Count == 0)
            return this;

        _isDropDownOpen = true;
        DropDown.Show();
        DropDown.Activate();
        WidgetInputRouterRegistry.TryFocus(DropDown);
        return this;
    }

    /// <summary>
    /// Closes the drop-down list.
    /// </summary>
    /// <returns>This instance for fluent chaining.</returns>
    public ComboBoxWidget CloseDropDown()
    {
        CollapseDropDown(restoreHeaderFocus: true);
        return this;
    }

    /// <summary>
    /// Toggles the drop-down list.
    /// </summary>
    /// <returns>This widget for fluent chaining.</returns>
    public ComboBoxWidget ToggleDropDown()
    {
        return IsDropDownOpen ? CloseDropDown() : OpenDropDown();
    }

    /// <summary>
    /// Sets the base Z-order used by the combo-box visuals.
    /// </summary>
    /// <param name="zOrder">The drawing order relative to other content.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public ComboBoxWidget SetComboBoxZOrder(int zOrder)
    {
        Header.SetButtonZOrder(zOrder);
        _chevron.ZOrder = zOrder + 2;
        DropDown.SetListBoxZOrder(zOrder + 10);
        return this;
    }

    /// <summary>
    /// Sets the collapsed header text and chevron color.
    /// </summary>
    /// <param name="color">The color to apply.</param>
    /// <returns>This instance for fluent chaining.</returns>
    public ComboBoxWidget SetHeaderTextColor(Color color)
    {
        Header.SetTextColor(color);
        _chevron.SetColor(color);
        return this;
    }

    /// <inheritdoc/>
    public override void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Header.Clicked -= OnHeaderClicked;
        DropDown.SelectedIndexChanged -= OnSelectedIndexChanged;
        DropDown.SelectionCommitted -= OnDropDownSelectionCommitted;
        DropDown.FocusLost -= OnDropDownFocusLost;
        base.Dispose();
    }

    /// <inheritdoc/>
    protected override void ProcessShown()
    {
        base.ProcessShown();

        if (!_isDropDownOpen)
            DropDown.Hide();
    }

    /// <inheritdoc/>
    protected override void ProcessHidden()
    {
        _isDropDownOpen = false;
        base.ProcessHidden();
    }

    private void CompleteInitialization(Rectangle bounds)
    {
        ConfigureHeaderContentBounds(bounds);

        Add(Header, Vector2.Zero);
        Add(_chevron);
        Add(DropDown, new Vector2(0f, bounds.Height));
        Header.Clicked += OnHeaderClicked;
        DropDown.SelectedIndexChanged += OnSelectedIndexChanged;
        DropDown.SelectionCommitted += OnDropDownSelectionCommitted;
        DropDown.FocusLost += OnDropDownFocusLost;

        IsInputEnabled = false;
        IsPointerInputEnabled = false;
        IsKeyboardInputEnabled = false;
        CanReceiveFocus = false;

        DropDown.Hide();
        SetComboBoxZOrder(0);
        RefreshHeaderText();
    }

    private void OnHeaderClicked()
    {
        ToggleDropDown();
    }

    private void OnSelectedIndexChanged(int index)
    {
        RefreshHeaderText();
        SelectedIndexChanged?.Invoke(index);
    }

    private void OnDropDownSelectionCommitted(int index)
    {
        if (index >= 0)
            CloseDropDown();
    }

    private void OnDropDownFocusLost()
    {
        if (!_isDropDownOpen)
            return;

        // Pointer-down moves focus before the header click is dispatched. If focus
        // moved back to this combo's own header, leave the list open long enough
        // for that click to toggle it closed normally. Any other focus target means
        // the user has moved on, so collapse without stealing focus back.
        WidgetBase? focusedWidget = WidgetInputRouterRegistry.GetFocusedWidget(DropDown);
        if (ReferenceEquals(focusedWidget, Header) || ReferenceEquals(focusedWidget, DropDown))
            return;

        CollapseDropDown(restoreHeaderFocus: false);
    }

    private void CollapseDropDown(bool restoreHeaderFocus)
    {
        if (!_isDropDownOpen)
            return;

        _isDropDownOpen = false;
        DropDown.Hide();

        if (!restoreHeaderFocus)
            return;

        Header.Activate();
        WidgetInputRouterRegistry.TryFocus(Header);
    }

    private void RefreshHeaderText()
    {
        Header.SetText(SelectedItem ?? Placeholder);
    }

    private void ConfigureHeaderContentBounds(Rectangle bounds)
    {
        Rectangle labelBounds = new(
            bounds.Left + 4,
            bounds.Top,
            Math.Max(1, bounds.Width - 28),
            bounds.Height);

        if (Mode == DirectDrawingMode.View)
            Header.Label.ScreenBounds = labelBounds;
        else
            Header.Label.WorldBounds = labelBounds;
    }

    private static Rectangle ResolveChevronBounds(Rectangle bounds)
    {
        const int width = 18;
        return new Rectangle(
            Math.Max(bounds.Left, bounds.Right - width - 4),
            bounds.Top,
            Math.Min(width, bounds.Width),
            bounds.Height);
    }

    private static Rectangle ValidateBounds(Rectangle bounds)
    {
        if (bounds.Width < 60 || bounds.Height < 20)
            throw new ArgumentOutOfRangeException(nameof(bounds), bounds, "Combo-box bounds are too small.");

        return bounds;
    }

    private static void ValidateDropDownHeight(int height)
    {
        if (height < 28)
            throw new ArgumentOutOfRangeException(nameof(height), "Drop-down height must be at least 28 pixels.");
    }

    private sealed class DropDownChevronDrawing : DirectDrawingMovableBase
    {
        private SKColor _color = SKColors.White;

        internal DropDownChevronDrawing(
            RenderSurfaceHostBase host,
            View view,
            Rectangle bounds,
            string nickname)
            : base(host, DirectDrawingMode.View, null, view, bounds, null, nickname)
        {
        }

        internal DropDownChevronDrawing(
            RenderSurfaceHostBase host,
            SceneLayer sceneLayer,
            Rectangle bounds,
            string nickname)
            : base(host, DirectDrawingMode.SceneLayer, sceneLayer, null, null, bounds, nickname)
        {
        }

        internal void SetColor(Color color)
        {
            _color = new SKColor(color.R, color.G, color.B, color.A);
            ForceRefresh();
        }

        /// <inheritdoc/>
        protected override void OnDraw(BackbufferBase backbuffer, RectangleF destRectScreen)
        {
            float width = Math.Min(8f, destRectScreen.Width - 4f);
            float centerX = destRectScreen.Left + destRectScreen.Width / 2f;
            float centerY = destRectScreen.Top + destRectScreen.Height / 2f;

            using var paint = new SKPaint
            {
                Color = _color,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1.75f,
                StrokeCap = SKStrokeCap.Round,
                StrokeJoin = SKStrokeJoin.Round
            };
            using var path = new SKPath();

            path.MoveTo(centerX - width / 2f, centerY - 2f);
            path.LineTo(centerX, centerY + 2f);
            path.LineTo(centerX + width / 2f, centerY - 2f);

            backbuffer.Canvas.DrawPath(path, paint);
        }
    }
}
