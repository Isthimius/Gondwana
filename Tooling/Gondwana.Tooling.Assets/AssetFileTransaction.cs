using System.Text.Json;
using Gondwana.Assets;

namespace Gondwana.Tooling.Assets;

/// <summary>Shared file staging, recovery, and retirement mechanics for scoped moves and deletions.</summary>
internal sealed class AssetFileTransaction
{
    private readonly DefinitionReferenceDiscovery _scan;
    private readonly Dictionary<string, byte[]> _looseWrites;
    private readonly Dictionary<string, DefinitionReferenceDiscovery.Archive> _archiveWrites;
    private readonly HashSet<string> _removals;
    private readonly IReadOnlyList<ReferenceRelocation> _mappings;
    private readonly List<string> _preview;
    private string Scope => _scan.Scope;
    private IEnumerable<string> AffectedFiles => _looseWrites.Keys.Concat(_archiveWrites.Keys).Concat(_removals).Distinct(DefinitionReferenceDiscovery.Paths);

    /// <summary>Creates a transaction over already validated, immutable prepared content.</summary>
    /// <param name="scan">Original snapshots and scan scope.</param>
    /// <param name="looseWrites">Prepared loose documents.</param>
    /// <param name="archiveWrites">Prepared archive contents and credentials.</param>
    /// <param name="removals">Files retired after installs.</param>
    /// <param name="mappings">Move mappings whose packed sources remain until destination installation.</param>
    /// <param name="preview">Receives cleanup warnings.</param>
    internal AssetFileTransaction(DefinitionReferenceDiscovery scan, Dictionary<string, byte[]> looseWrites,
        Dictionary<string, DefinitionReferenceDiscovery.Archive> archiveWrites, HashSet<string> removals,
        IReadOnlyList<ReferenceRelocation> mappings, List<string> preview)
    {
        _scan = scan;
        _looseWrites = looseWrites;
        _archiveWrites = archiveWrites;
        _removals = removals;
        _mappings = mappings;
        _preview = preview;
    }

    private sealed record RecoveryFile(string Target, string Backup, string Staged, bool Existed);

