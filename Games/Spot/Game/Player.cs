using Gondwana.Drawing;
using Gondwana.Drawing.Sprites;
using SkiaSharp;

namespace Spot.Game;

internal struct Player
{
    internal string Name { get; set; }
    internal PlayerType Type { get; set; }
    internal string ColorText { get; set; }
    internal SKColor Color { get; set; }
    internal Frame DefaultFrame { get; set; }
    internal Frame ActiveFrame { get; set; }
}
