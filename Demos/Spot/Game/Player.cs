using Gondwana.Drawing;

namespace Gondwana.Demos.Spot.Game;

internal sealed class Player
{
    internal string Name { get; set; }
    internal PlayerType Type { get; set; }
    internal ColorItem ColorItem { get; set; }
    internal Frame DefaultFrame { get; set; }
    internal Frame ActiveFrame { get; set; }
}
