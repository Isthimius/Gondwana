using System.Numerics;
using System.Reflection;
using System.Text.Json;

namespace Gondwana.Tooling.Studio.Core.Extensibility;

public enum PluginPackageOperationKind { Install, Update, Remove }
public enum PluginPackageOperationState { Pending, Applying, Failed, RecoveryRequired, Committed }

/// <summary>Durable operation metadata; paths are reconstructed beneath the fixed installation root.</summary>
public sealed record PluginPackageOperation
{
    public string Token { get; init; } = "";
    public string PluginId { get; init; } = "";
    public string Name { get; init; } = "";
    public string? Version { get; init; }
    public string? PreviousVersion { get; init; }
    public string Folder { get; init; } = "";
    public string? OriginalHash { get; init; }
    public string? PackageHash { get; init; }
    public PluginPackageOperationKind Kind { get; init; }
    public PluginPackageOperationState State { get; init; }
    public string? Diagnostic { get; init; }
}

/// <summary>A staged, validated proposal. Queue only after the user reviews and confirms it.</summary>
public sealed record PluginPackagePreview(PluginPackageOperation Operation, PluginManifest Manifest,
    string Destination, string VersionChange, IReadOnlyList<string> Dependencies)
{
    public const string TrustWarning = "Plugins are trusted executable code running with Studio's permissions. Validation and AssemblyLoadContext are not a security sandbox. Static inspection cannot guarantee safety or detect every lazy/native dependency failure.";
}

