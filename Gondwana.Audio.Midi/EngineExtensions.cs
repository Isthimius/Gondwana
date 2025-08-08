using Gondwana.Audio.Midi;

namespace Gondwana;

public static class EngineExtensions
{
    public static void InitializeMidiAudioFormats(this Engine engine)
    {
        MidiFileReader.RegisterDefaultReaders();
    }
}
