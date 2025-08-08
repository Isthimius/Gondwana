using System.Reflection;
using MeltySynth;
using Microsoft.Extensions.Logging;
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

    public static SoundFont SoundFont => _soundFont.Value;

    public static void RegisterDefaultReaders()
    {
        PlatformAudioFactory.Register(".mid", stream => CreateReader(stream));
        PlatformAudioFactory.Register(".midi", stream => CreateReader(stream));

        Engine.Logger.LogInformation("RegisterDefaultReaders() called");
    }

    public static WaveStream CreateReader(Stream stream, bool loop = false)
    {
        var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        buffer.Position = 0;

        var synth = new Synthesizer(_soundFont.Value, 44100);
        var midi = new MidiFile(buffer);
        var sequencer = new MidiFileSequencer(synth);
        sequencer.Play(midi, loop: false); // ok for false; will handle manual loop control

        double durationSeconds = midi.Length.TotalSeconds / 1000.0;
        var provider = new SynthesizerSampleProvider(sequencer, synth, midi, loop);
        Action<TimeSpan> seekHandler = ts => provider.Seek(ts);

        return new WaveProviderToWaveStream(provider.ToWaveProvider(), durationSeconds, seekHandler);
    }
}
