using Gondwana.Assets;
using Gondwana.Drawing.Sprites.GSPR;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Tooling.Assets;
using Gondwana.Tooling.Tilesheets.Sources;
using Gondwana.Tooling.WinForms;

namespace Gondwana.Tooling.Sprites.Editing;

/// <summary>
/// Loose or packed GTS definition loaded for sprite authoring and definition-driven preview.
/// </summary>
internal sealed class SpriteTilesheetSource : IDisposable
{
    /// <summary>
    /// Gets the file path.
    /// </summary>
    public string FilePath => Resource.Path;
    /// <summary>The complete physical source identity, separate from the logical member name.</summary>
    public AuthoringResource Resource { get; }
    /// <summary>
    /// Gets the base directory.
    /// </summary>
    public string BaseDirectory { get; }
    /// <summary>
    /// Gets the definition.
    /// </summary>
    public TilesheetDefinition Definition { get; }
    /// <summary>
    /// Gets the image.
    /// </summary>
    public Bitmap? Image { get; private set; }
    /// <summary>
    /// Gets the preview warning.
    /// </summary>
    public string? PreviewWarning { get; private set; }

    private SpriteTilesheetSource(AuthoringResource resource, TilesheetDefinition definition, AssetPackageCatalog? packages = null)
    {
        Resource = resource;
        BaseDirectory = resource.BaseDirectory;
        Definition = definition;
        using var owned = packages is null ? new AssetPackageCatalog() : null;
        LoadPreviewImage(packages ?? owned!, resource.ArchivePath);
    }

    /// <summary>
    /// Loads sprite tilesheet source from the supplied source.
    /// </summary>
    /// <param name="path">The path.</param>
    /// <returns>The resulting sprite tilesheet source.</returns>
    /// <param name="identity">The logical member identity; null requires a singleton.</param>
    /// <param name="packages">Optional shared authoring package catalog.</param>
    public static SpriteTilesheetSource LoadMember(string path, string? identity, AssetPackageCatalog? packages = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        path = Path.GetFullPath(path);
        return LoadResource(new AuthoringResource(path, Gondwana.Assets.AssetTypes.TilesheetDefinition), identity, packages);
    }

    /// <summary>
    /// Loads sprite tilesheet source from the supplied source.
    /// </summary>
    /// <param name="archivePath">The archive path.</param>
    /// <param name="entry">The entry.</param>
    /// <returns>The resulting sprite tilesheet source.</returns>
    public static SpriteTilesheetSource Load(string archivePath, string entry) =>
        LoadResource(new AuthoringResource(Path.GetFullPath(archivePath), AssetTypes.TilesheetDefinition, entry), null);

    /// <summary>
    /// Creates a frame definition for the selected tilesheet region and grid coordinates.
    /// </summary>
    /// <param name="region">The region.</param>
    /// <param name="x">The zero-based column in the tile grid.</param>
    /// <param name="y">The zero-based row in the tile grid.</param>
    /// <returns>The resulting sprite frame definition.</returns>
    public SpriteFrameDefinition CreateFrame(
        TilesheetRegionDefinition region,
        int x,
        int y) =>
        new()
        {
            Tilesheet = Definition.Name,
            RegionName = region.Name,
            XTile = x,
            YTile = y
        };

