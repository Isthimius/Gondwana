using System.Collections.Generic;

namespace HWG.Spot;

public class NewGameOptions
{
    public int PlayerCount { get; set; }

    public int BoardWidth { get; set; }
    public int BoardHeight { get; set; }

    public List<PlayerOptions> Players { get; set; } = new();
}

public class PlayerOptions
{
    public string Name { get; set; } = "";
    public PlayerType Type { get; set; }
    public string Color { get; set; }
}

public enum PlayerType
{
    Human,
    Computer
}