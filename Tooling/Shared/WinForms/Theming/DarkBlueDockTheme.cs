using WeifenLuo.WinFormsUI.Docking;
using WeifenLuo.WinFormsUI.ThemeVS2012;

namespace Gondwana.Tooling.WinForms.Theming;

/// <summary>Retains the dark VS2015 factories, with navy surfaces and regenerated blue indicator/button images.</summary>
internal sealed class DarkBlueDockTheme : VS2015DarkTheme
{
    private bool _disposed;

    public DarkBlueDockTheme()
    {
        ToolingAppearance.ConfigureDarkBlue(this);
        // VS2015 constructs these bitmaps eagerly. Updating only ColorPalette leaves old-colored button rectangles.
        ReleaseGeneratedImages();
        ImageService = new ImageService(this);
    }

    private void ReleaseGeneratedImages()
    {
        // IImageService exposes both generated images and two shared resource lookup maps.
        // Never dispose the latter: other DockPanels use the same resource instances.
        foreach (var image in typeof(IImageService).GetProperties()
            .Where(property => property.Name is not "Dockindicator_PaneDiamond_Hotspot" and not "DockIndicator_PaneDiamond_HotspotIndex")
            .Select(property => property.GetValue(ImageService)).OfType<Image>().Distinct())
            image.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            ReleaseGeneratedImages();
            PaintingService.CleanUp();
        }
        base.Dispose(disposing);
    }
}
