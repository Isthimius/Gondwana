using System.Buffers.Binary;
using System.Drawing;
using System.Globalization;
using Gondwana.Drawing;

namespace Gondwana.Tooling.Importers;

// Godot 4.2 TileMapLayer::set_tile_data (FORMAT_3), and Godot 4.4
// TileMapLayer::set_tile_map_data_from_array. See Fixtures/Godot4/README.md in tests.
internal static class GodotSceneTileData
{
    internal sealed record Cell(Point Position, int Source, Point Atlas, int Alternative);

    internal static List<Cell> Decode(string value, bool legacy)
    {
        string kind = legacy ? "PackedInt32Array" : "PackedByteArray";
        if (!value.StartsWith(kind + "(", StringComparison.Ordinal) || !value.EndsWith(')'))
            throw new InvalidDataException($"Expected {kind} tile placement data.");
        string body = value[(kind.Length + 1)..^1].Trim();
        byte[] bytes;
        if (!legacy && body.StartsWith('"')) bytes = Convert.FromBase64String(GodotTextResource.String(body));
        else
        {
            var parts = body.Length == 0 ? [] : body.Split(',', StringSplitOptions.TrimEntries);
            bytes = new byte[checked(parts.Length * (legacy ? 4 : 1))];
            for (int i = 0; i < parts.Length; i++)
                if (legacy) BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(i * 4), int.Parse(parts[i], CultureInfo.InvariantCulture));
                else bytes[i] = byte.Parse(parts[i], CultureInfo.InvariantCulture);
        }
        var cells = new List<Cell>();
        if (bytes.Length == 0) return cells;
        int start = legacy ? 0 : 2;
        if (bytes.Length < start || (bytes.Length - start) % 12 != 0)
            throw new InvalidDataException("Truncated Godot tile placement record; expected 12 bytes per cell.");
        if (!legacy && BinaryPrimitives.ReadUInt16LittleEndian(bytes) != 0)
            throw new InvalidDataException("Unsupported TileMapLayer data version; only version 0 is supported.");
        var positions = new HashSet<Point>();
        for (int i = start; i < bytes.Length; i += 12)
        {
            var data = bytes.AsSpan(i, 12);
            var position = new Point(BinaryPrimitives.ReadInt16LittleEndian(data), BinaryPrimitives.ReadInt16LittleEndian(data[2..]));
            if (!positions.Add(position)) throw new InvalidDataException($"Duplicate Godot cell {position}.");
            cells.Add(new(position, BinaryPrimitives.ReadUInt16LittleEndian(data[4..]),
                new(BinaryPrimitives.ReadUInt16LittleEndian(data[6..]), BinaryPrimitives.ReadUInt16LittleEndian(data[8..])),
                BinaryPrimitives.ReadUInt16LittleEndian(data[10..])));
        }
        return cells;
    }

    internal static TileTransform Transform(int alternative)
    {
        if ((alternative & ~0x7000) != 0)
            throw new InvalidDataException($"Alternative tile {alternative & ~0x7000} is unsupported; use base atlas tiles with optional H/V/transpose flags.");
        // Godot applies transpose before the horizontal/vertical flags (H + T is clockwise 90).
        var transform = (alternative & 0x4000) != 0 ? TileTransform.FlipDiagonal : TileTransform.Identity;
        if ((alternative & 0x1000) != 0) transform = TileTransformGeometry.Compose(transform, TileTransform.FlipHorizontal);
        if ((alternative & 0x2000) != 0) transform = TileTransformGeometry.Compose(transform, TileTransform.FlipVertical);
        return transform;
    }
}
