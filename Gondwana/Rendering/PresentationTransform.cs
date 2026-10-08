using System.Drawing;
using SkiaSharp;

namespace Gondwana.Rendering;

/// <summary>Aspect-preserving mapping between logical Backbuffer ScreenPx and adapter pixels.</summary>
/// <param name="Scale">The scale from logical backbuffer pixels to adapter pixels.</param>
/// <param name="DestinationRect">The centered presentation rectangle in adapter pixels.</param>
public readonly record struct PresentationTransform(float Scale, SKRect DestinationRect)
{
    /// <summary>Fits the entire logical image inside the adapter, centered on both axes.</summary>
    /// <param name="bufferWidth">The logical backbuffer width in pixels.</param>
    /// <param name="bufferHeight">The logical backbuffer height in pixels.</param>
    /// <param name="adapterWidth">The adapter width in pixels.</param>
    /// <param name="adapterHeight">The adapter height in pixels.</param>
    /// <returns>The aspect-preserving scale and centered destination rectangle.</returns>
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
    /// <param name="adapterPx">The adapter px.</param>
    /// <param name="screenPx">The point in logical backbuffer screen pixels.</param>
    /// <returns><see langword="true"/> if the operation succeeded; otherwise, <see langword="false"/>.</returns>
    public bool TryAdapterPxToScreenPx(PointF adapterPx, out PointF screenPx)
    {
        screenPx = Scale > 0
            ? new((adapterPx.X - DestinationRect.Left) / Scale, (adapterPx.Y - DestinationRect.Top) / Scale)
            : new(-1, -1);
        return Scale > 0 && DestinationRect.Contains(adapterPx.X, adapterPx.Y);
    }

    /// <summary>Maps logical dirty edges outwards to avoid gaps at fractional presentation scales.</summary>
    /// <param name="screenRect">The rectangle in logical backbuffer screen pixels.</param>
    /// <returns>The enclosing adapter-pixel rectangle, rounded outward to cover fractional edges.</returns>
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
