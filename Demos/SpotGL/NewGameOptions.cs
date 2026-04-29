using Gondwana.Demos.SpotGL.Game;
using System.Collections.Generic;

namespace Gondwana.Demos.SpotGL;

internal class NewGameOptions
{
    internal int PlayerCount { get; set; }
    internal int BoardWidth { get; set; }
    internal int BoardHeight { get; set; }
    internal List<Player> Players { get; set; } = new();
}
