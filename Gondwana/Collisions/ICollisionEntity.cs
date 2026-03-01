using System.Drawing;

namespace Gondwana.Collisions;

public interface ICollisionEntity
{
    Rectangle CollisionArea { get; }
}
