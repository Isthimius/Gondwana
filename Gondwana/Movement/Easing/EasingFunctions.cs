namespace Gondwana.Movement.Easing;

/// <summary>
/// Common easing functions for scripted/tweened movement.
/// Each function expects t in [0,1] and returns a value in [0,1].
/// </summary>
public static class EasingFunctions
{
    /// <summary>Linear progression; no easing. Constant speed.</summary>
    public static float Linear(float t) => t;

    /// <summary>Eases in slowly, then accelerates (quadratic).</summary>
    public static float EaseInQuad(float t) => t * t;

    /// <summary>Starts fast, eases out as it approaches the end (quadratic).</summary>
    public static float EaseOutQuad(float t) => t * (2f - t);

    /// <summary>Slow start and slow end; faster in the middle (quadratic).</summary>
    public static float EaseInOutQuad(float t)
    {
        t *= 2f;
        if (t < 1f) return 0.5f * t * t;
        t -= 1f;
        return -0.5f * (t * (t - 2f) - 1f);
    }

    /// <summary>Strong ease-in; very slow start then ramps up (cubic).</summary>
    public static float EaseInCubic(float t) => t * t * t;

    /// <summary>Strong ease-out; starts fast then glides to a stop (cubic).</summary>
    public static float EaseOutCubic(float t)
    {
        t -= 1f;
        return t * t * t + 1f;
    }

    /// <summary>Pronounced ease at both ends; smooth middle (cubic).</summary>
    public static float EaseInOutCubic(float t)
    {
        t *= 2f;
        if (t < 1f) return 0.5f * t * t * t;
        t -= 2f;
        return 0.5f * (t * t * t + 2f);
    }

    /// <summary>Very strong ease-in; crawls at start then accelerates hard (quartic).</summary>
    public static float EaseInQuart(float t) => t * t * t * t;

    /// <summary>Very strong ease-out; blasts then glides (quartic).</summary>
    public static float EaseOutQuart(float t)
    {
        t -= 1f;
        return 1f - (t * t * t * t);
    }

    /// <summary>Heavy easing at both ends; cinematic feel (quartic).</summary>
    public static float EaseInOutQuart(float t)
    {
        t *= 2f;
        if (t < 1f) return 0.5f * t * t * t * t;
        t -= 2f;
        return -0.5f * (t * t * t * t - 2f);
    }

    /// <summary>Extremely strong ease-in; barely moves at start (quintic).</summary>
    public static float EaseInQuint(float t) => t * t * t * t * t;

    /// <summary>Extremely strong ease-out; rockets then settles (quintic).</summary>
    public static float EaseOutQuint(float t)
    {
        t -= 1f;
        return t * t * t * t * t + 1f;
    }

    /// <summary>Maximal easing at both ends; very smooth (quintic).</summary>
    public static float EaseInOutQuint(float t)
    {
        t *= 2f;
        if (t < 1f) return 0.5f * t * t * t * t * t;
        t -= 2f;
        return 0.5f * (t * t * t * t * t + 2f);
    }

    /// <summary>Hermite smoothstep; gentle ease-in/out (C1 continuous).</summary>
    public static float SmoothStep(float t) => t * t * (3f - 2f * t);

    /// <summary>Smootherstep; stronger smoothing (C2 continuous).</summary>
    public static float SmootherStep(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

    /// <summary>Map an <see cref="EasingKind"/> to its function.</summary>
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
