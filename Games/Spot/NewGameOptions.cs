using Spot.Game;
using System.Collections.Generic;

namespace HWG.Spot;

internal class NewGameOptions
{
    internal int PlayerCount { get; set; }
    internal int BoardWidth { get; set; }
    internal int BoardHeight { get; set; }
    internal List<Player> Players { get; set; } = new();
}
