using Gondwana.Assets;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Gondwana.Audio;

/// <summary>
/// Represents an audio resource that can be played, paused, resumed, sought, and disposed.
/// Playback is supplied by the currently configured <see cref="IAudioBackend"/>.
/// </summary>
[JsonObject(IsReference = true)]
public class AudioResource : IDisposable
{
    /// <summary>
    /// Minimum allowed playback speed multiplier. Values below this are clamped.
    /// </summary>
    /// <remarks>1.0 is normal speed; values &lt;1 slow playback, &gt;1 speed up playback.</remarks>
    public const float MinimumPlaybackSpeed = 0.25f;

    /// <summary>
    /// Maximum allowed playback speed multiplier. Values above this are clamped.
    /// </summary>
    public const float MaximumPlaybackSpeed = 4.0f;

    private IAudioPlaybackHandle? _playback;
    private bool _disposed;

    [JsonProperty]
    private float _volume = 1.0f;

    [JsonProperty]
    private float _pan;

    [JsonProperty]
    private float _playbackSpeed = 1.0f;

    [JsonProperty]
    private bool _isLooping;

    /// <summary>
    /// Event raised when non-looping playback reaches the end of the resource.
    /// </summary>
    public event EventHandler? PlaybackCompleted;

    /// <summary>
    /// Optional asynchronous callback invoked when non-looping playback completes.
    /// </summary>
    public Func<Task>? PlaybackCompletedAsync;

    /// <summary>
    /// Event raised when the resource is disposed.
    /// </summary>
    public event EventHandler? Disposed;

    [JsonConstructor]
    private AudioResource()
    {
        Key = string.Empty;
    }

    internal AudioResource(
        string key,
        IAudioPlaybackHandle playback,
        float volume = 1.0f,
        float pan = 0.0f,
        float playbackSpeed = 1.0f,
        string? filePathOrExt = null,
        byte[]? rawBytes = null,
        AssetsFileIdentifier? assetIdentifier = null)
    {
        Key = key;
        _playback = playback ?? throw new ArgumentNullException(nameof(playback));

        AssetIdentifier = assetIdentifier;
        SourceFilePath = assetIdentifier is null && !string.IsNullOrWhiteSpace(filePathOrExt) && File.Exists(filePathOrExt)
            ? filePathOrExt
            : null;

        var ext = Path.GetExtension(filePathOrExt ?? string.Empty);
        SourceExtension = string.IsNullOrEmpty(ext) ? null : NormalizeExtension(ext);
        OriginalBytes = rawBytes;

        Volume = volume;
        Pan = pan;
        PlaybackSpeed = playbackSpeed;
        IsLooping = false;

        _playback.PlaybackCompleted += OnPlaybackCompleted;
    }

    /// <summary>Gets the unique key associated with this audio resource.</summary>
    [JsonProperty]
    public string Key { get; private set; }

    /// <summary>Gets the original byte array of the audio data, if available.</summary>
    [JsonIgnore]
    public byte[]? OriginalBytes { get; private set; }

    /// <summary>
    /// Original file path when the sound was loaded from disk. Null when loaded from an AssetsFile or URI.
    /// </summary>
    [JsonProperty]
    public string? SourceFilePath { get; private set; }

    /// <summary>Asset identifier when the sound was loaded from an AssetsFile.</summary>
    [JsonProperty]
    public AssetsFileIdentifier? AssetIdentifier { get; private set; }

    /// <summary>Normalized file extension used by the configured backend.</summary>
    [JsonProperty]
    public string? SourceExtension { get; private set; }

    /// <summary>Source URI when the resource was loaded by URI, primarily for browser playback.</summary>
    [JsonProperty]
    public string? SourceUri { get; private set; }

    /// <summary>
    /// Gets the temporary file path used by a backend that requires file-based decoding, if any.
    /// </summary>
    [JsonIgnore]
    public string? TempFilePath => _playback?.TemporaryFilePath;

    /// <summary>
    /// Gets a value indicating whether playback is currently paused.
    /// </summary>
    [JsonIgnore]
    public bool IsPaused => State == AudioPlaybackState.Paused;

    /// <summary>
    /// Gets a value indicating whether playback is currently active.
    /// </summary>
    [JsonIgnore]
    public bool IsPlaying => State == AudioPlaybackState.Playing;

    /// <summary>
    /// Gets the current high-level playback state of the resource.
    /// </summary>
    [JsonIgnore]
    public AudioPlaybackState State => _playback?.State ?? AudioPlaybackState.Stopped;

    /// <summary>
    /// Gets or sets the current playback position within the resource.
    /// Setting this property seeks to the specified position.
    /// </summary>
    [JsonIgnore]
    public TimeSpan CurrentTime
    {
        get => _playback?.CurrentTime ?? TimeSpan.Zero;
        set => Seek(value);
    }

    /// <summary>
    /// Gets the total duration of the resource, or <see cref="TimeSpan.Zero"/> when unknown.
    /// </summary>
    [JsonIgnore]
    public TimeSpan Duration => _playback?.Duration ?? TimeSpan.Zero;

    /// <summary>Gets or sets whether playback loops continuously.</summary>
    public bool IsLooping
    {
        get => _isLooping;
        set
        {
            _isLooping = value;
            if (_playback is not null)
                _playback.IsLooping = value;
        }
    }

    /// <summary>Gets or sets output volume in the range 0.0 through 1.0.</summary>
    public float Volume
    {
        get => _volume;
        set
        {
            if (float.IsNaN(value)) throw new ArgumentOutOfRangeException(nameof(value));
            _volume = Math.Clamp(value, 0f, 1f);
            if (_playback is not null)
                _playback.Volume = _volume;
        }
    }

