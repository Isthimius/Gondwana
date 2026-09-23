using System.IO.Compression;
using System.Text;
using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Animation;
using Gondwana.Drawing.Tilesheets.GTS;
using SkiaSharp;

namespace Gondwana.Tooling.Importers.Tests;

public sealed class AsepriteImportTests
{
    // Handcrafted binary fixtures from the documented ASE layout. No external artwork/tool dependency.
    private static byte[] Chunk(ushort type, Action<BinaryWriter> write)
    {
        using var memory = new MemoryStream(); using var writer = new BinaryWriter(memory);
        writer.Write(0u); writer.Write(type); write(writer);
        writer.Seek(0, SeekOrigin.Begin); writer.Write((uint)memory.Length);
        return memory.ToArray();
    }
    private static void Text(BinaryWriter w, string value) { byte[] bytes = Encoding.UTF8.GetBytes(value); w.Write((ushort)bytes.Length); w.Write(bytes); }
    private static byte[] Fixture(int depth, int direction, bool unsupported = false, int repeat = 0, bool tags = true)
    {
        using var memory = new MemoryStream(); using var w = new BinaryWriter(memory);
        w.Write(new byte[128]);
        for (int f = 0; f < 3; f++)
        {
            var chunks = new List<byte[]>();
            if (f == 0)
            {
                chunks.Add(Chunk(0x2004, c => { c.Write((ushort)1); c.Write((ushort)0); c.Write((ushort)0); c.Write(0u); c.Write((ushort)(unsupported ? 1 : 0)); c.Write((byte)255); c.Write(new byte[3]); Text(c, "Visible"); }));
                chunks.Add(Chunk(0x2004, c => { c.Write((ushort)0); c.Write((ushort)0); c.Write((ushort)0); c.Write(0u); c.Write((ushort)0); c.Write((byte)255); c.Write(new byte[3]); Text(c, "Hidden"); }));
                if (depth == 8) chunks.Add(Chunk(0x2019, c => { c.Write(2u); c.Write(0u); c.Write(1u); c.Write(new byte[8]); c.Write((ushort)0); c.Write(new byte[] { 0, 0, 0, 255 }); c.Write((ushort)0); c.Write(new byte[] { 255, 0, 0, 255 }); }));
                if (tags) chunks.Add(Chunk(0x2018, c => { c.Write((ushort)1); c.Write(new byte[8]); c.Write((ushort)0); c.Write((ushort)2); c.Write((byte)direction); c.Write((ushort)repeat); c.Write(new byte[10]); Text(c, "Walk"); }));
            }
            int frame = f;
            chunks.Add(Chunk(0x2005, c =>
            {
                c.Write((ushort)0); c.Write((short)0); c.Write((short)0); c.Write((byte)255); c.Write((ushort)(frame == 2 ? 1 : frame == 1 ? 2 : 0)); c.Write((short)0); c.Write(new byte[5]);
                if (frame == 2) { c.Write((ushort)0); return; }
                c.Write((ushort)2); c.Write((ushort)1);
                byte[] pixels = depth switch { 32 => [255, 0, 0, 255, 0, 0, 0, 0], 16 => [128, 255, 0, 0], _ => [1, 0] };
                if (frame == 0) c.Write(pixels);
                else { using var z = new ZLibStream(c.BaseStream, CompressionLevel.Optimal, true); z.Write(pixels); }
            }));
            w.Write((uint)(16 + chunks.Sum(c => c.Length))); w.Write((ushort)0xF1FA); w.Write((ushort)chunks.Count); w.Write((ushort)(100 + f * 100)); w.Write((ushort)0); w.Write(0u);
            foreach (var chunk in chunks) w.Write(chunk);
        }
        w.Seek(0, SeekOrigin.Begin); w.Write((uint)memory.Length); w.Write((ushort)0xA5E0); w.Write((ushort)3); w.Write((ushort)2); w.Write((ushort)1); w.Write((ushort)depth); w.Write(1u); w.Write((ushort)100);
        return memory.ToArray();
    }

