using Gondwana.Rendering;
using Gondwana.Widgets.Controls;
using SkiaSharp;
using View = Gondwana.Rendering.Views.View;

namespace Gondwana.Demos.Slider;

/// <summary>
/// The entire Slider sidebar is drawn and input-routed by Gondwana.Widgets.
/// No WinForms controls are required inside the game window.
/// </summary>
internal sealed class SliderSidebar : IDisposable
{
    internal const int Width = 240;

    private static readonly Color BackgroundColor = Color.FromArgb(255, 31, 40, 55);
    private static readonly Color ButtonColor = Color.FromArgb(255, 54, 76, 109);
    private static readonly Color ButtonHoverColor = Color.FromArgb(255, 68, 96, 137);
    private static readonly Color ButtonPressedColor = Color.FromArgb(255, 42, 61, 88);
    private static readonly string[] Dimensions = Enumerable.Range(3, 18)
        .Select(static value => value.ToString())
        .ToArray();

    private readonly PanelWidget _panel;
    private readonly ComboBoxWidget _columns;
    private readonly ComboBoxWidget _rows;
    private readonly LabelWidget _status;
    private readonly LabelWidget _loadingLabel;
    private readonly ProgressBarWidget _loadingProgress;

    internal int Columns => int.TryParse(_columns.SelectedItem, out int value) ? value : 4;
    internal int Rows => int.TryParse(_rows.SelectedItem, out int value) ? value : 3;

    // Arrow keys belong to the selector while one is being operated, not to the puzzle.
    internal bool IsEditingDimensions =>
        _columns.IsDropDownOpen || _rows.IsDropDownOpen;

    internal SliderSidebar(RenderSurfaceHostBase host, View view,
        int height,
        Action onNewPuzzle,
        Action<int, int> onOpenPicture,
        Action onShuffle,
        Action<bool> onGridChanged)
    {
        _panel = new PanelWidget(host, view,
            new Rectangle(0, 0, Width, Math.Max(1, height)), BackgroundColor, "slider.sidebar")
            .SetPanelZOrder(1000);

        AddLabel(host, view, new Rectangle(18, 47, 208, 48), "SLIDER", 30f, SKColors.White, "slider.title");
        AddLabel(host, view, new Rectangle(18, 105, 204, 23), "Columns", 17f, SKColors.LightGray, "slider.columnsLabel");
        _columns = new ComboBoxWidget(host, view, new Rectangle(18, 129, 202, 33),
            Dimensions, dropDownHeight: 220, nickname: "slider.columns");
        _panel.AddWidget(_columns, new Point(18, 129));
        _columns.SetComboBoxZOrder(1200);
        _columns.SetHeaderTextColor(Color.White);
        _columns.SelectedIndex = 1; // 4 columns

        AddLabel(host, view, new Rectangle(18, 171, 204, 23), "Rows", 17f, SKColors.LightGray, "slider.rowsLabel");
        _rows = new ComboBoxWidget(host, view, new Rectangle(18, 195, 202, 33),
            Dimensions, dropDownHeight: 220, nickname: "slider.rows");
        _panel.AddWidget(_rows, new Point(18, 195));
        _rows.SetComboBoxZOrder(1200);
        _rows.SetHeaderTextColor(Color.White);
        _rows.SelectedIndex = 0; // 3 rows

        AddButton(host, view, 246, "New puzzle", "slider.newPuzzle", onNewPuzzle);
        AddButton(host, view, 291, "Open picture...", "slider.openPicture", () => onOpenPicture(Columns, Rows));
        AddButton(host, view, 336, "Shuffle", "slider.shuffle", onShuffle);

        var grid = new CheckBoxWidget(host, view, new Rectangle(18, 394, 205, 29),
            "Show piece outlines", isChecked: true, nickname: "slider.grid")
            .SetTextColor(SKColors.White);
        grid.CheckedChanged += onGridChanged;
        _panel.AddWidget(grid, new Point(18, 394));

        AddLabel(host, view, new Rectangle(18, 444, 207, 48),
            "Arrow keys or click a tile beside the empty square.", 15f, SKColors.LightGray, "slider.instructions")
            .EnableWrapping();

        _status = AddLabel(host, view, new Rectangle(18, 502, 205, 88),
            "Starting...", 17f, SKColors.White, "slider.status");
        _loadingLabel = AddLabel(host, view, new Rectangle(18, 590, 204, 21),
            "Loading puzzle...", 13f, SKColors.LightGray, "slider.loadingLabel");
        _loadingProgress = new ProgressBarWidget(host, view,
            new Rectangle(18, 615, 204, 12), nickname: "slider.loadingProgress")
            .SetProgressZOrder(1400);
        _panel.AddWidget(_loadingProgress, new Point(18, 615));

        _panel.Show();
        SetLoading(-1f);
    }

    internal void SetLoading(float progress)
    {
        bool show = progress >= 0f;
        if (!show)
        {
            _loadingLabel.Hide();
            _loadingProgress.Hide();
            return;
        }

        _loadingProgress.Value = Math.Clamp(progress, 0f, 1f);
        _loadingLabel.Show();
        _loadingProgress.Show();
    }

    internal void SetStatus(string status) => _status.SetText(status);

    internal void Resize(int height) => _panel.Size = new Size(Width, Math.Max(1, height));

    private LabelWidget AddLabel(RenderSurfaceHostBase host, View view,
        Rectangle bounds, string text, float size, SKColor color, string nickname)
    {
        var label = new LabelWidget(host, view, bounds, text, nickname)
            .SetFont(SKTypeface.Default, size)
            .SetColors(color, SKColors.Transparent);
        _panel.AddWidget(label, bounds.Location);
        return label;
    }

    private void AddButton(RenderSurfaceHostBase host, View view,
        int top, string text, string nickname, Action clicked)
    {
        var button = new ButtonWidget(host, view, new Rectangle(18, top, 202, 36),
            text, nickname)
            .SetBackgroundColors(ButtonColor, ButtonHoverColor, ButtonPressedColor)
            .SetTextColor(Color.White);
        button.Clicked += clicked;
        _panel.AddWidget(button, new Point(18, top));
    }

    public void Dispose() => _panel.Dispose();
}
