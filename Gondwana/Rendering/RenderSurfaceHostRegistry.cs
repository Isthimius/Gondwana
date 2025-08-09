namespace Gondwana.Rendering;

public static class RenderSurfaceHostRegistry
{
    private static readonly List<RenderSurfaceHostBase> _all = new();
    public static IReadOnlyList<RenderSurfaceHostBase> All => _all;
    internal static void Register(RenderSurfaceHostBase host) => _all.Add(host);
    internal static void Unregister(RenderSurfaceHostBase host) => _all.Remove(host);
}
