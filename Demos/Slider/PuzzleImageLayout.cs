namespace Gondwana.Demos.Slider;

/// <summary>Aspect-preserving placement of an image inside the integer-cell puzzle atlas.</summary>
internal static class PuzzleImageLayout
{
    internal static RectangleF FitImage(int sourceWidth, int sourceHeight, int atlasWidth, int atlasHeight)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0 || atlasWidth <= 0 || atlasHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(sourceWidth), "Image and atlas dimensions must be positive.");

        double scale = Math.Min(atlasWidth / (double)sourceWidth, atlasHeight / (double)sourceHeight);
        float width = (float)(sourceWidth * scale);
        float height = (float)(sourceHeight * scale);

        return new RectangleF((atlasWidth - width) / 2f, (atlasHeight - height) / 2f, width, height);
    }
}
