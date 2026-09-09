using Gondwana.Assets;
using Gondwana.Cli.Commands.Assets;
using Gondwana.Drawing.Tilesheets.GTS;
using SkiaSharp;

namespace Gondwana.Cli.Commands.Tilesheets;

internal sealed record TilesheetInspection(TilesheetDefinition? Definition, int? Width, int? Height, IReadOnlyList<string> Errors)
{
    public static TilesheetInspection FromFile(string path)
    {
        try
        {
            return Inspect(TilesheetDefinitionSerializer.Load(path), Path.GetDirectoryName(Path.GetFullPath(path))!, null);
        }
        catch (Exception ex) { return new(null, null, null, [ex.Message]); }
    }

    public static TilesheetInspection FromStream(Stream stream, string baseDirectory, AssetsFile defaultBundle)
    {
        try { return Inspect(TilesheetDefinitionSerializer.Load(stream), baseDirectory, defaultBundle); }
        catch (Exception ex) { return new(null, null, null, [ex.Message]); }
    }

    private static TilesheetInspection Inspect(TilesheetDefinition definition, string baseDirectory, AssetsFile? defaultBundle)
    {
        var errors = new List<string>();
        int? width = null, height = null;
        AssetsFile? ownedBundle = null;
        try
        {
            var image = definition.Image ?? throw new InvalidDataException("Image source is required.");
            var hasFile = !string.IsNullOrWhiteSpace(image.FilePath);
            var hasBundle = !string.IsNullOrWhiteSpace(image.AssetsFilePath);
            var hasEntry = !string.IsNullOrWhiteSpace(image.AssetEntryName);
            if (hasFile && (hasBundle || hasEntry)) throw new InvalidDataException("Image.FilePath cannot be combined with an asset source.");
            Stream stream;
            if (hasFile) stream = File.OpenRead(Resolve(baseDirectory, image.FilePath!));
            else
            {
                if (!hasEntry || (!hasBundle && defaultBundle is null)) throw new InvalidDataException("Image requires FilePath or AssetsFilePath and AssetEntryName (or a containing bundle).");
                var bundle = defaultBundle;
                if (hasBundle)
                {
                    var path = Resolve(baseDirectory, image.AssetsFilePath!);
                    if (!File.Exists(path)) throw new FileNotFoundException("Image bundle not found: " + path);
                    if (defaultBundle is null || !Path.GetFullPath(defaultBundle.FilePath).Equals(path, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                        bundle = ownedBundle = AssetsFile.LoadOrCreate(path);
                }
                stream = bundle![AssetTypes.Image, image.AssetEntryName!] ?? throw new InvalidDataException("Image entry not found: " + image.AssetEntryName);
            }
            using (stream)
            using (var codec = SKCodec.Create(stream))
            {
                if (codec is null) throw new InvalidDataException("Source image is unreadable or has an unsupported format.");
                width = codec.Info.Width;
                height = codec.Info.Height;
            }
        }
        catch (Exception ex) { errors.Add("Image: " + ex.Message); }
        finally { ownedBundle?.Dispose(); }
        errors.AddRange(TilesheetDefinitionValidator.Validate(definition, width, height));
        return new(definition, width, height, errors);
    }

    private static string Resolve(string root, string path) => SafeFilePath.Resolve(root, path);
}
