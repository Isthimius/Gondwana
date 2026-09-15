using NAudio.Vorbis;
using NAudio.Wave;

namespace Gondwana.Audio.NAudio;

/// <summary>
/// Registry of file-format readers used by the NAudio backend.
/// </summary>
public static class NAudioReaderRegistry
{
    private sealed record Registration(Func<Stream, WaveStream>? StreamFactory, Func<string, WaveStream>? FileFactory);

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Registration> Readers = new(StringComparer.OrdinalIgnoreCase);

    static NAudioReaderRegistry()
    {
        Register(".wav", stream => new WaveFileReader(stream));
        Register(".mp3", stream => new Mp3FileReader(stream));
        Register(".ogg", stream => new VorbisWaveReader(stream));
        Register(".oga", stream => new VorbisWaveReader(stream));
        Register(".mogg", stream => new VorbisWaveReader(stream));
        RegisterFile(".wma", path => new MediaFoundationReader(path));
        RegisterFile(".m4a", path => new MediaFoundationReader(path));
    }

    /// <summary>Registers a stream-based NAudio reader for an extension.</summary>
    public static void Register(string extension, Func<Stream, WaveStream> readerFactory)
    {
        ArgumentNullException.ThrowIfNull(readerFactory);
        Readers[NormalizeExtension(extension)] = new Registration(readerFactory, null);
    }

    /// <summary>Registers a file-based NAudio reader for an extension.</summary>
    public static void RegisterFile(string extension, Func<string, WaveStream> readerFactory)
    {
        ArgumentNullException.ThrowIfNull(readerFactory);
        Readers[NormalizeExtension(extension)] = new Registration(null, readerFactory);
    }

    public static bool Supports(string fileNameOrExtension) => Readers.ContainsKey(GetExtension(fileNameOrExtension));

    public static IEnumerable<string> SupportedExtensions() => Readers.Keys.OrderBy(ext => ext);

    internal static (WaveStream Reader, string? TemporaryFilePath) Open(byte[] data, string fileNameOrExtension)
    {
        var extension = GetExtension(fileNameOrExtension);
        if (!Readers.TryGetValue(extension, out var registration))
            throw new NotSupportedException($"Format '{extension}' is not supported by Gondwana.Audio.NAudio.");

        if (registration.StreamFactory is not null)
        {
            var stream = new MemoryStream(data, writable: false);
            try
            {
                return (registration.StreamFactory(stream), null);
            }
            catch
            {
                stream.Dispose();
                throw;
            }
        }

        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + extension);
        try
        {
            File.WriteAllBytes(tempPath, data);
            return (registration.FileFactory!(tempPath), tempPath);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }
    }

    private static string GetExtension(string fileNameOrExtension)
    {
        var extension = Path.GetExtension(fileNameOrExtension);
        if (string.IsNullOrWhiteSpace(extension) && fileNameOrExtension.StartsWith('.'))
            extension = fileNameOrExtension;

        return NormalizeExtension(extension);
    }

    private static string NormalizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            throw new InvalidOperationException("Audio source has no file extension.");

        return extension.StartsWith('.') ? extension.ToLowerInvariant() : "." + extension.ToLowerInvariant();
    }

    internal static void TryDelete(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // Cleanup is best effort; playback disposal must not mask the original operation.
        }
    }
}
