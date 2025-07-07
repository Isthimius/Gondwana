using Gondwana.Extensibility;

namespace Gondwana.Audio.Midi;

public class Startup
{
    [EngineInit(InitTiming.PostInit, 1)]
    public static void Initialize()
    {
        PlatformAudioFactory.Register(".mid", stream => MidiFileReader.CreateReader(stream));
        PlatformAudioFactory.Register(".midi", stream => MidiFileReader.CreateReader(stream));
    }
}
