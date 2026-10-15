using Gondwana.Assets;
using ICSharpCode.SharpZipLib.Zip;

namespace Gondwana.Cli.Commands.Assets;

/// <summary>Preflights a final typed inventory and commits a package only after all dependencies resolve.</summary>
internal sealed partial class SemanticPackPlan
{
    private static readonly StringComparer Paths = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private readonly string _source;
    private readonly string _output;
    private readonly string? _password;
    private readonly bool _allowExternal;
    private readonly CancellationToken _cancellation;
    private readonly Dictionary<AssetsFileEntry, Entry> _entries = new();
    private readonly List<Entry> _incoming = [];
    private readonly List<Entry> _external = [];
    private bool _encrypt;

    private sealed class Entry(AssetLocation location, AssetLocation destination, byte[] bytes, PackDefinitionDocument? document)
    {
        /// <summary>Gets the physical input location.</summary>
        internal AssetLocation Location { get; } = location;
        /// <summary>Gets the final physical destination.</summary>
        internal AssetLocation Destination { get; } = destination;
        /// <summary>Gets or sets the buffered entry contents.</summary>
        internal byte[] Bytes { get; set; } = bytes;
        /// <summary>Gets the complete typed document, if native.</summary>
        internal PackDefinitionDocument? Document { get; } = document;
        /// <summary>Gets the resolved physical transformations.</summary>
        internal List<ReferenceRelocation> Mappings { get; } = [];
        /// <summary>Gets or sets whether an existing entry needs semantic updates.</summary>
        internal bool NeedsRewrite { get; set; }
    }

    private SemanticPackPlan(string source, string output, string? password, bool encrypt, bool allowExternal, CancellationToken cancellation)
    {
        _source = source;
        _output = output;
        _password = password;
        _encrypt = encrypt;
        _allowExternal = allowExternal;
        _cancellation = cancellation;
    }

    /// <summary>Inventories, parses, validates, resolves, and relocates all inputs without modifying the output.</summary>
    /// <param name="settings">Pack options.</param>
    /// <param name="map">The validated extension map.</param>
    /// <param name="fallback">The type used only for unknown extensions.</param>
    /// <param name="cancellation">Cancellation checked throughout planning and before committing.</param>
    /// <returns>A fully prepared pack transaction.</returns>
    internal static SemanticPackPlan Create(AssetsPackCommand.Settings settings, AssetTypeMap map, AssetTypes fallback, CancellationToken cancellation)
    {
        var plan = new SemanticPackPlan(Path.GetFullPath(settings.Source), Path.GetFullPath(settings.Output),
            settings.Password, settings.Encrypt, settings.AllowExternal, cancellation);
        cancellation.ThrowIfCancellationRequested();
        if (settings.Append && File.Exists(plan._output)) plan.ReadExisting();
        var names = new HashSet<AssetsFileEntry>();
        foreach (var path in Directory.EnumerateFiles(plan._source, "*", settings.Recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly).Order(StringComparer.Ordinal))
        {
            cancellation.ThrowIfCancellationRequested();
            var fullPath = Path.GetFullPath(path);
            if (Paths.Equals(fullPath, plan._output)) continue;
            var name = Path.GetRelativePath(plan._source, fullPath).Replace('\\', '/');
            SafeFilePath.ValidateRelative(name);
            var type = map.Infer(fullPath, fallback);
            var key = new AssetsFileEntry { AssetType = type, AssetName = name };
            if (!names.Add(key)) throw new InvalidDataException($"Conflicting destination identity: {type} / {name}.");
            var bytes = File.ReadAllBytes(fullPath);
            var entry = ReadEntry(new(type, fullPath), new(type, plan._output, name), bytes);
            plan._entries[key] = entry; // Append replaces this complete typed entry, never its members individually.
            plan._incoming.Add(entry);
        }
        foreach (var entry in plan._entries.Values)
        {
            cancellation.ThrowIfCancellationRequested();
            entry.Document?.Validate(entry.Location.ToString());
        }
        foreach (var entry in plan._entries.Values)
        {
            cancellation.ThrowIfCancellationRequested();
            plan.Analyze(entry);
        }
        foreach (var entry in plan._entries.Values.Where(entry => plan._incoming.Contains(entry) || entry.NeedsRewrite))
        {
            cancellation.ThrowIfCancellationRequested();
            if (entry.Document is not null)
            {
                entry.Document.Validate(entry.Location.ToString());
                entry.Bytes = entry.Document.Relocate(entry.Location, entry.Destination, entry.Mappings);
            }
        }
        return plan;
    }

