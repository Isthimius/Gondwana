#if !BROWSER
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Gondwana.Demos.SpotAvalonia.Game;
using System.Linq;

namespace Gondwana.Demos.SpotAvalonia;

/// <summary>
/// Player and board configuration dialog for SpotAvalonia (desktop targets only).
/// Opens via <c>ShowDialog&lt;NewGameOptions?&gt;</c>; returns <see langword="null"/> on cancel.
/// </summary>
internal sealed class NewGameDialog : Window
{
    private static readonly ColorItem[] _availableColors = GameConfig.AvailableColors;

    private static readonly string[] _boardSizes = GameConfig.BoardSizes;

    private readonly ComboBox _cboPlayerCount = new();
    private readonly ComboBox _cboWidth       = new();
    private readonly ComboBox _cboHeight      = new();

    private readonly TextBox[]   _nameBoxes    = new TextBox[4];
    private readonly ComboBox[]  _typeSelects  = new ComboBox[4];
    private readonly ComboBox[]  _colorSelects = new ComboBox[4];
    private readonly Border[]    _playerBorders = new Border[4];

    internal NewGameDialog(NewGameOptions? initialOptions = null)
    {
        Title  = "New Game";
        Width  = 510;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        BuildUI();

        if (initialOptions is not null)
            ApplyInitialOptions(initialOptions);
    }

    private void ApplyInitialOptions(NewGameOptions options)
    {
        int playerCountIndex = options.Players.Count - 2; // combo items start at "2"
        if (playerCountIndex >= 0 && playerCountIndex < _cboPlayerCount.ItemCount)
            _cboPlayerCount.SelectedIndex = playerCountIndex;

        int widthIndex = options.BoardWidth - 3;   // combo items start at "3"
        if (widthIndex >= 0 && widthIndex < _cboWidth.ItemCount)
            _cboWidth.SelectedIndex = widthIndex;

        int heightIndex = options.BoardHeight - 3; // combo items start at "3"
        if (heightIndex >= 0 && heightIndex < _cboHeight.ItemCount)
            _cboHeight.SelectedIndex = heightIndex;

        for (int i = 0; i < options.Players.Count && i < 4; i++)
        {
            var player = options.Players[i];
            _nameBoxes[i].Text = player.Name;
            _typeSelects[i].SelectedIndex = player.Type == PlayerType.Human ? 0 : 1;
            var match = _availableColors.FirstOrDefault(c => c.Color == player.ColorItem.Color);
            if (match is not null)
                _colorSelects[i].SelectedItem = match;
        }
    }

