using Gondwana.Assets;
using Gondwana.Drawing.Tilesheets.GTS;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using System.Collections.Immutable;

namespace Gondwana.Drawing.Tilesheets;

/// <summary>
/// Thread-safe singleton registry for <see cref="Tilesheet"/> instances.
/// </summary>
public sealed class TilesheetRegistry : IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Tilesheet> _sheets = new(StringComparer.Ordinal);

    // --- Singleton setup ---
    private static readonly Lazy<TilesheetRegistry> _instance =
        new(() => new TilesheetRegistry());

    /// <summary>
    /// Gets the singleton instance of the <see cref="TilesheetRegistry"/>.
    /// </summary>
    /// <value>The singleton instance.</value>
    public static TilesheetRegistry Instance => _instance.Value;

    // Private ctor ensures no one else can new this up
    private TilesheetRegistry()
    { }

    internal void Register(Tilesheet sheet, bool disposeReplaced = true)
    {
        if (sheet is null)
        {
            Engine.Logger.LogError("TilesheetRegistry: Attempt to register a null Tilesheet.");
            return;
        }

        Tilesheet? replaced = null;

        lock (_gate)
        {
            if (_sheets.TryGetValue(sheet.Name, out var existing) && !ReferenceEquals(existing, sheet))
            {
                _sheets[sheet.Name] = sheet;
                replaced = existing;
            }
            else
            {
                _sheets[sheet.Name] = sheet;
            }

            // Avoid duplicate subscriptions if the same instance is registered again.
            sheet.Disposed -= OnTilesheetDisposed;
            sheet.Disposed += OnTilesheetDisposed;
        }

        if (replaced is not null)
        {
            replaced.Disposed -= OnTilesheetDisposed;

            if (disposeReplaced)
                replaced.Dispose();
        }
    }

    /// <summary>
    /// Attempts to retrieve a <see cref="Tilesheet"/> by its name from the registry.
    /// </summary>
    /// <param name="name">The name of the tilesheet to retrieve. Cannot be <see langword="null"/>.</param>
    /// <param name="sheet">When this method returns, contains the tilesheet associated with the specified name, 
    /// if found; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the tilesheet was found in the registry; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// This method is thread-safe. If <paramref name="name"/> is <see langword="null"/>, a warning is logged and 
    /// the method returns <see langword="false"/>.
    /// </remarks>
    public bool TryGet(string name, out Tilesheet? sheet)
    {
        if (name is null)
        {
            Engine.Logger.LogWarning("TilesheetRegistry: Attempt to look up a null name.");
            sheet = null;
            return false;
        }

        lock (_gate)
            return _sheets.TryGetValue(name, out sheet!);
    }

    /// <summary>
    /// Retrieves a <see cref="Tilesheet"/> by its name from the registry, or <see langword="null"/> if not found.
    /// </summary>
    /// <param name="name">The name of the tilesheet to retrieve.</param>
    /// <returns>The tilesheet associated with the specified name, or <see langword="null"/> if not found.</returns>
    /// <remarks>
    /// This method is thread-safe and provides a convenient alternative to <see cref="TryGet"/> when 
    /// a null return value is acceptable.
    /// </remarks>
    public Tilesheet? GetOrNull(string name)
    {
        return TryGet(name, out var s) ? s : null;
    }

    /// <summary>
    /// Gets the <see cref="Tilesheet"/> associated with the specified name.
    /// </summary>
    /// <param name="name">The name of the tilesheet to retrieve. Cannot be <see langword="null"/>.</param>
    /// <returns>The <see cref="Tilesheet"/> associated with the specified name.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when a tilesheet with the specified name is not found in the registry.</exception>
    /// <remarks>
    /// This indexer is thread-safe and provides direct access to tilesheets by name using bracket notation.
    /// If you need to check for existence without throwing an exception, use <see cref="TryGet"/> or 
    /// <see cref="GetOrNull"/> instead.
    /// </remarks>
    public Tilesheet this[string name]
    {
        get
        {
            if (name is null)
                throw new ArgumentNullException(nameof(name));

            lock (_gate)
            {
                if (_sheets.TryGetValue(name, out var sheet))
                    return sheet;

                throw new KeyNotFoundException($"Tilesheet with name '{name}' was not found in the registry.");
            }
        }
    }

    /// <summary>
    /// Removes the tilesheet with the specified name from the collection.
    /// </summary>
    /// <param name="name">The name of the tilesheet to remove. Cannot be <see langword="null"/>.</param>
    /// <param name="dispose">A value indicating whether the removed tilesheet should be disposed.  <see langword="true"/> to dispose the
    /// tilesheet; otherwise, <see langword="false"/>. This is to prevent issues where a Tilesheet is removed, but existing references
    /// still exist.</param>
    /// <returns><see langword="true"/> if the tilesheet was successfully removed; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="name"/> is <see langword="null"/>.</exception>
    public bool Remove(string name, bool dispose = false)
    {
        if (name is null)
        {
            Engine.Logger.LogWarning("TilesheetRegistry: Attempt to remove a null name.");
            return false;
        }

        Tilesheet? removed = null;

        lock (_gate)
        {
            if (!_sheets.TryGetValue(name, out removed))
                return false;

            _sheets.Remove(name);
        }

        removed.Disposed -= OnTilesheetDisposed;

        if (dispose)
            removed.Dispose();

        return true;
    }

    internal bool Remove(string name, Tilesheet expected, bool dispose = false)
    {
        if (name is null)
        {
            Engine.Logger.LogWarning("TilesheetRegistry: Attempt to remove a null name.");
            return false;
        }

        if (expected is null)
        {
            Engine.Logger.LogWarning("TilesheetRegistry: Attempt to remove a null Tilesheet.");
            return false;
        }

        Tilesheet? removed = null;

        lock (_gate)
        {
            if (_sheets.TryGetValue(name, out var current) && ReferenceEquals(current, expected))
            {
                _sheets.Remove(name);
                removed = current;
            }
            else
            {
                return false;
            }
        }

        removed.Disposed -= OnTilesheetDisposed;

        if (dispose)
            removed.Dispose();

        return true;
    }

    /// <summary>
    /// Removes all tilesheets from the registry and disposes them.
    /// </summary>
    /// <remarks>
    /// This method is thread-safe. All tilesheets in the registry are disposed after being removed.
    /// Use with caution as any external references to the tilesheets will reference disposed objects.
    /// </remarks>
    public void Clear()
    {
        List<Tilesheet> copy;

        lock (_gate)
        {
            copy = _sheets.Values.ToList();
            _sheets.Clear();
        }

        foreach (var ts in copy)
        {
            ts.Disposed -= OnTilesheetDisposed;
            ts.Dispose();
        }
    }

    /// <summary>
    /// Gets a read-only list of all tilesheet names currently registered in the registry.
    /// </summary>
    /// <value>A read-only list containing the names of all registered tilesheets.</value>
    /// <remarks>
    /// This property is thread-safe and returns a snapshot of the names at the time of access.
    /// </remarks>
    public IReadOnlyList<string> Names
    {
        get
        {
            lock (_gate)
                return _sheets.Keys.ToList();
        }
    }

    /// <summary>
    /// Gets an immutable snapshot of all registered tilesheets as a dictionary.
    /// </summary>
    /// <returns>An immutable dictionary containing all registered tilesheets, keyed by their names.</returns>
    /// <remarks>
    /// This method is thread-safe and returns a snapshot of the registry at the time of invocation.
    /// The returned dictionary uses the same string comparer as the internal collection.
    /// </remarks>
    public IImmutableDictionary<string, Tilesheet> GetAll()
    {
        lock (_gate)
        {
            return _sheets.ToImmutableDictionary(kv => kv.Key, kv => kv.Value, _sheets.Comparer);
        }
    }

    /// <summary>
    /// Gets the number of tilesheets currently registered in the registry.
    /// </summary>
    /// <value>The total count of registered tilesheets.</value>
    /// <remarks>
    /// This property is thread-safe.
    /// </remarks>
    public int Count
    {
        get
        {
            lock (_gate)
                return _sheets.Count;
        }
    }

    internal void OnTilesheetRenamed(string oldName, string newName, Tilesheet sheet, bool disposeReplaced = true)
    {
        if (oldName is null)
            throw new ArgumentNullException(nameof(oldName));

        if (newName is null)
            throw new ArgumentNullException(nameof(newName));

        if (sheet is null)
            throw new ArgumentNullException(nameof(sheet));

        Tilesheet? replaced = null;

        lock (_gate)
        {
            // If old mapping points to this sheet, remove it.
            if (_sheets.TryGetValue(oldName, out var existingAtOld) && ReferenceEquals(existingAtOld, sheet))
                _sheets.Remove(oldName);

            // If something else is already at newName, warn and replace.
            if (_sheets.TryGetValue(newName, out var existingAtNew) && !ReferenceEquals(existingAtNew, sheet))
            {
                Engine.Logger.LogWarning(
                    "TilesheetRegistry: Renaming '{Old}'→'{New}' replaced a different registered Tilesheet instance.",
                    oldName, newName);
                replaced = existingAtNew;
            }

            _sheets[newName] = sheet;
        }

        if (replaced is not null)
        {
            replaced.Disposed -= OnTilesheetDisposed;

            if (disposeReplaced)
                replaced.Dispose();
        }
    }

    private void OnTilesheetDisposed(Tilesheet sheet)
    {
        if (sheet is null)
            return;

        Remove(sheet.Name, sheet, dispose: false);
    }

    public void Dispose()
    {
        Clear();
    }

    #region public shims to TilesheetFactory

    public Tilesheet LoadFromBitmap(string name, SKBitmap bitmap)
    {
        var tilesheet = TilesheetFactory.FromBitmap(name, bitmap);
        Register(tilesheet);
        return tilesheet;
    }

    public Tilesheet LoadFromStream(string name, Stream stream)
    {
        var tilesheet = TilesheetFactory.FromStream(name, stream);
        Register(tilesheet);
        return tilesheet;
    }

    public Tilesheet LoadFromImageFile(string name, string imageFilePath)
    {
        var tilesheet = TilesheetFactory.FromImageFile(name, imageFilePath);
        Register(tilesheet);
        return tilesheet;
    }

    public Tilesheet LoadFromAssetsFile(AssetsFile assetsFile, string entryName)
    {
        var tilesheet = TilesheetFactory.FromAssetsFile(assetsFile, entryName);
        Register(tilesheet);
        return tilesheet;
    }

    public Tilesheet LoadFromDefinitionFile(string gtsPath)
    {
        var tilesheet = TilesheetFactory.FromDefinitionFile(gtsPath);
        Register(tilesheet);
        return tilesheet;
    }

    public Tilesheet LoadFromDefinition(TilesheetDefinition definition, string? baseDirectory = null)
    {
        var tilesheet = TilesheetFactory.FromDefinition(definition, baseDirectory);
        Register(tilesheet);
        return tilesheet;
    }

    public Tilesheet LoadFromDefinitionAsset(AssetsFile assetsFile, string gtsEntryName)
    {
        var tilesheet = TilesheetFactory.FromDefinitionAsset(assetsFile, gtsEntryName);
        Register(tilesheet);

        return tilesheet;
    }

    #endregion public shims to TilesheetFactory
}