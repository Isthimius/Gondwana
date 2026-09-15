using Gondwana.Audio;
using Gondwana.Audio.NAudio;

namespace Gondwana;

/// <summary>
/// Engine registration helpers for the NAudio backend.
/// </summary>
public static class NAudioEngineExtensions
{
    /// <summary>
    /// Configures the engine's <see cref="AudioResourceManager"/> to use the NAudio desktop backend.
    /// </summary>
    /// <param name="engine">The engine instance to configure.</param>
    /// <returns>The same <see cref="Engine"/> instance for call chaining.</returns>
    public static Engine UseNAudio(this Engine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        AudioResourceManager.Instance.ConfigureBackend(NAudioAudioBackend.Instance);
        return engine;
    }
}
