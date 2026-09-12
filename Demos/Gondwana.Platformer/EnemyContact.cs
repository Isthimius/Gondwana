using System.Drawing;

namespace Gondwana.Demos.Platformer;

internal static class EnemyContact
{
    internal static bool IsStomp(Rectangle previousPlayer, Rectangle player,
        Rectangle previousEnemy, Rectangle enemy, float verticalVelocity)
    {
        // Test the top crossing before overlap, so a fast fall cannot tunnel through the head.
        var oldGap = previousPlayer.Bottom - previousEnemy.Top;
        var newGap = player.Bottom - enemy.Top;
        if (verticalVelocity <= 0f || oldGap > 1 || newGap < 0 || newGap <= oldGap)
            return false;

        var fraction = Math.Clamp(-oldGap / (float)(newGap - oldGap), 0f, 1f);
        var playerLeft = previousPlayer.Left + (player.Left - previousPlayer.Left) * fraction;
        var enemyLeft = previousEnemy.Left + (enemy.Left - previousEnemy.Left) * fraction;
        return playerLeft < enemyLeft + enemy.Width && playerLeft + player.Width > enemyLeft;
    }
}
