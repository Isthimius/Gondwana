using System.Drawing;

namespace Gondwana.Drawing.Collisions;

internal class CollisionManager
{
    public event CollisionEventHandler TileCollisions;

    internal void RaiseCollisionEvent(long tick)
    {
        // TODO: refactor this

        // only check for collisions if something is subscribed to see it
        if (TileCollisions != null)
        {
            List<Collision> collisions = new List<Collision>();

            // get a list of all Sprites and GridPoints that have collision detection turned on
            List<Tile> allSpritesAndCollisionTiles = new List<Tile>(Tile.TileCollisions);

            // add *all* Sprites if they are not already in list;
            // GridPoints don't move, so there's no need to do the same for them
            foreach (Tile tile in Gondwana.Drawing.Sprites.SpriteManager.AllSprites)
            {
                if (allSpritesAndCollisionTiles.IndexOf(tile) == -1)
                    allSpritesAndCollisionTiles.Add(tile);
            }

            // now cycle through each Tile with collision detection turned on and check for collisions
            foreach (Tile tilePrimary in Tile.TileCollisions)
            {
                List<Tile> secondaryList;

                // find the list of Tiles that need to be checked for collisions against tilePrimary
                switch (tilePrimary.DetectCollision)
                {
                    case CollisionDetectionType.All:
                        // cycle through all Tile objects marked for collisions, and all Sprite objects
                        secondaryList = allSpritesAndCollisionTiles;
                        break;

                    case CollisionDetectionType.OthersWithColDetect:
                        // only cycle through other Tile objects with detection turned on
                        secondaryList = Tile.TileCollisions;
                        break;

                    default:
                        // shouldn't ever get here
                        secondaryList = new List<Tile>();
                        break;
                }

                // add any collisions detected for the tilePrimary to the list
                collisions.AddRange(CheckForCollisions(tilePrimary, secondaryList));
            }

            // if collisions were detected...
            if (collisions.Count > 0)
            {
                // ...raise the event
                TileCollisions(new CollisionEventArgs(collisions));
            }
        }
    }

    private List<Collision> CheckForCollisions(Tile primary, List<Tile> secondaryList)
    {
        List<Collision> collisions = new List<Collision>();
        Rectangle primaryLoc = primary.CollisionArea;

        foreach (Tile tile in secondaryList)
        {
            // only check for collisions if on same layer
            if (tile.ParentGrid == primary.ParentGrid)
            {
                // Sprite can't collide with itself
                if (tile != primary)
                {
                    Rectangle secondaryLoc = tile.CollisionArea;

                    if (primaryLoc.IntersectsWith(secondaryLoc))
                    {
                        bool isNorth;   // is in top 25% of primaryLoc
                        bool isSouth;   // is in bottom 25% of primaryLoc
                        bool isWest;    // is in left 25% of primaryLoc
                        bool isEast;    // is in right 25% of primaryLoc

                        isNorth = secondaryLoc.IntersectsWith(new Rectangle(
                            primaryLoc.X, primaryLoc.Y,
                            primaryLoc.Width, (int)((float)primaryLoc.Height * (float)0.25)));

                        isSouth = secondaryLoc.IntersectsWith(new Rectangle(
                            primaryLoc.X, primaryLoc.Y + (int)((float)primaryLoc.Height * (float)0.75),
                            primaryLoc.Width, (int)((float)primaryLoc.Height * (float)0.25)));

                        isWest = secondaryLoc.IntersectsWith(new Rectangle(
                            primaryLoc.X, primaryLoc.Y,
                            (int)((float)primaryLoc.Width * (float)0.25), primaryLoc.Height));

                        isEast = secondaryLoc.IntersectsWith(new Rectangle(
                            primaryLoc.X + (int)((float)primaryLoc.Width * (float)0.75), primaryLoc.Y,
                            (int)((float)primaryLoc.Width * (float)0.25), primaryLoc.Height));

                        bool isNSCenter = !(isNorth ^ isSouth);
                        bool isWECenter = !(isWest ^ isEast);

                        if (isNSCenter && isWECenter)
                        {
                            collisions.Add(new Collision(primary, tile, CollisionDirectionFrom.Center));
                        }
                        else if (isWECenter && isNorth)
                        {
                            collisions.Add(new Collision(primary, tile, CollisionDirectionFrom.N));
                        }
                        else if (!isWECenter && isNorth && isEast)
                        {
                            collisions.Add(new Collision(primary, tile, CollisionDirectionFrom.NE));
                        }
                        else if (isNSCenter && isEast)
                        {
                            collisions.Add(new Collision(primary, tile, CollisionDirectionFrom.E));
                        }
                        else if (!isNSCenter && isEast && isSouth)
                        {
                            collisions.Add(new Collision(primary, tile, CollisionDirectionFrom.SE));
                        }
                        else if (isWECenter && isSouth)
                        {
                            collisions.Add(new Collision(primary, tile, CollisionDirectionFrom.S));
                        }
                        else if (!isWECenter && isSouth && isWest)
                        {
                            collisions.Add(new Collision(primary, tile, CollisionDirectionFrom.SW));
                        }
                        else if (isNSCenter && isWest)
                        {
                            collisions.Add(new Collision(primary, tile, CollisionDirectionFrom.W));
                        }
                        else if (!isNSCenter && isWest && isNorth)
                        {
                            collisions.Add(new Collision(primary, tile, CollisionDirectionFrom.NW));
                        }
                    }
                }
            }
        }

        return collisions;
    }
}