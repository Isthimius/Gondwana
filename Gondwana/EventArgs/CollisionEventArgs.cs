using Gondwana.Grid.Collisions;

namespace Gondwana.EventArgs;

public delegate void CollisionEventHandler(CollisionEventArgs e);

public class CollisionEventArgs : System.EventArgs
{
    public List<Collision> Collisions;

    public CollisionEventArgs(List<Collision> collisions)
    {
        Collisions = collisions;
    }
}
