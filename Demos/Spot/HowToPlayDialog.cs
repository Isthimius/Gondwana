using System.Drawing;
using System.Numerics;
using Gondwana.Drawing.Direct;
using Gondwana.Rendering;
using Gondwana.Rendering.Views;
using Gondwana.Widgets.Controls;
using Gondwana.Widgets.Dialogs;
using SkiaSharp;

namespace Gondwana.Demos.Spot;

internal sealed class HowToPlayDialog : DialogBox
{
    private const string Instructions = """
        The goal is simple: finish the game with more spots on the board than any other player.

        STARTING A GAME

        Choose Game > New Game. Select 2-4 players, a board size from 3x3 through 12x12, and whether each player is human- or computer-controlled.

        TAKING A TURN

        1. Click one of your spots to select it.
        2. Click an empty destination up to two squares away, horizontally, vertically, or diagonally.
        3. Every opposing spot immediately adjacent to the destination becomes yours.

        MOVES

        Clone - Move one square. Your original spot remains and a new spot is created at the destination.

        Jump - Move two squares. Your spot moves to the destination, leaving its original square empty.

        Click a selected spot again to deselect it. If a player has no legal move, that turn is skipped automatically.

        WINNING

        The game ends when no legal moves remain anywhere on the board or only one player still has spots. The player with the highest score wins; ties are possible.

        CONTROLS

        Left mouse button - Select a spot or choose its destination.

        Tab - Show or hide the score display.

        Game > New Game - Configure and start another game.

        Options - Toggle music, sound effects, spot jiggle, or clouds.
        """;

    private readonly ButtonWidget _closeButton;

    internal HowToPlayDialog(
        RenderSurfaceHostBase host,
        View view)
        : base(
            host,
            view,
            ResolveBounds(view),
            "How to play",
            showCloseButton: true,
            nickname: "spot.howToPlay")
    {
        Rectangle bounds = Panel.ScreenBounds;

        Panel.SetColor(Color.FromArgb(255, 24, 24, 30))
             .SetBorderColor(Color.FromArgb(255, 90, 90, 105))
             .SetCornerRadius(4f);
        TitleBar.SetColor(Color.FromArgb(255, 42, 42, 52))
                .SetCornerRadius(4f);
        TitleText.SetColors(SKColors.White, SKColors.Transparent);

        var instructionsBounds = new Rectangle(
            bounds.Left + 18,
            bounds.Top + 48,
            bounds.Width - 36,
            bounds.Height - 110);

        InstructionsLabel = new LabelWidget(
            host,
            view,
            instructionsBounds,
            Instructions,
            "spot.howToPlay.instructions")
            .SetFont(SKTypeface.Default, 17f)
            .SetColors(new SKColor(235, 235, 240), SKColors.Transparent)
            .SetAlignment(SKTextAlign.Left, TextBlock.VerticalAlign.Top)
            .EnableWrapping()
            .SetPadding(horizontal: 8f, vertical: 6f)
            .SetLabelZOrder(10_010);

        InstructionsLabel.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        InstructionsLabel.MouseWheelScrollPixels = 54;

        Add(
            InstructionsLabel,
            keepCurrentOffset: false,
            explicitLocalOffsetPx: new Vector2(18, 48));

        _closeButton = new ButtonWidget(
            host,
            view,
            new Rectangle(
                bounds.Left + (bounds.Width - 110) / 2,
                bounds.Bottom - 48,
                110,
                32),
            "Close",
            "spot.howToPlay.close")
            .SetBackgroundColors(
                Color.FromArgb(255, 52, 52, 62),
                Color.FromArgb(255, 70, 70, 82),
                Color.FromArgb(255, 38, 38, 46))
            .SetTextColor(Color.White)
            .SetButtonZOrder(10_020);

        _closeButton.Clicked += OnCloseClicked;
        Add(
            _closeButton,
            keepCurrentOffset: false,
            explicitLocalOffsetPx: new Vector2(
                (bounds.Width - 110) / 2,
                bounds.Height - 48));
    }

    internal LabelWidget InstructionsLabel { get; }

    protected override void OnAcceptRequested()
    {
        Close(DialogResult.Close);
    }

    public override void Dispose()
    {
        _closeButton.Clicked -= OnCloseClicked;
        base.Dispose();
    }

    private void OnCloseClicked()
    {
        Close(DialogResult.Close);
    }

    private static Rectangle ResolveBounds(View view)
    {
        Rectangle viewport = view.Viewport.TargetRectPx;
        const int width = 560;
        const int height = 560;

        return new Rectangle(
            viewport.Left + (viewport.Width - width) / 2,
            viewport.Top + (viewport.Height - height) / 2,
            width,
            height);
    }
}
