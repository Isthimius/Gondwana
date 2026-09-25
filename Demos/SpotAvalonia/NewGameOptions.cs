using Gondwana.Demos.SpotAvalonia.Game;

namespace Gondwana.Demos.SpotAvalonia;

internal class NewGameOptions
{
    internal int BoardWidth { get; set; }
    internal int BoardHeight { get; set; }
    internal List<Player> Players { get; set; } = new();
}
