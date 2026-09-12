using System.Drawing;
using Gondwana.Demos.Spot.Game;
using Gondwana.Drawing.Direct;
using Gondwana.Rendering;
using Gondwana.Rendering.Views;
using Gondwana.Widgets.Controls;
using Gondwana.Widgets.Layout;
using SkiaSharp;

namespace Gondwana.Demos.Spot;

/// <summary>
/// Small view-space HUD used by Spot to exercise the reusable Gondwana widget layer.
/// </summary>
internal sealed class SpotWidgetHud : IDisposable
{
    private const int PreferredWidth = 280;
    private const int Height = 64;
    private const int Margin = 10;
    private const int ZOrder = 30_000;

    private readonly RenderSurfaceHostBase _host;
    private readonly SpotGame _game;

    private PanelWidget? _panel;
    private LabelWidget? _turnLabel;
    private bool _disposed;

    internal SpotWidgetHud(RenderSurfaceHostBase host, SpotGame game)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _game = game ?? throw new ArgumentNullException(nameof(game));

        _game.GameStarted += OnGameStarted;
        _game.PlayerTurnStarted += OnPlayerTurnStarted;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _game.GameStarted -= OnGameStarted;
        _game.PlayerTurnStarted -= OnPlayerTurnStarted;
        _panel?.Dispose();
        _panel = null;
        _turnLabel = null;
    }

    private void OnGameStarted(SpotGame game)
    {
        BuildHud(game.CurrentPlayer);
    }

    private void OnPlayerTurnStarted(Player player)
    {
        if (_panel is null)
            BuildHud(player);
        else
            UpdateTurn(player);
    }

    private void BuildHud(Player player)
    {
        _panel?.Dispose();

        if (_host.ViewManager.Views.Count == 0)
            return;

        View view = _host.ViewManager.Views[0];
        Rectangle viewport = view.Viewport.TargetRectPx;
        int width = Math.Min(PreferredWidth, Math.Max(120, viewport.Width - Margin * 2));
        int x = viewport.Left + (viewport.Width - width) / 2;
        int y = viewport.Top + Margin;
        var panelBounds = new Rectangle(x, y, width, Height);

        _panel = new PanelWidget(
                _host,
                view,
                panelBounds,
                Color.FromArgb(220, 24, 27, 34),
                "spot.widget-hud")
            .SetBorderColor(Color.FromArgb(235, 220, 224, 232))
            .SetStrokeWidth(1f)
            .SetCornerRadius(10f)
            .SetPanelZOrder(ZOrder);

        var stack = new StackPanelWidget(
            _host,
            DirectDrawingMode.View,
            panelBounds.Location,
            WidgetOrientation.Vertical,
            spacing: 2f,
            nickname: "spot.widget-hud.stack");

        var title = new LabelWidget(
                _host,
                view,
                new Rectangle(Point.Empty, new Size(width - 20, 18)),
                "SPOT!",
                "spot.widget-hud.title")
            .SetFont(SKTypeface.Default, 13f)
            .SetTextColor(SKColors.Gainsboro)
            .SetAlignment(SKTextAlign.Center, TextBlock.VerticalAlign.Center)
            .EnableWrapping(false);

        _turnLabel = new LabelWidget(
                _host,
                view,
                new Rectangle(Point.Empty, new Size(width - 20, 26)),
                string.Empty,
                "spot.widget-hud.turn")
            .SetFont(SKTypeface.Default, 18f)
            .SetAlignment(SKTextAlign.Center, TextBlock.VerticalAlign.Center)
            .EnableWrapping(false);

        stack.AddWidget(title)
             .AddWidget(_turnLabel)
             .SetZOrder(ZOrder + 1);

        _panel.AddWidget(stack, new Point(10, 7));
        _panel.Show();

        UpdateTurn(player);
    }

    private void UpdateTurn(Player player)
    {
        _turnLabel?.SetText($"{player.Name}'s turn")
                   .SetTextColor(player.ColorItem.TextColor);
    }
}