    [Theory]
    [InlineData(32, 0)]
    [InlineData(16, 1)]
    [InlineData(8, 2)]
    [InlineData(32, 3)]
    public void BinaryFramesRenderAndTagsRoundTrip(int depth, int direction)
    {
        string directory = Path.Combine(Path.GetTempPath(), "GondwanaAseTests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try
        {
            string source = Path.Combine(directory, "hero.aseprite"); File.WriteAllBytes(source, Fixture(depth, direction));
            var request = new ExternalImportRequest(source, Path.Combine(directory, "output"));
            var result = new AsepriteImporter().Import(request);
            Assert.True(result.Analysis.CanImport, string.Join(";", result.Analysis.Diagnostics));
            using var atlas = SKBitmap.Decode(Path.Combine(request.OutputDirectory, "hero.atlas.png"));
            Assert.Equal(6, atlas.Width); Assert.Equal(1, atlas.Height);
            for (int i = 0; i < 3; i++)
            {
                Assert.Equal(depth == 16 ? new SKColor(128, 128, 128) : SKColors.Red, atlas.GetPixel(i * 2, 0));
                Assert.Equal(0, atlas.GetPixel(i * 2 + 1, 0).Alpha);
            }
            var gani = AnimationDefinitionSerializer.Load(Path.Combine(request.OutputDirectory, "hero-walk.gani"));
            Assert.Equal(direction is 1 or 3 ? new double?[] { 0.3, 0.2, 0.1 } : [0.1, 0.2, 0.3], gani.Frames.Select(f => f.DurationSeconds));
            Assert.Empty(AnimationDefinitionValidator.Validate(gani));
            Assert.Equal(direction >= 2 ? CycleType.PingPong : CycleType.Repeating, gani.CycleType);
            Assert.Empty(TilesheetDefinitionValidator.Validate(TilesheetDefinitionSerializer.Load(Path.Combine(request.OutputDirectory, "hero.gts")), 6, 1));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void UnsupportedBlendIsAnError()
    {
        using var stream = new MemoryStream(Fixture(32, 0, true));
        Assert.Throws<InvalidDataException>(() => AsepriteFile.Read(stream, (_, _, _) => { }));
    }

    [Fact]
    public void GroupOpacityFlagIsIndependentOfImageLayerOpacityFlag()
    {
        byte[] bytes = Fixture(32, 0);
        // Header flags at 14; first frame/layer chunk starts at 128+16.
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(14), 2);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(128 + 16 + 6 + 2), 1);
        bytes[128 + 16 + 6 + 12] = 128;
        using var stream = new MemoryStream(bytes);
        var sprite = AsepriteFile.Read(stream, (_, _, _) => { });
        Assert.Equal(128, sprite.Layers[0].Opacity);
        Assert.Equal(255, sprite.Layers[1].Opacity);
    }

    [Theory]
    [InlineData(0, true, new int[] { 0, 1, 2, 0, 1, 2 })]
    [InlineData(1, true, new int[] { 2, 1, 0, 2, 1, 0 })]
    [InlineData(2, true, new int[] { 0, 1, 2, 1, 0 })]
    [InlineData(3, true, new int[] { 2, 1, 0, 1, 2 })]
    [InlineData(0, false, new int[] { 0, 1, 2 })]
    public void FiniteAndUntaggedSequences(int direction, bool tags, int[] expected)
    {
        string directory = Path.Combine(Path.GetTempPath(), "AseTags-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try
        {
            string source = Path.Combine(directory, "hero.ase"); File.WriteAllBytes(source, Fixture(32, direction, repeat: 2, tags: tags));
            var result = new AsepriteImporter().Import(new(source, Path.Combine(directory, "out")));
            Assert.True(result.Analysis.CanImport, string.Join(";", result.Analysis.Diagnostics));
            var gani = AnimationDefinitionSerializer.Load(result.WrittenFiles.Single(p => p.EndsWith(".gani")));
            Assert.Equal(expected, gani.Frames.Select(f => f.XTile));
            Assert.Equal(tags ? CycleType.Simple : CycleType.Repeating, gani.CycleType);
            Assert.Equal(tags ? "hero.walk" : "hero.all", gani.Key);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void NormalGroupsVisibilityOpacityAndCelZOrderRenderCorrectly()
    {
        // Two child images overlap: applying group opacity once must leave alpha 128,
        // not the 192 produced by applying that opacity to each child separately.
        var layers = new AsepriteLayer[] {
            new(1, 1, 0, 0, 128, "Group"), new(1, 0, 1, 0, 255, "Red"),
            new(1, 0, 1, 0, 255, "Blue"), new(0, 0, 0, 0, 255, "Hidden") };
        AsepriteCel Cel(int layer, byte[] pixels, int z = 0) => new(layer, 0, 0, 255, z, 1, 1, pixels, null);
        var cels = new[] { Cel(1, [255, 0, 0, 255], 2), Cel(2, [0, 0, 255, 255]), Cel(3, [0, 255, 0, 255]) };
        var sprite = new AsepriteFile(1, 1, 32, 3, 0, layers, [new(100, cels, [])], []);
        using var frame = AsepriteImporter.RenderFrame(sprite, 0);
        Assert.Equal(new SKColor(255, 0, 0, 128), frame.GetPixel(0, 0));
        layers[0] = layers[0] with { Flags = 0 };
        using var hidden = AsepriteImporter.RenderFrame(sprite, 0);
        Assert.Equal(0, hidden.GetPixel(0, 0).Alpha);
        layers[0] = layers[0] with { Flags = 1, Opacity = 255 };
        layers[1] = layers[1] with { Opacity = 128 };
        cels[0] = cels[0] with { Opacity = 128 };
        cels[1] = cels[1] with { Opacity = 0 };
        using var translucent = AsepriteImporter.RenderFrame(sprite, 0);
        Assert.Equal(new SKColor(255, 0, 0, 64), translucent.GetPixel(0, 0));
    }
}