/// <summary>
/// Local package transactions. A lifetime reader lease prevents modification while any cooperating
/// Studio process may have loaded assemblies. All writers use one exclusive gate and durable journal.
/// </summary>
public sealed class PluginPackageOperations : IDisposable
{
    private readonly PluginPackageInspection _inspection;
    private readonly Action<string> _log;
    private FileStream? _session;
    private bool _started;
    private bool _journalHealthy = true;
    public string PluginRoot { get; }
    public string WorkRoot { get; }
    public string? Diagnostic { get; private set; }
    public bool CanDiscover => _session is not null;
    public bool CanManage => CanDiscover && _journalHealthy;
    public IReadOnlyList<PluginPackageOperation> Operations { get; private set; } = [];
    public bool RestartRequired => Operations.Any(o => o.State is PluginPackageOperationState.Pending or PluginPackageOperationState.Applying);
    // Deterministic failure injection at transaction boundaries, used by filesystem tests.
    internal Action<string>? Checkpoint { get; set; }
    private string JournalPath => Path.Combine(WorkRoot, "operations.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public PluginPackageOperations(string pluginRoot, Action<string> log, params Assembly[] sharedContracts)
    {
        PluginRoot = Path.GetFullPath(pluginRoot).TrimEnd(Path.DirectorySeparatorChar);
        var parent = Path.GetDirectoryName(PluginRoot) ?? throw new ArgumentException("Plugin root must have a parent.");
        WorkRoot = Path.Combine(parent, "." + Path.GetFileName(PluginRoot) + ".operations");
        _log = log;
        _inspection = new([typeof(IStudioPlugin).Assembly, .. sharedContracts]);
    }

    public static bool IsBundled(string? id, string? directory = null) =>
        id is "gondwana.project-diagnostics" or "gondwana.external-import" ||
        new[] { "ProjectDiagnostics", "ExternalImport" }.Contains(Path.GetFileName(directory), StringComparer.OrdinalIgnoreCase);

    private static void EnsureDirectory(string path)
    {
        var existing = path;
        while (!Directory.Exists(existing) && !File.Exists(existing))
            existing = Path.GetDirectoryName(existing) ?? throw new DirectoryNotFoundException($"No accessible parent for '{path}'.");
        PluginPackagePaths.EnsureNoLinks(existing);
        Directory.CreateDirectory(path);
        PluginPackagePaths.EnsureNoLinks(path);
    }

    private FileStream Gate()
    {
        EnsureDirectory(WorkRoot);
        var path = Path.Combine(WorkRoot, "writer.lock");
        if (File.Exists(path)) PluginPackagePaths.EnsureNoLinks(path);
        return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    }

    /// <summary>Call exactly once, before discovery, and retain this service until the process exits.</summary>
    public void StartSession()
    {
        if (_started) return;
        _started = true;
        try
        {
            using var gate = Gate();
            var lease = Path.Combine(WorkRoot, "sessions.lock");
            if (File.Exists(lease)) PluginPackagePaths.EnsureNoLinks(lease);
            FileStream? exclusive = null;
            try { exclusive = new FileStream(lease, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) { Diagnostic = "Package changes deferred: another Studio instance is using this installation. Close all Studio instances, then launch Studio again."; }
            using (exclusive)
            {
                try
                {
                    var journal = ReadJournal();
                    if (exclusive is not null)
                    {
                        foreach (var operation in journal.Operations.OrderBy(o => o.PluginId, StringComparer.Ordinal).ToArray())
                        {
                            if (operation.State == PluginPackageOperationState.Applying) Recover(journal, operation, "Interrupted transaction recovered at startup.");
                            else if (operation.State == PluginPackageOperationState.Pending) Apply(journal, operation);
                            else if (operation.State == PluginPackageOperationState.Committed && operation.Diagnostic is null) CleanupCommitted(journal, operation);
                        }
                    }
                    Operations = journal.Operations.ToArray();
                }
                catch (Exception ex) { _journalHealthy = false; Report($"Package journal '{JournalPath}' processing stopped; inspect retained transaction files and diagnostics. {ex.Message}"); }
            }
            // Gate is still held: no other process can obtain a writer lease between these opens.
            _session = new FileStream(lease, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (Diagnostic is not null) _log("Warning: [Plugin packages] " + Diagnostic);
        }
        catch (Exception ex)
        {
            Report($"Cannot coordinate plugin access at '{WorkRoot}'. Check installation-folder write permissions and close other Studio instances. Optional plugin discovery is withheld to avoid unsafe concurrent changes. {ex.Message}");
        }
    }

    public void ReportFailure(string message) => _log("Error: [Plugin packages] " + message);

    public void Refresh()
    {
        try
        {
            using var gate = Gate();
            Operations = ReadJournal().Operations.ToArray();
            _journalHealthy = true;
            if (Diagnostic?.StartsWith("Package journal", StringComparison.Ordinal) == true ||
                Diagnostic?.StartsWith("Cannot read pending", StringComparison.Ordinal) == true) Diagnostic = null;
        }
        catch (Exception ex) { _journalHealthy = false; Report($"Cannot read pending plugin operations: {ex.Message}"); }
    }

    private void Report(string message) { Diagnostic = message; _log("Error: [Plugin packages] " + message); }
    private string Transaction(PluginPackageOperation operation) => Path.Combine(WorkRoot, operation.Token);
    private string Payload(PluginPackageOperation operation) => Path.Combine(Transaction(operation), "payload");
    private string Backup(PluginPackageOperation operation) => Path.Combine(Transaction(operation), "backup");
    private string Destination(PluginPackageOperation operation) => Path.Combine(PluginRoot, operation.Folder);

    /// <summary>Copies to inactive staging and statically validates; does not queue or activate the package.</summary>
    public PluginPackagePreview Preview(string source, string? updateId = null)
    {
        using var gate = Gate();
        _ = ReadJournal(); // Never stage over an unreadable/newer journal.
        var operation = new PluginPackageOperation { Token = Guid.NewGuid().ToString("N") };
        var transaction = Transaction(operation);
        EnsureDirectory(transaction);
        try
        {
            var payload = Payload(operation);
            if (Directory.Exists(source)) PluginPackageInspection.CopyDirectory(source, payload);
            else
            {
                var extracted = Path.Combine(transaction, "extracted");
                EnsureDirectory(extracted);
                PluginPackageInspection.Extract(source, extracted);
                var roots = Directory.GetFiles(extracted, "plugin.json", SearchOption.AllDirectories);
                if (roots.Length != 1) throw new InvalidDataException("ZIP must contain one plugin.json, at its root or in one enclosing directory.");
                var package = Path.GetDirectoryName(roots[0])!;
                if (package != extracted && (Path.GetDirectoryName(package) != extracted || Directory.GetFileSystemEntries(extracted).Length != 1))
                    throw new InvalidDataException("Ambiguous ZIP layout: use package contents at the root or one enclosing package directory.");
                Directory.Move(package, payload);
                if (Directory.Exists(extracted)) Directory.Delete(extracted);
            }
            var manifest = _inspection.Validate(payload);
            if (IsBundled(manifest.Id)) throw new InvalidDataException("Studio-distributed plugins cannot be installed or updated here. Use Studio's distribution/update process; disabling remains available.");
            if (updateId is not null && manifest.Id != updateId) throw new InvalidDataException("The replacement manifest ID must match the selected plugin.");
            var installed = FindInstalled(manifest.Id!);
            if (updateId is not null && installed is null) throw new InvalidDataException("Selected installation is missing. Refresh and use Install Plugin instead.");
            var folder = installed is null ? "package-" + manifest.Id : Path.GetFileName(installed);
            Protect(manifest.Id!, folder);
            var target = Path.Combine(PluginRoot, folder);
            if (installed is null && (Directory.Exists(target) || File.Exists(target))) throw new InvalidDataException("Proposed destination is occupied by unrelated content.");
            PluginManifest? previous = installed is null ? null : PluginManifest.Read(installed);
            previous?.Validate();
            operation = operation with
            {
                PluginId = manifest.Id!,
                Name = manifest.Name!,
                Version = manifest.Version,
                PreviousVersion = previous?.Version,
                Folder = folder,
                OriginalHash = installed is null ? null : PluginPackageInspection.Fingerprint(installed),
                PackageHash = PluginPackageInspection.Fingerprint(payload),
                Kind = installed is null ? PluginPackageOperationKind.Install : PluginPackageOperationKind.Update
            };
            return new(operation, manifest, target, previous is null ? "New installation" : CompareVersions(manifest.Version!, previous.Version!) switch
            {
                > 0 => "Upgrade",
                < 0 => "Downgrade",
                _ => "Same-version replacement"
            }, PluginPackageInspection.Files(payload).Select(Path.GetFileName).Where(n => n != "plugin.json" && n != manifest.EntryAssembly).Select(n => n!).ToArray());
        }
        catch { DeleteOwned(transaction); throw; }
    }

    public PluginPackagePreview PreviewRemoval(string id)
    {
        using var gate = Gate();
        _ = ReadJournal();
        var installed = FindInstalled(id) ?? throw new InvalidDataException("Plugin is no longer installed. No removal was queued.");
        var folder = Path.GetFileName(installed);
        Protect(id, folder);
        var manifest = PluginManifest.Read(installed);
        var operation = new PluginPackageOperation
        {
            Token = Guid.NewGuid().ToString("N"),
            PluginId = id,
            Name = manifest.Name ?? id,
            Version = manifest.Version,
            Folder = folder,
            OriginalHash = PluginPackageInspection.Fingerprint(installed),
            Kind = PluginPackageOperationKind.Remove
        };
        EnsureDirectory(Transaction(operation));
        return new(operation, manifest, installed, "Removal", []);
    }

    /// <summary>Persist an explicitly confirmed preview. Same-version replacements/downgrades require separate consent.</summary>
    public void Queue(PluginPackagePreview preview, bool confirmed, bool replacementConfirmed = false)
    {
        if (!confirmed) throw new InvalidOperationException("Package changes require explicit confirmation.");
        using var gate = Gate();
        var journal = ReadJournal();
        var operation = preview.Operation;
        ValidateRecord(operation);
        CheckDestination(operation);
        if (operation.Kind == PluginPackageOperationKind.Update && !replacementConfirmed &&
            CompareVersions(operation.Version!, PluginManifest.Read(Destination(operation)).Version!) <= 0)
            throw new InvalidOperationException("Explicit downgrade/same-version replacement confirmation is required.");
        if (operation.Kind != PluginPackageOperationKind.Remove) ValidatePayload(operation);
        var prior = journal.Operations.SingleOrDefault(o => o.PluginId == operation.PluginId);
        if (prior is not null && !(operation.Kind == PluginPackageOperationKind.Update && prior.Kind == PluginPackageOperationKind.Update && prior.State == PluginPackageOperationState.Pending))
            throw new InvalidOperationException("Cancel the existing operation before requesting a different change for this plugin.");
        if (prior?.Token == operation.Token) throw new InvalidOperationException("This preview is already queued.");
        if (prior is null && journal.Operations.Count >= 128) throw new InvalidOperationException("At most 128 package operations can be pending.");
        if (prior is not null) journal.Operations.Remove(prior);
        journal.Operations.Add(operation with { State = PluginPackageOperationState.Pending, Diagnostic = null });
        Save(journal);
        Operations = journal.Operations.ToArray();
        if (prior is not null) TryDeleteTransaction(prior);
        _log($"[Plugin packages] {operation.Kind} queued for '{operation.PluginId}'; restart required.");
    }

    public void Discard(PluginPackagePreview preview)
    {
        using var gate = Gate();
        ValidateRecord(preview.Operation);
        if (ReadJournal().Operations.All(o => o.Token != preview.Operation.Token)) TryDeleteTransaction(preview.Operation);
    }

    public void Cancel(string id)
    {
        using var gate = Gate();
        var journal = ReadJournal();
        var operation = journal.Operations.Single(o => o.PluginId == id);
        if (operation.State is PluginPackageOperationState.Applying or PluginPackageOperationState.RecoveryRequired or PluginPackageOperationState.Committed || Directory.Exists(Backup(operation)))
            throw new InvalidOperationException("Recovery data must be preserved. Close Studio and follow the operation diagnostics before changing this transaction.");
        journal.Operations.Remove(operation);
        Save(journal);
        Operations = journal.Operations.ToArray();
        TryDeleteTransaction(operation);
    }

    private string? FindInstalled(string id)
    {
        if (File.Exists(PluginRoot)) throw new IOException("The plugin root is occupied by a file, not a directory.");
        if (!Directory.Exists(PluginRoot)) return null;
        PluginPackagePaths.EnsureNoLinks(PluginRoot);
        var matches = new List<string>();
        foreach (var directory in Directory.GetDirectories(PluginRoot))
        {
            PluginPackagePaths.EnsureNoLinks(directory);
            var path = Path.Combine(directory, "plugin.json");
            if (!File.Exists(path)) continue;
            PluginPackagePaths.EnsureNoLinks(path);
            try { if (PluginManifest.Read(directory).Id == id) matches.Add(directory); }
            catch (Exception ex) when (ex is JsonException or InvalidDataException) { /* Invalid unrelated packages remain the discovery host's responsibility. */ }
        }
        if (matches.Count > 1) throw new InvalidDataException($"Conflicting installations of '{id}'. Remove the ambiguity before managing this ID.");
        return matches.SingleOrDefault();
    }

    private static void Protect(string id, string folder)
    {
        PluginPackageInspection.SafeRelative(folder);
        if (folder.Contains('/')) throw new InvalidDataException("Destination must be one immediate package directory.");
        if (IsBundled(id, folder)) throw new InvalidDataException("Bundled Studio packages are protected. Disable them instead; Studio's updater owns their files.");
    }

    private void CheckDestination(PluginPackageOperation operation)
    {
        Protect(operation.PluginId, operation.Folder);
        var installed = FindInstalled(operation.PluginId);
        var target = Destination(operation);
        if (operation.Kind == PluginPackageOperationKind.Install)
        {
            if (installed is not null || Directory.Exists(target) || File.Exists(target)) throw new IOException("Installation destination or plugin ID changed since review. Cancel and review again.");
        }
        else if (installed is null || !PluginPackagePaths.Comparer.Equals(installed, target) || PluginPackageInspection.Fingerprint(target) != operation.OriginalHash)
            throw new IOException("Installed package changed or disappeared since review. Cancel and review the current installation again.");
    }

    private void ValidatePayload(PluginPackageOperation operation)
    {
        var manifest = _inspection.Validate(Payload(operation));
        if (manifest.Id != operation.PluginId || manifest.Version != operation.Version || PluginPackageInspection.Fingerprint(Payload(operation)) != operation.PackageHash)
            throw new InvalidDataException("Staged package changed since review. Cancel and review the source again.");
    }

    private void Apply(Journal journal, PluginPackageOperation operation)
    {
        var committed = false;
        try
        {
            CheckDestination(operation);
            if (operation.Kind != PluginPackageOperationKind.Remove) ValidatePayload(operation);
            EnsureDirectory(PluginRoot);
            operation = Replace(journal, operation, PluginPackageOperationState.Applying, null);
            Save(journal); // Write ahead: recovery can reason about every following rename.
            Checkpoint?.Invoke("before-backup");
            if (operation.Kind != PluginPackageOperationKind.Install) Directory.Move(Destination(operation), Backup(operation));
            Checkpoint?.Invoke("after-backup");
            if (operation.Kind != PluginPackageOperationKind.Remove) Directory.Move(Payload(operation), Destination(operation));
            Checkpoint?.Invoke("after-publish");
            operation = Replace(journal, operation, PluginPackageOperationState.Committed, null);
            Save(journal);
            committed = true;
            CleanupCommitted(journal, operation);
        }
        catch (Exception ex)
        {
            _log($"Error: [Plugin packages] {operation.Kind} '{operation.PluginId}': {ex}");
            if (!committed && operation.State is PluginPackageOperationState.Applying or PluginPackageOperationState.Committed) Recover(journal, operation, ex.Message);
            else
            {
                Replace(journal, operation, PluginPackageOperationState.Failed, ex.Message);
                Save(journal);
            }
        }
    }

    private void Recover(Journal journal, PluginPackageOperation operation, string reason)
    {
        try
        {
            var target = Destination(operation);
            var backup = Backup(operation);
            if (Directory.Exists(backup))
            {
                if (PluginManifest.Read(backup).Id != operation.PluginId || PluginPackageInspection.Fingerprint(backup) != operation.OriginalHash) throw new IOException("Backup changed unexpectedly; manual recovery is required.");
                if (Directory.Exists(target))
                {
                    if (operation.Kind != PluginPackageOperationKind.Update || PluginManifest.Read(target).Id != operation.PluginId || PluginPackageInspection.Fingerprint(target) != operation.PackageHash)
                        throw new IOException("Destination contains unexpected content; neither it nor the backup will be deleted.");
                    Directory.Move(target, Path.Combine(Transaction(operation), "rejected"));
                }
                Directory.Move(backup, target);
            }
            else if (operation.Kind == PluginPackageOperationKind.Install)
            {
                if (Directory.Exists(target))
                {
                    if (PluginManifest.Read(target).Id != operation.PluginId || PluginPackageInspection.Fingerprint(target) != operation.PackageHash) throw new IOException("Unexpected installation content; manual recovery required.");
                    Directory.Move(target, Path.Combine(Transaction(operation), "rejected"));
                }
            }
            else if (!Directory.Exists(target) || PluginManifest.Read(target).Id != operation.PluginId || PluginPackageInspection.Fingerprint(target) != operation.OriginalHash)
                throw new IOException("Original installation cannot be verified; preserve transaction files for manual recovery.");
            Replace(journal, operation, PluginPackageOperationState.Failed, $"{reason} Original installation preserved/restored. Cancel and review again; this operation will not automatically retry.");
        }
        catch (Exception ex)
        {
            Replace(journal, operation, PluginPackageOperationState.RecoveryRequired,
                $"{reason} Recovery stopped: {ex.Message} Close all Studio instances. Inspect '{Transaction(operation)}' and '{Destination(operation)}'; restore the verified backup manually. Do not delete recovery data. Automatic retries are disabled.");
        }
        Save(journal);
    }

    private void CleanupCommitted(Journal journal, PluginPackageOperation operation)
    {
        try
        {
            if (Directory.Exists(Backup(operation)) && (PluginManifest.Read(Backup(operation)).Id != operation.PluginId || PluginPackageInspection.Fingerprint(Backup(operation)) != operation.OriginalHash))
                throw new IOException("Backup content changed; it has been retained for inspection.");
            VerifyTransactionContents(operation);
            DeleteOwned(Transaction(operation));
            journal.Operations.RemoveAll(o => o.Token == operation.Token);
            Save(journal);
        }
        catch (Exception ex)
        {
            Replace(journal, operation, PluginPackageOperationState.Committed, $"Operation applied; cleanup retained '{Transaction(operation)}': {ex.Message}");
            Save(journal);
        }
    }

    private static PluginPackageOperation Replace(Journal journal, PluginPackageOperation operation, PluginPackageOperationState state, string? diagnostic)
    {
        var next = operation with { State = state, Diagnostic = diagnostic };
        var index = journal.Operations.FindIndex(o => o.Token == operation.Token);
        if (index < 0) journal.Operations.Add(next);
        else journal.Operations[index] = next;
        return next;
    }

    private void DeleteOwned(string path)
    {
        if (!Directory.Exists(path)) return;
        if (Path.GetDirectoryName(path) != WorkRoot || !Guid.TryParseExact(Path.GetFileName(path), "N", out _)) throw new InvalidDataException("Unsafe transaction cleanup path.");
        PluginPackagePaths.ValidateTree(path); // Never follow links while deleting staged or backup content.
        Directory.Delete(path, true);
    }
    private void VerifyTransactionContents(PluginPackageOperation operation)
    {
        var path = Transaction(operation);
        if (!Directory.Exists(path)) return;
        foreach (var child in Directory.GetFileSystemEntries(path))
        {
            var name = Path.GetFileName(child);
            var expected = name == "backup" ? operation.OriginalHash : operation.PackageHash;
            if (name is not ("payload" or "backup" or "rejected") || !Directory.Exists(child) ||
                PluginPackageInspection.Fingerprint(child) != expected)
                throw new IOException($"Unexpected transaction content retained at '{child}'. Inspect it manually while Studio is closed.");
        }
    }
    private void TryDeleteTransaction(PluginPackageOperation operation)
    {
        try { VerifyTransactionContents(operation); DeleteOwned(Transaction(operation)); }
        catch (Exception ex) { _log($"Warning: [Plugin packages] Staging retained at '{Transaction(operation)}': {ex.Message}"); }
    }

    private sealed class Journal
    {
        [System.Text.Json.Serialization.JsonRequired]
        public int SchemaVersion { get; set; } = 1;
        [System.Text.Json.Serialization.JsonRequired]
        public List<PluginPackageOperation> Operations { get; set; } = [];
    }

    private static void ValidateRecord(PluginPackageOperation operation)
    {
        if (!Guid.TryParseExact(operation.Token, "N", out _) || !PluginManifest.IsValidId(operation.PluginId) ||
            !Enum.IsDefined(operation.Kind) || !Enum.IsDefined(operation.State)) throw new InvalidDataException("Invalid package journal identity or operation.");
        Protect(operation.PluginId, operation.Folder);
        if (operation.Kind != PluginPackageOperationKind.Install && operation.OriginalHash is null ||
            operation.Kind != PluginPackageOperationKind.Remove && operation.PackageHash is null)
            throw new InvalidDataException("Package journal is missing content fingerprints.");
    }

    private Journal ReadJournal()
    {
        FileStream input;
        try
        {
            PluginPackagePaths.EnsureNoLinks(JournalPath);
            input = File.OpenRead(JournalPath);
        }
        catch (FileNotFoundException) { return new(); }
        using var stream = input;
        if (stream.Length > 1024 * 1024) throw new InvalidDataException("Package journal exceeds 1 MiB.");
        var journal = JsonSerializer.Deserialize<Journal>(stream) ?? throw new InvalidDataException("Empty package journal.");
        if (journal.SchemaVersion != 1 || journal.Operations is null || journal.Operations.Count > 128) throw new InvalidDataException("Unsupported or invalid package journal.");
        foreach (var operation in journal.Operations) ValidateRecord(operation);
        if (journal.Operations.Select(o => o.PluginId).Distinct(StringComparer.Ordinal).Count() != journal.Operations.Count ||
            journal.Operations.Select(o => o.Token).Distinct(StringComparer.Ordinal).Count() != journal.Operations.Count)
            throw new InvalidDataException("Duplicate journal identities.");
        return journal;
    }

    private void Save(Journal journal)
    {
        var temporary = Path.Combine(WorkRoot, "journal-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var bytes = JsonSerializer.SerializeToUtf8Bytes(journal, JsonOptions);
                if (bytes.Length > 1024 * 1024) throw new InvalidDataException("Pending-operation journal exceeds 1 MiB; cancel completed/unneeded operations first.");
                stream.Write(bytes);
                stream.Flush(true);
            }
            File.Move(temporary, JournalPath, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    /// <summary>SemVer precedence; build metadata does not change precedence.</summary>
    public static int CompareVersions(string left, string right)
    {
        var a = left.Split('+')[0].Split('-', 2);
        var b = right.Split('+')[0].Split('-', 2);
        var ac = a[0].Split('.'); var bc = b[0].Split('.');
        for (var i = 0; i < 3; i++) { var c = BigInteger.Parse(ac[i]).CompareTo(BigInteger.Parse(bc[i])); if (c != 0) return c; }
        if (a.Length != b.Length) return a.Length == 1 ? 1 : -1;
        if (a.Length == 1) return 0;
        var ap = a[1].Split('.'); var bp = b[1].Split('.');
        for (var i = 0; i < Math.Min(ap.Length, bp.Length); i++)
        {
            var an = BigInteger.TryParse(ap[i], out var av); var bn = BigInteger.TryParse(bp[i], out var bv);
            var c = an && bn ? av.CompareTo(bv) : an != bn ? an ? -1 : 1 : StringComparer.Ordinal.Compare(ap[i], bp[i]);
            if (c != 0) return c;
        }
        return ap.Length.CompareTo(bp.Length);
    }

    public void Dispose() { _session?.Dispose(); _session = null; }
}
