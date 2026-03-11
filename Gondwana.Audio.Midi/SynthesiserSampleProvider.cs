using MeltySynth;
using NAudio.Wave;

namespace Gondwana.Audio.Midi;

/// <summary>
/// Provides real-time audio sample generation from MIDI data using software synthesis.
/// </summary>
/// <remarks>
/// <para>
/// This class bridges the MeltySynth MIDI synthesizer with NAudio's audio streaming infrastructure
/// by implementing <see cref="ISampleProvider"/>. It continuously renders MIDI data to stereo IEEE float
/// audio samples at 44.1 kHz, with support for optional looping and time-based seeking.
/// </para>
/// <para>
/// The provider monitors synthesizer output for silence detection to determine when playback has completed.
/// When looping is enabled, the MIDI sequencer automatically restarts upon detecting silence. Otherwise,
/// playback terminates and the provider returns zero samples.
/// </para>
/// <para>
/// Audio rendering is performed in chunks using internal stereo buffers (8192 frames) to balance
/// performance with memory usage. Silence is detected using a small floating-point tolerance threshold
/// to account for numerical precision.
/// </para>
/// </remarks>
/// <seealso cref="ISampleProvider"/>
/// <seealso cref="MidiFileReader"/>
public class SynthesizerSampleProvider : ISampleProvider
{
    private readonly MidiFileSequencer _sequencer;
    private readonly Synthesizer _synthesizer;
    private readonly MeltySynth.MidiFile _midiFile;
    private readonly bool _loop;
    private const float Tolerance = 1e-6f; // Define a small tolerance for floating-point comparison
    private readonly float[] _left = new float[8192];
    private readonly float[] _right = new float[8192];

    /// <summary>
    /// Initializes a new instance of the <see cref="SynthesizerSampleProvider"/> class with the specified
    /// MIDI sequencer, synthesizer, MIDI file, and looping behavior.
    /// </summary>
    /// <param name="sequencer">
    /// The <see cref="MidiFileSequencer"/> that controls MIDI event playback timing and sequencing.
    /// </param>
    /// <param name="synth">
    /// The <see cref="Synthesizer"/> instance that generates audio samples from MIDI events using a SoundFont.
    /// </param>
    /// <param name="midiFile">
    /// The <see cref="MeltySynth.MidiFile"/> containing the MIDI data to be synthesized.
    /// </param>
    /// <param name="loop">
    /// <c>true</c> to automatically restart playback when the MIDI file completes and silence is detected;
    /// <c>false</c> to stop playback after the MIDI file completes.
    /// </param>
    /// <remarks>
    /// <para>
    /// The sequencer must already be configured with the synthesizer and should have playback initiated
    /// before samples are requested through <see cref="Read"/>. The looping behavior is handled internally
    /// by detecting silence in the synthesizer output and manually restarting the sequencer when needed.
    /// </para>
    /// </remarks>
    public SynthesizerSampleProvider(
        MidiFileSequencer sequencer,
        Synthesizer synth,
        MeltySynth.MidiFile midiFile,
        bool loop)
    {
        _sequencer = sequencer;
        _synthesizer = synth;
        _midiFile = midiFile;
        _loop = loop;
    }

