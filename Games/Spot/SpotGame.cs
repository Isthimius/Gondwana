using System;

namespace HWG.Spot
{
    internal class SpotGame
    {
        internal Player[] Players { get; set; } = Array.Empty<Player>();
        internal SpotGameField SpotGameField { get; set; } = SpotGameField.Create(12, 12, Array.Empty<Player>());

        private int _currentPlayerIndex = 0;

        internal Player CurrentPlayer => Players[_currentPlayerIndex];

        internal Player NextPlayer()
        {
            _currentPlayerIndex = (_currentPlayerIndex + 1) % Players.Length;
            return CurrentPlayer;
        }

        internal void NewGame(int width, int height, params Player[] players)
        {
            if (players.Length < 2)
                throw new ArgumentException("At least two players are required to start a game.", nameof(players));

            if (players.Length > 4)
                throw new ArgumentException("No more than four players can play at the same time.", nameof(players));

            if (width < 3 || height < 3)
                throw new ArgumentException("The game field must be at least 3x3 in size.", nameof(width));

            if (width > 12 || height > 12)
                throw new ArgumentException("The game field cannot be larger than 12x12 in size.", nameof(width));

            SpotGameField = SpotGameField.Create(width, height, players);
            Players = players;
            _currentPlayerIndex = 0;
        }
    }
}
