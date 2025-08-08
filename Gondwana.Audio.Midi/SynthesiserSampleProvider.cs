using MeltySynth;
using NAudio.Midi;
using NAudio.Wave;

namespace Gondwana.Audio.Midi;

public class SynthesizerSampleProvider : ISampleProvider
{
    private readonly MidiFileSequencer _sequencer;
    private readonly Synthesizer _synthesizer;
    private readonly MeltySynth.MidiFile _midiFile;
    private readonly bool _loop;
    private const float Tolerance = 1e-6f; // Define a small tolerance for floating-point comparison
    private readonly float[] _left = new float[8192];
    private readonly float[] _right = new float[8192];

    public SynthesizerSampleProvider(MidiFileSequencer sequencer, Synthesizer synth, MeltySynth.MidiFile midiFile, bool loop)
    {
        _sequencer = sequencer;
        _synthesizer = synth;
        _midiFile = midiFile;
        _loop = loop;
    }

    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);

    public int Read(float[] buffer, int offset, int count)
    {
        int framesRequested = count / 2;
        int framesRendered = 0;

        while (framesRendered < framesRequested)
        {
            int remaining = framesRequested - framesRendered;
            int renderCount = Math.Min(remaining, _left.Length);

            _synthesizer.Render(_left, _right);

            bool silent = true;
            for (int i = 0; i < renderCount; i++)
            {
                buffer[offset++] = _left[i];
                buffer[offset++] = _right[i];
                if (Math.Abs(_left[i]) > Tolerance || Math.Abs(_right[i]) > Tolerance)
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

    public void Seek(TimeSpan time)
    {
        _synthesizer.Reset();
        _sequencer.Stop();

        _sequencer.Play(_midiFile, loop: false);

        double seconds = time.TotalSeconds;
        int framesToSkip = (int)(seconds * _synthesizer.SampleRate);
        float[] tempLeft = new float[512];
        float[] tempRight = new float[512];

        while (framesToSkip > 0)
        {
            int chunk = Math.Min(framesToSkip, 512);
            _synthesizer.Render(tempLeft, tempRight);
            framesToSkip -= chunk;
        }
    }
}
