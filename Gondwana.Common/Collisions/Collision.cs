using Gondwana.Common.Enums;

namespace Gondwana.Common.Collisions
{
    // TODO: refactor Collision class
    public class Collision
    {
        /// <summary>
        /// A Tile receiving a collision
        /// </summary>
        public Tile PrimaryTile;

        /// <summary>
        /// The Tile colliding with the PrimaryTile
        /// </summary>
        public Tile SecondaryTile;

        /// <summary>
        /// The direction from which the SecondaryTile is colliding with the PrimaryTile
        /// </summary>
        public CollisionDirectionFrom CollisionDirectionFrom;

        public Collision(Tile _primaryTile, Tile _secondaryTile, CollisionDirectionFrom _from)
        {
            PrimaryTile = _primaryTile;
            SecondaryTile = _secondaryTile;
            CollisionDirectionFrom = _from;
        }
    }
}
