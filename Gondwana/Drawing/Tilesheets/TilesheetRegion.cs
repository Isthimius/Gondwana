using Gondwana.SkiaSharp;
using Newtonsoft.Json;
using SkiaSharp;
using System.Drawing;
using System.Numerics;

namespace Gondwana.Drawing.Tilesheets;

public sealed class TilesheetRegion
{
    private TilesheetRegionSlice?[,]? _tileCache;

    #region ctors

    [JsonConstructor]
    private TilesheetRegion() { }

    internal TilesheetRegion(string name, Rectangle area, Size spacing, Size tileSize, Overhang overhangPixels)
    {
        Name = name;
        Area = area;
        Spacing = spacing;
        TileSize = tileSize;
        OverhangPixels = overhangPixels;
    }

    #endregion ctors

    [JsonProperty]
    public string Name { get; set; } = "default";

    [JsonProperty]
    public Rectangle Area { get; set; }

    [JsonProperty]
    public Size Spacing { get; set; }

    [JsonProperty]
    public Size TileSize { get; set; }

    [JsonProperty]
    public Overhang OverhangPixels { get; set; } = Overhang.None;

    #region private methods

    private void BuildTileCache()
    {
        ClearCache();

        if (TileSize.Width <= 0 || TileSize.Height <= 0)
            return;

        int xTiles = (SkBitmap.Width - InitialOffsetX + XPixelsBetweenTiles) / (_tileSize.Width + XPixelsBetweenTiles);
        int yTiles = (SkBitmap.Height - InitialOffsetY + YPixelsBetweenTiles) / (_tileSize.Height + YPixelsBetweenTiles);

        _tileCache = new TilesheetRegionSlice?[xTiles, yTiles];

        for (int y = 0; y < yTiles; y++)
        {
            for (int x = 0; x < xTiles; x++)
            {
                var srcRect = GetTileBounds(x, y);
                if (!SkBitmap.Info.Rect.Contains(srcRect.ToSKRectI()))
                    continue;

                var srcInfo = SkBitmap.Info;

                // IMPORTANT: preserve alpha + color type from the masked tilesheet bitmap
                var sliceInfo = new SKImageInfo(
                    _tileSize.Width,
                    _tileSize.Height,
                    srcInfo.ColorType,
                    srcInfo.AlphaType
                );

                var bmp = new SKBitmap(sliceInfo);

                // ensure any untouched pixels are transparent
                bmp.Erase(SKColors.Transparent);

                if (SkBitmap.ExtractSubset(bmp, srcRect.ToSKRectI()))
                {
                    var img = SKImage.FromBitmap(bmp);
                    _tileCache[x, y] = new TilesheetRegionSlice(bmp, img);
                }
                else
                {
                    bmp.Dispose();
                }
            }
        }
    }

    private void ClearCache()
    {
        if (_tileCache == null)
            return;

        for (int y = 0; y < _tileCache.GetLength(1); y++)
        {
            for (int x = 0; x < _tileCache.GetLength(0); x++)
            {
                _tileCache[x, y]?.Bitmap.Dispose();
                _tileCache[x, y]?.Image.Dispose();
                _tileCache[x, y] = null;
            }
        }

        _tileCache = null;
    }

    #endregion private methods
}