    /// <summary>
    /// Gets the wave format of the synthesized audio output.
    /// </summary>
    /// <value>
    /// A <see cref="NAudio.Wave.WaveFormat"/> configured for stereo (2 channels) IEEE float samples at 44.1 kHz.
    /// </value>
    /// <remarks>
    /// This format is fixed and matches the output format of the MeltySynth synthesizer.
    /// All audio samples provided by <see cref="Read"/> conform to this format.
    /// </remarks>
    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);

    /// <summary>
    /// Reads synthesized audio samples into the provided buffer.
    /// </summary>
    /// <param name="buffer">
    /// The destination buffer to receive interleaved stereo audio samples (left, right, left, right, ...).
    /// </param>
    /// <param name="offset">
    /// The zero-based index in the buffer at which to begin writing samples.
    /// </param>
    /// <param name="count">
    /// The maximum number of samples to read. This value represents individual sample values, not frames.
    /// For stereo audio, each frame consists of 2 samples (one per channel).
    /// </param>
    /// <returns>
    /// The actual number of samples written to the buffer. This will be an even number (2 × frame count)
    /// for stereo output. Returns zero when playback has completed and looping is disabled.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method renders MIDI audio in real-time by requesting synthesis from the underlying
    /// <see cref="Synthesizer"/> and interleaving the stereo output into the destination buffer.
    /// Rendering is performed in chunks using internal buffers to optimize performance.
    /// </para>
    /// <para>
    /// When silence is detected in the synthesized output:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>If looping is enabled (<c>_loop</c> is <c>true</c>), the MIDI sequencer
    ///   is automatically restarted to repeat playback from the beginning.</description></item>
    ///   <item><description>If looping is disabled, the method stops filling the buffer and returns
    ///   the number of samples rendered up to that point, which may be less than <paramref name="count"/>.</description></item>
    /// </list>
    /// <para>
    /// Silence is detected by checking if all synthesized samples in a chunk have absolute values
    /// below the <c>Tolerance</c> threshold (1e-6).
    /// </para>
    /// </remarks>
    public int Read(float[] buffer, int offset, int count)
    {
        int framesRequested = count / 2;
        int framesRendered = 0;

        while (framesRendered < framesRequested)
        {
            int remaining = framesRequested - framesRendered;
            int renderCount = Math.Min(remaining, _left.Length);

            var leftSpan = _left.AsSpan(0, renderCount);
            var rightSpan = _right.AsSpan(0, renderCount);

            // IMPORTANT: render via the sequencer, not directly via the synth.
            _sequencer.Render(leftSpan, rightSpan);

            bool silent = true;

            for (int i = 0; i < renderCount; i++)
            {
                float left = leftSpan[i];
                float right = rightSpan[i];

                buffer[offset++] = left;
                buffer[offset++] = right;

                if (Math.Abs(left) > Tolerance || Math.Abs(right) > Tolerance)
                    silent = false;
            }

            framesRendered += renderCount;

            if (silent)
            {
                if (_loop)
                {
                    _sequencer.Play(_midiFile, loop: false); // manual restart
                }
                else
                {
                    break;
                }
            }
        }

        return framesRendered * 2;
    }

    private readonly float[] _seekLeft = new float[512];
    private readonly float[] _seekRight = new float[512];

    /// <summary>
    /// Seeks to a specific time position in the MIDI playback by resetting the synthesizer and
    /// fast-forwarding through the specified duration.
    /// </summary>
    /// <param name="time">
    /// The target time position to seek to, measured from the beginning of the MIDI file.
    /// </param>
    /// <remarks>
    /// <para>
    /// This method implements seek functionality by:
    /// </para>
    /// <list type="number">
    ///   <item><description>Resetting the synthesizer to clear all note states and audio buffers</description></item>
    ///   <item><description>Stopping the current MIDI sequencer playback</description></item>
    ///   <item><description>Restarting playback from the beginning of the MIDI file</description></item>
    ///   <item><description>Rendering and discarding audio samples until the target time position is reached</description></item>
    /// </list>
    /// <para>
    /// The fast-forward operation renders audio in chunks of 512 frames using dedicated seek buffers
    /// to minimize memory allocation overhead. All rendered samples during seeking are discarded.
    /// </para>
    /// <para>
    /// <b>Performance Note:</b> Seeking is implemented by rendering from the beginning, which can be
    /// computationally expensive for large seek distances. The operation time scales linearly with
    /// the target <paramref name="time"/> value.
    /// </para>
    /// <para>
    /// After seeking completes, subsequent calls to <see cref="Read"/> will provide audio samples
    /// starting from the target time position.
    /// </para>
    /// </remarks>
    public void Seek(TimeSpan time)
    {
        _synthesizer.Reset();
        _sequencer.Stop();
        _sequencer.Play(_midiFile, loop: false);

        int framesToSkip = (int)(time.TotalSeconds * _synthesizer.SampleRate);

        while (framesToSkip > 0)
        {
            int chunk = Math.Min(framesToSkip, _seekLeft.Length);

            // IMPORTANT: seek-forward must also advance through the sequencer.
            _sequencer.Render(_seekLeft.AsSpan(0, chunk), _seekRight.AsSpan(0, chunk));

            framesToSkip -= chunk;
        }
    }
}