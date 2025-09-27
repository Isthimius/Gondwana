using System.Reflection;
using MeltySynth;
using Microsoft.Extensions.Logging;
using NAudio.Wave;

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

    internal static void RegisterDefaultReaders()
    {
        PlatformAudioFactory.Register(".mid", stream => CreateReader(stream));
        PlatformAudioFactory.Register(".midi", stream => CreateReader(stream));

        Engine.Logger.LogInformation("RegisterDefaultReaders() called");
    }

    public static WaveStream CreateReader(Stream stream)
    {
        var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        buffer.Position = 0;

        var synth = new Synthesizer(SoundFont, 44100);
        var midi = new MidiFile(buffer);
        var sequencer = new MidiFileSequencer(synth);

        // Start playback once. Do NOT enable internal looping.
        sequencer.Play(midi, loop: false);

        // Provider must NOT loop; SoundResource will handle loop restarts.
        var provider = new SynthesizerSampleProvider(sequencer, synth, midi, loop: false);

        // Duration -> bytes for WaveStream.Length; keep seek wiring.
        return new WaveProviderToWaveStream(provider.ToWaveProvider(), midi.Length.TotalSeconds, provider.Seek);
    }
}