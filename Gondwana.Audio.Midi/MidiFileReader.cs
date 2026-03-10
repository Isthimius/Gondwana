using System.Reflection;
using MeltySynth;
using Microsoft.Extensions.Logging;
using NAudio.Wave;

namespace Gondwana.Audio.Midi;

/// <summary>
/// Provides factory methods for creating audio streams from MIDI files using software synthesis.
/// </summary>
/// <remarks>
/// <para>
/// This class manages MIDI file playback by combining the MeltySynth MIDI synthesizer with NAudio's
/// audio streaming infrastructure. It loads a General MIDI SoundFont from embedded resources and
/// registers factory methods to handle .mid and .midi file extensions.
/// </para>
/// <para>
/// The synthesizer uses the embedded TimGM6mb.sf2 SoundFont (approximately 6MB) for instrument samples,
/// which is loaded lazily on first access and cached for the application lifetime.
/// </para>
/// </remarks>
public static class MidiFileReader
{
    private static readonly Lazy<SoundFont> _soundFont = new(() =>
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("Gondwana.Audio.Midi.TimGM6mb.sf2")
            ?? throw new InvalidOperationException("Embedded SoundFont 'TimGM6mb.sf2' not found.");

        return new SoundFont(stream);
    });

    /// <summary>
    /// Gets the General MIDI SoundFont used for synthesizing MIDI audio.
    /// </summary>
    /// <value>
    /// A <see cref="MeltySynth.SoundFont"/> instance loaded from the embedded TimGM6mb.sf2 resource.
    /// </value>
    /// <remarks>
    /// <para>
    /// The SoundFont is loaded lazily on first access from an embedded resource and cached for the
    /// application's lifetime. This SoundFont provides instrument samples for General MIDI playback.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the embedded SoundFont resource 'Gondwana.Audio.Midi.TimGM6mb.sf2' cannot be found.
    /// </exception>
    public static SoundFont SoundFont => _soundFont.Value;

    /// <summary>
    /// Registers factory functions for .mid and .midi file extensions with the <see cref="PlatformAudioFactory"/>
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method should be called during engine initialization to enable MIDI file support.
    /// It registers the <see cref="CreateReader"/> method as the factory function for both
    /// .mid and .midi file extensions.
    /// </para>
    /// <para>
    /// After registration, the audio subsystem can automatically create appropriate readers
    /// when MIDI files are loaded through the standard audio resource pipeline.
    /// </para>
    /// </remarks>
    /// <seealso cref="CreateReader"/>
    /// <seealso cref="PlatformAudioFactory.Register"/>
    internal static void RegisterDefaultReaders()
    {
        PlatformAudioFactory.Register(".mid", stream => CreateReader(stream));
        PlatformAudioFactory.Register(".midi", stream => CreateReader(stream));

        Engine.Logger.LogInformation("RegisterDefaultReaders() called");
    }

    /// <summary>
    /// Creates a <see cref="WaveStream"/> that synthesizes and streams audio data from a MIDI file.
    /// </summary>
    /// <param name="stream">The input stream containing MIDI file data to be synthesized.</param>
    /// <returns>
    /// A <see cref="WaveStream"/> that provides synthesized audio output at 44.1kHz stereo,
    /// with support for seeking to specific time positions.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method creates a complete MIDI playback pipeline:
    /// </para>
    /// <list type="number">
    ///   <item><description>Loads the MIDI file data from the input stream into a memory buffer</description></item>
    ///   <item><description>Creates a <see cref="Synthesizer"/> instance using the embedded <see cref="SoundFont"/></description></item>
    ///   <item><description>Initializes a <see cref="MidiFileSequencer"/> to control playback timing</description></item>
    ///   <item><description>Wraps the synthesizer output in a <see cref="SynthesizerSampleProvider"/></description></item>
    ///   <item><description>Returns a <see cref="WaveProviderToWaveStream"/> with seek support</description></item>
    /// </list>
    /// <para>
    /// The returned stream does not loop internally; looping should be handled by the calling code
    /// (typically through <see cref="Gondwana.Audio.SoundResource"/>). The stream supports seeking
    /// to arbitrary time positions for interactive playback control.
    /// </para>
    /// <para>
    /// Audio output specifications:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Sample Rate: 44.1 kHz</description></item>
    ///   <item><description>Channels: Stereo (2 channels)</description></item>
    ///   <item><description>Format: IEEE Float (32-bit per sample)</description></item>
    /// </list>
    /// </remarks>
    /// <seealso cref="SoundFont"/>
    /// <seealso cref="SynthesizerSampleProvider"/>
    /// <seealso cref="WaveProviderToWaveStream"/>
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