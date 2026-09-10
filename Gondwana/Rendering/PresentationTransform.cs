using System.Drawing;
using SkiaSharp;

namespace Gondwana.Rendering;

/// <summary>Aspect-preserving mapping between logical Backbuffer ScreenPx and adapter pixels.</summary>
public readonly record struct PresentationTransform(float Scale, SKRect DestinationRect)
{
    /// <summary>Fits the entire logical image inside the adapter, centered on both axes.</summary>
    public static PresentationTransform Fit(int bufferWidth, int bufferHeight, int adapterWidth, int adapterHeight)
    {
        if (bufferWidth <= 0 || bufferHeight <= 0 || adapterWidth <= 0 || adapterHeight <= 0)
            return default;

        float scale = Math.Min((float)adapterWidth / bufferWidth, (float)adapterHeight / bufferHeight);
        float width = bufferWidth * scale;
        float height = bufferHeight * scale;
        return new(scale, SKRect.Create((adapterWidth - width) / 2f, (adapterHeight - height) / 2f, width, height));
    }

    /// <summary>Maps input without clamping margins. False means outside the presented game area.</summary>
    public bool TryAdapterPxToScreenPx(PointF adapterPx, out PointF screenPx)
    {
        screenPx = Scale > 0
            ? new((adapterPx.X - DestinationRect.Left) / Scale, (adapterPx.Y - DestinationRect.Top) / Scale)
            : new(-1, -1);
        return Scale > 0 && DestinationRect.Contains(adapterPx.X, adapterPx.Y);
    }

    /// <summary>Maps logical dirty edges outwards to avoid gaps at fractional presentation scales.</summary>
    public Rectangle ScreenRectToAdapterRect(Rectangle screenRect)
        => Scale <= 0 ? Rectangle.Empty : Rectangle.FromLTRB(
            (int)Math.Floor(DestinationRect.Left + screenRect.Left * Scale),
            (int)Math.Floor(DestinationRect.Top + screenRect.Top * Scale),
            (int)Math.Ceiling(DestinationRect.Left + screenRect.Right * Scale),
            (int)Math.Ceiling(DestinationRect.Top + screenRect.Bottom * Scale));

    internal static int ScaleDimension(int dimension, float renderScale)
    {
        double scaled = Math.Round(dimension * (double)renderScale, MidpointRounding.AwayFromZero);
        if (scaled > int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(renderScale), "Scaled dimension exceeds the supported integer range.");
        return Math.Max(1, (int)scaled);
    }
}

/// <summary>Filtering used only when presenting the finished Backbuffer.</summary>
public enum RenderScalingFilter
{
    /// <summary>Bilinear presentation filtering.</summary>
    Linear,
    /// <summary>Unfiltered nearest-pixel presentation for pixel art.</summary>
    NearestNeighbor
}
