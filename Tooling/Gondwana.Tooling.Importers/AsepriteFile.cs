using System.IO.Compression;
using System.Text;
using SkiaSharp;

namespace Gondwana.Tooling.Importers;

public sealed record AsepriteLayer(int Flags, int Type, int Level, int Blend, byte Opacity, string Name);
public sealed record AsepriteCel(int Layer, int X, int Y, byte Opacity, int Z, int Width, int Height, byte[] Pixels, int? LinkedFrame);
public sealed record AsepriteFrame(int DurationMilliseconds, IReadOnlyList<AsepriteCel> Cels, SKColor[] Palette);
public sealed record AsepriteTag(string Name, int From, int To, int Direction, int Repeat);
public sealed record AsepriteFile(int Width, int Height, int Depth, uint Flags, byte TransparentIndex,
    IReadOnlyList<AsepriteLayer> Layers, IReadOnlyList<AsepriteFrame> Frames, IReadOnlyList<AsepriteTag> Tags)
{
    public static AsepriteFile Read(Stream stream, Action<ExternalImportSeverity, string, string> report, CancellationToken token = default)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, true);
        uint length = reader.ReadUInt32();
        if (reader.ReadUInt16() != 0xA5E0 || length != stream.Length) throw new InvalidDataException("Invalid Aseprite header or file length.");
        int count = reader.ReadUInt16(), width = reader.ReadUInt16(), height = reader.ReadUInt16(), depth = reader.ReadUInt16();
        uint flags = reader.ReadUInt32();
        int speed = reader.ReadUInt16();
        reader.ReadBytes(8);
        byte transparent = reader.ReadByte();
        reader.ReadBytes(5);
        int ratioX = reader.ReadByte(), ratioY = reader.ReadByte();
        if (ratioX > 0 && ratioY > 0 && ratioX != ratioY) throw new InvalidDataException("Non-square Aseprite pixel aspect ratio is unsupported.");
        if (count == 0 || width == 0 || height == 0 || depth is not (8 or 16 or 32) || (long)width * height * count > 64_000_000)
            throw new InvalidDataException("Invalid or excessive Aseprite canvas/frame dimensions or color depth.");
        stream.Position = 128;
        var layers = new List<AsepriteLayer>();
        var frames = new List<AsepriteFrame>();
        var tags = new List<AsepriteTag>();
        var palette = new SKColor[256];
        bool paletteDefined = false;
        for (int f = 0; f < count; f++)
        {
            token.ThrowIfCancellationRequested();
            long start = stream.Position;
            uint size = reader.ReadUInt32();
            if (size < 16 || start + size > stream.Length || reader.ReadUInt16() != 0xF1FA) throw new InvalidDataException("Invalid Aseprite frame boundary.");
            uint chunks = reader.ReadUInt16();
            int duration = reader.ReadUInt16();
            reader.ReadUInt16();
            uint newer = reader.ReadUInt32();
            if (newer != 0) chunks = newer;
            if (chunks > size / 6) throw new InvalidDataException("Invalid Aseprite chunk count.");
            var cels = new List<AsepriteCel>();
            var payloads = new List<(int Kind, byte[] Bytes)>();
            for (int c = 0; c < chunks; c++)
            {
                token.ThrowIfCancellationRequested();
                long chunkStart = stream.Position;
                uint chunkSize = reader.ReadUInt32();
                int kind = reader.ReadUInt16();
                if (chunkSize < 6 || chunkStart + chunkSize > start + size) throw new InvalidDataException("Invalid Aseprite chunk boundary.");
                payloads.Add((kind, ReadBytes(reader, checked((int)chunkSize - 6))));
            }
            bool modernPalette = payloads.Any(p => p.Kind == 0x2019);
            foreach (var (kind, bytes) in payloads)
            {
                token.ThrowIfCancellationRequested();
                using var payload = new MemoryStream(bytes);
                using var r = new BinaryReader(payload, Encoding.UTF8);
                switch (kind)
                {
                    case 0x2004:
                        if (f != 0) throw new InvalidDataException("Layer layout changes after the first frame are unsupported.");
                        int layerFlags = r.ReadUInt16(), type = r.ReadUInt16(), level = r.ReadUInt16();
                        r.ReadBytes(4);
                        int blend = r.ReadUInt16(); byte opacity = r.ReadByte(); r.ReadBytes(3);
                        string layerName = ReadString(r);
                        if (type is not (0 or 1)) throw new InvalidDataException("Aseprite tilemap layers are unsupported.");
                        if ((layerFlags & 64) != 0) throw new InvalidDataException("Aseprite reference layers are unsupported.");
                        if (type == 1 && (flags & 2) == 0) { blend = 0; opacity = 255; }
                        else if (type == 0 && (flags & 1) == 0) opacity = 255;
                        if (blend != 0) throw new InvalidDataException($"Aseprite blend mode {blend} is unsupported; only Normal is currently rendered.");
                        layers.Add(new(layerFlags, type, level, blend, opacity, layerName));
                        break;
                    case 0x2005:
                        int layer = r.ReadUInt16(), x = r.ReadInt16(), y = r.ReadInt16(); byte alpha = r.ReadByte();
                        int celType = r.ReadUInt16(), z = r.ReadInt16(); r.ReadBytes(5);
                        int w = 0, h = 0; byte[] pixels = []; int? linked = null;
                        if (celType == 1) linked = r.ReadUInt16();
                        else if (celType is 0 or 2)
                        {
                            w = r.ReadUInt16(); h = r.ReadUInt16();
                            int byteCount = checked(w * h * (depth / 8));
                            if (byteCount > 256_000_000) throw new InvalidDataException("Cel data is too large.");
                            if (celType == 0) pixels = ReadBytes(r, byteCount);
                            else
                            {
                                using var zlib = new ZLibStream(payload, CompressionMode.Decompress);
                                pixels = new byte[byteCount]; zlib.ReadExactly(pixels);
                                if (zlib.ReadByte() != -1) throw new InvalidDataException("Compressed cel exceeds declared dimensions.");
                            }
                        }
                        else throw new InvalidDataException($"Unsupported Aseprite cel type {celType} (tilemap cels are not imported).");
                        if (cels.Any(c => c.Layer == layer)) throw new InvalidDataException("Duplicate cel for layer/frame.");
                        cels.Add(new(layer, x, y, alpha, z, w, h, pixels, linked));
                        break;
                    case 0x2019:
                        uint paletteSize = r.ReadUInt32(), first = r.ReadUInt32(), last = r.ReadUInt32(); r.ReadBytes(8);
                        if (paletteSize > 65536 || first > last || last >= paletteSize) throw new InvalidDataException("Invalid palette range.");
                        if (palette.Length < paletteSize) Array.Resize(ref palette, (int)paletteSize);
                        for (uint i = first; i <= last; i++)
                        {
                            int entryFlags = r.ReadUInt16();
                            palette[i] = new SKColor(r.ReadByte(), r.ReadByte(), r.ReadByte(), r.ReadByte());
                            if ((entryFlags & 1) != 0) ReadString(r);
                        }
                        paletteDefined = true;
                        break;
                    case 0x0004:
                    case 0x0011:
                        if (modernPalette) break;
                        int packets = r.ReadUInt16(), index = 0;
                        for (int packet = 0; packet < packets; packet++)
                        {
                            index += r.ReadByte(); int entries = r.ReadByte(); if (entries == 0) entries = 256;
                            for (int i = 0; i < entries; i++)
                            {
                                byte red = r.ReadByte(), green = r.ReadByte(), blue = r.ReadByte();
                                if (index >= 256) throw new InvalidDataException("Old palette exceeds 256 colors.");
                                palette[index++] = kind == 4 ? new(red, green, blue) : new((byte)(red * 255 / 63), (byte)(green * 255 / 63), (byte)(blue * 255 / 63));
                            }
                        }
                        paletteDefined = true;
                        break;
                    case 0x2018:
                        int tagCount = r.ReadUInt16(); r.ReadBytes(8);
                        for (int i = 0; i < tagCount; i++)
                        {
                            int from = r.ReadUInt16(), to = r.ReadUInt16(), direction = r.ReadByte(), repeat = r.ReadUInt16();
                            r.ReadBytes(10); string tagName = ReadString(r);
                            if (from > to || to >= count || direction > 3) throw new InvalidDataException("Invalid animation tag.");
                            tags.Add(new(tagName, from, to, direction, repeat));
                        }
                        break;
                    case 0x2006:
                        if ((r.ReadUInt32() & 1) != 0) throw new InvalidDataException("Precise/scaled cel bounds are unsupported.");
                        break;
                    case 0x2007:
                        int profile = r.ReadUInt16(), profileFlags = r.ReadUInt16();
                        if (profile == 2 || profileFlags != 0)
                            throw new InvalidDataException("Embedded ICC and custom-gamma Aseprite color profiles are unsupported.");
                        report(ExternalImportSeverity.Info, "aseprite.profile", "Color profile metadata is omitted; pixel values are preserved.");
                        break;
                    case 0x2008:
                    case 0x2023:
                        throw new InvalidDataException("External Aseprite dependencies and tilemaps are unsupported.");
                    default:
                        report(ExternalImportSeverity.Info, "aseprite.metadata", $"Chunk 0x{kind:X4} metadata is omitted.");
                        break;
                }
            }
            if (depth == 8 && !paletteDefined) throw new InvalidDataException("Indexed sprite has no palette; default application palettes are not guessed.");
            if (duration == 0) duration = speed;
            if (duration <= 0) throw new InvalidDataException("Aseprite frame duration must be positive.");
            frames.Add(new(duration, cels, (SKColor[])palette.Clone()));
            stream.Position = start + size;
        }
        if (stream.Position != stream.Length) throw new InvalidDataException("Unexpected trailing Aseprite frame data.");
        return new(width, height, depth, flags, transparent, layers, frames, tags);
    }

    private static byte[] ReadBytes(BinaryReader reader, int count)
    {
        var bytes = reader.ReadBytes(count);
        return bytes.Length == count ? bytes : throw new EndOfStreamException("Truncated Aseprite chunk.");
    }
    private static string ReadString(BinaryReader reader) => Encoding.UTF8.GetString(ReadBytes(reader, reader.ReadUInt16()));
}