    private static Entry ReadEntry(AssetLocation location, AssetLocation destination, byte[] bytes)
    {
        try { return new(location, destination, bytes, PackDefinitionDocument.Read(location.AssetType, bytes, location.AssetEntryName ?? location.Path)); }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException)
        { throw new InvalidDataException($"{location}: {ex.Message}", ex); }
    }

    private void ReadExisting()
    {
        AssetsFile.Validate(_output, _password);
        using (var zip = new ZipFile(File.OpenRead(_output)))
            _encrypt |= zip.Cast<ZipEntry>().Any(entry => entry.AESKeySize > 0);
        using var archive = AssetsFile.LoadOrCreate(_output, _password, _encrypt, register: false);
        foreach (var key in archive.GetAllEntries())
        {
            _cancellation.ThrowIfCancellationRequested();
            using var stream = archive.Get(key.AssetType, key.AssetName)!;
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            var location = new AssetLocation(key.AssetType, _output, key.AssetName);
            _entries.Add(key, ReadEntry(location, location, buffer.ToArray()));
        }
    }

    /// <summary>Writes a staging archive and atomically replaces the destination after successful validation.</summary>
    /// <returns>The number of input files packed.</returns>
    internal int Commit()
    {
        _cancellation.ThrowIfCancellationRequested();
        if (_incoming.Count == 0) return 0;
        var directory = Path.GetDirectoryName(_output)!;
        Directory.CreateDirectory(directory);
        var staging = Path.Combine(directory, $".{Path.GetFileName(_output)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var archive = AssetsFile.LoadOrCreate(staging, _password, _encrypt, register: false))
            {
                foreach (var pair in _entries.OrderBy(pair => pair.Key.AssetType).ThenBy(pair => pair.Key.AssetName, StringComparer.Ordinal))
                {
                    _cancellation.ThrowIfCancellationRequested();
                    using var stream = new MemoryStream(pair.Value.Bytes, writable: false);
                    archive.Add(pair.Key.AssetType, pair.Key.AssetName, stream);
                }
                archive.Save();
            }
            AssetsFile.Validate(staging, _password);
            _cancellation.ThrowIfCancellationRequested();
            File.Move(staging, _output, overwrite: true);
            return _incoming.Count;
        }
        finally { if (File.Exists(staging)) File.Delete(staging); }
    }

    private static bool Same(AssetLocation left, AssetLocation right) => left.AssetType == right.AssetType &&
        Paths.Equals(left.Path, right.Path) && StringComparer.OrdinalIgnoreCase.Equals(left.AssetEntryName, right.AssetEntryName);

    private static StringComparer Identities(AssetTypes type) => type == AssetTypes.AnimationDefinition ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private static bool Contains(Entry entry, string identity) => entry.Document?.Members().Any(member => Identities(entry.Location.AssetType).Equals(member.Identity, identity)) == true;

    private Entry Logical(AssetTypes type, string identity, string label)
    {
        var candidates = _entries.Values.Where(entry => entry.Location.AssetType == type && Contains(entry, identity)).ToArray();
        if (candidates.Length != 1)
            throw new InvalidDataException($"{label}: logical {type} '{identity}' has {candidates.Length} matching documents in the final inventory. Specify an explicit source. {string.Join("; ", candidates.Select(entry => entry.Location))}");
        return candidates[0];
    }

    private Entry? FindPacked(AssetLocation location)
    {
        if (!Paths.Equals(location.Path, _output)) return null;
        return _entries.GetValueOrDefault(new AssetsFileEntry { AssetType = location.AssetType, AssetName = location.AssetEntryName! });
    }

    private Entry Resolve(AssetLocation location, string? identity, string label)
    {
        _cancellation.ThrowIfCancellationRequested();
        var found = _entries.Values.FirstOrDefault(entry => Same(entry.Location, location));
        if (found is null && location.AssetEntryName is not null) found = FindPacked(location);
        if (found is null && location.AssetEntryName is null)
        {
            // A loose source omitted from this invocation may already be a typed entry during append.
            var relative = Path.GetRelativePath(_source, location.Path).Replace('\\', '/');
            if (relative != ".." && !relative.StartsWith("../", StringComparison.Ordinal) && !Path.IsPathRooted(relative))
                found = _entries.GetValueOrDefault(new AssetsFileEntry { AssetType = location.AssetType, AssetName = relative });
        }
        if (found is null)
        {
            if (_entries.Values.Any(entry => Paths.Equals(entry.Location.Path, location.Path) &&
                StringComparer.OrdinalIgnoreCase.Equals(entry.Location.AssetEntryName, location.AssetEntryName) && entry.Location.AssetType != location.AssetType))
                throw new InvalidDataException($"{label}: dependency {location} was inventoried under an incompatible asset type.");
            if (location.AssetEntryName is not null && Paths.Equals(location.Path, _output))
                throw new InvalidDataException($"{label}: missing destination entry {location} (member '{identity}').");
            if (!File.Exists(location.Path))
                throw new InvalidDataException($"{label}: missing dependency {location} (member '{identity}').");
            if (!_allowExternal)
                throw new InvalidDataException($"{label}: dependency {location} (member '{identity}') is outside the planned package. Include it or explicitly use --allow-external.");
            found = _external.FirstOrDefault(entry => Same(entry.Location, location));
            if (found is null)
            {
                byte[] bytes;
                if (location.AssetEntryName is null) bytes = File.ReadAllBytes(location.Path);
                else
                {
                    using var archive = AssetsFile.LoadOrCreate(location.Path, _password, false, register: false);
                    if (!archive.GetAllEntries().Any(key => key.AssetType == location.AssetType && StringComparer.OrdinalIgnoreCase.Equals(key.AssetName, location.AssetEntryName)))
                        throw new InvalidDataException($"{label}: missing external typed entry {location}.");
                    using var stream = archive.Get(location.AssetType, location.AssetEntryName)!;
                    using var buffer = new MemoryStream();
                    stream.CopyTo(buffer);
                    bytes = buffer.ToArray();
                }
                found = ReadEntry(location, location, bytes);
                found.Document?.Validate(label);
                _external.Add(found);
            }
        }
        if (identity is not null && !Contains(found, identity))
            throw new InvalidDataException($"{label}: {location} does not contain logical member '{identity}'.");
        return found;
    }

    private static void Map(Entry owner, AssetLocation source, AssetLocation destination)
    {
        if (Same(source, destination)) return;
        var previous = owner.Mappings.FirstOrDefault(mapping => Same(mapping.Source, source));
        if (previous is not null)
        {
            if (!Same(previous.Destination, destination)) throw new InvalidDataException($"Conflicting dependency mapping: {source}.");
            return;
        }
        owner.Mappings.Add(new(source, destination));
        owner.NeedsRewrite = true;
    }

    private static AssetLocation Physical(Entry owner, AssetTypes type, string? file, string? archive, string? entry, int? kind, string label)
    {
        if (kind is null && string.IsNullOrWhiteSpace(file) && string.IsNullOrWhiteSpace(archive) &&
            !string.IsNullOrWhiteSpace(entry) && owner.Location.AssetEntryName is not null) archive = owner.Location.Path;
        bool loose = !string.IsNullOrWhiteSpace(file);
        bool packed = !string.IsNullOrWhiteSpace(archive) && !string.IsNullOrWhiteSpace(entry);
        if (loose == packed || (loose && (!string.IsNullOrWhiteSpace(archive) || !string.IsNullOrWhiteSpace(entry))) ||
            (kind is not null && kind != (packed ? 1 : 0)))
            throw new InvalidDataException($"{label}: missing or conflicting physical dependency source.");
        return new(type, Path.GetFullPath(loose ? file! : archive!, Path.GetDirectoryName(owner.Location.Path)!), packed ? entry : null);
    }
}
