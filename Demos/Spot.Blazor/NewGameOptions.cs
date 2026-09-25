using Gondwana.Demos.SpotBlazor.Game;

namespace Gondwana.Demos.SpotBlazor;

internal class NewGameOptions
{
    internal int BoardWidth { get; set; }
    internal int BoardHeight { get; set; }
    internal List<Player> Players { get; set; } = new();
}
