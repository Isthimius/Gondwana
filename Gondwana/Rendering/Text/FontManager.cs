using System.Reflection;
using SkiaSharp;

namespace Gondwana.Rendering.Text;

/// <summary>
/// Centralized manager for loading, retrieving, and unloading shared fonts.
/// Fonts are stored by string key and reused across the application.
/// </summary>
public sealed class FontManager : IDisposable
{
    private static readonly Lazy<FontManager> _instance = new(() => new FontManager());

    private readonly Dictionary<string, SKTypeface> _fonts = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    /// <summary>
    /// Gets the singleton instance of the <see cref="FontManager"/>.
    /// </summary>
    public static FontManager Instance => _instance.Value;

    /// <summary>
    /// Prevents direct instantiation.
    /// </summary>
    private FontManager() { }

    /// <summary>
    /// Loads a font from a file path and stores it under the given key.
    /// If the key already exists, the old font is disposed and replaced.
    /// </summary>
    /// <param name="key">Logical name for the font.</param>
    /// <param name="filePath">Path to the font file.</param>
    /// <returns>The loaded font.</returns>
    /// <exception cref="ArgumentException">Thrown when key or filePath is invalid.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the font could not be loaded.</exception>
    public SKTypeface LoadFromFile(string key, string filePath)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Font key cannot be null or whitespace.", nameof(key));

        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("Font file path cannot be null or whitespace.", nameof(filePath));

        var typeface = SKTypeface.FromFile(filePath);
        if (typeface == null)
            throw new InvalidOperationException($"Failed to load font from file: {filePath}");

        ReplaceInternal(key, typeface);
        return typeface;
    }

    /// <summary>
    /// Loads a font from an embedded resource in the specified assembly and stores it under the given key.
    /// If the key already exists, the old font is disposed and replaced.
    /// </summary>
    /// <param name="key">Logical name for the font.</param>
    /// <param name="assembly">Assembly containing the embedded font resource.</param>
    /// <param name="resourceName">Fully qualified embedded resource name.</param>
    /// <returns>The loaded font.</returns>
    /// <exception cref="ArgumentException">Thrown when arguments are invalid.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the resource is missing or the font could not be loaded.</exception>
    public SKTypeface LoadFromResource(string key, Assembly assembly, string resourceName)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Font key cannot be null or whitespace.", nameof(key));

        if (assembly == null)
            throw new ArgumentNullException(nameof(assembly));

        if (string.IsNullOrWhiteSpace(resourceName))
            throw new ArgumentException("Resource name cannot be null or whitespace.", nameof(resourceName));

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
            throw new InvalidOperationException(
                $"Embedded font resource not found: '{resourceName}' in assembly '{assembly.FullName}'.");

        var typeface = SKTypeface.FromStream(stream);
        if (typeface == null)
            throw new InvalidOperationException($"Failed to load font from embedded resource: {resourceName}");

        ReplaceInternal(key, typeface);
        return typeface;
    }

    /// <summary>
    /// Loads a font from an embedded resource in the calling assembly and stores it under the given key.
    /// If the key already exists, the old font is disposed and replaced.
    /// </summary>
    public SKTypeface LoadFromResource(string key, string resourceName)
    {
        return LoadFromResource(key, Assembly.GetCallingAssembly(), resourceName);
    }

    /// <summary>
    /// Gets a font by key.
    /// </summary>
    /// <param name="key">Logical name of the font.</param>
    /// <returns>The matching font.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the key does not exist.</exception>
    public SKTypeface Get(string key)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Font key cannot be null or whitespace.", nameof(key));

        if (!_fonts.TryGetValue(key, out var typeface))
            throw new KeyNotFoundException($"No font is registered under key '{key}'.");

        return typeface;
    }

    /// <summary>
    /// Tries to get a font by key.
    /// </summary>
    public bool TryGet(string key, out SKTypeface? typeface)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(key))
        {
            typeface = null;
            return false;
        }

        return _fonts.TryGetValue(key, out typeface);
    }

    /// <summary>
    /// Retrieves the typeface associated with the specified key, or returns the default typeface if the key is not
    /// found or is null or whitespace.
    /// </summary>
    /// <param name="key">The key used to identify the desired typeface. If null, empty, or consists only of whitespace, the default
    /// typeface is returned.</param>
    /// <returns>The typeface associated with the specified key, or the default typeface if the key is not found or the key is
    /// null or whitespace.</returns>
    public SKTypeface GetOrDefault(string key)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(key))
            return SKTypeface.Default;

        return _fonts.TryGetValue(key, out var typeface)
            ? typeface
            : SKTypeface.Default;
    }

    /// <summary>
    /// Returns true if a font exists for the given key.
    /// </summary>
    public bool Contains(string key)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(key))
            return false;

        return _fonts.ContainsKey(key);
    }

    /// <summary>
    /// Removes a single font entry by key and disposes the stored typeface.
    /// </summary>
    /// <param name="key">Logical name of the font.</param>
    /// <returns>True if the font was found and removed; otherwise false.</returns>
    public bool Remove(string key)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(key))
            return false;

        if (!_fonts.TryGetValue(key, out var existing))
            return false;

        _fonts.Remove(key);
        existing.Dispose();
        return true;
    }

    /// <summary>
    /// Disposes all loaded fonts and clears the manager.
    /// </summary>
    public void Clear()
    {
        ThrowIfDisposed();

        foreach (var font in _fonts.Values)
            font.Dispose();

        _fonts.Clear();
    }

    /// <summary>
    /// Gets all currently registered font keys.
    /// </summary>
    public IReadOnlyCollection<string> Keys
    {
        get
        {
            ThrowIfDisposed();
            return _fonts.Keys.ToArray();
        }
    }

    private void ReplaceInternal(string key, SKTypeface newTypeface)
    {
        if (_fonts.TryGetValue(key, out var existing))
        {
            existing.Dispose();
            _fonts[key] = newTypeface;
        }
        else
        {
            _fonts.Add(key, newTypeface);
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(FontManager));
    }

    /// <summary>
    /// Disposes all loaded fonts managed by this instance and prevents further use.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        Clear();
        _disposed = true;
    }
}
