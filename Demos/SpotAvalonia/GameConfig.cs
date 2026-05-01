using Gondwana.Demos.SpotAvalonia.Game;
using SkiaSharp;
using System.Linq;

namespace Gondwana.Demos.SpotAvalonia;

/// <summary>
/// Shared default values used by the new-game UI (dialog and overlay).
/// </summary>
internal static class GameConfig
{
    /// <summary>The selectable player colors, in display order.</summary>
    internal static readonly ColorItem[] AvailableColors =
    [
        new ColorItem("Red",    SKColors.Red,    SKColors.White),
        new ColorItem("Blue",   SKColors.Blue,   SKColors.White),
        new ColorItem("Yellow", SKColors.Yellow, SKColors.Blue),
        new ColorItem("Violet", SKColors.Violet, SKColors.White),
        new ColorItem("Green",  SKColors.Green,  SKColors.Black),
    ];

    /// <summary>The selectable board dimension values (3 to 12 inclusive).</summary>
    internal static readonly string[] BoardSizes =
        Enumerable.Range(3, 10).Select(n => n.ToString()).ToArray();

    /// <summary>Default board width/height index in <see cref="BoardSizes"/> (8×8).</summary>
    internal const int DefaultBoardSizeIndex = 5;

    /// <summary>Default player-count index (4 players).</summary>
    internal const int DefaultPlayerCountIndex = 2;
}
