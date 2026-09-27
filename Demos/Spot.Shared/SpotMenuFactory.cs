using System.Drawing;
using Gondwana.Rendering;
using Gondwana.Rendering.Views;
using Gondwana.Widgets.Menus;

namespace Gondwana.Demos.Spot;

internal sealed class SpotMenuActions
{
    internal required Action NewGame { get; init; }
    internal required Action<bool> MusicChanged { get; init; }
    internal required Action<bool> SoundEffectsChanged { get; init; }
    internal required Action<bool> JiggleChanged { get; init; }
    internal required Action<bool> CloudsChanged { get; init; }
    internal required Action HowToPlay { get; init; }
    internal required Action About { get; init; }
    internal Action? Exit { get; init; }
}

internal readonly record struct SpotMenuState(
    bool Music,
    bool SoundEffects,
    bool Jiggle,
    bool Clouds);

internal static class SpotMenuFactory
{
    internal const int Height = 32;

    internal static MenuBarWidget Create(
        RenderSurfaceHostBase host,
        View view,
        SpotMenuState state,
        SpotMenuActions actions)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(actions);

        var menuBar = new MenuBarWidget(
            host,
            view,
            new Rectangle(0, 0, view.Viewport.TargetRectPx.Width, Height));

        menuBar
            .AddMenu(
                "Game",
                game =>
                {
                    game.AddItem("New Game", actions.NewGame, mnemonic: 'N');

                    if (actions.Exit is not null)
                    {
                        game.AddSeparator()
                            .AddItem("Exit", actions.Exit, mnemonic: 'X');
                    }

                },
                mnemonic: 'G')
            .AddMenu(
                "Options",
                options => options
                    .AddCheckItem(
                        "Music",
                        actions.MusicChanged,
                        isChecked: state.Music,
                        key: "options.music",
                        mnemonic: 'M')
                    .AddCheckItem(
                        "Sound Effects",
                        actions.SoundEffectsChanged,
                        isChecked: state.SoundEffects,
                        key: "options.soundEffects",
                        mnemonic: 'S')
                    .AddCheckItem(
                        "Jiggle",
                        actions.JiggleChanged,
                        isChecked: state.Jiggle,
                        key: "options.jiggle",
                        mnemonic: 'J')
                    .AddCheckItem(
                        "Clouds",
                        actions.CloudsChanged,
                        isChecked: state.Clouds,
                        key: "options.clouds",
                        mnemonic: 'C'),
                mnemonic: 'O')
            .AddMenu(
                "Help",
                help => help
                    .AddItem("How to play", actions.HowToPlay, mnemonic: 'P')
                    .AddSeparator()
                    .AddItem("About", actions.About, mnemonic: 'A'),
                mnemonic: 'H');

        menuBar.Show();
        return menuBar;
    }
}
