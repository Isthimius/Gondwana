namespace Gondwana.Audio.Midi;

/// <summary>
/// Provides extension methods for the <see cref="Engine"/> class to support MIDI audio functionality.
/// </summary>
public static class EngineExtensions
{
    /// <summary>
    /// Initializes MIDI audio format support by registering default MIDI file readers (.mid and .midi extensions)
    /// with the platform audio factory.
    /// </summary>
    /// <param name="engine">The <see cref="Engine"/> instance to initialize MIDI audio formats for.</param>
    /// <remarks>
    /// <para>
    /// This method registers factory functions that can create audio streams from MIDI files.
    /// The registration enables the engine's audio subsystem to recognize and process .mid and .midi
    /// file formats using the built-in MIDI synthesis capabilities.
    /// </para>
    /// <para>
    /// This method should typically be called during engine initialization, after <see cref="Engine.Initialize"/>
    /// but before loading any MIDI audio resources.
    /// </para>
    /// </remarks>
    /// <seealso cref="MidiFileReader.RegisterDefaultReaders"/>
    public static void InitializeMidiAudioFormats(this Engine engine)
    {
        MidiFileReader.RegisterDefaultReaders();
    }
}