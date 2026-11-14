using System.IO.Compression;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Runtime.Loader;

namespace Gondwana.Tooling.Studio.Core.Extensibility;

/// <summary>Bounded, metadata-only inspection. Never loads an assembly into an execution context.</summary>
internal sealed class PluginPackageInspection(IEnumerable<Assembly> contracts)
{
    internal const int MaximumEntries = 4096;
    internal const long MaximumBytes = 256L * 1024 * 1024;
    private readonly IReadOnlyDictionary<string, Assembly> _shared = PluginLoadContext.GetSharedAssemblies(contracts);
    private readonly HashSet<string> _framework = FrameworkAssemblyCatalog.ReadRuntime();

    internal static void SafeRelative(string relative)
    {
        if (relative.Length > 240 || relative.Split('/').Length > 16)
            throw new InvalidDataException("Package path exceeds the length/depth limit.");
        foreach (var part in relative.Split('/'))
        {
            var stem = part.Split('.')[0];
            if (part.Length == 0 || part is "." or ".." || part.EndsWith('.') || part.EndsWith(' ') ||
                part.Any(c => c < 32 || "<>:\\|?*\"".Contains(c)) ||
                new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" }.Contains(stem, StringComparer.OrdinalIgnoreCase))
                throw new InvalidDataException($"Unsafe Windows package path: {relative}");
        }
    }

    internal static string[] Files(string directory)
    {
        PluginPackagePaths.EnsureNoLinks(directory);
        var files = new List<string>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long size = 0;
        void Walk(string current)
        {
            foreach (var path in Directory.EnumerateFileSystemEntries(current))
            {
                PluginPackagePaths.EnsureNoLinks(path);
                var relative = Path.GetRelativePath(directory, path).Replace(Path.DirectorySeparatorChar, '/');
                SafeRelative(relative);
                if (!names.Add(relative)) throw new InvalidDataException($"Case-colliding package path: {relative}");
                if (names.Count > MaximumEntries) throw new InvalidDataException("Package exceeds the 4096-entry limit.");
                if (Directory.Exists(path)) Walk(path);
                else
                {
                    size = checked(size + new FileInfo(path).Length);
                    if (size > MaximumBytes) throw new InvalidDataException("Package exceeds the 256 MiB expanded-size limit.");
                    files.Add(path);
                }
            }
        }
        Walk(directory);
        return files.Order(StringComparer.Ordinal).ToArray();
    }

    internal static string Fingerprint(string directory)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var files = Files(directory);
        foreach (var child in Directory.EnumerateDirectories(directory, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
            hash.AppendData(Encoding.UTF8.GetBytes(Path.GetRelativePath(directory, child).Replace(Path.DirectorySeparatorChar, '/') + "/\0"));
        foreach (var file in files)
        {
            hash.AppendData(Encoding.UTF8.GetBytes(Path.GetRelativePath(directory, file).Replace(Path.DirectorySeparatorChar, '/') + "\0"));
            using var stream = File.OpenRead(file);
            hash.AppendData(SHA256.HashData(stream));
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    internal PluginManifest Validate(string directory)
    {
        var files = Files(directory);
        var manifestPath = Path.Combine(directory, "plugin.json");
        if (new FileInfo(manifestPath).Length > 65536) throw new InvalidDataException("Manifest exceeds 64 KiB.");
        if (files.Any(f => Path.GetFileName(f).Equals("plugin.json", StringComparison.OrdinalIgnoreCase) && f != manifestPath))
            throw new InvalidDataException("Nested or ambiguously cased plugin manifests are not supported.");
        var manifest = PluginManifest.Read(directory);
        manifest.Validate();
        if (manifest.ContractVersion != PluginManifest.SupportedContractVersion)
            throw new InvalidDataException($"Unsupported contract {manifest.ContractVersion}; Studio supports {PluginManifest.SupportedContractVersion}.");
        var entry = Path.Combine(directory, manifest.EntryAssembly!);
        if (!File.Exists(entry)) throw new InvalidDataException($"Missing entry assembly: {manifest.EntryAssembly}");
        _ = AssemblyName.GetAssemblyName(entry);
        PluginLoadContext.ValidateAssemblies(directory, _shared);
        var available = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var managed = new List<string>();
        foreach (var file in files.Where(f => f.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                available.Add(AssemblyName.GetAssemblyName(file).Name!);
                managed.Add(file);
            }
            catch (BadImageFormatException) { /* Native dependencies are permitted. */ }
        }
        var resolver = new AssemblyDependencyResolver(entry);
        foreach (var file in managed)
        {
            using var stream = File.OpenRead(file);
            using var pe = new PEReader(stream);
            var metadata = pe.GetMetadataReader();
            foreach (var handle in metadata.AssemblyReferences)
            {
                var name = metadata.GetString(metadata.GetAssemblyReference(handle).Name);
                if (_shared.ContainsKey(name) || _framework.Contains(name)) continue;
                var resolved = resolver.ResolveAssemblyToPath(new AssemblyName(name));
                var adjacent = Path.Combine(directory, name + ".dll");
                if (!available.Contains(name) || (resolved is null && !File.Exists(adjacent)))
                    throw new InvalidDataException($"Missing dependency '{name}' referenced by '{Path.GetFileName(file)}'. Obtain the complete package and its dependency metadata from the publisher.");
                PluginPackagePaths.RequireContained(directory, resolved ?? adjacent);
            }
        }
        foreach (var file in files.Where(f => f.EndsWith(".deps.json", StringComparison.OrdinalIgnoreCase)))
        {
            using var stream = File.OpenRead(file);
            using var document = JsonDocument.Parse(stream);
            if (!document.RootElement.TryGetProperty("targets", out var targets)) continue;
            foreach (var target in targets.EnumerateObject())
                foreach (var library in target.Value.EnumerateObject())
                    foreach (var groupName in new[] { "runtime", "native", "runtimeTargets" })
                    {
                        if (!library.Value.TryGetProperty(groupName, out var group)) continue;
                        foreach (var asset in group.EnumerateObject())
                        {
                            SafeRelative(asset.Name);
                            var name = asset.Name.Split('/').Last();
                            if (name == "_._" || PluginLoadContext.IsReserved(Path.GetFileNameWithoutExtension(name), _shared)) continue;
                            if (!files.Any(f => Path.GetFileName(f).Equals(name, StringComparison.OrdinalIgnoreCase)))
                                throw new InvalidDataException($"Missing dependency asset '{asset.Name}' declared by '{Path.GetFileName(file)}'.");
                        }
                    }
        }
        return manifest;
    }

    internal static void CopyDirectory(string source, string destination)
    {
        var files = Files(source);
        Directory.CreateDirectory(destination);
        foreach (var child in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, child)));
        long copied = 0;
        foreach (var file in files)
        {
            PluginPackagePaths.EnsureNoLinks(file);
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using var input = File.OpenRead(file);
            using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            CopyBounded(input, output, ref copied);
            output.Flush(true);
        }
    }

    private static readonly uint[] CrcTable = Enumerable.Range(0, 256).Select(value =>
    {
        var crc = (uint)value;
        for (var bit = 0; bit < 8; bit++) crc = (crc & 1) != 0 ? 0xedb88320U ^ (crc >> 1) : crc >> 1;
        return crc;
    }).ToArray();

    private static void CopyBounded(Stream input, Stream output, ref long total, uint? expectedCrc = null, long? expectedLength = null)
    {
        var buffer = new byte[81920];
        var before = total;
        var crc = uint.MaxValue;
        int count;
        while ((count = input.Read(buffer)) != 0)
        {
            total = checked(total + count);
            if (total > MaximumBytes) throw new InvalidDataException("Package exceeds the 256 MiB expanded-size limit.");
            if (expectedCrc is not null)
                for (var i = 0; i < count; i++) crc = CrcTable[(crc ^ buffer[i]) & 0xff] ^ (crc >> 8);
            output.Write(buffer, 0, count);
        }
        if (expectedCrc is not null && (~crc != expectedCrc || total - before != expectedLength))
            throw new InvalidDataException("Corrupt ZIP entry: CRC or expanded length does not match its directory record.");
    }

    internal static void Extract(string source, string destination)
    {
        PluginPackagePaths.EnsureNoLinks(source);
        if (new FileInfo(source).Length > MaximumBytes) throw new InvalidDataException("ZIP exceeds the 256 MiB archive-size limit.");
        // Bound central-directory allocation before ZipArchive materializes its entry collection.
        using (var input = File.OpenRead(source))
        {
            var tail = new byte[(int)Math.Min(input.Length, 65557)];
            input.Position = input.Length - tail.Length;
            input.ReadExactly(tail);
            var end = -1;
            for (var i = tail.Length - 22; i >= 0; i--)
                if (BitConverter.ToUInt32(tail, i) == 0x06054b50 && i + 22 + BitConverter.ToUInt16(tail, i + 20) == tail.Length) { end = i; break; }
            if (end < 0 || BitConverter.ToUInt16(tail, end + 4) != 0 || BitConverter.ToUInt16(tail, end + 6) != 0 ||
                BitConverter.ToUInt16(tail, end + 10) > MaximumEntries || BitConverter.ToUInt32(tail, end + 12) > 4 * 1024 * 1024)
                throw new InvalidDataException("Malformed, multi-volume, ZIP64, or oversized ZIP directory. Use a normal ZIP with at most 4096 entries.");
        }
        using var archive = ZipFile.OpenRead(source);
        if (archive.Entries.Count > MaximumEntries) throw new InvalidDataException("ZIP exceeds the 4096-entry limit.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long expanded = 0;
        foreach (var entry in archive.Entries)
        {
            var name = entry.FullName.TrimEnd('/');
            SafeRelative(name);
            if (entry.FullName.EndsWith('/') && (entry.Length != 0 || entry.Crc32 != 0))
                throw new InvalidDataException("ZIP directories must not contain file data.");
            if (!names.Add(name)) throw new InvalidDataException($"Duplicate ZIP entry: {name}");
            var kind = (entry.ExternalAttributes >> 16) & 0xf000;
            if (kind is not (0 or 0x8000 or 0x4000) || (entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException($"Links and special ZIP entries are forbidden: {name}");
            expanded = checked(expanded + entry.Length);
            if (expanded > MaximumBytes) throw new InvalidDataException("ZIP exceeds the 256 MiB expanded-size limit.");
        }
        long copied = 0;
        foreach (var entry in archive.Entries)
        {
            var target = Path.Combine(destination, entry.FullName.Replace('/', Path.DirectorySeparatorChar));
            if (entry.FullName.EndsWith('/')) { Directory.CreateDirectory(target); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using var input = entry.Open();
            using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            CopyBounded(input, output, ref copied, entry.Crc32, entry.Length);
            output.Flush(true);
        }
    }
}
