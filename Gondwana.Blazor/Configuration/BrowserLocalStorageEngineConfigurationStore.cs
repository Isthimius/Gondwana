using Gondwana.Configuration;
using Microsoft.JSInterop;
using Newtonsoft.Json;

namespace Gondwana.Blazor.Configuration;

/// <summary>
/// Persists Gondwana engine configuration in browser <c>localStorage</c>.
/// </summary>
/// <remarks>
/// Blazor WebAssembly exposes an <see cref="IJSInProcessRuntime"/>, allowing the
/// synchronous <see cref="IEngineConfigurationStore"/> contract to participate in
/// the normal engine initialization and shutdown sequence.
/// </remarks>
public sealed class BrowserLocalStorageEngineConfigurationStore : IEngineConfigurationStore
{
    public const string DefaultStorageKey = "gondwana.configuration";

    private readonly IJSInProcessRuntime _jsRuntime;
    private readonly string _storageKey;
    private bool _disposed;

    /// <summary>
    /// Creates a local-storage-backed configuration store and loads its current value.
    /// </summary>
    public BrowserLocalStorageEngineConfigurationStore(
        IJSRuntime jsRuntime,
        string storageKey = DefaultStorageKey,
        bool autoSave = false)
    {
        ArgumentNullException.ThrowIfNull(jsRuntime);
        ArgumentException.ThrowIfNullOrWhiteSpace(storageKey);

        _jsRuntime = jsRuntime as IJSInProcessRuntime
            ?? throw new PlatformNotSupportedException(
                "BrowserLocalStorageEngineConfigurationStore requires an in-process Blazor WebAssembly JS runtime.");
        _storageKey = storageKey;
        AutoSave = autoSave;

        var json = _jsRuntime.Invoke<string?>("localStorage.getItem", _storageKey);
        Configuration = string.IsNullOrWhiteSpace(json)
            ? new EngineConfiguration()
            : JsonConvert.DeserializeObject<EngineConfiguration>(json) ?? new EngineConfiguration();
    }

    /// <inheritdoc />
    public EngineConfiguration Configuration { get; }

    /// <inheritdoc />
    public bool AutoSave { get; set; }

    /// <inheritdoc />
    public void Save()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var json = JsonConvert.SerializeObject(Configuration, Formatting.Indented);
        _jsRuntime.InvokeVoid("localStorage.setItem", _storageKey, json);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
            return;

        if (AutoSave)
            Save();

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
