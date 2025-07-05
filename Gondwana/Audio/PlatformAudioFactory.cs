using NAudio.Wave;

namespace Gondwana.Audio;

public static class PlatformAudioFactory
{
    private static readonly Dictionary<string, Func<Stream, WaveStream>> _readers = new(StringComparer.OrdinalIgnoreCase);

    static PlatformAudioFactory()
    {
        // Core format support
        Register(".wav", stream => new WaveFileReader(stream));
        Register(".mp3", stream => new Mp3FileReader(stream));
    }

    public static void Register(string extension, Func<Stream, WaveStream> readerFactory)
    {
        _readers[NormalizeExt(extension)] = readerFactory;
    }

    public static bool Supports(string fileNameOrExt)
    {
        var ext = NormalizeExt(Path.GetExtension(fileNameOrExt));
        return _readers.ContainsKey(ext);
    }

    public static IEnumerable<string> SupportedExtensions()
    {
        return _readers.Keys.OrderBy(ext => ext);
    }

    internal static WaveStream CreateReader(Stream input, string fileNameOrExt)
    {
        var ext = NormalizeExt(Path.GetExtension(fileNameOrExt));

        if (_readers.TryGetValue(ext, out var factory))
            return factory(input);

        throw new NotSupportedException($"Format '{ext}' is not supported on this platform.");
    }

    private static string NormalizeExt(string ext)
        => ext.StartsWith('.') ? ext.ToLowerInvariant() : "." + ext.ToLowerInvariant();
}