    /// <summary>Executes the transaction with a fault hook for staging/install/retirement regression tests.</summary>
    /// <param name="checkpoint">Optional checkpoint observer; exceptions trigger rollback.</param>
    /// <param name="cancellation">Cancellation token.</param>
    internal void CommitCore(Action<string, int>? checkpoint, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        _scan.Recheck();
        var id = Guid.NewGuid().ToString("N");
        var directory = Directory.Exists(Scope) ? Scope : Path.GetDirectoryName(Scope)!;
        var manifest = Path.Combine(directory, $".gondwana-relocate-{id}.json");
        var completedManifest = manifest + ".completed";
        var recovery = new List<RecoveryFile>();
        var created = new List<string>();
        var touched = new HashSet<string>(DefinitionReferenceDiscovery.Paths);
        bool installing = false;
        bool retain = false;
        bool completed = false;
        try
        {
            foreach (var path in AffectedFiles.Order(StringComparer.Ordinal))
            {
                cancellation.ThrowIfCancellationRequested();
                DefinitionReferenceDiscovery.CheckPath(path);
                var parent = Path.GetDirectoryName(path)!;
                var missing = new Stack<string>();
                for (var current = parent; !Directory.Exists(current); current = Path.GetDirectoryName(current)!) missing.Push(current);
                while (missing.TryPop(out var folder)) { Directory.CreateDirectory(folder); created.Add(folder); }
                var item = new RecoveryFile(path, Path.Combine(parent, $".gondwana-relocate-{id}-{recovery.Count}.old"),
                    Path.Combine(parent, $".gondwana-relocate-{id}-{recovery.Count}.new"), File.Exists(path));
                recovery.Add(item);
                if (item.Existed) File.Copy(path, item.Backup, overwrite: false);
                if (_looseWrites.TryGetValue(path, out var bytes)) File.WriteAllBytes(item.Staged, bytes);
                else if (_archiveWrites.TryGetValue(path, out var archive))
                {
                    void StageArchive(string stagePath, Dictionary<AssetsFileEntry, byte[]> entries)
                    {
                        using var output = AssetsFile.LoadOrCreate(stagePath, archive.Password, archive.Encrypt, register: false);
                        foreach (var pair in entries.OrderBy(p => p.Key.AssetType).ThenBy(p => p.Key.AssetName, StringComparer.Ordinal))
                        {
                            using var stream = new MemoryStream(pair.Value, writable: false);
                            output.Add(pair.Key.AssetType, pair.Key.AssetName, stream);
                        }
                        output.Save();
                        AssetsFile.Validate(stagePath, archive.Password);
                    }
                    var retained = RetainedEntries(path);
                    if (retained.Count != 0)
                    {
                        StageArchive(item.Staged + ".final", archive.Entries);
                        var intermediate = new Dictionary<AssetsFileEntry, byte[]>(archive.Entries);
                        foreach (var pair in retained) intermediate.Add(pair.Key, pair.Value);
                        StageArchive(item.Staged, intermediate);
                    }
                    else StageArchive(item.Staged, archive.Entries);
                }
                checkpoint?.Invoke("stage", recovery.Count);
            }
            File.WriteAllText(manifest, JsonSerializer.Serialize(new { State = "Prepared", Files = recovery }, new JsonSerializerOptions { WriteIndented = true }));
            _scan.Recheck();
            cancellation.ThrowIfCancellationRequested();
            installing = true;
            int index = 0;
            // Archives losing entries are installed after other destinations and inbound-reference documents.
            foreach (var item in recovery.Where(r => File.Exists(r.Staged)).OrderBy(r => _mappings.Any(m => m.Source.AssetEntryName is not null && DefinitionReferenceDiscovery.Paths.Equals(m.Source.Path, r.Target))))
            {
                cancellation.ThrowIfCancellationRequested();
                DefinitionReferenceDiscovery.CheckPath(item.Target);
                CheckUnchanged(item.Target);
                File.Move(item.Staged, item.Target, overwrite: item.Existed);
                touched.Add(item.Target);
                checkpoint?.Invoke("install", ++index);
            }
            VerifyWrites(retainSources: true);
            foreach (var path in _scan.Files.Keys.Except(AffectedFiles, DefinitionReferenceDiscovery.Paths)) CheckUnchanged(path);
            index = 0;
            foreach (var item in recovery.Where(r => File.Exists(r.Staged + ".final")))
            {
                cancellation.ThrowIfCancellationRequested();
                DefinitionReferenceDiscovery.CheckPath(item.Target);
                File.Move(item.Staged + ".final", item.Target, overwrite: true);
                checkpoint?.Invoke("retire", ++index);
            }
            foreach (var path in _removals)
            {
                cancellation.ThrowIfCancellationRequested();
                DefinitionReferenceDiscovery.CheckPath(path);
                CheckUnchanged(path);
                File.Delete(path);
                touched.Add(path);
                checkpoint?.Invoke("retire", ++index);
            }
            cancellation.ThrowIfCancellationRequested();
            VerifyWrites();
            File.WriteAllText(completedManifest, JsonSerializer.Serialize(new { State = "Completed", Files = recovery }, new JsonSerializerOptions { WriteIndented = true }));
            checkpoint?.Invoke("complete-manifest", 0);
            File.Replace(completedManifest, manifest, destinationBackupFileName: null);
            completed = true;
        }
        catch (Exception original)
        {
            var failures = new List<Exception> { original };
            if (installing)
                foreach (var item in recovery.Where(item => touched.Contains(item.Target)).Reverse())
                {
                    try
                    {
                        DefinitionReferenceDiscovery.CheckPath(item.Target);
                        if (item.Existed) File.Copy(item.Backup, item.Target, overwrite: true);
                        else File.Delete(item.Target);
                    }
                    catch (Exception rollback) { failures.Add(rollback); }
                }
            if (failures.Count > 1)
            {
                retain = true;
                throw new AggregateException($"Rollback incomplete. Keep recovery manifest and sibling backups: {manifest}", failures);
            }
            throw;
        }
        finally
        {
            if (!retain)
            {
                try
                {
                    foreach (var item in recovery)
                    {
                        File.Delete(item.Staged);
                        File.Delete(item.Staged + ".final");
                        File.Delete(item.Backup);
                    }
                    File.Delete(completedManifest);
                    File.Delete(manifest);
                    foreach (var folder in created.AsEnumerable().Reverse())
                        if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any()) Directory.Delete(folder);
                }
                catch (IOException) when (completed) { _preview.Add($"Operation completed; remove leftover recovery files listed in {manifest} after inspection."); }
                catch (UnauthorizedAccessException) when (completed) { _preview.Add($"Operation completed; remove leftover recovery files listed in {manifest} after inspection."); }
            }
        }
    }

    private void CheckUnchanged(string path)
    {
        DefinitionReferenceDiscovery.CheckPath(path);
        var original = _scan.Files.GetValueOrDefault(path);
        if (original is null ? File.Exists(path) || Directory.Exists(path) : !File.Exists(path) || !File.ReadAllBytes(path).AsSpan().SequenceEqual(original))
            throw new IOException($"Resource changed during relocation: {path}.");
    }

    private Dictionary<AssetsFileEntry, byte[]> RetainedEntries(string path)
    {
        var result = new Dictionary<AssetsFileEntry, byte[]>();
        foreach (var mapping in _mappings.Where(m => m.Source.AssetEntryName is not null && DefinitionReferenceDiscovery.Paths.Equals(m.Source.Path, path)))
        {
            var key = new AssetsFileEntry { AssetType = mapping.Source.AssetType, AssetName = mapping.Source.AssetEntryName! };
            if (!_archiveWrites[path].Entries.ContainsKey(key)) result.Add(key, _scan.Archives[path].Entries[key]);
        }
        return result;
    }

    private void VerifyWrites(bool retainSources = false)
    {
        foreach (var pair in _looseWrites)
            if (!File.ReadAllBytes(pair.Key).AsSpan().SequenceEqual(pair.Value)) throw new IOException($"Committed bytes differ: {pair.Key}.");
        foreach (var pair in _archiveWrites)
        {
            using var archive = AssetsFile.LoadOrCreate(pair.Key, pair.Value.Password, pair.Value.Encrypt, register: false);
            var actual = AssetImportPlan.Snapshot(archive);
            var expected = new Dictionary<AssetsFileEntry, byte[]>(pair.Value.Entries);
            if (retainSources) foreach (var retained in RetainedEntries(pair.Key)) expected.Add(retained.Key, retained.Value);
            if (actual.Count != expected.Count || expected.Any(p => !actual.TryGetValue(p.Key, out var bytes) || !bytes.AsSpan().SequenceEqual(p.Value)))
                throw new IOException($"Committed archive differs: {pair.Key}.");
        }
    }
}
