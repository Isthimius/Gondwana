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

    /// <summary>
    /// Registers a tilesheet in the registry. If a tilesheet with the same name already exists, it is replaced.
    /// </summary>
    /// <param name="sheet">The tilesheet to register. Cannot be <see langword="null"/>.</param>
    /// <param name="disposeReplaced">If <see langword="true"/>, disposes any replaced tilesheet with the same name; otherwise, <see langword="false"/>.</param>
    /// <remarks>
    /// This method is thread-safe. If <paramref name="sheet"/> is <see langword="null"/>, an error is logged and the method returns without registering.
    /// The method subscribes to the tilesheet's Disposed event to automatically unregister it when disposed.
    /// </remarks>
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

    /// <summary>
    /// Removes the tilesheet with the specified name from the registry, but only if it matches the expected instance.
    /// </summary>
    /// <param name="name">The name of the tilesheet to remove. Cannot be <see langword="null"/>.</param>
    /// <param name="expected">The expected tilesheet instance. The removal only occurs if the registry contains this exact instance. Cannot be <see langword="null"/>.</param>
    /// <param name="dispose">If <see langword="true"/>, disposes the removed tilesheet; otherwise, <see langword="false"/>.</param>
    /// <returns><see langword="true"/> if the tilesheet was successfully removed; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// This method is thread-safe. It uses reference equality to ensure the correct instance is removed.
    /// If <paramref name="name"/> or <paramref name="expected"/> is <see langword="null"/>, a warning is logged and the method returns <see langword="false"/>.
    /// </remarks>
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

    /// <summary>
    /// Handles the renaming of a tilesheet by updating its registry entry from the old name to the new name.
    /// </summary>
    /// <param name="oldName">The previous name of the tilesheet. Cannot be <see langword="null"/>.</param>
    /// <param name="newName">The new name of the tilesheet. Cannot be <see langword="null"/>.</param>
    /// <param name="sheet">The tilesheet instance being renamed. Cannot be <see langword="null"/>.</param>
    /// <param name="disposeReplaced">If <see langword="true"/>, disposes any existing tilesheet that is replaced at the new name; otherwise, <see langword="false"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="oldName"/>, <paramref name="newName"/>, or <paramref name="sheet"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// This method is thread-safe. If a different tilesheet already exists at <paramref name="newName"/>, it will be replaced and a warning logged.
    /// </remarks>
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

    /// <summary>
    /// Releases all resources used by the <see cref="TilesheetRegistry"/> by clearing and disposing all registered tilesheets.
    /// </summary>
    public void Dispose()
    {
        Clear();
    }

    #region public shims to TilesheetFactory

    /// <summary>
    /// Creates a new tilesheet from an existing SKBitmap and registers it in the registry.
    /// </summary>
    /// <param name="name">The name to assign to the tilesheet.</param>
    /// <param name="bitmap">The SKBitmap containing the tilesheet image data.</param>
    /// <returns>The newly created and registered <see cref="Tilesheet"/>.</returns>
    public Tilesheet LoadFromBitmap(string name, SKBitmap bitmap)
    {
        var tilesheet = TilesheetFactory.FromBitmap(name, bitmap);
        Register(tilesheet);
        return tilesheet;
    }

    /// <summary>
    /// Creates a new tilesheet from a stream and registers it in the registry.
    /// </summary>
    /// <param name="name">The name to assign to the tilesheet.</param>
    /// <param name="stream">The stream containing the tilesheet image data.</param>
    /// <returns>The newly created and registered <see cref="Tilesheet"/>.</returns>
    public Tilesheet LoadFromStream(string name, Stream stream)
    {
        var tilesheet = TilesheetFactory.FromStream(name, stream);
        Register(tilesheet);
        return tilesheet;
    }

    /// <summary>
    /// Creates a new tilesheet from an image file and registers it in the registry.
    /// </summary>
    /// <param name="name">The name to assign to the tilesheet.</param>
    /// <param name="imageFilePath">The file path to the image file.</param>
    /// <returns>The newly created and registered <see cref="Tilesheet"/>.</returns>
    public Tilesheet LoadFromImageFile(string name, string imageFilePath)
    {
        var tilesheet = TilesheetFactory.FromImageFile(name, imageFilePath);
        Register(tilesheet);
        return tilesheet;
    }

    /// <summary>
    /// Creates a new tilesheet from an entry in an assets file and registers it in the registry.
    /// </summary>
    /// <param name="assetsFile">The assets file containing the tilesheet image.</param>
    /// <param name="entryName">The name of the entry within the assets file.</param>
    /// <returns>The newly created and registered <see cref="Tilesheet"/>.</returns>
    public Tilesheet LoadFromAssetsFile(AssetsFile assetsFile, string entryName)
    {
        var tilesheet = TilesheetFactory.FromAssetsFile(assetsFile, entryName);
        Register(tilesheet);
        return tilesheet;
    }

    /// <summary>
    /// Creates a new tilesheet from a GTS definition file and registers it in the registry.
    /// </summary>
    /// <param name="gtsPath">The file path to the GTS (Gondwana Tilesheet) definition file.</param>
    /// <returns>The newly created and registered <see cref="Tilesheet"/>.</returns>
    public Tilesheet LoadFromDefinitionFile(string gtsPath)
    {
        var tilesheet = TilesheetFactory.FromDefinitionFile(gtsPath);
        Register(tilesheet);
        return tilesheet;
    }

    /// <summary>
    /// Creates a new tilesheet from a tilesheet definition object and registers it in the registry.
    /// </summary>
    /// <param name="definition">The tilesheet definition containing configuration and metadata.</param>
    /// <param name="baseDirectory">The optional base directory for resolving relative paths in the definition. If <see langword="null"/>, the current directory is used.</param>
    /// <returns>The newly created and registered <see cref="Tilesheet"/>.</returns>
    public Tilesheet LoadFromDefinition(TilesheetDefinition definition, string? baseDirectory = null)
    {
        var tilesheet = TilesheetFactory.FromDefinition(definition, baseDirectory);
        Register(tilesheet);
        return tilesheet;
    }

    /// <summary>
    /// Creates a new tilesheet from a GTS definition stored as an entry in an assets file and registers it in the registry.
    /// </summary>
    /// <param name="assetsFile">The assets file containing the GTS definition entry.</param>
    /// <param name="gtsEntryName">The name of the GTS definition entry within the assets file.</param>
    /// <returns>The newly created and registered <see cref="Tilesheet"/>.</returns>
    public Tilesheet LoadFromDefinitionAsset(AssetsFile assetsFile, string gtsEntryName)
    {
        var tilesheet = TilesheetFactory.FromDefinitionAsset(assetsFile, gtsEntryName);
        Register(tilesheet);

        return tilesheet;
    }

    #endregion public shims to TilesheetFactory
}