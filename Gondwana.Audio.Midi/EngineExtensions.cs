namespace Gondwana.Audio.Midi;

public static class EngineExtensions
{
    public static void InitializeMidiAudioFormats(this Engine engine)
    {
        MidiFileReader.RegisterDefaultReaders();
    }
}