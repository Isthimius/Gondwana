using Gondwana.Audio;
using NAudio.Vorbis;
using NAudio.Wave;

namespace Gondwana.WinForms.Audio;

/// <summary>
/// Registers extended audio format support for Windows using NAudio and third-party extensions.
/// </summary>
internal static class WinFormsAudioSupport
{
    internal static void RegisterExtendedAudioFormats()
    {
        // OGG / OGA / MOGG (Vorbis)
        PlatformAudioFactory.Register(".ogg", stream => new VorbisWaveReader(stream));
        PlatformAudioFactory.Register(".oga", stream => new VorbisWaveReader(stream));
        PlatformAudioFactory.Register(".mogg", stream => new VorbisWaveReader(stream));

        // WMA / M4A (requires file, will be created by SoundResourceManager)
        PlatformAudioFactory.Register(".wma", stream => new MediaFoundationReader(GetFilePathFromStream(stream)), requiresFile: true);
        PlatformAudioFactory.Register(".m4a", stream => new MediaFoundationReader(GetFilePathFromStream(stream)), requiresFile: true);
    }

    /// <summary>
    /// Helper to extract a file path from a stream, used only for requiresFile=true formats.
    /// </summary>
    /// <remarks>
    /// This will throw if the stream is not a FileStream. By design, we only pass FileStreams to these factories.
    /// </remarks>
    private static string GetFilePathFromStream(Stream stream)
    {
        if (stream is FileStream fs)
            return fs.Name;

        throw new InvalidOperationException("Expected a FileStream for media formats that require file access.");
    }
}