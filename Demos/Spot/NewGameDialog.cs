using System;
using System.Drawing;
using System.Linq;
using System.Numerics;
using Gondwana.Demos.Spot.Game;
using Gondwana.Rendering;
using Gondwana.Rendering.Views;
using Gondwana.Widgets;
using Gondwana.Widgets.Controls;
using Gondwana.Widgets.Dialogs;
using SkiaSharp;

namespace Gondwana.Demos.Spot;

internal sealed class NewGameDialog : DialogBox
{
    private const int TopRowLabelZOrder = 10_090;
    private const int TopRowComboZOrder = 10_100;
    private const int BoardSizeDropDownHeight = 244;

    private static readonly string[] PlayerCountItems = ["2", "3", "4"];
    private static readonly string[] BoardSizeItems = ["3", "4", "5", "6", "7", "8", "9", "10", "11", "12"];
    private static readonly string[] PlayerTypeItems = ["Human", "Computer"];
    private static readonly string[] DefaultPlayerNames = ["Eugene", "Ward", "Robert", "Patrick"];

    private static readonly ColorItem[] AvailableColors =
    [
        new("Red", SKColors.Red, SKColors.White),
        new("Blue", SKColors.Blue, SKColors.White),
        new("Yellow", SKColors.Yellow, SKColors.Blue),
        new("Violet", SKColors.Violet, SKColors.White),
        new("Green", SKColors.Green, SKColors.Black)
    ];

    private readonly ComboBoxWidget _playerCount;
    private readonly ComboBoxWidget _boardWidth;
    private readonly ComboBoxWidget _boardHeight;
    private readonly PanelWidget[] _playerPanels = new PanelWidget[4];
    private readonly LabelWidget[] _playerHeaders = new LabelWidget[4];
    private readonly TextBoxWidget[] _playerNames = new TextBoxWidget[4];
    private readonly ComboBoxWidget[] _playerTypes = new ComboBoxWidget[4];
    private readonly ComboBoxWidget[] _playerColors = new ComboBoxWidget[4];
    private readonly ButtonWidget _startButton;
    private readonly ButtonWidget _cancelButton;

    private bool _updatingColors;
    private bool _optionsCaptured;

    internal NewGameOptions Options { get; private set; }
    internal WidgetBase InitialFocusTarget => _playerNames[0];

