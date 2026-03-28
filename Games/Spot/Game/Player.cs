using Gondwana.Drawing;
using SkiaSharp;

namespace HWG.Spot.Game;

internal sealed class Player
{
    internal string Name { get; set; }
    internal PlayerType Type { get; set; }
    internal string ColorText { get; set; }
    internal SKColor Color { get; set; }
    internal Frame DefaultFrame { get; set; }
    internal Frame ActiveFrame { get; set; }
}
