using Gondwana.Audio;
using Gondwana.Audio.GAUD;
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

    [Fact]
    public void ExistingClrSignaturesRemainCallable()
    {
        Func<string, string, float, float, AudioResource> file = manager.LoadFromFile;
        Func<string, Stream, string, float, float, AudioResource> stream = manager.LoadFromStream;
        Func<AssetsFile, float, float, List<AudioResource>> assets = manager.LoadFromEngineAssetsFile;
        Func<string, string?, float?, float?, AudioResource?> clone = manager.Clone;
        Assert.Equal(4, file.Method.GetParameters().Length);
        Assert.Equal(5, stream.Method.GetParameters().Length);
        Assert.Equal(3, assets.Method.GetParameters().Length);
        Assert.Equal(4, clone.Method.GetParameters().Length);
        using var data = new MemoryStream([1, 2, 3]);
        var sound = stream("legacy", data, ".wav", .5f, -.5f);
        Assert.Equal(1f, sound.PlaybackSpeed);
        sound.PlaybackSpeed = 2f;
        Assert.Equal(2f, clone("legacy", "copy", null, null)!.PlaybackSpeed);
    }

    [Fact]
    public void NonOverwritingEngineStateMergeAppliesPlaybackSpeed()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        var sound = manager.LoadFromUri("music", "music.ogg", playbackSpeed: 2f);
        try
        {
            File.WriteAllText(path, JsonConvert.SerializeObject(new { SoundResources = manager.GetAll() }));
            sound.PlaybackSpeed = .5f;
            EngineState.MergeFromFile(path, overwriteExisting: false, parts: EngineStateParts.Audio);
            Assert.Same(sound, manager.Get("music"));
            Assert.Equal(2f, sound.PlaybackSpeed);
            Assert.Single(backend.Handles);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void InvalidUriAfterBackendCreationReleasesHandleAndPreservesExistingResource()
    {
        var original = manager.LoadFromUri("music", "music.ogg");
        Assert.Throws<UriFormatException>(() => manager.LoadFromUri("music", "http://["));
        Assert.Same(original, manager.Get("music"));
        Assert.False(backend.Handles[0].Disposed);
        Assert.True(backend.Handles[1].Disposed);
        Assert.Equal(0, backend.Handles[1].CompletionSubscribers);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FailedResourceConstructionReleasesHandle(bool uri)
    {
        backend.FailDuringSetup = true;
        using var data = new MemoryStream([1, 2, 3]);
        Assert.Throws<InvalidOperationException>(() =>
        {
            if (uri) manager.LoadFromUri("broken", "music.ogg");
            else manager.LoadFromStream("broken", data, ".wav");
        });
        Assert.True(Assert.Single(backend.Handles).Disposed);
        Assert.Equal(0, backend.Handles[0].CompletionSubscribers);
        Assert.False(manager.Contains("broken"));
    }

    [Fact]
    public void FailedRegistrationReleasesTheNewHandle()
    {
        var original = manager.LoadFromUri("music", "music.ogg");
        original.Disposed += (_, _) => throw new InvalidOperationException("Disposal subscriber failed");
        var error = Assert.Throws<InvalidOperationException>(() => manager.LoadFromUri("music", "replacement.ogg"));
        Assert.Equal("Disposal subscriber failed", error.Message);
        Assert.True(backend.Handles[1].Disposed);
        Assert.Equal(0, backend.Handles[1].CompletionSubscribers);
        Assert.False(manager.Contains("music"));
    }

    [Fact]
    public async Task ConcurrentAssetLoads_SkipExistingResourceWithoutDisposingFirstResult()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".assets");
        using var assets = AssetsFile.LoadOrCreate(path);
        assets.Add(AssetTypes.Audio, "sound.wav", new MemoryStream([1, 2, 3]));
        using var release = new ManualResetEventSlim();
        var creating = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        backend.BeforeCreate = () =>
        {
            creating.TrySetResult();
            Assert.True(release.Wait(TimeSpan.FromSeconds(10)));
        };
        var first = Task.Run(() => manager.LoadFromEngineAssetsFile(assets));
        Task<List<AudioResource>>? second = null;
        try
        {
            await creating.Task.WaitAsync(TimeSpan.FromSeconds(5));
            second = Task.Run(() =>
            {
                secondStarted.SetResult();
                return manager.LoadFromEngineAssetsFile(assets);
            });
            await secondStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Assert.ThrowsAsync<TimeoutException>(() => second.WaitAsync(TimeSpan.FromMilliseconds(100)));
            release.Set();
            var sound = Assert.Single(await first);
            Assert.Empty(await second);
            Assert.Same(sound, manager.Get("sound.wav"));
            Assert.False(Assert.Single(backend.Handles).Disposed);
        }
        finally
        {
            release.Set();
            await first;
            if (second is not null) await second;
            backend.BeforeCreate = null;
        }
    }

    [Fact]
    public void GaudRoundTripAndMaterializationPreserveLooseSourceAndSettings()
    {
        var root = Path.Combine(Path.GetTempPath(), "GondwanaGaud-" + Guid.NewGuid());
        var mediaDirectory = Path.Combine(root, "media");
        var definitionDirectory = Path.Combine(root, "definitions");
        Directory.CreateDirectory(mediaDirectory);
        Directory.CreateDirectory(definitionDirectory);
        var audioPath = Path.Combine(mediaDirectory, "music.wav");
        var gaudPath = Path.Combine(definitionDirectory, "audio.gaud");

        try
        {
            File.WriteAllBytes(audioPath, [1, 2, 3, 4]);
            var sound = manager.LoadFromFile("music", audioPath, .35f, -.2f, 1.5f);
            sound.IsLooping = true;

            AudioDefinitionSerializer.Save(gaudPath, manager);

            var definition = AudioDefinitionSerializer.Load(gaudPath);
            var resource = Assert.Single(definition.Resources);
            Assert.Equal("music", resource.Key);
            Assert.Equal(AudioResourceSourceKind.LooseFile, resource.SourceKind);
            Assert.False(Path.IsPathRooted(resource.FilePath));
            Assert.Equal(.35f, resource.Volume);
            Assert.Equal(-.2f, resource.Pan);
            Assert.Equal(1.5f, resource.PlaybackSpeed);
            Assert.True(resource.IsLooping);

            manager.Clear();
            AudioDefinitionSerializer.LoadIntoManager(definition);

            var restored = manager.Get("music");
            Assert.NotNull(restored);
            Assert.Equal(Path.GetFullPath(audioPath), Path.GetFullPath(restored!.SourceFilePath!));
            Assert.Equal(.35f, restored.Volume);
            Assert.Equal(-.2f, restored.Pan);
            Assert.Equal(1.5f, restored.PlaybackSpeed);
            Assert.True(restored.IsLooping);
        }
        finally
        {
            manager.Clear();
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void GaudRoundTripMaterializesPackedAudioFromLoadedGaf()
    {
        var root = Path.Combine(Path.GetTempPath(), "GondwanaGaud-" + Guid.NewGuid());
        var definitionDirectory = Path.Combine(root, "definitions");
        Directory.CreateDirectory(definitionDirectory);
        var gafPath = Path.Combine(root, "sounds.gaf");
        var gaudPath = Path.Combine(definitionDirectory, "audio.gaud");

        try
        {
            using var assets = AssetsFile.LoadOrCreate(gafPath);
            assets.Add(AssetTypes.Audio, "tone.wav", new MemoryStream([4, 3, 2, 1]));
            var sound = Assert.Single(
                manager.LoadFromEngineAssetsFile(
                    assets,
                    defaultVolume: .6f,
                    defaultPan: .25f,
                    defaultPlaybackSpeed: 1.25f));
            sound.IsLooping = true;

            AudioDefinitionSerializer.Save(gaudPath, manager);
            var definition = AudioDefinitionSerializer.Load(gaudPath);
            var resource = Assert.Single(definition.Resources);
            Assert.Equal(AudioResourceSourceKind.PackedAsset, resource.SourceKind);
            Assert.False(Path.IsPathRooted(resource.AssetsFilePath));
            Assert.Equal("tone.wav", resource.AssetEntryName);

            manager.Clear();
            AudioDefinitionSerializer.LoadIntoManager(definition);

            var restored = manager.Get("tone.wav");
            Assert.NotNull(restored);
            Assert.Same(assets, restored!.AssetIdentifier!.AssetsFile);
            Assert.Equal(.6f, restored.Volume);
            Assert.Equal(.25f, restored.Pan);
            Assert.Equal(1.25f, restored.PlaybackSpeed);
            Assert.True(restored.IsLooping);
        }
        finally
        {
            manager.Clear();
            AssetsFile.ClearAll();
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EngineStateAudioRoundTripsThroughInlineOrExternalGaud(bool separateGaudFile)
    {
        var root = Path.Combine(Path.GetTempPath(), "GondwanaGaud-" + Guid.NewGuid());
        var mediaDirectory = Path.Combine(root, "media");
        Directory.CreateDirectory(mediaDirectory);
        var audioPath = Path.Combine(mediaDirectory, "music.wav");
        var statePath = Path.Combine(root, "state.json");

        try
        {
            File.WriteAllBytes(audioPath, [9, 8, 7]);
            var sound = manager.LoadFromFile("music", audioPath, .4f, .15f, 1.75f);
            sound.IsLooping = true;

            new EngineState().SaveToFile(
                statePath,
                parts: EngineStateParts.Audio,
                separateGaudFile: separateGaudFile);

            var stateJson = File.ReadAllText(statePath);
            Assert.Contains("\"Audio\"", stateJson);

            if (separateGaudFile)
                Assert.True(File.Exists(Path.Combine(root, "state.audio", "audio.gaud")));

            manager.Clear();
            EngineState.LoadFromFile(statePath, parts: EngineStateParts.Audio);

            var restored = manager.Get("music");
            Assert.NotNull(restored);
            Assert.Equal(.4f, restored!.Volume);
            Assert.Equal(.15f, restored.Pan);
            Assert.Equal(1.75f, restored.PlaybackSpeed);
            Assert.True(restored.IsLooping);
            Assert.Equal(Path.GetFullPath(audioPath), Path.GetFullPath(restored.SourceFilePath!));
        }
        finally
        {
            manager.Clear();
            AssetsFile.ClearAll();
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void GaudValidationRejectsDuplicateKeysAndInvalidSource()
    {
        var definition = new AudioDefinition
        {
            Resources =
            [
                new AudioResourceDefinition
                {
                    Key = "music",
                    SourceKind = AudioResourceSourceKind.LooseFile,
                    FilePath = "music.ogg"
                },
                new AudioResourceDefinition
                {
                    Key = "music",
                    SourceKind = AudioResourceSourceKind.Uri,
                    SourceUri = ""
                }
            ]
        };

        var errors = AudioDefinitionValidator.Validate(definition);
        Assert.Contains(errors, error => error.Contains("duplicate key", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, error => error.Contains("SourceUri is empty", StringComparison.Ordinal));
    }

    private sealed class Backend : IAudioBackend
    {
        public string Name => "Test";
        public bool Fail { get; set; }
        public bool FailDuringSetup { get; set; }
        public Action? BeforeCreate { get; set; }
        public List<Handle> Handles { get; } = [];
        private Handle Create()
        {
            BeforeCreate?.Invoke();
            if (Fail) throw new NotSupportedException();
            var handle = new Handle { FailDuringSetup = FailDuringSetup };
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
        public bool FailDuringSetup { get; set; }
        private float volume;
        public float Volume
        {
            get => volume;
            set
            {
                if (FailDuringSetup) throw new InvalidOperationException("Setup failed");
                volume = value;
            }
        }
        public int CompletionSubscribers => PlaybackCompleted?.GetInvocationList().Length ?? 0;
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
