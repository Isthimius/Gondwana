using NAudio.Wave;

namespace Gondwana.Audio.NAudio;

/// <summary>
/// Lightweight variable-rate sample provider. It changes playback speed by advancing through
/// source frames at a configurable rate and linearly interpolating between adjacent frames.
/// Pitch changes naturally with speed; pitch preservation is intentionally not part of the core contract.
/// </summary>
internal sealed class VariableSpeedSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly int _channels;
    private readonly float[] _currentFrame;
    private readonly float[] _nextFrame;
    private readonly object _sync = new();

    private bool _initialized;
    private bool _hasCurrent;
    private bool _hasNext;
    private double _phase;
    private float _playbackSpeed = 1.0f;

    /// <summary>
    /// Initializes a new instance of the <see cref="VariableSpeedSampleProvider"/> class.
    /// </summary>
    /// <param name="source">The underlying <see cref="ISampleProvider"/> to read samples from.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="source"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the source exposes zero audio channels.</exception>
    public VariableSpeedSampleProvider(ISampleProvider source)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _channels = source.WaveFormat.Channels;
        if (_channels <= 0)
            throw new ArgumentException("Source must expose at least one audio channel.", nameof(source));

        _currentFrame = new float[_channels];
        _nextFrame = new float[_channels];
    }

    /// <summary>
    /// Gets the <see cref="WaveFormat"/> of the underlying source provider.
    /// </summary>
    public WaveFormat WaveFormat => _source.WaveFormat;

    /// <summary>
    /// Gets or sets the playback speed multiplier used to advance through source frames.
    /// </summary>
    /// <remarks>
    /// Values are clamped to the supported range defined by <see cref="AudioResource.MinimumPlaybackSpeed"/> and
    /// <see cref="AudioResource.MaximumPlaybackSpeed"/>. Changing the speed affects both playback rate and pitch.
    /// </remarks>
    public float PlaybackSpeed
    {
        get => Volatile.Read(ref _playbackSpeed);
        set => Volatile.Write(
            ref _playbackSpeed,
            Math.Clamp(value, AudioResource.MinimumPlaybackSpeed, AudioResource.MaximumPlaybackSpeed));
    }

    /// <summary>
    /// Reads floating-point samples from the underlying source while performing linear interpolation
    /// between adjacent frames to achieve the configured playback speed.
    /// </summary>
    /// <param name="buffer">Destination buffer to receive samples.</param>
    /// <param name="offset">Offset into <paramref name="buffer"/> where writing begins.</param>
    /// <param name="count">Maximum number of floating-point samples to write into the buffer.</param>
    /// <returns>The number of samples actually written into <paramref name="buffer"/>.</returns>
    public int Read(float[] buffer, int offset, int count)
    {
        if (count <= 0)
            return 0;

        lock (_sync)
        {
            EnsureInitialized();
            if (!_hasCurrent)
                return 0;

            var frameCapacity = count / _channels;
            var framesWritten = 0;

            while (framesWritten < frameCapacity && _hasCurrent)
            {
                var destination = offset + (framesWritten * _channels);
                var phase = (float)_phase;

                for (var channel = 0; channel < _channels; channel++)
                {
                    var next = _hasNext ? _nextFrame[channel] : _currentFrame[channel];
                    buffer[destination + channel] = _currentFrame[channel] + ((next - _currentFrame[channel]) * phase);
                }

                framesWritten++;
                _phase += PlaybackSpeed;

                while (_phase >= 1.0 && _hasCurrent)
                {
                    _phase -= 1.0;
                    AdvanceFrame();
                }
            }

            return framesWritten * _channels;
        }
    }

    /// <summary>
    /// Clears interpolation state after the underlying stream position changes.
    /// </summary>
    /// <param name="reposition">Optional action to reposition the underlying stream before clearing state.</param>
    public void Reset(Action? reposition = null)
    {
        lock (_sync)
        {
            reposition?.Invoke();
            _initialized = false;
            _hasCurrent = false;
            _hasNext = false;
            _phase = 0;
        }
    }

    private void EnsureInitialized()
    {
        if (_initialized)
            return;

        _hasCurrent = ReadFrame(_currentFrame);
        _hasNext = _hasCurrent && ReadFrame(_nextFrame);
        _phase = 0;
        _initialized = true;
    }

    private void AdvanceFrame()
    {
        if (!_hasNext)
        {
            _hasCurrent = false;
            return;
        }

        Array.Copy(_nextFrame, _currentFrame, _channels);
        _hasCurrent = true;
        _hasNext = ReadFrame(_nextFrame);
    }

    private bool ReadFrame(float[] frame)
    {
        var read = 0;
        while (read < _channels)
        {
            var count = _source.Read(frame, read, _channels - read);
            if (count == 0)
                return false;

            read += count;
        }

        return true;
    }
}
