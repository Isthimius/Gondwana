using MeltySynth;
using NAudio.Wave;

namespace Gondwana.Audio.Midi;

public class SynthesizerSampleProvider : ISampleProvider
{
    private readonly Synthesizer synthesizer;

    public SynthesizerSampleProvider(Synthesizer synthesizer)
    {
        this.synthesizer = synthesizer;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(synthesizer.SampleRate, 2);
    }

    public WaveFormat WaveFormat { get; }

    public int Read(float[] buffer, int offset, int count)
    {
        int samples = count / 2; // stereo: two floats per frame

        // Create spans for left and right channels
        var left = new Span<float>(buffer, offset, samples);
        var right = new Span<float>(buffer, offset + samples, samples);

        // Render into left/right
        synthesizer.Render(left, right);

        // Interleave back into buffer
        for (int i = 0; i < samples; i++)
        {
            buffer[offset + (i * 2)] = left[i];
            buffer[offset + (i * 2) + 1] = right[i];
        }

        return count;
    }
}
