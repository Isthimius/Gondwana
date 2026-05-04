using System.Runtime.Versioning;

namespace Gondwana.Audio.Browser;

/// <summary>
/// Controls a single audio track loaded through <see cref="BrowserAudioManager"/>.
/// </summary>
/// <remarks>
/// <para>
/// Instances are created by <see cref="BrowserAudioManager.Load"/>. Each instance
/// wraps a single HTML <c>&lt;audio&gt;</c> element on the browser side via the
/// <c>gondwana-audio</c> JavaScript module.
/// </para>
/// </remarks>
[SupportedOSPlatform("browser")]
public sealed class BrowserAudioPlayer
{
    private float _volume;
    private bool _isLooping;

    internal BrowserAudioPlayer(string key, float volume, bool loop)
    {
        Key = key;
        _volume = volume;
        _isLooping = loop;
    }

    /// <summary>Gets the unique key that identifies this audio track.</summary>
    public string Key { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the track loops continuously.
    /// </summary>
    public bool IsLooping
    {
        get => _isLooping;
        set
        {
            _isLooping = value;
            BrowserAudioInterop.SetLoop(Key, value);
        }
    }

    /// <summary>
    /// Gets or sets the playback volume in the range [0.0, 1.0].
    /// </summary>
    public float Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0f, 1f);
            BrowserAudioInterop.SetVolume(Key, _volume);
        }
    }

    /// <summary>Starts (or resumes) playback.</summary>
    /// <param name="fromStart">
    /// <see langword="true"/> to seek to the beginning before playing;
    /// <see langword="false"/> to resume from the current position.
    /// </param>
    public void Play(bool fromStart = true)
        => BrowserAudioInterop.Play(Key, fromStart);

    /// <summary>Pauses playback without resetting the position.</summary>
    public void Pause()
        => BrowserAudioInterop.Pause(Key);

    /// <summary>Stops playback and resets the track to the beginning.</summary>
    public void Stop()
        => BrowserAudioInterop.Stop(Key);
}
