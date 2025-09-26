using Microsoft.Extensions.Logging;
using System.Collections.Immutable;

namespace Gondwana.Drawing.Tilesheets;

/// <summary>
/// Thread-safe singleton registry for <see cref="Tilesheet"/> instances.
/// </summary>
public sealed class TilesheetRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Tilesheet> _sheets = new(StringComparer.Ordinal);

    // --- Singleton setup ---
    private static readonly Lazy<TilesheetRegistry> _instance =
        new(() => new TilesheetRegistry());

    public static TilesheetRegistry Instance => _instance.Value;

    // Private ctor ensures no one else can new this up
    private TilesheetRegistry() { }

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
        }

        if (disposeReplaced && replaced is not null)
            replaced.Dispose();
    }

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

    public Tilesheet? GetOrNull(string name)
    {
        return TryGet(name, out var s) ? s : null;
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

        if (dispose)
            removed?.Dispose();

        return true;
    }

    internal bool Remove(string name, Tilesheet expected, bool dispose = false)
    {
        if (name is null)
        {
            Engine.Logger.LogWarning("TilesheetRegistry: Attempt to remove a null name.");
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
                return false; // mapping changed or not present
            }
        }

        if (dispose) removed?.Dispose();
        return true;
    }

    public void Clear()
    {
        List<Tilesheet> copy;
        lock (_gate)
        {
            copy = _sheets.Values.ToList();
            _sheets.Clear();
        }

        foreach (var ts in copy)
            ts.Dispose();
    }

    public IReadOnlyList<string> Names
    {
        get
        {
            lock (_gate)
                return _sheets.Keys.ToList();
        }
    }

    public IImmutableDictionary<string, Tilesheet> GetAll()
    {
        lock (_gate)
        {
            return _sheets.ToImmutableDictionary(kv => kv.Key, kv => kv.Value, _sheets.Comparer);
        }
    }

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
        if (oldName is null) throw new ArgumentNullException(nameof(oldName));
        if (newName is null) throw new ArgumentNullException(nameof(newName));
        if (sheet is null) throw new ArgumentNullException(nameof(sheet));

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

        if (disposeReplaced && replaced is not null)
            replaced.Dispose();
    }
}