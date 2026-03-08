namespace Gondwana.Movement.Easing;

/// <summary>
/// Enumeration of all supported easing functions for animation and movement interpolation.
/// Each easing function expects a normalized time value t ∈ [0,1] and returns a smoothed progression value ∈ [0,1].
/// Use with <see cref="EasingFunctions.From"/> to obtain the corresponding easing function delegate.
/// </summary>
public enum EasingKind
{
    /// <summary>
    /// Linear easing with constant velocity throughout the transition.
    /// No acceleration or deceleration is applied; the output equals the input.
    /// Formula: f(t) = t
    /// </summary>
    Linear,

    /// <summary>
    /// Quadratic ease-in curve that starts slowly and gradually accelerates.
    /// Creates a gentle acceleration effect suitable for subtle animations.
    /// Formula: f(t) = t²
    /// </summary>
    EaseInQuad,

    /// <summary>
    /// Quadratic ease-out curve that starts quickly and gradually decelerates.
    /// Creates a gentle deceleration effect as the animation approaches completion.
    /// Formula: f(t) = t(2 - t)
    /// </summary>
    EaseOutQuad,

    /// <summary>
    /// Quadratic ease-in/out curve with slow start and end, faster movement in the middle.
    /// Provides smooth acceleration and deceleration for balanced animations.
    /// Combines ease-in for the first half and ease-out for the second half.
    /// </summary>
    EaseInOutQuad,

    /// <summary>
    /// Cubic ease-in curve that starts very slowly with strong acceleration toward the end.
    /// More pronounced than quadratic easing; ideal for dramatic entrances.
    /// Formula: f(t) = t³
    /// </summary>
    EaseInCubic,

    /// <summary>
    /// Cubic ease-out curve that starts quickly with strong deceleration toward the end.
    /// More pronounced than quadratic easing; ideal for dramatic stops.
    /// Formula: f(t) = (t-1)³ + 1
    /// </summary>
    EaseOutCubic,

    /// <summary>
    /// Cubic ease-in/out curve with gentle start and finish, smooth transition through the center.
    /// Provides more pronounced easing than quadratic while maintaining smoothness.
    /// Well-suited for natural-feeling object movements.
    /// </summary>
    EaseInOutCubic,

    /// <summary>
    /// Quartic ease-in curve with very slow start and steep acceleration.
    /// Creates a heavy, cinematic feel with dramatic acceleration buildup.
    /// Formula: f(t) = t⁴
    /// </summary>
    EaseInQuart,

    /// <summary>
    /// Quartic ease-out curve with fast start and steep deceleration.
    /// Creates a heavy, cinematic feel with dramatic deceleration to rest.
    /// Formula: f(t) = 1 - (t-1)⁴
    /// </summary>
    EaseOutQuart,

    /// <summary>
    /// Quartic ease-in/out curve with balanced acceleration and deceleration.
    /// Heavy easing at both ends creates a cinematic, weighty motion feel.
    /// Ideal for important UI transitions or camera movements.
    /// </summary>
    EaseInOutQuart,

    /// <summary>
    /// Quintic ease-in curve with extremely slow start and powerful acceleration.
    /// The strongest ease-in; barely moves at the beginning before accelerating dramatically.
    /// Formula: f(t) = t⁵
    /// </summary>
    EaseInQuint,

    /// <summary>
    /// Quintic ease-out curve with very fast start and strong deceleration.
    /// The strongest ease-out; rockets forward then settles gradually to rest.
    /// Formula: f(t) = (t-1)⁵ + 1
    /// </summary>
    EaseOutQuint,

    /// <summary>
    /// Quintic ease-in/out curve providing the smoothest easing with nearly flat velocity at the midpoint.
    /// Maximal easing at both ends creates extremely smooth, natural-feeling motion.
    /// Best for long, flowing animations requiring the most gradual transitions.
    /// </summary>
    EaseInOutQuint,

    /// <summary>
    /// Hermite smoothstep interpolation with smooth edges and continuous first derivative (C¹).
    /// Provides gentle ease-in and ease-out with zero velocity at endpoints.
    /// Formula: f(t) = 3t² - 2t³
    /// Commonly used in procedural generation and shader programming.
    /// </summary>
    SmoothStep,

    /// <summary>
    /// Ken Perlin's improved smootherstep interpolation with gentler transitions and continuous second derivative (C²).
    /// Even smoother than SmoothStep with flatter velocity curve at the center.
    /// Formula: f(t) = 6t⁵ - 15t⁴ + 10t³
    /// Preferred for high-quality interpolation where smoothness is critical.
    /// </summary>
    SmootherStep
}