    /// <summary>
    /// Attempts to resolve a frame to its tilesheet region and image-pixel bounds.
    /// </summary>
    /// <param name="frame">The frame.</param>
    /// <param name="region">When this method returns, contains the region.</param>
    /// <param name="bounds">When this method returns, contains the bounds.</param>
    /// <returns><see langword="true"/> if the operation succeeded; otherwise, <see langword="false"/>.</returns>
    public bool TryResolve(
        SpriteFrameDefinition frame,
        out TilesheetRegionDefinition? region,
        out Rectangle bounds)
    {
        region = null;
        bounds = Rectangle.Empty;

        if (!string.Equals(frame.Tilesheet, Definition.Name, StringComparison.Ordinal))
            return false;

        region = Definition.Regions.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, frame.RegionName, StringComparison.OrdinalIgnoreCase));
        if (region is null)
            return false;

        var (columns, rows) = TilesheetDefinitionValidator.GridSize(region);
        if (frame.XTile < 0 ||
            frame.YTile < 0 ||
            frame.XTile >= columns ||
            frame.YTile >= rows)
        {
            return false;
        }

        bounds = FrameBounds(region, frame.XTile, frame.YTile);
        return true;
    }

    /// <summary>
    /// Computes a frame's pixel bounds within the tilesheet image.
    /// </summary>
    /// <param name="region">The region.</param>
    /// <param name="x">The zero-based column in the tile grid.</param>
    /// <param name="y">The zero-based row in the tile grid.</param>
    /// <returns>The frame rectangle in tilesheet image pixels.</returns>
    public static Rectangle FrameBounds(
        TilesheetRegionDefinition region,
        int x,
        int y) =>
        new(
            checked((int)(
                (long)region.Area.X +
                region.RegionMargin.Left +
                region.TilePadding.Left +
                x * ((long)region.TileSize.Width +
                     region.TilePadding.Left +
                     region.TilePadding.Right))),
            checked((int)(
                (long)region.Area.Y +
                region.RegionMargin.Top +
                region.TilePadding.Top +
                y * ((long)region.TileSize.Height +
                     region.TilePadding.Top +
                     region.TilePadding.Bottom))),
            region.TileSize.Width,
            region.TileSize.Height);

    private void LoadPreviewImage(AssetPackageCatalog packages, string? archivePath)
    {
        var image = Definition.Image;
        if (image is null)
        {
            PreviewWarning = "GTS does not define an image source.";
            return;
        }

        try
        {
            Image = AuthoringPreviewImage.Load(Definition, BaseDirectory, packages, archivePath);
        }
        catch (Exception ex) when (
            ex is IOException or
            InvalidDataException or
            ArgumentException or
            InvalidOperationException or
            NotSupportedException or
            UnauthorizedAccessException or
            System.Runtime.InteropServices.ExternalException)
        {
            PreviewWarning = "Image cannot be previewed: " + ex.Message;
        }
    }

    /// <summary>
    /// Releases the resources owned by this instance.
    /// </summary>
    public void Dispose()
    {
        Image?.Dispose();
        Image = null;
    }

    /// <summary>Loads a singleton source document.</summary>
    /// <param name="path">The physical document path.</param>
    /// <returns>The source.</returns>
    public static SpriteTilesheetSource Load(string path) => LoadMember(path, null);

    /// <summary>Loads every member for authoring source selection.</summary>
    /// <param name="path">The physical document path.</param>
    /// <returns>All logical sources.</returns>
    /// <param name="packages">Optional shared authoring package catalog.</param>
    public static IReadOnlyList<SpriteTilesheetSource> LoadAll(string path, AssetPackageCatalog? packages = null) =>
        TilesheetDefinitionSerializer.LoadAll(path).Select(definition => new SpriteTilesheetSource(new AuthoringResource(Path.GetFullPath(path), Gondwana.Assets.AssetTypes.TilesheetDefinition), definition, packages)).ToList();

    /// <summary>Loads one logical source from a packed definition document.</summary>
    /// <param name="archivePath">The containing archive path.</param>
    /// <param name="entry">The physical entry name.</param>
    /// <param name="identity">The logical member identity.</param>
    /// <returns>The selected source.</returns>
    /// <param name="packages">Optional shared authoring package catalog.</param>
    public static SpriteTilesheetSource LoadPacked(string archivePath, string entry, string identity, AssetPackageCatalog? packages = null) =>
        LoadResource(new AuthoringResource(Path.GetFullPath(archivePath), Gondwana.Assets.AssetTypes.TilesheetDefinition, entry), identity, packages);

    internal static SpriteTilesheetSource LoadResource(AuthoringResource resource, string? identity, AssetPackageCatalog? packages = null)
    {
        using var owned = packages is null ? new AssetPackageCatalog() : null;
        packages ??= owned!;
        return new(resource, new AuthoringResourceResolver(packages).LoadTilesheet(resource, identity), packages);
    }
}