    private void BuildUI()
    {
        var root = new Border
        {
            Padding    = new Thickness(12),
            Background = Brushes.CornflowerBlue,
        };
        var outerStack = new StackPanel { Spacing = 8 };
        root.Child = outerStack;

        // ── Top row: player count + board size ──────────────────────────────
        var topRow = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        topRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        topRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        topRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        topRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        topRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        topRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        topRow.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

        var playersLabel   = Label("Players", margin: new Thickness(0, 0, 6, 0));
        var boardSizeLabel = Label("Board Size", margin: new Thickness(16, 0, 6, 0));
        var xLabel         = Label("×", margin: new Thickness(4, 0));

        _cboPlayerCount.ItemsSource    = new[] { "2", "3", "4" };
        _cboPlayerCount.SelectedIndex  = GameConfig.DefaultPlayerCountIndex;
        _cboPlayerCount.MinWidth       = 55;
        _cboPlayerCount.SelectionChanged += CboPlayerCount_SelectionChanged;

        _cboWidth.ItemsSource   = _boardSizes;
        _cboWidth.SelectedIndex = GameConfig.DefaultBoardSizeIndex;   // 8
        _cboWidth.MinWidth      = 55;

        _cboHeight.ItemsSource   = _boardSizes;
        _cboHeight.SelectedIndex = GameConfig.DefaultBoardSizeIndex;  // 8
        _cboHeight.MinWidth      = 55;

        Grid.SetColumn(playersLabel,   0);
        Grid.SetColumn(_cboPlayerCount, 1);
        Grid.SetColumn(boardSizeLabel, 3);
        Grid.SetColumn(_cboWidth,      4);
        Grid.SetColumn(xLabel,         5);
        Grid.SetColumn(_cboHeight,     6);

        topRow.Children.Add(playersLabel);
        topRow.Children.Add(_cboPlayerCount);
        topRow.Children.Add(boardSizeLabel);
        topRow.Children.Add(_cboWidth);
        topRow.Children.Add(xLabel);
        topRow.Children.Add(_cboHeight);
        outerStack.Children.Add(topRow);

        // ── Per-player rows ─────────────────────────────────────────────────
        for (int i = 0; i < 4; i++)
        {
            var border = new Border
            {
                BorderBrush     = Brushes.White,
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(4),
                Padding         = new Thickness(8, 6),
            };

            var rowGrid = new Grid();
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(120)));
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(120)));

            string defaultName = GameConfig.DefaultPlayerNames[i];
            var nameBox = new TextBox { Text = defaultName, Watermark = defaultName };
            var typeCombo = new ComboBox
            {
                ItemsSource   = new[] { "Human", "Computer" },
                SelectedIndex = i == 0 ? 0 : 1,
                MinWidth      = 110,
            };

            var colorCombo = CreateColorCombo(i);

            Grid.SetColumn(nameBox,   0);
            Grid.SetColumn(typeCombo, 1);
            Grid.SetColumn(colorCombo, 2);

            rowGrid.Children.Add(nameBox);
            rowGrid.Children.Add(typeCombo);
            rowGrid.Children.Add(colorCombo);

            var headerStack = new StackPanel { Spacing = 4 };
            headerStack.Children.Add(new TextBlock
            {
                Text       = $"Player {i + 1}",
                FontWeight = FontWeight.SemiBold,
                Margin     = new Thickness(0, 0, 0, 4),
            });
            headerStack.Children.Add(rowGrid);
            border.Child = headerStack;

            _nameBoxes[i]    = nameBox;
            _typeSelects[i]  = typeCombo;
            _playerBorders[i] = border;
            outerStack.Children.Add(border);
        }

        // ── Button row ──────────────────────────────────────────────────────
        var buttonRow = new StackPanel
        {
            Orientation         = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing             = 12,
            Margin              = new Thickness(0, 4, 0, 0),
        };

        var startBtn  = new Button { Content = "Start",  MinWidth = 120 };
        var cancelBtn = new Button { Content = "Cancel", MinWidth = 120 };
        startBtn.Click  += StartButton_Click;
        cancelBtn.Click += (_, _) => Close(null);

        buttonRow.Children.Add(startBtn);
        buttonRow.Children.Add(cancelBtn);
        outerStack.Children.Add(buttonRow);

        Content = root;

        // Initial visibility: show all 4, since default count = 4
        UpdatePlayerVisibility(4);
    }

    private ComboBox CreateColorCombo(int playerIndex)
    {
        var combo = new ComboBox
        {
            MinWidth = 110,
            ItemTemplate = new FuncDataTemplate<ColorItem>((item, _) =>
            {
                if (item is null)
                    return new TextBlock();

                var panel = new StackPanel
                {
                    Orientation         = Orientation.Horizontal,
                    Spacing             = 6,
                    VerticalAlignment   = VerticalAlignment.Center,
                };
                panel.Children.Add(new Border
                {
                    Width           = 20,
                    Height          = 14,
                    Background      = new SolidColorBrush(new Color(255, item.Color.Red, item.Color.Green, item.Color.Blue)),
                    BorderBrush     = Brushes.Black,
                    BorderThickness = new Thickness(1),
                });
                panel.Children.Add(new TextBlock { Text = item.Name });
                return panel;
            }),
        };

        _colorSelects[playerIndex] = combo;

        combo.ItemsSource   = _availableColors;
        combo.SelectedIndex = playerIndex < _availableColors.Length ? playerIndex : 0;
        combo.SelectionChanged += (s, _) => OnColorChanged((ComboBox)s!);

        return combo;
    }

    private static TextBlock Label(string text, Thickness? margin = null)
        => new TextBlock
        {
            Text                = text,
            VerticalAlignment   = VerticalAlignment.Center,
            Margin              = margin ?? new Thickness(0),
        };

    private void CboPlayerCount_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_cboPlayerCount.SelectedIndex < 0)
            return;
        int count = int.Parse((string)_cboPlayerCount.SelectedItem!);
        UpdatePlayerVisibility(count);
    }

    private void UpdatePlayerVisibility(int playerCount)
    {
        for (int i = 0; i < 4; i++)
        {
            if (_playerBorders[i] is not null)
                _playerBorders[i].IsVisible = i < playerCount;
        }
    }

    private void OnColorChanged(ComboBox changedCombo)
    {
        if (changedCombo.SelectedItem is not ColorItem selected)
            return;

        foreach (var combo in _colorSelects)
        {
            if (combo == changedCombo || combo is null)
                continue;

            if (combo.SelectedItem is ColorItem current && current.Color == selected.Color)
            {
                // Reassign this combo to the first available color.
                foreach (var candidate in _availableColors)
                {
                    bool taken = _colorSelects
                        .Where(c => c != combo && c is not null)
                        .Any(c => c.SelectedItem is ColorItem ci && ci.Color == candidate.Color);

                    if (!taken)
                    {
                        combo.SelectedItem = candidate;
                        break;
                    }
                }
            }
        }
    }

    internal NewGameOptions GetCurrentOptions()
    {
        int playerCount = int.Parse((string)_cboPlayerCount.SelectedItem!);
        int boardWidth  = int.Parse((string)_cboWidth.SelectedItem!);
        int boardHeight = int.Parse((string)_cboHeight.SelectedItem!);

        var options = new NewGameOptions
        {
            BoardWidth  = boardWidth,
            BoardHeight = boardHeight,
        };

        for (int i = 0; i < playerCount; i++)
        {
            var colorItem = (ColorItem)_colorSelects[i].SelectedItem!;
            options.Players.Add(new Player
            {
                Name      = _nameBoxes[i].Text ?? GameConfig.DefaultPlayerNames[i],
                Type      = _typeSelects[i].SelectedIndex == 0 ? PlayerType.Human : PlayerType.Computer,
                ColorItem = colorItem,
            });
        }

        return options;
    }

    private void StartButton_Click(object? sender, RoutedEventArgs e)
    {
        Close(GetCurrentOptions());
    }
}
#endif
