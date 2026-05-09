using System.Collections.Concurrent;
using Gondwana.Assets;

namespace Gondwana.Drawing;

/// <summary>
/// Manages loaded <see cref="SvgResource"/> instances keyed by asset name.
/// </summary>
public sealed class SvgResourceManager : IDisposable
{
    private static readonly Lazy<SvgResourceManager> _instance = new(() => new SvgResourceManager());
    private readonly ConcurrentDictionary<string, SvgResource> _svgResources = new();
    private bool _disposed;

    private SvgResourceManager()
    { }

    /// <summary>
    /// Gets the singleton instance of the <see cref="SvgResourceManager"/>.
    /// </summary>
    public static SvgResourceManager Instance => _instance.Value;

    /// <summary>
    /// Loads an SVG resource from a file path.
    /// </summary>
    /// <param name="key">Unique resource key.</param>
    /// <param name="path">SVG file path.</param>
    /// <returns>The loaded resource.</returns>
    public SvgResource LoadFromFile(string key, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var resource = SvgResource.Load(path);
        return AddOrReplace(key, resource);
    }

    /// <summary>
    /// Loads all SVG resources from an <see cref="AssetsFile"/>.
    /// </summary>
    /// <param name="resourceFile">The assets file containing SVG entries.</param>
    /// <returns>The list of newly loaded resources.</returns>
    public List<SvgResource> LoadFromEngineAssetsFile(AssetsFile resourceFile)
    {
        ArgumentNullException.ThrowIfNull(resourceFile);
        var loaded = new List<SvgResource>();

        foreach (var entry in resourceFile.GetAllEntries())
        {
            if (entry.AssetType != AssetTypes.Svg || _svgResources.ContainsKey(entry.AssetName))
                continue;

            using var stream = resourceFile.Get(entry.AssetType, entry.AssetName);
            if (stream is null)
            {
                Engine.Logger.LogWarning("Failed to retrieve stream for SVG resource: {Key}", entry.AssetName);
                continue;
            }

            try
            {
                var svg = SvgResource.Load(stream);
                loaded.Add(AddOrReplace(entry.AssetName, svg));
            }
            catch (Exception ex)
            {
                Engine.Logger.LogError(ex, "Error loading SVG resource from asset file for key: {Key}", entry.AssetName);
                throw;
            }
        }

        return loaded;
    }

    /// <summary>
    /// Determines whether a resource key is loaded.
    /// </summary>
    public bool Contains(string key) => _svgResources.ContainsKey(key);

    /// <summary>
    /// Gets a resource by key or null when not found.
    /// </summary>
    public SvgResource? Get(string key) => _svgResources.TryGetValue(key, out var resource) ? resource : null;

    /// <summary>
    /// Gets all loaded resources.
    /// </summary>
    public Dictionary<string, SvgResource> GetAll() => _svgResources.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

    /// <summary>
    /// Unloads a resource by key.
    /// </summary>
    public void Unload(string key)
    {
        if (_svgResources.TryRemove(key, out var resource))
            resource.Dispose();
    }

    /// <summary>
    /// Clears all loaded resources.
    /// </summary>
    public void Clear()
    {
        foreach (var resource in _svgResources.Values)
            resource.Dispose();

        _svgResources.Clear();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        Clear();
        _disposed = true;
    }

    private SvgResource AddOrReplace(string key, SvgResource resource)
    {
        if (_svgResources.TryGetValue(key, out var existing))
            existing.Dispose();

        _svgResources[key] = resource;
        return resource;
    }
}
