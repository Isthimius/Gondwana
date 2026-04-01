using Gondwana.Drawing;
using SkiaSharp;

namespace HWG.Spot.Game;

public sealed class Player
{
    public string Name { get; set; }
    public PlayerType Type { get; set; }
    public ColorItem ColorItem { get; set; }
    public Frame DefaultFrame { get; set; }
    public Frame ActiveFrame { get; set; }
}
