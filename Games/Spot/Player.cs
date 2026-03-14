using Gondwana.Drawing.Sprites;

namespace HWG.Spot;

internal struct Player
{
    internal string Name { get; set; }
    internal PlayerType Type { get; set; }
    internal string Color { get; set; }
    internal Sprite? Sprite { get; set; }
}
