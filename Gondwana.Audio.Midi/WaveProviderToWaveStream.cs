using NAudio.Wave;

namespace Gondwana.Audio.Midi;

/// <summary>
/// Wraps an <see cref="IWaveProvider"/> as a <see cref="WaveStream"/> with support for length and seeking.
/// </summary>
public class WaveProviderToWaveStream : WaveStream
{
    private readonly IWaveProvider source;
    private readonly WaveFormat waveFormat;
    private long position;
    private readonly long _midiLengthBytes;

    // Delegate for seeking into the underlying synthesizer
    private readonly Action<TimeSpan>? _seekHandler;

    /// <summary>
    /// Initializes a new instance of the <see cref="WaveProviderToWaveStream"/> class.
    /// </summary>
    /// <param name="source">The underlying wave provider to wrap.</param>
    /// <param name="durationSeconds">The duration of the audio stream in seconds.</param>
    /// <param name="seekHandler">Optional callback handler for seeking operations. If null, seeking will not be supported.</param>
    public WaveProviderToWaveStream(IWaveProvider source, double durationSeconds, Action<TimeSpan>? seekHandler)
    {
        this.source = source;
        this.waveFormat = source.WaveFormat;
        _midiLengthBytes = (long)(durationSeconds * waveFormat.AverageBytesPerSecond);
        _seekHandler = seekHandler;
    }

    /// <summary>
    /// Gets the wave format of the stream.
    /// </summary>
    public override WaveFormat WaveFormat => waveFormat;

    /// <summary>
    /// Gets the length of the stream in bytes.
    /// </summary>
    public override long Length => _midiLengthBytes;

    /// <summary>
    /// Gets or sets the current position in the stream in bytes.
    /// </summary>
    /// <exception cref="NotSupportedException">Thrown when attempting to set the position without a seek handler.</exception>
    public override long Position
    {
        get => position;
        set
        {
            if (_seekHandler == null)
                throw new NotSupportedException("Seeking is not supported for this stream.");

            // clamp
            var clamped = Math.Max(0, Math.Min(value, Length));
            // align to frame (BlockAlign is bytes per frame)
            clamped -= clamped % waveFormat.BlockAlign;

            position = clamped;

            var seconds = (double)position / waveFormat.AverageBytesPerSecond;
            _seekHandler(TimeSpan.FromSeconds(seconds));
        }
    }

    /// <summary>
    /// Reads audio data from the underlying wave provider.
    /// </summary>
    /// <param name="buffer">The buffer to read data into.</param>
    /// <param name="offset">The offset in the buffer to start writing data.</param>
    /// <param name="count">The maximum number of bytes to read.</param>
    /// <returns>The number of bytes actually read.</returns>
    public override int Read(byte[] buffer, int offset, int count)
    {
        int read = source.Read(buffer, offset, count);
        position += read;
        return read;
    }
}