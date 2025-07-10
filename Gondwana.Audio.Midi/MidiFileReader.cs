using System.Reflection;
using MeltySynth;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Gondwana.Audio.Midi;

public static class MidiFileReader
{
    private static readonly Lazy<SoundFont> _soundFont = new(() =>
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("Gondwana.Audio.Midi.TimGM6mb.sf2")
            ?? throw new InvalidOperationException("Embedded SoundFont 'TimGM6mb.sf2' not found.");

        return new SoundFont(stream);
    });

    private static SoundFont SoundFont => _soundFont.Value;

    //static MidiFileReader()
    //{
    //    var assembly = Assembly.GetExecutingAssembly();
    //    using var stream = assembly.GetManifestResourceStream("Gondwana.Audio.Midi.TimGM6mb.sf2")
    //        ?? throw new InvalidOperationException("Embedded SoundFont not found.");

    //    _soundFont = new SoundFont(stream);
    //}

    public static void RegisterDefaultReaders()
    {
        PlatformAudioFactory.Register(".mid", stream => CreateReader(stream));
        PlatformAudioFactory.Register(".midi", stream => CreateReader(stream));
    }

    public static WaveStream CreateReader(Stream stream)
    {
        if (_soundFont == null)
            throw new InvalidOperationException("SoundFont not loaded.");

        stream.Position = 0;

        var synthesizer = new Synthesizer(_soundFont.Value, sampleRate: 44100);
        var sequencer = new MidiFileSequencer(synthesizer);
        sequencer.Play(new MidiFile(stream), loop: false);

        var sampleProvider = new SynthesizerSampleProvider(synthesizer);
        var waveProvider = new SampleToWaveProvider(sampleProvider);

        // Convert IWaveProvider to WaveStream
        return new WaveProviderToWaveStream(waveProvider);
    }
}