    internal NewGameDialog(
        RenderSurfaceHostBase host,
        View view,
        NewGameOptions? initialOptions = null)
        : base(host, view, ResolveBounds(view), "New Game", showCloseButton: true, nickname: "spot.newGame")
    {
        Rectangle bounds = Panel.ScreenBounds;

        Panel.SetColor(Color.CornflowerBlue)
             .SetBorderColor(Color.FromArgb(255, 85, 100, 135))
             .SetCornerRadius(4f);
        TitleBar.SetColor(Color.FromArgb(255, 57, 76, 112))
                .SetCornerRadius(4f);

        var playersLabel = CreateLabel(host, view, Offset(bounds, 18, 48, 66, 28), "Players");
        playersLabel.TextBlock.ZOrder = TopRowLabelZOrder;
        Add(playersLabel, keepCurrentOffset: false, explicitLocalOffsetPx: new Vector2(18, 48));

        _playerCount = new ComboBoxWidget(
            host,
            view,
            Offset(bounds, 82, 48, 72, 28),
            PlayerCountItems,
            dropDownHeight: 84,
            nickname: "spot.newGame.playerCount");
        _playerCount.SetComboBoxZOrder(TopRowComboZOrder);
        Add(_playerCount, keepCurrentOffset: false, explicitLocalOffsetPx: new Vector2(82, 48));

        var boardLabel = CreateLabel(host, view, Offset(bounds, 190, 48, 90, 28), "Board Size");
        boardLabel.TextBlock.ZOrder = TopRowLabelZOrder;
        Add(boardLabel, keepCurrentOffset: false, explicitLocalOffsetPx: new Vector2(190, 48));

        _boardWidth = new ComboBoxWidget(
            host,
            view,
            Offset(bounds, 282, 48, 70, 28),
            BoardSizeItems,
            dropDownHeight: BoardSizeDropDownHeight,
            nickname: "spot.newGame.boardWidth");
        _boardWidth.SetComboBoxZOrder(TopRowComboZOrder);
        Add(_boardWidth, keepCurrentOffset: false, explicitLocalOffsetPx: new Vector2(282, 48));

        var byLabel = CreateLabel(host, view, Offset(bounds, 356, 48, 24, 28), "×")
            .SetAlignment(SKTextAlign.Center, Gondwana.Drawing.Direct.TextBlock.VerticalAlign.Center);
        byLabel.TextBlock.ZOrder = TopRowLabelZOrder;
        Add(byLabel, keepCurrentOffset: false, explicitLocalOffsetPx: new Vector2(356, 48));

        _boardHeight = new ComboBoxWidget(
            host,
            view,
            Offset(bounds, 384, 48, 70, 28),
            BoardSizeItems,
            dropDownHeight: BoardSizeDropDownHeight,
            nickname: "spot.newGame.boardHeight");
        _boardHeight.SetComboBoxZOrder(TopRowComboZOrder);
        Add(_boardHeight, keepCurrentOffset: false, explicitLocalOffsetPx: new Vector2(384, 48));

        for (int i = 0; i < 4; i++)
            CreatePlayerRow(host, view, bounds, i);

        _startButton = new ButtonWidget(
            host,
            view,
            Offset(bounds, 18, 338, 228, 34),
            "Start",
            "spot.newGame.start")
            .SetBackgroundColors(
                Color.FromArgb(255, 46, 80, 122),
                Color.FromArgb(255, 57, 97, 147),
                Color.FromArgb(255, 35, 65, 100))
            .SetTextColor(Color.White)
            .SetButtonZOrder(10_020);
        _startButton.Clicked += OnStartClicked;
        Add(_startButton, keepCurrentOffset: false, explicitLocalOffsetPx: new Vector2(18, 338));

        _cancelButton = new ButtonWidget(
            host,
            view,
            Offset(bounds, 252, 338, 228, 34),
            "Cancel",
            "spot.newGame.cancel")
            .SetBackgroundColors(
                Color.FromArgb(255, 52, 62, 80),
                Color.FromArgb(255, 68, 78, 98),
                Color.FromArgb(255, 40, 48, 64))
            .SetTextColor(Color.White)
            .SetButtonZOrder(10_020);
        _cancelButton.Clicked += OnCancelClicked;
        Add(_cancelButton, keepCurrentOffset: false, explicitLocalOffsetPx: new Vector2(252, 338));

        Options = CreateDefaultOptions();

        ApplyDefaults();

        if (initialOptions is not null)
            ApplyInitialOptions(initialOptions);

        HookEvents();

        for (int i = 0; i < _playerColors.Length; i++)
            OnColorSelectionChanged(i);

        UpdatePlayerVisibility();
        RefreshAllColorHeaders();
    }

    private void CreatePlayerRow(RenderSurfaceHostBase host, View view, Rectangle dialogBounds, int index)
    {
        int y = 84 + index * 62;
        var panelBounds = Offset(dialogBounds, 18, y, 462, 56);

        var panel = new PanelWidget(
            host,
            view,
            panelBounds,
            Color.FromArgb(24, 255, 255, 255),
            $"spot.newGame.player{index + 1}")
            .SetBorderColor(Color.FromArgb(185, 70, 86, 120))
            .SetStrokeWidth(1.25f)
            .SetCornerRadius(4f)
            .SetPanelZOrder(10_004);

        _playerPanels[index] = panel;
        Add(panel, keepCurrentOffset: false, explicitLocalOffsetPx: new Vector2(18, y));

        var header = new LabelWidget(
            host,
            view,
            new Rectangle(panelBounds.Left + 8, panelBounds.Top + 2, 180, 18),
            DefaultPlayerNames[index],
            $"spot.newGame.player{index + 1}.header")
            .SetFont(SKTypeface.Default, 13f)
            .SetColors(SKColors.White, SKColors.Transparent);
        _playerHeaders[index] = header;
        panel.AddWidget(header, new Point(8, 2));

        var name = new TextBoxWidget(
            host,
            view,
            new Rectangle(panelBounds.Left + 8, panelBounds.Top + 23, 190, 27),
            DefaultPlayerNames[index],
            nickname: $"spot.newGame.player{index + 1}.name");
        name.MaxLength = 24;
        name.SetTextBoxZOrder(10_010);
        _playerNames[index] = name;
        panel.AddWidget(name, new Point(8, 23));

        var type = new ComboBoxWidget(
            host,
            view,
            new Rectangle(panelBounds.Left + 204, panelBounds.Top + 23, 120, 27),
            PlayerTypeItems,
            dropDownHeight: 60,
            nickname: $"spot.newGame.player{index + 1}.type");
        _playerTypes[index] = type;
        panel.AddWidget(type, new Point(204, 23));
        type.SetComboBoxZOrder(10_030);

        var color = new ComboBoxWidget(
            host,
            view,
            new Rectangle(panelBounds.Left + 330, panelBounds.Top + 23, 124, 27),
            AvailableColors.Select(static item => item.Name),
            dropDownHeight: 130,
            nickname: $"spot.newGame.player{index + 1}.color");
        _playerColors[index] = color;
        panel.AddWidget(color, new Point(330, 23));
        color.SetComboBoxZOrder(10_040);
    }

