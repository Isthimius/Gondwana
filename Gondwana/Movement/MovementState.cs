using System.Numerics;

namespace Gondwana.Movement;

public struct MotionState
{
    // Position is always stored in GRID space (float col,row).
    // Pixel targets convert in adapters.
    public Vector2 Position;     // (col,row)
    public Vector2 Velocity;     // Δ(grid)/sec
    public Vector2 Acceleration; // Δ(grid)/sec^2

    public float? MaxSpeed;       // in grid units/sec
    public float LinearDamping;  // 0..1 per second (e.g., 0.1f)
    public bool WrapX, WrapY;   // enable layer wrapping

    public void ClampVelocity()
    {
        if (MaxSpeed is null) return;

        var v = Velocity;
        var len = v.Length();
        
        if (len > MaxSpeed && MaxSpeed > 0)
            Velocity = v * (MaxSpeed.Value / len);
    }
}