    /// <summary>Gets or sets stereo pan from -1.0 (left) through 1.0 (right).</summary>
    public float Pan
    {
        get => _pan;
        set
        {
            if (float.IsNaN(value)) throw new ArgumentOutOfRangeException(nameof(value));
            _pan = Math.Clamp(value, -1f, 1f);
            if (_playback is not null)
                _playback.Pan = _pan;
        }
    }

    /// <summary>
    /// Gets or sets playback speed. 1.0 is normal speed; supported values are clamped to 0.25 through 4.0.
    /// </summary>
    public float PlaybackSpeed
    {
        get => _playbackSpeed;
        set
        {
            if (float.IsNaN(value)) throw new ArgumentOutOfRangeException(nameof(value));
            _playbackSpeed = Math.Clamp(value, MinimumPlaybackSpeed, MaximumPlaybackSpeed);
            if (_playback is not null)
                _playback.PlaybackSpeed = _playbackSpeed;
        }
    }

    /// <summary>
    /// Starts playback of the resource.
    /// </summary>
    /// <param name="fromStart">If true, playback begins from the start of the resource; otherwise resumes from the current position.</param>
    public void Play(bool fromStart = true) => Playback.Play(fromStart);

    /// <summary>
    /// Pauses playback if currently playing.
    /// </summary>
    public void Pause() => Playback.Pause();

    /// <summary>
    /// Resumes playback if currently paused.
    /// </summary>
    public void Resume() => Playback.Resume();

    /// <summary>
    /// Seeks to the specified playback position.
    /// </summary>
    /// <param name="position">Target playback position. Values outside the valid range are clamped by the backend.</param>
    public void Seek(TimeSpan position) => Playback.Seek(position);

    /// <summary>
    /// Stops playback immediately.
    /// </summary>
    public void Stop() => Playback.Stop();

    internal void SetSourceUri(string uri)
    {
        SourceUri = uri;
        SourceFilePath = null;
        SourceExtension = NormalizeExtension(Path.GetExtension(new Uri(uri, UriKind.RelativeOrAbsolute).IsAbsoluteUri
            ? new Uri(uri).AbsolutePath
            : uri));
    }

    internal void SetAssetIdentifier(AssetsFileIdentifier identifier) => AssetIdentifier = identifier;

    internal void CopySourceFrom(AudioResource original)
    {
        SourceFilePath = original.SourceFilePath;
        AssetIdentifier = original.AssetIdentifier;
    }

    /// <summary>
    /// Ensures this serialized resource is re-created in <see cref="AudioResourceManager"/> from its persisted source.
    /// </summary>
    internal void ReloadIntoManager(bool forceReload = false)
    {
        if (string.IsNullOrWhiteSpace(Key))
            throw new InvalidOperationException("AudioResource has no Key and cannot be reloaded.");

        var manager = AudioResourceManager.Instance;

        if (!forceReload && manager.TryGet(Key, out var existing) && existing is not null)
        {
            existing.Volume = Volume;
            existing.Pan = Pan;
            existing.PlaybackSpeed = PlaybackSpeed;
            existing.IsLooping = IsLooping;
            return;
        }

        if (forceReload && manager.Contains(Key))
            manager.Unload(Key);

        AudioResource loaded;
        if (AssetIdentifier is not null)
        {
            using var stream = AssetIdentifier.Data;
            if (stream is null)
                throw new InvalidOperationException($"Missing asset data for {Key}.");

            loaded = manager.LoadFromStream(Key, stream, SourceExtension ?? ".wav", Volume, Pan, PlaybackSpeed);
            loaded.SetAssetIdentifier(AssetIdentifier);
        }
        else if (!string.IsNullOrWhiteSpace(SourceFilePath))
        {
            loaded = manager.LoadFromFile(Key, SourceFilePath, Volume, Pan, PlaybackSpeed);
        }
        else if (!string.IsNullOrWhiteSpace(SourceUri))
        {
            loaded = manager.LoadFromUri(Key, SourceUri, Volume, Pan, PlaybackSpeed);
        }
        else
        {
            throw new InvalidOperationException($"AudioResource '{Key}' has no persisted source.");
        }

        loaded.IsLooping = IsLooping;
    }

    private IAudioPlaybackHandle Playback
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _playback ?? throw new InvalidOperationException(
                $"AudioResource '{Key}' is not attached to an audio backend. Configure an audio backend before loading or restoring audio.");
        }
    }

    private async void OnPlaybackCompleted(object? sender, EventArgs e)
    {
        try
        {
            if (PlaybackCompletedAsync is not null)
                await PlaybackCompletedAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Engine.Logger.LogError(ex, "PlaybackCompletedAsync threw an exception for audio resource: {Key}", Key);
        }
        finally
        {
            PlaybackCompleted?.Invoke(this, EventArgs.Empty);
        }
    }

    private static string? NormalizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return null;

        return extension.StartsWith('.')
            ? extension.ToLowerInvariant()
            : "." + extension.ToLowerInvariant();
    }

    /// <summary>
    /// Disposes the audio resource and releases any underlying playback handle and temporary files.
    /// After disposal the instance must not be used.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        if (_playback is not null)
        {
            _playback.PlaybackCompleted -= OnPlaybackCompleted;
            _playback.Dispose();
            _playback = null;
        }

        _disposed = true;
        Disposed?.Invoke(this, EventArgs.Empty);
        GC.SuppressFinalize(this);
    }
}
