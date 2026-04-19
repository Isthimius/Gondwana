using Gondwana.Demos.Spot.Game;
using System.Collections.Generic;

namespace Gondwana.Demos.Spot;

internal class NewGameOptions
{
    internal int PlayerCount { get; set; }
    internal int BoardWidth { get; set; }
    internal int BoardHeight { get; set; }
    internal List<Player> Players { get; set; } = new();
}
