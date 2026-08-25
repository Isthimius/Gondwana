using System.Drawing;

namespace Gondwana.Effects;

internal static class EffectGeometry
{
    internal static RectangleF GetRevealRect(
        RectangleF bounds,
        EffectDirection direction,
        float progress)
    {
        progress = Math.Clamp(progress, 0f, 1f);

        if (progress <= 0f || bounds.Width <= 0f || bounds.Height <= 0f)
            return RectangleF.Empty;

        if (progress >= 1f || direction == EffectDirection.None)
            return bounds;

        float width = bounds.Width * progress;
        float height = bounds.Height * progress;

        return direction switch
        {
            EffectDirection.FromLeftToRight =>
                new RectangleF(bounds.Left, bounds.Top, width, bounds.Height),
            EffectDirection.FromRightToLeft =>
                new RectangleF(bounds.Right - width, bounds.Top, width, bounds.Height),
            EffectDirection.FromTopToBottom =>
                new RectangleF(bounds.Left, bounds.Top, bounds.Width, height),
            EffectDirection.FromBottomToTop =>
                new RectangleF(bounds.Left, bounds.Bottom - height, bounds.Width, height),
            EffectDirection.FromTopLeftToBottomRight =>
                new RectangleF(bounds.Left, bounds.Top, width, height),
            EffectDirection.FromTopRightToBottomLeft =>
                new RectangleF(bounds.Right - width, bounds.Top, width, height),
            EffectDirection.FromBottomLeftToTopRight =>
                new RectangleF(bounds.Left, bounds.Bottom - height, width, height),
            EffectDirection.FromBottomRightToTopLeft =>
                new RectangleF(bounds.Right - width, bounds.Bottom - height, width, height),
            _ => bounds
        };
    }
}
