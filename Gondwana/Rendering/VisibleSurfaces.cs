using System.Collections.ObjectModel;
using System.Drawing;

namespace Gondwana.Rendering;

public static class VisibleSurfaces
{
    private static readonly List<VisibleSurfaceBase> _surfaces = new();
    private static Rectangle _maxSurfaceSize = new();
    private static readonly VisibleSurfacesInstance _instance = new(0);

    public static int Count => _surfaces.Count;
    public static ReadOnlyCollection<VisibleSurfaceBase> AllVisibleSurfaces => _surfaces.AsReadOnly();
    public static Rectangle MaxSurfaceSize => _maxSurfaceSize;

    public static double ForcedRefreshRate
    {
        get => _instance.RefreshRate;
        set => _instance.SetVisibleSurfaceRefreshTimer(value);
    }

    public static void Add(VisibleSurfaceBase surface)
    {
        if (_surfaces.Contains(surface)) return;

        _surfaces.Add(surface);
        RecalculateMaxSurfaceSize();
    }

    public static void Remove(VisibleSurfaceBase surface)
    {
        if (!_surfaces.Remove(surface)) return;

        RecalculateMaxSurfaceSize();
    }

    private static void RecalculateMaxSurfaceSize()
    {
        _maxSurfaceSize = new Rectangle();

        foreach (var surface in _surfaces)
        {
            _maxSurfaceSize = Rectangle.Union(_maxSurfaceSize, new Rectangle(0, 0, surface.Width, surface.Height));
        }

        Drawing.Sprites.Sprites.CreateChildSprites();
    }

    internal static IReadOnlyList<VisibleSurfaceBase> InternalSurfaces => _surfaces;
}
