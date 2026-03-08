using Microsoft.Extensions.Logging;
using NAudio.Wave;

namespace Gondwana.Audio;

/// <summary>
/// Provides platform-specific audio format support through a registry of audio reader factories.
/// </summary>
/// <remarks>This static class maintains a registry of supported audio formats and their corresponding
/// reader implementations. Platform-specific implementations can register additional format support
/// by providing reader factories. By default, WAV and MP3 formats are supported.</remarks>
public static class PlatformAudioFactory
{
    private static readonly Dictionary<string, (Func<Stream, WaveStream> readerFactory, bool requiresFile)> _readers = new(StringComparer.OrdinalIgnoreCase);

    static PlatformAudioFactory()
    {
        // Core format support
        Register(".wav", stream => new WaveFileReader(stream));
        Register(".mp3", stream => new Mp3FileReader(stream));
    }

    /// <summary>
    /// Registers a reader factory for a specific audio file format.
    /// </summary>
    /// <remarks>If a reader for the specified extension already exists, it will be replaced with the new factory.
    /// The extension comparison is case-insensitive.</remarks>
    /// <param name="extension">The file extension to register (e.g., ".wav", ".mp3"). Can be with or without the leading dot.</param>
    /// <param name="readerFactory">A factory function that creates a <see cref="WaveStream"/> from an input stream.</param>
    /// <param name="requiresFile">A value indicating whether the reader requires a physical file on disk rather than a stream. Defaults to <see langword="false"/>.</param>
    public static void Register(string extension, Func<Stream, WaveStream> readerFactory, bool requiresFile = false)
    {
        Engine.Logger.LogInformation("Registering audio reader for extension: {Extension}", extension);
        _readers[NormalizeExt(extension)] = (readerFactory, requiresFile);
    }

    /// <summary>
    /// Determines whether the specified audio format is supported.
    /// </summary>
    /// <param name="fileNameOrExt">A file name or file extension to check for support (e.g., "audio.mp3" or ".mp3").</param>
    /// <returns><see langword="true"/> if the format is supported; otherwise, <see langword="false"/>.</returns>
    public static bool Supports(string fileNameOrExt)
    {
        var ext = NormalizeExt(Path.GetExtension(fileNameOrExt));
        return _readers.ContainsKey(ext);
    }

    /// <summary>
    /// Gets a collection of all supported audio file extensions.
    /// </summary>
    /// <returns>An enumerable collection of supported file extensions in alphabetical order, including the leading dot (e.g., ".mp3", ".wav").</returns>
    public static IEnumerable<string> SupportedExtensions()
    {
        return _readers.Keys.OrderBy(ext => ext);
    }

    internal static (Func<Stream, WaveStream> factory, bool requiresFile) GetReaderFactory(string fileNameOrExt)
    {
        var ext = NormalizeExt(Path.GetExtension(fileNameOrExt));

        if (_readers.TryGetValue(ext, out var entry))
        {
            return entry;
        }

        Engine.Logger.LogError("Unsupported audio format: {Extension}", ext);
        throw new NotSupportedException($"Format '{ext}' is not supported on this platform.");
    }

    internal static string NormalizeExt(string ext)
        => ext.StartsWith('.') ? ext.ToLowerInvariant() : "." + ext.ToLowerInvariant();
}