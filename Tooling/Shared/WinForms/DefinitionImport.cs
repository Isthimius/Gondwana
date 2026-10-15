namespace Gondwana.Tooling.Editing;

/// <summary>Prepares independent import batches before an editor mutates its document.</summary>
internal static class DefinitionImport
{
    /// <summary>Loads, selects, clones, and uniquely names an import batch atomically.</summary>
    /// <typeparam name="T">The definition type.</typeparam>
    /// <param name="path">The source document path.</param>
    /// <param name="existing">Existing destination members.</param>
    /// <param name="load">The format's document loader.</param>
    /// <param name="select">Selects source indices; null imports all, a null result cancels.</param>
    /// <param name="clone">Clones and rebases a member.</param>
    /// <param name="identity">Reads a logical identity.</param>
    /// <param name="rename">Assigns a collision-free identity.</param>
    /// <param name="fallback">The default identity for an unnamed legacy singleton.</param>
    /// <returns>The prepared batch, empty when canceled or unselected.</returns>
    internal static List<T> Prepare<T>(string path, IReadOnlyList<T> existing, Func<string, IReadOnlyList<T>> load,
        Func<IReadOnlyList<T>, IReadOnlyList<int>?>? select, Func<T, T> clone,
        Func<T, string> identity, Action<T, string> rename, string fallback)
    {
        var source = load(path);
        var indices = select is null ? Enumerable.Range(0, source.Count).ToArray() : select(source);
        if (indices is null || indices.Count == 0) return [];
        if (indices.Any(index => index < 0 || index >= source.Count) || indices.Distinct().Count() != indices.Count)
            throw new ArgumentException("Select each source definition at most once using a valid index.", nameof(select));
        var names = existing.Select(identity).ToHashSet(StringComparer.Ordinal);
        var reserved = indices.Select(index => identity(source[index])).ToHashSet(StringComparer.Ordinal);
        var result = new List<T>();
        foreach (var index in indices.OrderBy(index => index))
        {
            var member = clone(source[index]);
            var basis = string.IsNullOrWhiteSpace(identity(member)) ? fallback : identity(member);
            var name = basis;
            if (names.Contains(name))
            {
                int suffix = 2;
                do { name = basis + "-" + suffix++; }
                while (names.Contains(name) || reserved.Contains(name));
            }
            names.Add(name);
            rename(member, name);
            result.Add(member);
        }
        return result;
    }

    /// <summary>Rebases a filesystem reference without reading or moving the resource.</summary>
    /// <param name="path">The optional resource path.</param>
    /// <param name="sourceDirectory">The original document directory.</param>
    /// <param name="destinationDirectory">The current editor directory.</param>
    /// <returns>A destination-relative path, or an absolute path across volumes.</returns>
    internal static string? Rebase(string? path, string sourceDirectory, string destinationDirectory) =>
        string.IsNullOrWhiteSpace(path) ? path : Path.GetRelativePath(destinationDirectory, Path.GetFullPath(path, sourceDirectory));
}
