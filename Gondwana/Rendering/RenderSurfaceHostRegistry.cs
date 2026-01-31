namespace Gondwana.Rendering;

/// <summary>
/// Provides a central registry for tracking all active <see cref="RenderSurfaceHostBase"/> instances
/// in the application.
/// </summary>
/// <remarks>
/// This registry maintains a collection of all render surface hosts that have been created and not yet disposed.
/// Render surface hosts automatically register themselves upon construction and unregister upon disposal.
/// Use the <see cref="All"/> property to enumerate all active render surface hosts, which is useful for
/// debugging, diagnostics, or global rendering operations.
/// </remarks>
public static class RenderSurfaceHostRegistry
{
    private static readonly List<RenderSurfaceHostBase> _all = new();

    /// <summary>
    /// Gets a read-only collection of all currently registered <see cref="RenderSurfaceHostBase"/> instances.
    /// </summary>
    /// <value>
    /// An <see cref="IReadOnlyList{T}"/> of <see cref="RenderSurfaceHostBase"/> instances representing
    /// all active render surface hosts in the application.
    /// </value>
    /// <remarks>
    /// This collection is automatically maintained as render surface hosts are created and disposed.
    /// The collection reflects the current state at the time of access and may change as hosts are
    /// added or removed.
    /// </remarks>
    public static IReadOnlyList<RenderSurfaceHostBase> All => _all;

    internal static void Register(RenderSurfaceHostBase host) => _all.Add(host);

    internal static void Unregister(RenderSurfaceHostBase host) => _all.Remove(host);
}