    private void ApplyDefaults()
    {
        _playerCount.SelectedIndex = 2; // 4 players
        _boardWidth.SelectedIndex = 5;  // 8
        _boardHeight.SelectedIndex = 5; // 8

        _playerTypes[0].SelectedIndex = 0; // Human
        for (int i = 1; i < 4; i++)
            _playerTypes[i].SelectedIndex = 1; // Computer

        for (int i = 0; i < 4; i++)
            _playerColors[i].SelectedIndex = i;
    }

    private void ApplyInitialOptions(NewGameOptions options)
    {
        SetComboSelection(_playerCount, options.PlayerCount.ToString());
        SetComboSelection(_boardWidth, options.BoardWidth.ToString());
        SetComboSelection(_boardHeight, options.BoardHeight.ToString());

        for (int i = 0; i < options.Players.Count && i < 4; i++)
        {
            Player player = options.Players[i];
            _playerNames[i].SetText(player.Name);
            _playerHeaders[i].SetText(player.Name);
            _playerTypes[i].SelectedIndex = player.Type == PlayerType.Human ? 0 : 1;

            int colorIndex = Array.FindIndex(
                AvailableColors,
                candidate => candidate.Color == player.ColorItem.Color);

            if (colorIndex >= 0)
                _playerColors[i].SelectedIndex = colorIndex;
        }
    }

    private void HookEvents()
    {
        _playerCount.SelectedIndexChanged += _ => UpdatePlayerVisibility();

        for (int i = 0; i < 4; i++)
        {
            int playerIndex = i;
            _playerNames[i].TextChanged += text =>
                _playerHeaders[playerIndex].SetText(string.IsNullOrWhiteSpace(text)
                    ? $"Player {playerIndex + 1}"
                    : text);
            _playerNames[i].Submitted += _ => OnStartClicked();

            _playerColors[i].SelectedIndexChanged += _ =>
                OnColorSelectionChanged(playerIndex);
        }
    }

    protected override void ProcessShown()
    {
        base.ProcessShown();
        UpdatePlayerVisibility();
    }

    private void UpdatePlayerVisibility()
    {
        int playerCount = ParseSelectedInt(_playerCount, 4);

        for (int i = 0; i < _playerPanels.Length; i++)
        {
            if (i < playerCount)
                _playerPanels[i].Show();
            else
                _playerPanels[i].Hide();
        }
    }

    private void OnColorSelectionChanged(int changedIndex)
    {
        if (_updatingColors)
            return;

        _updatingColors = true;
        try
        {
            int selectedIndex = _playerColors[changedIndex].SelectedIndex;
            if (selectedIndex < 0)
                return;

            for (int i = 0; i < _playerColors.Length; i++)
            {
                if (i == changedIndex || _playerColors[i].SelectedIndex != selectedIndex)
                    continue;

                int replacement = FindUnusedColorIndex(i);
                if (replacement >= 0)
                    _playerColors[i].SelectedIndex = replacement;
            }

            RefreshAllColorHeaders();
        }
        finally
        {
            _updatingColors = false;
        }
    }

    private int FindUnusedColorIndex(int comboIndex)
    {
        for (int candidate = 0; candidate < AvailableColors.Length; candidate++)
        {
            bool alreadyUsed = false;

            for (int other = 0; other < _playerColors.Length; other++)
            {
                if (other == comboIndex)
                    continue;

                if (_playerColors[other].SelectedIndex == candidate)
                {
                    alreadyUsed = true;
                    break;
                }
            }

            if (!alreadyUsed)
                return candidate;
        }

        return -1;
    }

