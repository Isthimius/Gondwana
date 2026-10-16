using Gondwana.Assets;

namespace Gondwana.Tooling.Assets;

/// <summary>Copies an in-memory GAF with portable outbound references, without changing inbound references or the source.</summary>
public static class AssetArchiveCopy
{
    /// <summary>Saves a new archive using its final destination for every reference calculation.</summary>
    /// <param name="source">Borrowed archive, including unsaved entries.</param>
    /// <param name="destination">New file path; existing destinations are rejected.</param>
    /// <param name="password">New archive password.</param>
    /// <param name="encrypt">New archive encryption setting.</param>
    public static void Save(AssetsFile source, string destination, string? password, bool encrypt)
    {
        destination = Path.GetFullPath(destination);
        DefinitionReferenceDiscovery.CheckPath(destination);
        if (File.Exists(destination) || Directory.Exists(destination)) throw new IOException($"Copy destination exists: {destination}.");
        var sourcePath = Path.GetFullPath(source.SourcePath!);
        var contents = AssetImportPlan.Snapshot(source);
        var mappings = contents.Keys.Select(key => new ReferenceRelocation(new(key.AssetType, sourcePath, key.AssetName), new(key.AssetType, destination, key.AssetName))).ToArray();
        var transformed = contents.ToDictionary(pair => pair.Key, pair =>
            PackDefinitionDocument.Read(pair.Key.AssetType, pair.Value, pair.Key.AssetName)?.Relocate(
                new(pair.Key.AssetType, sourcePath, pair.Key.AssetName), new(pair.Key.AssetType, destination, pair.Key.AssetName), mappings, false) ?? pair.Value);
        var temporary = Path.Combine(Path.GetDirectoryName(destination)!, ".gondwana-copy-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var copy = AssetsFile.LoadOrCreate(temporary, password, encrypt, register: false))
            {
                foreach (var pair in transformed)
                {
                    using var stream = new MemoryStream(pair.Value, writable: false);
                    copy.Add(pair.Key.AssetType, pair.Key.AssetName, stream);
                }
                copy.Save();
            }
            AssetsFile.Validate(temporary, password);
            File.Move(temporary, destination, overwrite: false);
        }
        finally { File.Delete(temporary); }
    }
}
