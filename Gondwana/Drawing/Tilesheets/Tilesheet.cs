using System.Drawing;
using Gondwana.Assets;
using Gondwana.Physics.Collisions;
using Gondwana.SkiaSharp;
using Microsoft.Extensions.Logging;
using SkiaSharp;

namespace Gondwana.Drawing.Tilesheets;

/// <summary>
/// Represents a tilesheet image and metadata for rendering tiles.
/// </summary>
public sealed class Tilesheet : IDisposable
{
    private SKImage? _atlasImage;

    [Newtonsoft.Json.JsonIgnore]
    internal DefinitionProvenance? DefinitionProvenance { get; set; }

    /// <summary>
    /// Lazily-created image view of the complete tilesheet used by the command recorder
    /// for DrawAtlas batching. The bitmap remains the authoritative source.
    /// </summary>
    internal SKImage? AtlasImage
    {
        get
        {
            if (_disposed || SkBitmap is null || SkBitmap.IsEmpty)
                return null;

            return _atlasImage ??= SKImage.FromBitmap(SkBitmap);
        }
    }

    /// <summary>
    /// Occurs after the object releases its resources.
    /// </summary>
    public event Action<Tilesheet>? Disposed;

    private Tilesheet() { }

    internal Tilesheet(string name, SKBitmap bitmap, bool addDefaultRegion = true)
    {
        Name = name;
        SkBitmap = bitmap ?? throw new ArgumentNullException(nameof(bitmap));

        if (addDefaultRegion)
            AddDefaultRegion();
    }

    internal Tilesheet(string name, Stream stream, bool addDefaultRegion = true)
        : this(
            name,
            SKBitmap.Decode(stream) ?? throw new ArgumentException("Invalid image stream."),
            addDefaultRegion)
    {
    }

    internal Tilesheet(string name, string file, bool addDefaultRegion = true)
        : this(
            name,
            SKBitmap.Decode(file) ?? throw new ArgumentException($"Invalid image file: {file}"),
            addDefaultRegion)
    {
        ImageFilePath = file;
    }

    internal Tilesheet(AssetsFile resFile, string entryName, bool addDefaultRegion = true)
    {
        ArgumentNullException.ThrowIfNull(resFile);

        if (string.IsNullOrWhiteSpace(entryName))
            throw new ArgumentException("Asset entry name must be a non-empty string.", nameof(entryName));

        AssetIdentifier = new AssetsFileIdentifier(resFile, AssetTypes.Image, entryName);

        using var assetStream = AssetIdentifier.Data
            ?? throw new InvalidOperationException(
                $"Tilesheet asset '{entryName}' could not be loaded from AssetsFile '{resFile.FilePath}'. " +
                "The asset entry does not exist or returned a null data stream.");

        SkBitmap = SKBitmap.Decode(assetStream)
            ?? throw new ArgumentException(
                $"Failed to decode tilesheet bitmap from asset '{entryName}' in AssetsFile '{resFile.FilePath}'. " +
                "The asset data is corrupt or not a supported image format.");

        Name = entryName;

        if (addDefaultRegion)
            AddDefaultRegion();
    }

