using Gondwana.Drawing;

namespace Gondwana.Tooling.Importers;

/// <summary>Decodes orthogonal/isometric Tiled GIDs, applying the axis swap before H/V flips.</summary>
public static class TiledTileTransform
{
    /// <summary>Clears all four high bits and returns the native orientation and any retained hex-only flag.</summary>
    public static (uint Gid, TileTransform Transform, bool HasHexRotation) Decode(uint raw)
    {
        var transform = (raw & 0x20000000) != 0 ? TileTransform.FlipDiagonal : TileTransform.Identity;
        if ((raw & 0x80000000) != 0) transform = TileTransformGeometry.Compose(transform, TileTransform.FlipHorizontal);
        if ((raw & 0x40000000) != 0) transform = TileTransformGeometry.Compose(transform, TileTransform.FlipVertical);
        return (raw & 0x0fffffff, transform, (raw & 0x10000000) != 0);
    }
}
