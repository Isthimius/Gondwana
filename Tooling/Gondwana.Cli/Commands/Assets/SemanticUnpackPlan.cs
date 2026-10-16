using Gondwana.Assets;
using Gondwana.Tooling.Assets;

namespace Gondwana.Cli.Commands.Assets;

/// <summary>Prepares semantic exports and commits staged files with rollback of replaced output.</summary>
internal sealed class SemanticUnpackPlan
{
    private readonly string _source;
    private readonly string _output;
    private readonly bool _overwrite;
    private readonly CancellationToken _cancellation;
    private readonly List<(string Name, string Target, byte[] Bytes)> _files = [];

    private SemanticUnpackPlan(string source, string output, bool overwrite, CancellationToken cancellation)
    {
        _source = source;
        _output = output;
        _overwrite = overwrite;
        _cancellation = cancellation;
    }

    /// <summary>Validates selection, destinations, and dependencies and prepares all transformed bytes without writing.</summary>
    /// <param name="path">The source GAF.</param>
    /// <param name="output">The output directory.</param>
    /// <param name="password">The archive password, if required.</param>
    /// <param name="type">Optional type filter.</param>
    /// <param name="entryName">Optional exact entry selector, using GAF case-insensitive identity.</param>
    /// <param name="overwrite">Whether existing destination files may be replaced.</param>
    /// <param name="cancellation">Cancellation token.</param>
    /// <returns>A prepared export transaction.</returns>
    internal static SemanticUnpackPlan Create(string path, string output, string? password, AssetTypes? type,
        string? entryName, bool overwrite, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var plan = new SemanticUnpackPlan(Path.GetFullPath(path), Path.GetFullPath(output), overwrite, cancellation);
        RejectLinkedSourceAncestry(plan._source);
        using var archive = BundleHelper.Open(plan._source, password);
        var selected = archive.GetAllEntries().Where(key => (type is null || key.AssetType == type) &&
            (entryName is null || StringComparer.OrdinalIgnoreCase.Equals(key.AssetName, entryName)))
            .OrderBy(key => key.AssetName, StringComparer.Ordinal).ThenBy(key => key.AssetType).ToArray();
        if (entryName is not null && selected.Length != 1)
            throw new InvalidDataException($"Entry '{entryName}' has {selected.Length} exact typed matches. Specify --type for an ambiguous name; basename matching is not supported.");
        var destinations = selected.ToDictionary(key => key, key => SafeFilePath.Resolve(plan._output, key.AssetName));
        var targets = destinations.Values.ToArray();
        if (targets.Distinct(StringComparer.OrdinalIgnoreCase).Count() != targets.Length)
            throw new InvalidDataException("Selected asset entries collide at the same output filename. Select one --type or --entry.");
        foreach (var pair in destinations)
        {
            cancellation.ThrowIfCancellationRequested();
            plan.CheckDestination(pair.Key.AssetName);
            if (targets.Any(other => other.StartsWith(pair.Value + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidDataException($"Selected entries contain a file/directory conflict: {pair.Value}.");
        }
        if (selected.Length == 0) return plan;
        var contents = SemanticPackPlan.PrepareExtraction(archive, plan._source, destinations, password, cancellation);
        foreach (var key in selected) plan._files.Add((key.AssetName, destinations[key], contents[key]));
        return plan;
    }

    private static void RejectLinkedSourceAncestry(string source)
    {
        for (var current = source; current is not null; current = Path.GetDirectoryName(current))
        {
            if ((File.Exists(current) || Directory.Exists(current)) &&
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0 ||
                new FileInfo(current).LinkTarget is not null)
                throw new IOException($"Source GAF path contains a symbolic link or junction: {current}.");
        }
    }

    private string CheckDestination(string name)
    {
        var target = SafeFilePath.Resolve(_output, name);
        if (StringComparer.OrdinalIgnoreCase.Equals(target, _source))
            throw new IOException($"Export cannot overwrite its source GAF: {target}.");
        if (Directory.Exists(target)) throw new IOException($"Destination is a directory: {target}.");
        if (File.Exists(target) && !_overwrite) throw new IOException($"File exists: {target}. Use --overwrite to replace it.");
        for (var current = target; current is not null; current = Path.GetDirectoryName(current))
        {
            if (current != target && File.Exists(current)) throw new IOException($"Destination parent is a file: {current}.");
            var parent = Path.GetDirectoryName(current);
            // Reject differently cased existing names even on case-sensitive filesystems.
            if (parent is not null && Directory.Exists(parent))
            {
                var namePart = Path.GetFileName(current);
                if (!OperatingSystem.IsWindows() && Directory.EnumerateFileSystemEntries(parent).Any(existing =>
                    StringComparer.OrdinalIgnoreCase.Equals(Path.GetFileName(existing), namePart) && Path.GetFileName(existing) != namePart))
                    throw new IOException($"Case-insensitive destination collision: {current}.");
            }
        }
        return target;
    }

    /// <summary>Stages every file, then installs files with backups and best-effort rollback on failure or cancellation.</summary>
    /// <remarks>Multi-file export is not globally atomic. Recovery backups are retained if rollback itself fails.</remarks>
    /// <returns>The number of exported physical entries.</returns>
    internal int Commit()
    {
        _cancellation.ThrowIfCancellationRequested();
        if (_files.Count == 0) return 0;
        foreach (var file in _files) CheckDestination(file.Name);
        var createdDirectories = new List<string>();
        var journal = new List<(string Name, string Target, string? Backup, bool Installed)>();
        var stagingName = ".gondwana-unpack-" + Guid.NewGuid().ToString("N");
        var staging = SafeFilePath.Resolve(_output, stagingName);
        bool retainRecovery = false;
        bool committed = false;
        Exception? originalFailure = null;
        try
        {
            CreateDirectories(_output, createdDirectories);
            SafeFilePath.Resolve(_output, stagingName);
            Directory.CreateDirectory(staging);
            for (int i = 0; i < _files.Count; i++)
            {
                _cancellation.ThrowIfCancellationRequested();
                var stageFile = SafeFilePath.Resolve(_output, stagingName + "/" + i + ".new");
                using var stream = new FileStream(stageFile, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                stream.Write(_files[i].Bytes);
            }
            for (int i = 0; i < _files.Count; i++)
            {
                _cancellation.ThrowIfCancellationRequested();
                var file = _files[i];
                var target = CheckDestination(file.Name);
                CreateDirectories(Path.GetDirectoryName(target)!, createdDirectories);
                target = CheckDestination(file.Name);
                string? backup = null;
                if (_overwrite && File.Exists(target))
                {
                    backup = SafeFilePath.Resolve(_output, stagingName + "/" + i + ".old");
                    File.Move(target, backup);
                }
                journal.Add((file.Name, target, backup, false));
                File.Move(SafeFilePath.Resolve(_output, stagingName + "/" + i + ".new"), target);
                journal[^1] = (file.Name, target, backup, true);
            }
            _cancellation.ThrowIfCancellationRequested();
            committed = true;
        }
        catch (Exception failure)
        {
            originalFailure = failure;
            var errors = new List<Exception> { failure };
            foreach (var item in journal.AsEnumerable().Reverse())
            {
                try
                {
                    SafeFilePath.Resolve(_output, item.Name);
                    if (item.Installed) File.Delete(item.Target);
                    if (item.Backup is not null) File.Move(item.Backup, item.Target);
                }
                catch (Exception rollback) { errors.Add(rollback); }
            }
            if (errors.Count > 1)
            {
                retainRecovery = true;
                throw new AggregateException($"Export failed and rollback was incomplete. Recovery files remain at {staging}.", errors);
            }
            throw;
        }
        finally
        {
            if (!retainRecovery)
            {
                try
                {
                    if (Directory.Exists(staging))
                    {
                        // Delete only known transaction files; do not recursively follow unexpected content.
                        for (int i = 0; i < _files.Count; i++)
                            foreach (var suffix in new[] { ".new", ".old" })
                                File.Delete(SafeFilePath.Resolve(_output, stagingName + "/" + i + suffix));
                        Directory.Delete(staging);
                    }
                    foreach (var directory in createdDirectories.AsEnumerable().Reverse())
                    {
                        SafeFilePath.Resolve(directory, ".path-check");
                        if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory);
                    }
                }
                catch (Exception cleanup)
                {
                    throw new IOException($"Export {(committed ? "committed" : "failed and was rolled back")}, but transaction cleanup was incomplete at {staging}.",
                        originalFailure is null ? cleanup : new AggregateException(originalFailure, cleanup));
                }
            }
        }
        return _files.Count;
    }

    private static void CreateDirectories(string directory, List<string> created)
    {
        var missing = new Stack<string>();
        for (var current = directory; current is not null && !Directory.Exists(current); current = Path.GetDirectoryName(current)) missing.Push(current);
        while (missing.TryPop(out var next))
        {
            SafeFilePath.Resolve(next, ".path-check");
            Directory.CreateDirectory(next);
            created.Add(next);
        }
    }
}
