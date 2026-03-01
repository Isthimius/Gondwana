namespace Gondwana.Collisions;

public enum CollisionResponse
{
    Solid,   // push-out / block movement
    Trigger  // do not push-out, just report
}

