using System.Drawing;
using System.Numerics;
using Gondwana.Drawing.Direct;
using Gondwana.Rendering;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;

namespace Gondwana.Widgets.Controls;

/// <summary>
/// Provides a compact selection control that expands into a <see cref="ListBoxWidget"/>.
/// </summary>
public sealed class ComboBoxWidget : ContainerWidget
{
    private const int DefaultDropDownHeight = 144;

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

        CompleteInitialization(bounds);
    }

    /// <summary>
    /// Creates a scene-layer combo box.
    /// </summary>
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
    public ComboBoxWidget SetItems(IEnumerable<string> items)
    {
        DropDown.SetItems(items);
        RefreshHeaderText();
        return this;
    }

    /// <summary>
    /// Adds an item to the end of the combo box.
    /// </summary>
    public ComboBoxWidget AddItem(string item)
    {
        DropDown.AddItem(item);
        return this;
    }

    /// <summary>
    /// Opens the drop-down list when items are available.
    /// </summary>
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
    public ComboBoxWidget CloseDropDown()
    {
        if (!_isDropDownOpen)
            return this;

        _isDropDownOpen = false;
        DropDown.Hide();
        Header.Activate();
        WidgetInputRouterRegistry.TryFocus(Header);
        return this;
    }

    /// <summary>
    /// Toggles the drop-down list.
    /// </summary>
    public ComboBoxWidget ToggleDropDown()
    {
        return IsDropDownOpen ? CloseDropDown() : OpenDropDown();
    }

    /// <summary>
    /// Sets the base Z-order used by the combo-box visuals.
    /// </summary>
    public ComboBoxWidget SetComboBoxZOrder(int zOrder)
    {
        Header.SetButtonZOrder(zOrder);
        DropDown.SetListBoxZOrder(zOrder + 10);
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
        Add(Header, Vector2.Zero);
        Add(DropDown, new Vector2(0f, bounds.Height));

        Header.Clicked += OnHeaderClicked;
        DropDown.SelectedIndexChanged += OnSelectedIndexChanged;

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

        if (index >= 0)
            CloseDropDown();

        SelectedIndexChanged?.Invoke(index);
    }

    private void RefreshHeaderText()
    {
        string text = SelectedItem ?? Placeholder;
        Header.SetText($"{text}  ▼");
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
}
