using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.SkiaSharp;
using Gondwana.Tooling.Assets;
using Gondwana.Tooling.Tilesheets.Sources;
using SkiaSharp;

namespace Gondwana.Tooling.WinForms;

/// <summary>Shared Skia decoding/masking and owned GDI bitmap conversion for authoring controls.</summary>
internal static class AuthoringPreviewImage
{
    public static Bitmap Load(TilesheetDefinition definition, string baseDirectory,
        AssetPackageCatalog packages, string? containingArchive = null)
    {
        using var decoded = new AuthoringResourceResolver(packages).DecodeImage(definition, baseDirectory, containingArchive);
        using var rgba = new SKBitmap(new SKImageInfo(decoded.Width, decoded.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul));
        using var pixels = decoded.PeekPixels();
        if (!pixels.ReadPixels(rgba.Info, rgba.GetPixels(), rgba.RowBytes, 0, 0))
            throw new InvalidDataException("Could not decode image pixels.");
        if (definition.Mask is { } mask)
            rgba.ApplyAlphaMask(new SKColor(mask.Red, mask.Green, mask.Blue, mask.Alpha), mask.Tolerance);
        using var encoded = rgba.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = encoded.AsStream();
        using var bitmap = new Bitmap(stream);
        return new Bitmap(bitmap);
    }
}
