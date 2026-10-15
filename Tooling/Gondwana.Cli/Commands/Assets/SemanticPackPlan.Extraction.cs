using Gondwana.Assets;

namespace Gondwana.Cli.Commands.Assets;

internal sealed partial class SemanticPackPlan
{
    /// <summary>Reuses the typed dependency inventory for export, with selected entries mapped to loose destinations.</summary>
    /// <param name="archive">The validated source archive.</param>
    /// <param name="path">Its fully resolved physical path.</param>
    /// <param name="destinations">Selected typed entries and their validated loose output paths.</param>
    /// <param name="password">Password for referenced external archives when required.</param>
    /// <param name="cancellation">Cancellation token.</param>
    /// <returns>Prepared bytes for each selected entry; no source or destination is written.</returns>
    internal static IReadOnlyDictionary<AssetsFileEntry, byte[]> PrepareExtraction(AssetsFile archive, string path,
        IReadOnlyDictionary<AssetsFileEntry, string> destinations, string? password, CancellationToken cancellation)
    {
        var plan = new SemanticPackPlan(Path.GetDirectoryName(path)!, path, password, false, true, cancellation)
        {
            // Export must not reinterpret an existing external loose path as a same-named archive entry.
            _resolveLooseFromInventory = false
        };
        foreach (var key in archive.GetAllEntries())
        {
            cancellation.ThrowIfCancellationRequested();
            using var stream = archive.Get(key.AssetType, key.AssetName)!;
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            var source = new AssetLocation(key.AssetType, path, key.AssetName);
            var destination = destinations.TryGetValue(key, out var target) ? new AssetLocation(key.AssetType, target) : source;
            plan._entries.Add(key, ReadEntry(source, destination, buffer.ToArray()));
        }
        foreach (var key in destinations.Keys)
        {
            cancellation.ThrowIfCancellationRequested();
            var entry = plan._entries[key];
            entry.Document?.Validate(entry.Location.ToString());
            plan.Analyze(entry);
        }
        var targets = new HashSet<string>(destinations.Values, Paths);
        foreach (var dependency in plan._external)
            if (targets.Contains(dependency.Location.Path))
                throw new InvalidDataException($"Export destination would overwrite an external dependency: {dependency.Location.Path}.");
        var result = new Dictionary<AssetsFileEntry, byte[]>();
        foreach (var key in destinations.Keys)
        {
            cancellation.ThrowIfCancellationRequested();
            var entry = plan._entries[key];
            result.Add(key, entry.Document?.Relocate(entry.Location, entry.Destination, entry.Mappings) ?? entry.Bytes);
        }
        return result;
    }
}
