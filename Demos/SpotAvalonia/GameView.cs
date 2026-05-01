using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Gondwana.Avalonia.Rendering;
using Gondwana.Demos.SpotAvalonia.Game;
using Microsoft.Extensions.Logging;

namespace Gondwana.Demos.SpotAvalonia;

/// <summary>
/// Single-view host for SpotAvalonia on browser/WASM targets.
/// Used as the <c>MainView</c> of <see cref="Avalonia.Controls.ApplicationLifetimes.ISingleViewApplicationLifetime"/>.
/// </summary>
internal sealed class GameView : UserControl
{
    private static readonly ColorItem[] _availableColors = GameConfig.AvailableColors;

    private readonly AvaloniaBitmapRenderSurfaceControl _renderSurface = new();
    private SpotAvaloniaGameHost? _host;

    // Overlay controls kept so they can be hidden once the game starts.
    private Border?   _overlay;
    private Button?   _startButton;
    private ComboBox  _cboPlayerCount = new();
    private ComboBox  _cboWidth       = new();
    private ComboBox  _cboHeight      = new();
    private readonly TextBox[]  _nameBoxes   = new TextBox[4];
    private readonly ComboBox[] _typeSelects = new ComboBox[4];
    private readonly ComboBox[] _colorSelects = new ComboBox[4];
    private readonly Border[]   _playerBorders = new Border[4];

    internal GameView()
    {
        _renderSurface.HorizontalAlignment = HorizontalAlignment.Stretch;
        _renderSurface.VerticalAlignment   = VerticalAlignment.Stretch;

        _overlay = BuildOverlay();

        var root = new Panel();
        root.Children.Add(_renderSurface);
        root.Children.Add(_overlay);

        Content = root;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (_host != null)
            return;

        _host = new SpotAvaloniaGameHost(_renderSurface);

        // Subscribe before Initialize() so the handler fires during initialization.
        _host.Engine.InitializationComplete += () =>
        {
            _host.Engine.Configuration.TargetFPS = 0;

            // Enable the Start button now that the engine is ready.
            Dispatcher.UIThread.Post(() =>
            {
                if (_startButton is not null)
                    _startButton.IsEnabled = true;
            });
        };

        _host.Initialize(logLevel: LogLevel.Warning);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _host?.Dispose();
        _host = null;
        base.OnDetachedFromVisualTree(e);
    }

    private Border BuildOverlay()
    {
        string[] boardSizes = GameConfig.BoardSizes;

        var outerStack = new StackPanel { Spacing = 8 };

        // ── Top row: player count + board size ──────────────────────────────
        var topRow = new WrapPanel { Orientation = Orientation.Horizontal };

        _cboPlayerCount.ItemsSource    = new[] { "2", "3", "4" };
        _cboPlayerCount.SelectedIndex  = GameConfig.DefaultPlayerCountIndex;
        _cboPlayerCount.MinWidth       = 55;
        _cboPlayerCount.Margin         = new Thickness(4);
        _cboPlayerCount.SelectionChanged += CboPlayerCount_SelectionChanged;

        _cboWidth.ItemsSource   = boardSizes;
        _cboWidth.SelectedIndex = GameConfig.DefaultBoardSizeIndex;
        _cboWidth.MinWidth      = 55;
        _cboWidth.Margin        = new Thickness(4);

        _cboHeight.ItemsSource   = boardSizes;
        _cboHeight.SelectedIndex = GameConfig.DefaultBoardSizeIndex;
        _cboHeight.MinWidth      = 55;
        _cboHeight.Margin        = new Thickness(4);

        topRow.Children.Add(new TextBlock { Text = "Players", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4) });
        topRow.Children.Add(_cboPlayerCount);
        topRow.Children.Add(new TextBlock { Text = "Board",   VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4) });
        topRow.Children.Add(_cboWidth);
        topRow.Children.Add(new TextBlock { Text = "×",       VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(2) });
        topRow.Children.Add(_cboHeight);
        outerStack.Children.Add(topRow);

        // ── Per-player rows ─────────────────────────────────────────────────
        for (int i = 0; i < 4; i++)
        {
            var rowBorder = new Border
            {
                BorderBrush     = Brushes.White,
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(4),
                Padding         = new Thickness(6, 4),
            };

            var rowPanel = new WrapPanel { Orientation = Orientation.Horizontal };

            var nameBox = new TextBox { Text = $"Player {i + 1}", MinWidth = 100, Margin = new Thickness(4) };
            var typeCombo = new ComboBox
            {
                ItemsSource   = new[] { "Human", "Computer" },
                SelectedIndex = i == 0 ? 0 : 1,
                MinWidth      = 110,
                Margin        = new Thickness(4),
            };
            var colorCombo = CreateColorCombo(i);

            rowPanel.Children.Add(new TextBlock { Text = $"Player {i + 1}", FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 8, 0), MinWidth = 60 });
            rowPanel.Children.Add(nameBox);
            rowPanel.Children.Add(typeCombo);
            rowPanel.Children.Add(colorCombo);

            rowBorder.Child = rowPanel;

            _nameBoxes[i]     = nameBox;
            _typeSelects[i]   = typeCombo;
            _playerBorders[i] = rowBorder;
            outerStack.Children.Add(rowBorder);
        }

        // ── Start button ────────────────────────────────────────────────────
        _startButton = new Button
        {
            Content             = "Start Game",
            HorizontalAlignment = HorizontalAlignment.Center,
            MinWidth            = 140,
            Margin              = new Thickness(0, 8, 0, 0),
            IsEnabled           = false,
        };
        _startButton.Click += StartButton_Click;
        outerStack.Children.Add(_startButton);

        // Wrap in a semi-transparent panel centered on screen
        var overlay = new Border
        {
            Background          = new SolidColorBrush(Colors.CornflowerBlue, 0.92),
            BorderBrush         = Brushes.White,
            BorderThickness     = new Thickness(2),
            CornerRadius        = new CornerRadius(8),
            Padding             = new Thickness(16),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
            Child               = outerStack,
        };

        UpdatePlayerVisibility(4);
        return overlay;
    }

    private ComboBox CreateColorCombo(int playerIndex)
    {
        var combo = new ComboBox
        {
            MinWidth = 110,
            Margin   = new Thickness(4),
            ItemTemplate = new FuncDataTemplate<ColorItem>((item, _) =>
            {
                if (item is null)
                    return new TextBlock();
                var panel = new StackPanel
                {
                    Orientation       = Orientation.Horizontal,
                    Spacing           = 6,
                    VerticalAlignment = VerticalAlignment.Center,
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

    private void StartButton_Click(object? sender, RoutedEventArgs e)
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
                Name      = _nameBoxes[i].Text ?? $"Player {i + 1}",
                Type      = _typeSelects[i].SelectedIndex == 0 ? PlayerType.Human : PlayerType.Computer,
                ColorItem = colorItem,
            });
        }

        if (_overlay is not null)
            _overlay.IsVisible = false;

        _host?.Engine.EngineDispatcher.Post(() => _host.StartNewGame(options));
    }
}
