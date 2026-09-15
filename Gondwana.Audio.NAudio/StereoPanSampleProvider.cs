using NAudio.Wave;

namespace Gondwana.Audio.NAudio;

/// <summary>Applies independent left/right gain to a stereo floating-point source.</summary>
internal sealed class StereoPanSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;

    /// <summary>
    /// Initializes a new instance of the <see cref="StereoPanSampleProvider"/> class.
    /// </summary>
    /// <param name="source">The underlying stereo floating-point sample provider.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="source"/> does not expose exactly two channels.
    /// </exception>
    public StereoPanSampleProvider(ISampleProvider source)
    {
        if (source.WaveFormat.Channels != 2)
            throw new ArgumentException("StereoPanSampleProvider requires a stereo source.", nameof(source));

        _source = source;
    }

    /// <summary>
    /// Gets the wave format of the underlying source.
    /// </summary>
    public WaveFormat WaveFormat => _source.WaveFormat;

    /// <summary>
    /// Gets or sets the gain multiplier applied to left-channel samples.
    /// </summary>
    public float LeftVolume { get; set; } = 1.0f;

    /// <summary>
    /// Gets or sets the gain multiplier applied to right-channel samples.
    /// </summary>
    public float RightVolume { get; set; } = 1.0f;

    /// <summary>
    /// Reads samples from the source and applies independent left and right gain.
    /// </summary>
    /// <param name="buffer">Destination buffer for the samples read.</param>
    /// <param name="offset">Offset into <paramref name="buffer"/> where writing begins.</param>
    /// <param name="count">Maximum number of samples to read.</param>
    /// <returns>The number of samples read into <paramref name="buffer"/>.</returns>
    public int Read(float[] buffer, int offset, int count)
    {
        var read = _source.Read(buffer, offset, count);
        for (var index = 0; index + 1 < read; index += 2)
        {
            buffer[offset + index] *= LeftVolume;
            buffer[offset + index + 1] *= RightVolume;
        }

        return read;
    }
}
