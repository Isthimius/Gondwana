using System.Numerics;

namespace Gondwana.Movement.Scripted;

public struct ScriptedMovement
{
    public MovementScriptType Type;     // None, TweenTo, Toward
    public Vector2 Origin;              // starting point captured when scheduling the tween
    public Vector2 Target;
    public float DurationSec;           // for TweenTo
    public float ElapsedSec;            // for TweenTo
    public float SpeedPerSec;           // for Toward
    public float SnapEpsilon;           // both
    public Func<float, float>? Easing;  // optional; TweenTo only
}
