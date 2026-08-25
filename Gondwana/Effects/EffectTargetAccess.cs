using System.Drawing;
using Gondwana.Rendering.Views;
using Gondwana.Scenes;

namespace Gondwana.Effects;

internal static class EffectTargetAccess
{
    internal static float GetOpacity(object target) => target switch
    {
        View view => view.EffectOpacity,
        SceneLayer layer => layer.EffectOpacity,
        _ => throw Unsupported(target)
    };

    internal static void SetOpacity(object target, float value)
    {
        value = Math.Clamp(value, 0f, 1f);

        switch (target)
        {
            case View view:
                view.EffectOpacity = value;
                break;
            case SceneLayer layer:
                layer.EffectOpacity = value;
                break;
            default:
                throw Unsupported(target);
        }
    }

    internal static float GetReveal(object target) => target switch
    {
        View view => view.EffectReveal,
        SceneLayer layer => layer.EffectReveal,
        _ => throw Unsupported(target)
    };

    internal static EffectDirection GetRevealDirection(object target) => target switch
    {
        View view => view.EffectRevealDirection,
        SceneLayer layer => layer.EffectRevealDirection,
        _ => throw Unsupported(target)
    };

    internal static void SetReveal(
        object target,
        float value,
        EffectDirection direction)
    {
        value = Math.Clamp(value, 0f, 1f);

        switch (target)
        {
            case View view:
                view.EffectReveal = value;
                view.EffectRevealDirection = direction;
                break;
            case SceneLayer layer:
                layer.EffectReveal = value;
                layer.EffectRevealDirection = direction;
                break;
            default:
                throw Unsupported(target);
        }
    }

    internal static PointF GetOffsetFactor(object target) => target switch
    {
        View view => view.EffectOffsetFactor,
        SceneLayer layer => layer.EffectOffsetFactor,
        _ => throw Unsupported(target)
    };

    internal static PointF GetOffsetPixels(object target) => target switch
    {
        View view => view.EffectOffsetPx,
        SceneLayer layer => layer.EffectOffsetPx,
        _ => throw Unsupported(target)
    };

    internal static void SetTransform(
        object target,
        PointF factor,
        PointF pixels)
    {
        switch (target)
        {
            case View view:
                view.EffectOffsetFactor = factor;
                view.EffectOffsetPx = pixels;
                break;
            case SceneLayer layer:
                layer.EffectOffsetFactor = factor;
                layer.EffectOffsetPx = pixels;
                break;
            default:
                throw Unsupported(target);
        }
    }

    private static ArgumentException Unsupported(object target) =>
        new($"Unsupported effect target type: {target.GetType().FullName}.", nameof(target));
}
