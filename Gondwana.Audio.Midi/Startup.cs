namespace Gondwana.Audio.Midi;

public class Startup
{
    public static void Initialize()
    {
        PlatformAudioFactory.Register(".mid", stream => MidiFileReader.CreateReader(stream));
        PlatformAudioFactory.Register(".midi", stream => MidiFileReader.CreateReader(stream));
    }
}
