using Gondwana.Audio;
using Gondwana.Audio.NAudio;

namespace Gondwana;

/// <summary>Engine registration helpers for the NAudio backend.</summary>
public static class NAudioEngineExtensions
{
    /// <summary>Configures <see cref="AudioResourceManager"/> to use the NAudio desktop backend.</summary>
    public static Engine UseNAudio(this Engine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        AudioResourceManager.Instance.ConfigureBackend(NAudioAudioBackend.Instance);
        return engine;
    }
}
