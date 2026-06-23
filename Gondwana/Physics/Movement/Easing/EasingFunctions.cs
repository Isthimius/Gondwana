namespace Gondwana.Physics.Movement.Easing;

/// <summary>
/// Common easing functions for scripted/tweened movement.
/// Each function expects t in [0,1] and returns a value in [0,1].
/// </summary>
public static class EasingFunctions
{
    /// <summary>Linear progression; no easing. Constant speed.</summary>
    /// <param name="t">Normalized time value between 0 and 1.</param>
    /// <returns>The linear interpolation value, equal to the input.</returns>
    public static float Linear(float t) => t;

    /// <summary>Eases in slowly, then accelerates (quadratic).</summary>
    /// <param name="t">Normalized time value between 0 and 1.</param>
    /// <returns>The eased value following a quadratic curve, starting slowly and accelerating.</returns>
    public static float EaseInQuad(float t) => t * t;

    /// <summary>Starts fast, eases out as it approaches the end (quadratic).</summary>
    /// <param name="t">Normalized time value between 0 and 1.</param>
    /// <returns>The eased value following a quadratic curve, starting quickly and decelerating.</returns>
    public static float EaseOutQuad(float t) => t * (2f - t);

    /// <summary>Slow start and slow end; faster in the middle (quadratic).</summary>
    /// <param name="t">Normalized time value between 0 and 1.</param>
    /// <returns>The eased value with quadratic acceleration and deceleration at both ends.</returns>
    public static float EaseInOutQuad(float t)
    {
        t *= 2f;
        if (t < 1f) return 0.5f * t * t;
        t -= 1f;
        return -0.5f * (t * (t - 2f) - 1f);
    }

    /// <summary>Strong ease-in; very slow start then ramps up (cubic).</summary>
    /// <param name="t">Normalized time value between 0 and 1.</param>
    /// <returns>The eased value following a cubic curve, starting very slowly and accelerating strongly.</returns>
    public static float EaseInCubic(float t) => t * t * t;

    /// <summary>Strong ease-out; starts fast then glides to a stop (cubic).</summary>
    /// <param name="t">Normalized time value between 0 and 1.</param>
    /// <returns>The eased value following a cubic curve, starting quickly and decelerating strongly.</returns>
    public static float EaseOutCubic(float t)
    {
        t -= 1f;
        return t * t * t + 1f;
    }

    /// <summary>Pronounced ease at both ends; smooth middle (cubic).</summary>
    /// <param name="t">Normalized time value between 0 and 1.</param>
    /// <returns>The eased value with cubic acceleration and deceleration at both ends.</returns>
    public static float EaseInOutCubic(float t)
    {
        t *= 2f;
        if (t < 1f) return 0.5f * t * t * t;
        t -= 2f;
        return 0.5f * (t * t * t + 2f);
    }

    /// <summary>Very strong ease-in; crawls at start then accelerates hard (quartic).</summary>
    /// <param name="t">Normalized time value between 0 and 1.</param>
    /// <returns>The eased value following a quartic curve, starting extremely slowly and accelerating heavily.</returns>
    public static float EaseInQuart(float t) => t * t * t * t;

    /// <summary>Very strong ease-out; blasts then glides (quartic).</summary>
    /// <param name="t">Normalized time value between 0 and 1.</param>
    /// <returns>The eased value following a quartic curve, starting very quickly and decelerating heavily.</returns>
    public static float EaseOutQuart(float t)
    {
        t -= 1f;
        return 1f - t * t * t * t;
    }

    /// <summary>Heavy easing at both ends; cinematic feel (quartic).</summary>
    /// <param name="t">Normalized time value between 0 and 1.</param>
    /// <returns>The eased value with quartic acceleration and deceleration at both ends.</returns>
    public static float EaseInOutQuart(float t)
    {
        t *= 2f;
        if (t < 1f) return 0.5f * t * t * t * t;
        t -= 2f;
        return -0.5f * (t * t * t * t - 2f);
    }

    /// <summary>Extremely strong ease-in; barely moves at start (quintic).</summary>
    /// <param name="t">Normalized time value between 0 and 1.</param>
    /// <returns>The eased value following a quintic curve, starting almost imperceptibly and accelerating dramatically.</returns>
    public static float EaseInQuint(float t) => t * t * t * t * t;

    /// <summary>Extremely strong ease-out; rockets then settles (quintic).</summary>
    /// <param name="t">Normalized time value between 0 and 1.</param>
    /// <returns>The eased value following a quintic curve, starting explosively and decelerating dramatically.</returns>
    public static float EaseOutQuint(float t)
    {
        t -= 1f;
        return t * t * t * t * t + 1f;
    }

    /// <summary>Maximal easing at both ends; very smooth (quintic).</summary>
    /// <param name="t">Normalized time value between 0 and 1.</param>
    /// <returns>The eased value with quintic acceleration and deceleration at both ends.</returns>
    public static float EaseInOutQuint(float t)
    {
        t *= 2f;
        if (t < 1f) return 0.5f * t * t * t * t * t;
        t -= 2f;
        return 0.5f * (t * t * t * t * t + 2f);
    }

    /// <summary>Hermite smoothstep; gentle ease-in/out (C1 continuous).</summary>
    /// <param name="t">Normalized time value between 0 and 1.</param>
    /// <returns>The smoothed value using Hermite interpolation with continuous first derivative.</returns>
    public static float SmoothStep(float t) => t * t * (3f - 2f * t);

    /// <summary>Smootherstep; stronger smoothing (C2 continuous).</summary>
    /// <param name="t">Normalized time value between 0 and 1.</param>
    /// <returns>The smoothed value using Ken Perlin's smootherstep with continuous second derivative.</returns>
    public static float SmootherStep(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

    /// <summary>Map an <see cref="EasingKind"/> to its function.</summary>
    /// <param name="kind">The easing kind enumeration value to map.</param>
    /// <returns>A function delegate that performs the specified easing calculation, or <see cref="Linear"/> if the kind is unrecognized.</returns>
    public static Func<float, float> From(EasingKind kind)
    {
        return kind switch
        {
            EasingKind.Linear => Linear,
            EasingKind.EaseInQuad => EaseInQuad,
            EasingKind.EaseOutQuad => EaseOutQuad,
            EasingKind.EaseInOutQuad => EaseInOutQuad,
            EasingKind.EaseInCubic => EaseInCubic,
            EasingKind.EaseOutCubic => EaseOutCubic,
            EasingKind.EaseInOutCubic => EaseInOutCubic,
            EasingKind.EaseInQuart => EaseInQuart,
            EasingKind.EaseOutQuart => EaseOutQuart,
            EasingKind.EaseInOutQuart => EaseInOutQuart,
            EasingKind.EaseInQuint => EaseInQuint,
            EasingKind.EaseOutQuint => EaseOutQuint,
            EasingKind.EaseInOutQuint => EaseInOutQuint,
            EasingKind.SmoothStep => SmoothStep,
            EasingKind.SmootherStep => SmootherStep,
            _ => Linear
        };
    }
}
