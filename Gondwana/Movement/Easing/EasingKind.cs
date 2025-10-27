namespace Gondwana.Movement.Easing;

/// <summary>
/// All supported easing functions. Each function expects t ∈ [0,1] and returns a smoothed [0,1].
/// </summary>
public enum EasingKind
{
    /// <summary>Linear easing; no acceleration or deceleration.</summary>
    Linear,

    /// <summary>Quadratic ease-in; starts slowly and accelerates.</summary>
    EaseInQuad,

    /// <summary>Quadratic ease-out; starts quickly and decelerates.</summary>
    EaseOutQuad,

    /// <summary>Quadratic ease-in/out; slow start and end, fast middle.</summary>
    EaseInOutQuad,

    /// <summary>Cubic ease-in; starts very slowly, strong acceleration.</summary>
    EaseInCubic,

    /// <summary>Cubic ease-out; starts quickly, strong deceleration.</summary>
    EaseOutCubic,

    /// <summary>Cubic ease-in/out; gentle start and finish, smooth center.</summary>
    EaseInOutCubic,

    /// <summary>Quartic ease-in; very slow start with steep acceleration.</summary>
    EaseInQuart,

    /// <summary>Quartic ease-out; fast start with steep deceleration.</summary>
    EaseOutQuart,

    /// <summary>Quartic ease-in/out; balanced acceleration and deceleration.</summary>
    EaseInOutQuart,

    /// <summary>Quintic ease-in; extremely slow start with powerful acceleration.</summary>
    EaseInQuint,

    /// <summary>Quintic ease-out; very fast start with strong deceleration.</summary>
    EaseOutQuint,

    /// <summary>Quintic ease-in/out; smoothest ease with a flat midpoint.</summary>
    EaseInOutQuint,

    /// <summary>SmoothStep; Hermite interpolation (3t² - 2t³), smooth edges.</summary>
    SmoothStep,

    /// <summary>SmootherStep; improved Hermite interpolation (6t⁵ - 15t⁴ + 10t³) with gentler transitions.</summary>
    SmootherStep
}