    /// <summary>
    /// Creates a tilesheet from another tilesheet's metadata while loading a replacement image.
    /// Region and per-frame collision metadata are copied.
    /// </summary>
    /// <param name="baseSheet">The tilesheet whose definition is copied.</param>
    /// <param name="name">The name of the new tilesheet.</param>
    /// <param name="file">The source image file for the new tilesheet.</param>
    internal Tilesheet(Tilesheet baseSheet, string name, string file)
    {
        ArgumentNullException.ThrowIfNull(baseSheet);

        Name = name;
        SkBitmap = SKBitmap.Decode(file)
            ?? throw new ArgumentException($"Invalid image file: {file}");

        ImageFilePath = file;
        ValueBag = new(baseSheet.ValueBag);

        foreach (var region in baseSheet.Regions)
        {
            var copiedRegion = AddRegion(
                region.Name,
                region.Area,
                region.TileSize,
                region.TilePadding,
                region.RegionMargin,
                region.Overhang,
                region.CollisionAdjust,
                region.CollisionType);

            for (int y = 0; y < region.Rows; y++)
            {
                for (int x = 0; x < region.Columns; x++)
                {
                    if (region.TryGetFrameCollisionAdjustOverride(
                        x,
                        y,
                        out var frameCollisionAdjust))
                    {
                        copiedRegion.SetFrameCollisionAdjust(
                            x,
                            y,
                            frameCollisionAdjust);
                    }

                    if (region.TryGetFrameCollisionTypeOverride(
                        x,
                        y,
                        out var frameCollisionType))
                    {
                        copiedRegion.SetFrameCollisionType(
                            x,
                            y,
                            frameCollisionType);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Gets the current tilesheet bitmap used for rendering.
    /// </summary>
    public SKBitmap SkBitmap { get; private set; } = null!;
    /// <summary>
    /// Gets the source bitmap retained before masking or alpha conversion.
    /// </summary>
    public SKBitmap? SkBitmapOriginal { get; private set; }
    /// <summary>
    /// Gets the name.
    /// </summary>
    public string Name { get; internal set; } = string.Empty;
    /// <summary>
    /// Gets the named frame regions in the tilesheet.
    /// </summary>
    public List<TilesheetRegion> Regions { get; private set; } = new();
    /// <summary>
    /// Gets the default tilesheet region.
    /// </summary>
    public TilesheetRegion DefaultRegion => this[TilesheetRegion.DefaultRegionName];
    /// <summary>
    /// Gets or sets the custom typed values associated with this object.
    /// </summary>
    public TypedValueBag ValueBag { get; set; } = new();
    /// <summary>
    /// Gets the packed asset from which the image was loaded, if any.
    /// </summary>
    public AssetsFileIdentifier? AssetIdentifier { get; private set; }
    /// <summary>
    /// Gets the loose source image path, when available.
    /// </summary>
    public string ImageFilePath { get; private set; } = string.Empty;
    /// <summary>
    /// Gets the color used for transparency masking.
    /// </summary>
    public SKColor? MaskColor { get; private set; }
    /// <summary>
    /// Gets the per-channel tolerance used for transparency masking.
    /// </summary>
    public byte MaskTolerance { get; private set; } = 5;
    /// <summary>
    /// Gets whether alpha premultiplication has been applied.
    /// </summary>
    public bool Premultiplied { get; private set; }

    /// <summary>
    /// Adds a region with the supplied default collision adjustment and collision type.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="area">The area.</param>
    /// <param name="tileSize">The tile size.</param>
    /// <param name="tilePadding">The tile padding.</param>
    /// <param name="regionMargin">The region margin.</param>
    /// <param name="overhangPixels">The overhang pixels.</param>
    /// <param name="collisionAdjust">The adjustment applied to the frame's collision bounds.</param>
    /// <param name="collisionType">The collision type.</param>
    /// <returns>The region added to the tilesheet.</returns>
    public TilesheetRegion AddRegion(
        string name,
        Rectangle area,
        Size tileSize,
        Spacing? tilePadding = null,
        Spacing? regionMargin = null,
        Spacing? overhangPixels = null,
        CollisionAdjust? collisionAdjust = null,
        TileCollisionType collisionType = TileCollisionType.None)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Region name must be a non-empty string.", nameof(name));

        if (GetRegion(name) != null)
            throw new ArgumentException($"A tilesheet region named '{name}' already exists.", nameof(name));

        var region = new TilesheetRegion(
            this,
            name,
            area,
            tileSize,
            tilePadding ?? Spacing.None,
            regionMargin ?? Spacing.None,
            overhangPixels ?? Spacing.None,
            collisionAdjust ?? CollisionAdjust.None,
            collisionType);

        Regions.Add(region);
        return region;
    }

    /// <summary>
    /// Finds a tilesheet region by its case-insensitive name.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns>The matching region, or <see langword="null"/> if no region matches.</returns>
    public TilesheetRegion? GetRegion(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        foreach (var region in Regions)
        {
            if (string.Equals(region.Name, name, StringComparison.OrdinalIgnoreCase))
                return region;
        }

        return null;
    }

    /// <summary>
    /// Removes the named tilesheet region and optionally disposes it.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="dispose">Whether to dispose the removed resource.</param>
    /// <returns><see langword="true"/> if the region was found and removed; otherwise, <see langword="false"/>.</returns>
    public bool RemoveRegion(string name, bool dispose = true)
    {
        var region = GetRegion(name);
        if (region == null)
            return false;

        Regions.Remove(region);

        if (dispose)
            region.Dispose();

        return true;
    }

    /// <summary>
    /// Creates a frame reference at the specified tile coordinates.
    /// </summary>
    /// <param name="regionName">The name of the tilesheet region.</param>
    /// <param name="x">The zero-based column in the tile grid.</param>
    /// <param name="y">The zero-based row in the tile grid.</param>
    /// <returns>A frame reference to the requested region and tile coordinates.</returns>
    public Frame GetFrame(string regionName, int x, int y) =>
        new(this, regionName, x, y);

    /// <summary>
    /// Creates a frame reference at the specified tile coordinates.
    /// </summary>
    /// <param name="x">The zero-based column in the tile grid.</param>
    /// <param name="y">The zero-based row in the tile grid.</param>
    /// <returns>A frame reference to the requested region and tile coordinates.</returns>
    public Frame GetFrame(int x, int y) =>
        new(this, TilesheetRegion.DefaultRegionName, x, y);

    /// <summary>
    /// Applies a color-key transparency mask and rebuilds the frame cache.
    /// </summary>
    /// <param name="maskColor">The color to make transparent; null uses the default mask color.</param>
    /// <param name="tolerance">The tolerance.</param>
    public void ApplyMask(SKColor? maskColor = null, byte tolerance = 5)
    {
        if (SkBitmap == null || SkBitmap.IsEmpty)
            throw new ArgumentException("Invalid bitmap.");

        var targetColor = maskColor ?? SKColors.White;

        MaskColor = targetColor;
        MaskTolerance = tolerance;
        Premultiplied = true;

        InvalidateAtlasImage();
        ClearTileCache();

        if (SkBitmap.Info.AlphaType == SKAlphaType.Opaque)
        {
            var info = new SKImageInfo(
                SkBitmap.Width,
                SkBitmap.Height,
                SkBitmap.Info.ColorType,
                SKAlphaType.Premul);

            var withAlpha = new SKBitmap(info);
            using (var canvas = new SKCanvas(withAlpha))
                canvas.DrawBitmap(SkBitmap, 0, 0);

            SkBitmap.Dispose();
            SkBitmap = withAlpha;
        }

        SkBitmapOriginal?.Dispose();
        SkBitmapOriginal = SkBitmap.Copy();

        SkBitmap.ApplyAlphaMask(targetColor, tolerance);
        SkBitmap = SkBitmap.PremultiplyAlpha();

        BuildTileCache();
    }

    /// <summary>
    /// Premultiplies the bitmap alpha and rebuilds the frame cache.
    /// </summary>
    public void ApplyPremultiplyAlpha()
    {
        if (SkBitmap == null || SkBitmap.IsEmpty)
            throw new ArgumentException("Invalid bitmap.");

        Premultiplied = true;
        InvalidateAtlasImage();
        ClearTileCache();

        SkBitmapOriginal?.Dispose();
        SkBitmapOriginal = SkBitmap.Copy();
        SkBitmap = SkBitmap.PremultiplyAlpha();

        BuildTileCache();
    }

    /// <summary>
    /// Encodes the tilesheet bitmap using the requested image format and quality.
    /// </summary>
    /// <param name="format">The encoded image format.</param>
    /// <param name="quality">The image encoding quality, from 0 through 100.</param>
    /// <returns>The encoded image bytes.</returns>
    public byte[] ToByteArray(
        SKEncodedImageFormat format = SKEncodedImageFormat.Png,
        int quality = 100)
    {
        if (SkBitmap == null || SkBitmap.IsEmpty)
            throw new ArgumentException("Invalid bitmap.");

        return SkBitmap.EncodeBitmapToBytes(format, quality);
    }

    /// <summary>
    /// Persists this tilesheet's source image to a file and promotes the tilesheet
    /// from a runtime-only bitmap to a file-backed tilesheet.
    /// </summary>
    /// <param name="imageFilePath">The destination image file path.</param>
    /// <param name="format">The encoded image format.</param>
    /// <param name="quality">The encoding quality from 0 through 100.</param>
    /// <remarks>
    /// When masking or alpha premultiplication has transformed the runtime bitmap,
    /// the original source bitmap is persisted. The corresponding GTS metadata will
    /// reapply that transformation when the tilesheet is loaded.
    /// </remarks>
    public void PersistImageToFile(
        string imageFilePath,
        SKEncodedImageFormat format = SKEncodedImageFormat.Png,
        int quality = 100)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(Tilesheet));

        if (string.IsNullOrWhiteSpace(imageFilePath))
        {
            throw new ArgumentException(
                "Image file path must be a non-empty string.",
                nameof(imageFilePath));
        }

        if (quality is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quality),
                quality,
                "Image encoding quality must be between 0 and 100.");
        }

        if (SkBitmap == null || SkBitmap.IsEmpty)
        {
            throw new InvalidOperationException(
                "Tilesheet does not contain a valid bitmap to persist.");
        }

        var fullPath = Path.GetFullPath(imageFilePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var sourceBitmap = SkBitmapOriginal ?? SkBitmap;
        var imageBytes = sourceBitmap.EncodeBitmapToBytes(format, quality);
        File.WriteAllBytes(fullPath, imageBytes);

        ImageFilePath = fullPath;
        AssetIdentifier = null;
    }

    /// <summary>
    /// Gets the cached image for the specified frame.
    /// </summary>
    /// <param name="regionName">The name of the tilesheet region.</param>
    /// <param name="x">The zero-based column in the tile grid.</param>
    /// <param name="y">The zero-based row in the tile grid.</param>
    /// <returns>The cached frame image, or <see langword="null"/> if the frame is unavailable. The tilesheet retains ownership.</returns>
    public SKImage? GetImage(string regionName, int x, int y) =>
        GetRegion(regionName)?.GetImage(x, y);

    /// <summary>
    /// Gets the cached bitmap for the specified frame.
    /// </summary>
    /// <param name="regionName">The name of the tilesheet region.</param>
    /// <param name="x">The zero-based column in the tile grid.</param>
    /// <param name="y">The zero-based row in the tile grid.</param>
    /// <returns>The cached frame bitmap, or <see langword="null"/> if the frame is unavailable. The tilesheet retains ownership.</returns>
    public SKBitmap? GetBitmap(string regionName, int x, int y) =>
        GetRegion(regionName)?.GetBitmap(x, y);

    /// <summary>
    /// Collects the cached frame bitmaps indexed by their tile coordinates.
    /// </summary>
    /// <returns>A dictionary of cached bitmaps keyed by frame coordinates. The tilesheet retains ownership of the bitmaps.</returns>
    public Dictionary<(string regionName, int x, int y), SKBitmap> GetAllBitmaps()
    {
        var tiles = new Dictionary<(string regionName, int x, int y), SKBitmap>();

        foreach (var region in Regions)
        {
            foreach (var tile in region.GetAllBitmaps())
                tiles[(region.Name, tile.Key.x, tile.Key.y)] = tile.Value;
        }

        return tiles;
    }

    /// <summary>
    /// Collects the cached frame images indexed by their tile coordinates.
    /// </summary>
    /// <returns>A dictionary of cached images keyed by frame coordinates. The tilesheet retains ownership of the images.</returns>
    public Dictionary<(string regionName, int x, int y), SKImage> GetAllImages()
    {
        var tiles = new Dictionary<(string regionName, int x, int y), SKImage>();

        foreach (var region in Regions)
        {
            foreach (var tile in region.GetAllImages())
                tiles[(region.Name, tile.Key.x, tile.Key.y)] = tile.Value;
        }

        return tiles;
    }

    /// <summary>
    /// Gets the named tilesheet region.
    /// </summary>
    /// <param name="regionName">The name of the tilesheet region.</param>
    public TilesheetRegion this[string regionName] =>
        GetRegion(regionName)
        ?? throw new ArgumentException(
            $"No tilesheet region named '{regionName}' exists.",
            nameof(regionName));

    /// <summary>
    /// Gets a frame reference at the specified tile coordinates.
    /// </summary>
    /// <param name="regionName">The name of the tilesheet region.</param>
    /// <param name="x">The zero-based column in the tile grid.</param>
    /// <param name="y">The zero-based row in the tile grid.</param>
    public Frame this[string regionName, int x, int y] =>
        GetFrame(regionName, x, y);

    /// <summary>
    /// Gets a frame reference at the specified tile coordinates.
    /// </summary>
    /// <param name="x">The zero-based column in the tile grid.</param>
    /// <param name="y">The zero-based row in the tile grid.</param>
    public Frame this[int x, int y] => GetFrame(x, y);

    private void BuildTileCache()
    {
        foreach (var region in Regions)
            region.BuildTileCache();
    }

    private void ClearTileCache()
    {
        foreach (var region in Regions)
            region.ClearTileCache();
    }

    private void InvalidateAtlasImage()
    {
        _atlasImage?.Dispose();
        _atlasImage = null;
    }

    private void AddDefaultRegion(
        Size? tileSize = null,
        Spacing? tilePadding = null,
        Spacing? regionMargin = null,
        Spacing? overhangPixels = null,
        CollisionAdjust? collisionAdjust = null,
        TileCollisionType collisionType = TileCollisionType.None)
    {
        AddRegion(
            TilesheetRegion.DefaultRegionName,
            new Rectangle(0, 0, SkBitmap.Width, SkBitmap.Height),
            tileSize ?? Size.Empty,
            tilePadding ?? Spacing.None,
            regionMargin ?? Spacing.None,
            overhangPixels ?? Spacing.None,
            collisionAdjust ?? CollisionAdjust.None,
            collisionType);
    }

    private bool _disposed;

    /// <summary>
    /// Releases the resources owned by this instance.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        _disposed = true;

        if (!disposing)
            return;

        foreach (var region in Regions)
            region.Dispose();

        Regions.Clear();

        InvalidateAtlasImage();
        SkBitmap?.Dispose();
        SkBitmapOriginal?.Dispose();

        try
        {
            Disposed?.Invoke(this);
        }
        catch (Exception ex)
        {
            Engine.Logger.LogError(ex, "Error during Tilesheet Disposed event handling.");
        }

        Disposed = null;
    }
}
