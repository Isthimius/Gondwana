using Gondwana.Demos.SpotAvalonia.Game;
using System.Collections.Generic;

namespace Gondwana.Demos.SpotAvalonia;

internal class NewGameOptions
{
    internal int BoardWidth { get; set; }
    internal int BoardHeight { get; set; }
    internal List<Player> Players { get; set; } = new();
}