    private void RefreshAllColorHeaders()
    {
        for (int i = 0; i < _playerColors.Length; i++)
        {
            int colorIndex = _playerColors[i].SelectedIndex;
            if (colorIndex < 0 || colorIndex >= AvailableColors.Length)
                continue;

            ColorItem item = AvailableColors[colorIndex];
            _playerColors[i].Header
                .SetBackgroundColors(
                    ToDrawingColor(item.Color),
                    Brighten(ToDrawingColor(item.Color), 22),
                    Darken(ToDrawingColor(item.Color), 22))
                .SetTextColor(ToDrawingColor(item.TextColor));
        }
    }

    private void OnStartClicked()
    {
        CaptureOptions();
        Close(DialogResult.OK);
    }

    private void OnCancelClicked()
    {
        CaptureOptions();
        Close(DialogResult.Cancel);
    }

    protected override void OnAcceptRequested()
    {
        CaptureOptions();
        Close(DialogResult.OK);
    }

    protected override void OnClosed(DialogResult result)
    {
        CaptureOptions();
        base.OnClosed(result);
    }

    private void CaptureOptions()
    {
        if (_optionsCaptured)
            return;

        _optionsCaptured = true;

        int playerCount = ParseSelectedInt(_playerCount, 4);
        var options = new NewGameOptions
        {
            PlayerCount = playerCount,
            BoardWidth = ParseSelectedInt(_boardWidth, 8),
            BoardHeight = ParseSelectedInt(_boardHeight, 8)
        };

        for (int i = 0; i < playerCount; i++)
        {
            int colorIndex = Math.Clamp(_playerColors[i].SelectedIndex, 0, AvailableColors.Length - 1);

            options.Players.Add(new Player
            {
                Name = _playerNames[i].Text,
                Type = _playerTypes[i].SelectedIndex == 0 ? PlayerType.Human : PlayerType.Computer,
                ColorItem = AvailableColors[colorIndex]
            });
        }

        Options = options;
    }

    public override void Dispose()
    {
        _startButton.Clicked -= OnStartClicked;
        _cancelButton.Clicked -= OnCancelClicked;
        base.Dispose();
    }

    private static NewGameOptions CreateDefaultOptions()
    {
        var options = new NewGameOptions
        {
            PlayerCount = 4,
            BoardWidth = 8,
            BoardHeight = 8
        };

        for (int i = 0; i < 4; i++)
        {
            options.Players.Add(new Player
            {
                Name = DefaultPlayerNames[i],
                Type = i == 0 ? PlayerType.Human : PlayerType.Computer,
                ColorItem = AvailableColors[i]
            });
        }

        return options;
    }

    private static LabelWidget CreateLabel(
        RenderSurfaceHostBase host,
        View view,
        Rectangle bounds,
        string text)
    {
        return new LabelWidget(host, view, bounds, text)
            .SetFont(SKTypeface.Default, 14f)
            .SetColors(SKColors.White, SKColors.Transparent)
            .SetAlignment(SKTextAlign.Left, Gondwana.Drawing.Direct.TextBlock.VerticalAlign.Center);
    }

    private static void SetComboSelection(ComboBoxWidget combo, string value)
    {
        for (int i = 0; i < combo.Items.Count; i++)
        {
            if (string.Equals(combo.Items[i], value, StringComparison.Ordinal))
            {
                combo.SelectedIndex = i;
                return;
            }
        }
    }

    private static int ParseSelectedInt(ComboBoxWidget combo, int fallback)
    {
        return int.TryParse(combo.SelectedItem, out int value) ? value : fallback;
    }

    private static Rectangle Offset(Rectangle origin, int x, int y, int width, int height)
    {
        return new Rectangle(origin.Left + x, origin.Top + y, width, height);
    }

    private static Rectangle ResolveBounds(View view)
    {
        Rectangle viewport = view.Viewport.TargetRectPx;
        const int width = 498;
        const int height = 390;

        return new Rectangle(
            viewport.Left + (viewport.Width - width) / 2,
            viewport.Top + (viewport.Height - height) / 2,
            width,
            height);
    }

    private static Color ToDrawingColor(SKColor color)
    {
        return Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue);
    }

    private static Color Brighten(Color color, int amount)
    {
        return Color.FromArgb(
            color.A,
            Math.Min(255, color.R + amount),
            Math.Min(255, color.G + amount),
            Math.Min(255, color.B + amount));
    }

    private static Color Darken(Color color, int amount)
    {
        return Color.FromArgb(
            color.A,
            Math.Max(0, color.R - amount),
            Math.Max(0, color.G - amount),
            Math.Max(0, color.B - amount));
    }
}
