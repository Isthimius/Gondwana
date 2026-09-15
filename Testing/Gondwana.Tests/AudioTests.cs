using Gondwana.Audio;
using Gondwana.Assets;
using Newtonsoft.Json;

namespace Gondwana.Tests;

[Collection("Global engine state")]
public sealed class AudioTests : IDisposable
{
    private readonly AudioResourceManager manager = AudioResourceManager.Instance;
    private readonly Backend backend = new();

    public AudioTests()
    {
        manager.Clear();
        manager.ConfigureBackend(backend);
    }

    public void Dispose() => manager.Clear();

    [Fact]
    public void PortableControlsAndCompletionAreForwarded()
    {
        var sound = manager.LoadFromUri("music", "music.ogg", 2, -2, 8);
        var handle = backend.Handles.Single();
        Assert.Equal(1, handle.Volume);
        Assert.Equal(-1, handle.Pan);
        Assert.Equal(4, handle.PlaybackSpeed);
        sound.PlaybackSpeed = 0;
        Assert.Equal(.25f, handle.PlaybackSpeed);
        Assert.Throws<ArgumentOutOfRangeException>(() => sound.PlaybackSpeed = float.NaN);
        sound.Play();
        Assert.True(sound.IsPlaying);
        sound.Pause();
        Assert.True(sound.IsPaused);
        sound.Resume();
        Assert.True(sound.IsPlaying);
        sound.CurrentTime = TimeSpan.FromSeconds(3);
        Assert.Equal(TimeSpan.FromSeconds(3), sound.CurrentTime);
        Assert.Equal(TimeSpan.FromSeconds(10), sound.Duration);
        var completed = 0;
        var asyncCompleted = 0;
        sound.PlaybackCompleted += (_, _) => completed++;
        sound.PlaybackCompletedAsync = () => { asyncCompleted++; return Task.CompletedTask; };
        handle.Complete();
        Assert.Equal(1, completed);
        Assert.Equal(1, asyncCompleted);
        sound.Stop();
        sound.Dispose();
        handle.Complete();
        Assert.Equal(1, completed);
        Assert.False(manager.Contains("music"));
        Assert.Throws<ObjectDisposedException>(() => sound.Play());
    }

    [Fact]
    public void UriCloneAndJsonRestoreRetainSourceAndSettings()
    {
        var sound = manager.LoadFromUri("music", "assets/music.ogg", .4f, .2f, 1.5f);
        sound.IsLooping = true;
        var clone = manager.Clone("music", "copy")!;
        Assert.Equal(sound.SourceUri, clone.SourceUri);
        Assert.Equal(1.5f, clone.PlaybackSpeed);
        Assert.True(clone.IsLooping);
        clone.Play();
        Assert.False(sound.IsPlaying);
        var json = JsonConvert.SerializeObject(sound);
        manager.Clear();
        var restored = JsonConvert.DeserializeObject<AudioResource>(json)!;
        restored.ReloadIntoManager();
        var loaded = manager.Get("music")!;
        Assert.Equal("assets/music.ogg", loaded.SourceUri);
        Assert.Equal(.4f, loaded.Volume);
        Assert.Equal(.2f, loaded.Pan);
        Assert.Equal(1.5f, loaded.PlaybackSpeed);
        Assert.True(loaded.IsLooping);
    }

    [Fact]
    public void FailedReplacementPreservesResourceAndUnloadNotifiesOnce()
    {
        var sound = manager.LoadFromUri("music", "music.ogg");
        backend.Fail = true;
        Assert.Throws<NotSupportedException>(() => manager.LoadFromUri("music", "bad.ogg"));
        Assert.Same(sound, manager.Get("music"));
        Assert.False(backend.Handles[0].Disposed);
        Assert.Throws<InvalidOperationException>(() => manager.ConfigureBackend(new Backend()));
        var notifications = 0;
        void OnDisposed(object? sender, (string Key, AudioResource Resource) args) => notifications++;
        manager.SoundDisposed += OnDisposed;
        try
        {
            manager.Unload("music");
            sound.Dispose();
            Assert.Equal(1, notifications);
        }
        finally { manager.SoundDisposed -= OnDisposed; }
        manager.ConfigureBackend(new Backend());
    }

    [Fact]
    public void FileCloneRetainsPersistedSource()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".wav");
        try
        {
            File.WriteAllBytes(path, [1, 2, 3]);
            manager.LoadFromFile("file", path);
            var clone = manager.Clone("file", "copy")!;
            Assert.Equal(path, clone.SourceFilePath);
            var json = JsonConvert.SerializeObject(clone);
            manager.Clear();
            JsonConvert.DeserializeObject<AudioResource>(json)!.ReloadIntoManager();
            Assert.Equal(new byte[] { 1, 2, 3 }, manager.Get("copy")!.OriginalBytes);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void AssetCloneAndReloadKeepAssetIdentity()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".assets");
        using var assets = AssetsFile.LoadOrCreate(path);
        assets.Add(AssetTypes.Audio, "sound.wav", new MemoryStream([1, 2, 3]));
        var sound = Assert.Single(manager.LoadFromEngineAssetsFile(assets));
        Assert.Equal("sound.wav", sound.AssetIdentifier!.AssetName);
        var clone = manager.Clone("sound.wav", "copy")!;
        Assert.Same(sound.AssetIdentifier, clone.AssetIdentifier);
        manager.Clear();
        clone.ReloadIntoManager();
        Assert.Same(assets, manager.Get("copy")!.AssetIdentifier!.AssetsFile);
        Assert.Equal(new byte[] { 1, 2, 3 }, manager.Get("copy")!.OriginalBytes);
    }

    private sealed class Backend : IAudioBackend
    {
        public string Name => "Test";
        public bool Fail { get; set; }
        public List<Handle> Handles { get; } = [];
        private Handle Create()
        {
            if (Fail) throw new NotSupportedException();
            var handle = new Handle();
            Handles.Add(handle);
            return handle;
        }
        public IAudioPlaybackHandle CreateFromUri(string key, string uri, float volume, float pan, float playbackSpeed) => Create();
        public IAudioPlaybackHandle CreateFromBytes(string key, byte[] data, string fileNameOrExtension, float volume, float pan, float playbackSpeed) => Create();
    }

    private sealed class Handle : IAudioPlaybackHandle
    {
        public event EventHandler? PlaybackCompleted;
        public AudioPlaybackState State { get; private set; }
        public TimeSpan CurrentTime { get; private set; }
        public TimeSpan Duration => TimeSpan.FromSeconds(10);
        public bool IsLooping { get; set; }
        public float Volume { get; set; }
        public float Pan { get; set; }
        public float PlaybackSpeed { get; set; }
        public string? TemporaryFilePath => null;
        public bool Disposed { get; private set; }
        public void Play(bool fromStart = true) { if (fromStart) CurrentTime = TimeSpan.Zero; State = AudioPlaybackState.Playing; }
        public void Pause() => State = AudioPlaybackState.Paused;
        public void Resume() => State = AudioPlaybackState.Playing;
        public void Stop() => State = AudioPlaybackState.Stopped;
        public void Seek(TimeSpan position) => CurrentTime = position;
        public void Complete() { State = AudioPlaybackState.Stopped; PlaybackCompleted?.Invoke(this, EventArgs.Empty); }
        public void Dispose() => Disposed = true;
    }
}
