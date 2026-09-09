using System.Drawing;
using System.IO.Compression;
using Gondwana.Assets;
using Gondwana.Cli.Commands.Assets;
using Gondwana.Cli.Commands.Tilesheets;
using Gondwana.Drawing;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Physics.Collisions;
using SkiaSharp;
using Spectre.Console.Cli;

namespace Gondwana.Tests.Cli;

[Collection("Global engine state")]
public sealed class AssetAndTilesheetCommandTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "GondwanaContent_" + Guid.NewGuid().ToString("N"));
    public AssetAndTilesheetCommandTests() => Directory.CreateDirectory(root);
    public void Dispose() => Directory.Delete(root, recursive: true);
    private static int Run(params string[] args)
    {
        var app = new CommandApp();
        app.Configure(c =>
        {
            c.AddCommand<AssetsPackCommand>("pack"); c.AddCommand<AssetsValidateCommand>("validate");
            c.AddCommand<AssetsInspectCommand>("inspect"); c.AddCommand<AssetsExtractCommand>("unpack");
            c.AddCommand<TilesheetValidateCommand>("gts-validate"); c.AddCommand<TilesheetInfoCommand>("gts-info");
        });
        return app.Run(args);
    }

    private string Bundle(params string[] entries)
    {
        var file = Path.Combine(root, Guid.NewGuid() + ".assets");
        using var zip = ZipFile.Open(file, ZipArchiveMode.Create);
        foreach (var name in entries)
        {
            using var writer = new StreamWriter(zip.CreateEntry(name).Open());
            writer.Write("content");
        }
        return file;
    }

    [Fact]
    public void Bundle_RejectsMalformedFilesAndKeysAndDuplicates()
    {
        var file = Path.Combine(root, "bad.assets");
        File.WriteAllText(file, "not a zip");
        Assert.Equal(1, Run("validate", file));
        Assert.Equal(1, Run("validate", Bundle("unknown-entry")));
        Assert.Equal(1, Run("validate", Bundle("999_bad.bin")));
        Assert.Equal(1, Run("validate", Bundle("Misc_same.bin", "Misc_SAME.bin")));
        Assert.Equal(1, Run("validate", Path.Combine(root, "missing.assets")));
        Assert.False(File.Exists(Path.Combine(root, "missing.assets")));
    }

    [Theory]
    [InlineData("../escape.txt")]
    [InlineData("..\\escape.txt")]
    [InlineData("/escape.txt")]
    [InlineData("C:\\escape.txt")]
    [InlineData("safe/file.txt:stream")]
    [InlineData("safe/../escape.txt")]
    [InlineData("CON.txt")]
    public void Unpack_RejectsUnsafePathsBeforeAnyWrite(string name)
    {
        var output = Path.Combine(root, "out");
        Assert.Equal(1, Run("unpack", Bundle("Misc_good.txt", "Misc_" + name), output));
        Assert.False(Directory.Exists(output));
    }

    [Fact]
    public void Unpack_PreservesPaths_AndRequiresExplicitOverwrite()
    {
        var output = Path.Combine(root, "out");
        var file = Bundle("Misc_nested/file.txt");
        Assert.Equal(0, Run("validate", file));
        Assert.Equal(0, Run("inspect", file));
        Assert.Equal(0, Run("unpack", file, output));
        var target = Path.Combine(output, "nested", "file.txt");
        Assert.Equal("content", File.ReadAllText(target));
        File.WriteAllText(target, "keep");
        Assert.Equal(1, Run("unpack", file, output));
        Assert.Equal("keep", File.ReadAllText(target));
        Assert.Equal(0, Run("unpack", file, output, "--overwrite"));
        Assert.Equal("content", File.ReadAllText(target));
    }

    [Fact]
    public void Unpack_RejectsCrossTypeFilenameCollisions()
    {
        var output = Path.Combine(root, "out");
        Assert.Equal(1, Run("unpack", Bundle("Misc_file.txt", "Audio_file.txt"), output));
        Assert.False(Directory.Exists(output));
    }

    [Fact]
    public void PackAlias_RoundTrips_AndEncryptedValidationRequiresPassword()
    {
        var source = Path.Combine(root, "source");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "readme.txt"), "hello");
        var bundle = Path.Combine(root, "packed.assets");
        Assert.Equal(0, Run("pack", source, bundle));
        Assert.Equal(0, Run("validate", bundle));
        Assert.Equal(0, Run("pack", source, bundle, "--password", "test-only", "--encrypt"));
        Assert.Equal(1, Run("validate", bundle));
        Assert.Equal(0, Run("validate", bundle, "--password", "test-only"));
    }

    private TilesheetDefinition Definition()
    {
        using var bitmap = new SKBitmap(32, 32);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using (var output = File.Create(Path.Combine(root, "image.png"))) data.SaveTo(output);
        return new TilesheetDefinition
        {
            Name = "fixture", Image = new() { FilePath = "image.png" },
            Regions = [new() { Name = "main", Area = new Rectangle(0, 0, 32, 32), TileSize = new Size(16, 16), Frames = [new() { XTile = 1, YTile = 1 }] }]
        };
    }

    [Fact]
    public void Tilesheet_ValidAndMalformedFiles_ReturnAppropriateExitCodes()
    {
        var path = Path.Combine(root, "sheet.gts");
        TilesheetDefinitionSerializer.Save(path, Definition());
        Assert.Empty(TilesheetInspection.FromFile(path).Errors);
        Assert.Equal(0, Run("gts-validate", path));
        Assert.Equal(0, Run("gts-info", path));
        File.WriteAllText(path, "{ malformed");
        Assert.Equal(1, Run("gts-validate", path));
        File.WriteAllText(path, "{\"Regions\":[null]}");
        Assert.Equal(1, Run("gts-validate", path));
        File.WriteAllText(path, "{}");
        Assert.Equal(1, Run("gts-validate", path));
    }

    [Fact]
    public void Tilesheet_ReportsMissingImageBoundsCollisionOverhangAndFrameFailures()
    {
        var definition = Definition();
        var region = definition.Regions[0];
        region.Area = new Rectangle(16, 0, 32, 32);
        region.Overhang = new Spacing(-1, 0, 0, 0);
        region.CollisionAdjust = new CollisionAdjust(0, 0, 17, 0);
        region.Frames.Add(new() { XTile = 1, YTile = 1 });
        region.Frames.Add(new() { XTile = 3, YTile = 2 });
        var errors = TilesheetDefinitionValidator.Validate(definition, 32, 32);
        Assert.Contains(errors, e => e.Contains("outside the source image"));
        Assert.Contains(errors, e => e.Contains("overhang"));
        Assert.Contains(errors, e => e.Contains("invert"));
        Assert.Contains(errors, e => e.Contains("duplicate frame"));
        Assert.Contains(errors, e => e.Contains("outside the frame grid"));
        definition.Regions.Add(new() { Name = "MAIN", TileSize = new Size(0, 0) });
        errors = TilesheetDefinitionValidator.Validate(definition);
        Assert.Contains(errors, e => e.Contains("duplicate name"));
        Assert.Contains(errors, e => e.Contains("dimensions must be positive"));
        definition.Image.FilePath = "missing.png";
        var path = Path.Combine(root, "bad.gts");
        TilesheetDefinitionSerializer.Save(path, definition);
        Assert.Contains(TilesheetInspection.FromFile(path).Errors, e => e.StartsWith("Image:"));
    }

    [Fact]
    public void Tilesheet_AllowsNegativeCollisionInsetsAndUsesRuntimePaddingGrid()
    {
        var definition = Definition();
        var region = definition.Regions[0];
        region.CollisionAdjust = new CollisionAdjust(-2, -2, -2, -2);
        region.TilePadding = new Spacing(1, 1, 1, 1);
        region.RegionMargin = new Spacing(1, 1, 1, 1);
        region.Frames.Clear();
        Assert.Equal((1L, 1L), TilesheetDefinitionValidator.GridSize(region));
        Assert.Empty(TilesheetDefinitionValidator.Validate(definition, 32, 32));
    }

    [Fact]
    public void Bundle_ValidatesPackedGtsAgainstContainingImage()
    {
        var definition = Definition();
        definition.Image = new() { AssetEntryName = "image.png" };
        var file = Path.Combine(root, "packed.assets");
        using (var bundle = AssetsFile.LoadOrCreate(file))
        {
            bundle.Add(AssetTypes.Image, Path.Combine(root, "image.png"), "image.png");
            var gts = Path.Combine(root, "packed.gts");
            TilesheetDefinitionSerializer.Save(gts, definition);
            bundle.Add(AssetTypes.TilesheetDefinition, gts, "packed.gts");
            bundle.Save();
        }
        Assert.Equal(0, Run("validate", file));
        Assert.Equal(1, Run("validate", Bundle("TilesheetDefinition_bad.gts")));
    }

    [Theory]
    [InlineData("../image.png")]
    [InlineData("..\\image.png")]
    [InlineData("/image.png")]
    [InlineData("C:\\image.png")]
    public void Tilesheet_RejectsEscapingImageAndBundleReferences(string reference)
    {
        var definition = Definition();
        var path = Path.Combine(root, "untrusted.gts");
        foreach (var image in new[] { new TilesheetImageDefinition { FilePath = reference }, new TilesheetImageDefinition { AssetsFilePath = reference, AssetEntryName = "image.png" } })
        {
            definition.Image = image;
            TilesheetDefinitionSerializer.Save(path, definition);
            var result = TilesheetInspection.FromFile(path);
            Assert.Null(result.Width);
            Assert.Contains(result.Errors, e => e.Contains("Unsafe asset path"));
            var packed = Path.Combine(root, "untrusted.assets");
            using (var bundle = AssetsFile.LoadOrCreate(packed))
            {
                bundle.Add(AssetTypes.TilesheetDefinition, path, "untrusted.gts");
                bundle.Save();
            }
            Assert.Contains(BundleHelper.ValidateContents(packed, null), e => e.Contains("Unsafe asset path"));
        }
    }

    [Fact]
    public void Inspection_SkipsPayloadIntegrityPass_WhileValidationAndExtractionEnforceIt()
    {
        var path = Path.Combine(root, "bad-crc.assets");
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        using (var writer = new StreamWriter(zip.CreateEntry("Misc_data.txt", CompressionLevel.NoCompression).Open())) writer.Write("original payload");
        var bytes = File.ReadAllBytes(path);
        var offset = bytes.AsSpan().IndexOf(System.Text.Encoding.UTF8.GetBytes("original payload"));
        Assert.True(offset >= 0);
        bytes[offset] = (byte)'X';
        File.WriteAllBytes(path, bytes);
        Assert.Equal(0, Run("inspect", path));
        Assert.Equal(1, Run("validate", path));
        var output = Path.Combine(root, "out");
        Assert.Equal(1, Run("unpack", path, output));
        Assert.False(Directory.Exists(output));
    }
}
