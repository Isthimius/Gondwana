using System.IO.Compression;
using System.Text;
using Gondwana.Drawing.Animation.GANI;
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
    private static byte[] Fixture(int depth, int direction, bool unsupported = false)
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
                chunks.Add(Chunk(0x2018, c => { c.Write((ushort)1); c.Write(new byte[8]); c.Write((ushort)0); c.Write((ushort)2); c.Write((byte)direction); c.Write((ushort)0); c.Write(new byte[10]); Text(c, "Walk"); }));
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
}
