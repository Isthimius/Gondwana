using Gondwana.Drawing;

namespace Gondwana.Demos.SpotGL.Game;

internal sealed class Player
{
    internal string Name { get; set; } = null!;
    internal PlayerType Type { get; set; }
    internal ColorItem ColorItem { get; set; } = null!;
    internal Frame DefaultFrame { get; set; }
    internal Frame ActiveFrame { get; set; }
}
