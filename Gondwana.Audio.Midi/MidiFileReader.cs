using System.Reflection;
using Gondwana.Audio.NAudio;
using MeltySynth;
using Microsoft.Extensions.Logging;
using NAudio.Wave;

namespace Gondwana.Audio.Midi;

/// <summary>
/// Provides factory methods for creating audio streams from MIDI files using software synthesis.
/// </summary>
public static class MidiFileReader
{
    private static readonly Lazy<SoundFont> _soundFont = new(() =>
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("Gondwana.Audio.Midi.TimGM6mb.sf2")
            ?? throw new InvalidOperationException("Embedded SoundFont 'TimGM6mb.sf2' not found.");

        return new SoundFont(stream);
    });

    /// <summary>Gets the General MIDI SoundFont used for synthesizing MIDI audio.</summary>
    public static SoundFont SoundFont => _soundFont.Value;

    /// <summary>
    /// Registers .mid and .midi readers with <see cref="NAudioReaderRegistry"/>.
    /// Call this after configuring the NAudio backend and before loading MIDI resources.
    /// </summary>
    internal static void RegisterDefaultReaders()
    {
        NAudioReaderRegistry.Register(".mid", CreateReader);
        NAudioReaderRegistry.Register(".midi", CreateReader);
        Engine.Logger.LogInformation("Registered Gondwana.Audio.Midi readers with the NAudio backend.");
    }

    /// <summary>
    /// Creates a <see cref="WaveStream"/> that synthesizes and streams audio data from a MIDI file.
    /// </summary>
    public static WaveStream CreateReader(Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        buffer.Position = 0;

        var synth = new Synthesizer(SoundFont, 44100);
        var midi = new MidiFile(buffer);
        var sequencer = new MidiFileSequencer(synth);
        sequencer.Play(midi, loop: false);

        var provider = new SynthesizerSampleProvider(sequencer, synth, midi, loop: false);
        return new WaveProviderToWaveStream(provider.ToWaveProvider(), midi.Length.TotalSeconds, provider.Seek);
    }
}
