using NAudio.Vorbis;
using NAudio.Wave;

namespace Gondwana.Audio.WinForms;

/// <summary>
/// Registers extended audio format support for Windows using NAudio and third-party extensions.
/// </summary>
public static class WinFormsAudioSupport
{
    public static void RegisterExtendedAudioFormats()
    {
        // OGG / OGA / MOGG (Vorbis)
        PlatformAudioFactory.Register(".ogg", stream => new VorbisWaveReader(stream));
        PlatformAudioFactory.Register(".oga", stream => new VorbisWaveReader(stream));
        PlatformAudioFactory.Register(".mogg", stream => new VorbisWaveReader(stream));

        // WMA (requires file, uses Media Foundation)
        PlatformAudioFactory.Register(".wma", stream =>
        {
            string temp = SaveStreamToTempFile(stream, ".wma");
            return new MediaFoundationReader(temp);
        }, requiresFile: true);

        // M4A (requires file, uses Media Foundation)
        PlatformAudioFactory.Register(".m4a", stream =>
        {
            string temp = SaveStreamToTempFile(stream, ".m4a");
            return new MediaFoundationReader(temp);
        }, requiresFile: true);
    }

    private static string SaveStreamToTempFile(Stream stream, string ext)
    {
        string tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ext);
        stream.Position = 0;
        using var fs = File.Create(tempPath);
        stream.CopyTo(fs);
        return tempPath;
    }
}
