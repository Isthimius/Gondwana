using Gondwana.Drawing.Animation.GANI;
using Gondwana.Drawing.Tilesheets.GTS;
using Gondwana.Tooling.Assets;
using Gondwana.Tooling.Tilesheets.Sources;
using Gondwana.Tooling.WinForms;

namespace Gondwana.Tooling.Animations.Editing;

/// <summary>
/// A loose or packed GTS definition loaded as an authoring source for GANI frame references.
/// </summary>
internal sealed class TilesheetSource : IDisposable
{
    /// <summary>Gets the full loose document path or containing archive path; use Resource for packed entry identity.</summary>
    public string FilePath => Resource.Path;
    /// <summary>The complete physical source identity, separate from the logical member name.</summary>
    public AuthoringResource Resource { get; }
    /// <summary>Gets the full directory containing the document or archive, used to resolve its dependencies.</summary>
    public string BaseDirectory { get; }
    /// <summary>Gets the selected deserialized GTS member used by the authoring preview.</summary>
    public TilesheetDefinition Definition { get; }
    /// <summary>Gets the source-owned masked preview bitmap, or null if image loading failed or the source was disposed.</summary>
    public Bitmap? Image { get; private set; }
    /// <summary>Gets image-load failure details, or null when no preview problem was recorded.</summary>
    public string? PreviewWarning { get; private set; }

    private TilesheetSource(
        AuthoringResource resource,
        TilesheetDefinition definition, AssetPackageCatalog? packages = null)
    {
        Resource = resource;
        BaseDirectory = resource.BaseDirectory;
        Definition = definition;
        using var owned = packages is null ? new AssetPackageCatalog() : null;
        LoadPreviewImage(packages ?? owned!, resource.ArchivePath);
    }

    /// <summary>Loads a logical member from a loose GTS file and prepares its authoring preview dependencies.</summary>
    /// <param name="path">Loose document path, normalized against the process current directory.</param>
    /// <param name="identity">Logical member identity, or null to require a singleton document.</param>
    /// <param name="packages">Borrowed package cache, or null to use a temporary cache during loading.</param>
    /// <returns>A source owned by the caller, which must dispose its preview resources.</returns>
    public static TilesheetSource LoadMember(string path, string? identity, AssetPackageCatalog? packages = null)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("GTS path must be a non-empty string.", nameof(path));

        path = Path.GetFullPath(path);
        return LoadResource(new AuthoringResource(path, Gondwana.Assets.AssetTypes.TilesheetDefinition), identity, packages);
    }

    /// <summary>Creates a logical frame reference without modifying the source or validating grid coordinates.</summary>
    /// <param name="region">Region whose name is stored in the reference.</param>
    /// <param name="x">Zero-based tile column to store.</param>
    /// <param name="y">Zero-based tile row to store.</param>
    /// <returns>A new reference using this tilesheet's logical name and the supplied region and coordinates.</returns>
    public AnimationFrameDefinition CreateFrame(
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

    /// <summary>Resolves a logical frame by exact tilesheet name, case-insensitive region name, and valid grid coordinates.</summary>
    /// <param name="frame">Frame reference to resolve against this tilesheet.</param>
    /// <param name="region">Matching region, or null if no region matched; may be non-null when coordinates are invalid.</param>
    /// <param name="bounds">Image-pixel bounds on success; otherwise Rectangle.Empty.</param>
    /// <returns>True if the reference and grid coordinates resolve; image availability is not checked.</returns>
    public bool TryResolve(
        AnimationFrameDefinition frame,
        out TilesheetRegionDefinition? region,
        out Rectangle bounds)
    {
        region = null;
        bounds = Rectangle.Empty;

        if (!string.Equals(
                frame.Tilesheet,
                Definition.Name,
                StringComparison.Ordinal))
        {
            return false;
        }

        region = Definition.Regions.FirstOrDefault(
            candidate => string.Equals(
                candidate.Name,
                frame.RegionName,
                StringComparison.OrdinalIgnoreCase));

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

    /// <summary>Computes frame image-pixel bounds using region origin, margins, and per-tile padding.</summary>
    /// <param name="region">Region supplying frame dimensions and spacing.</param>
    /// <param name="x">Zero-based tile column; grid bounds are not checked.</param>
    /// <param name="y">Zero-based tile row; grid bounds are not checked.</param>
    /// <returns>The untransformed source rectangle in image pixels.</returns>
    /// <exception cref="OverflowException">Computed coordinates exceed the integer range.</exception>
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

    /// <summary>Disposes and clears the owned preview bitmap; borrowed package catalogs are unaffected.</summary>
    public void Dispose()
    {
        Image?.Dispose();
        Image = null;
    }

    /// <summary>Loads a singleton loose GTS document with its preview resources.</summary>
    /// <param name="path">Loose document path.</param>
    /// <returns>A source owned by the caller, which must dispose it.</returns>
    public static TilesheetSource Load(string path) => LoadMember(path, null);

    /// <summary>Loads all GTS members from a loose file in document order and prepares their previews.</summary>
    /// <param name="path">Loose document path.</param>
    /// <param name="packages">Borrowed package cache, or null to create temporary caches during loading.</param>
    /// <returns>Independent source objects; the caller must dispose every returned source.</returns>
    public static IReadOnlyList<TilesheetSource> LoadAll(string path, AssetPackageCatalog? packages = null) =>
        TilesheetDefinitionSerializer.LoadAll(path).Select(definition => new TilesheetSource(new AuthoringResource(Path.GetFullPath(path), Gondwana.Assets.AssetTypes.TilesheetDefinition), definition, packages)).ToList();

    /// <summary>Loads one logical member from an exact typed packed GTS entry.</summary>
    /// <param name="archivePath">Containing archive path, normalized against the process current directory.</param>
    /// <param name="entry">Exact entry name within the expected definition asset type.</param>
    /// <param name="identity">Logical member identity within the packed document.</param>
    /// <param name="packages">Borrowed package cache, or null to use a temporary cache during loading.</param>
    /// <returns>A source owned by the caller, which must dispose its preview resources.</returns>
    public static TilesheetSource LoadPacked(string archivePath, string entry, string identity, AssetPackageCatalog? packages = null) =>
        LoadResource(new AuthoringResource(Path.GetFullPath(archivePath), Gondwana.Assets.AssetTypes.TilesheetDefinition, entry), identity, packages);

    internal static TilesheetSource LoadResource(AuthoringResource resource, string? identity, AssetPackageCatalog? packages = null)
    {
        using var owned = packages is null ? new AssetPackageCatalog() : null;
        packages ??= owned!;
        return new(resource, new AuthoringResourceResolver(packages).LoadTilesheet(resource, identity), packages);
    }
}


internal readonly record struct FramePreview(
    Bitmap Image,
    Rectangle SourceBounds);
