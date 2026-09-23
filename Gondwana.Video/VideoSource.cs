using Gondwana.Assets;

namespace Gondwana.Video;

/// <summary>A URI, borrowed/owned stream, or GAF asset to open with a video backend.</summary>
public sealed class VideoSource
{
    private readonly Action<IVideoPlayer> _open;
    private VideoSource(Action<IVideoPlayer> open) => _open = open;
    internal void Open(IVideoPlayer player) => _open(player);

    /// <summary>Creates a reusable URI source.</summary>
    public static VideoSource FromUri(Uri source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return new(player => player.Open(source));
    }

    /// <summary>Creates a stream source. Ownership transfers to the player on successful Open
    /// unless leaveOpen is true. Do not reuse an owned source after replacement/disposal.
    /// A borrowed stream must remain open and exclusively available to the player.</summary>
    public static VideoSource FromStream(Stream source, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (!source.CanRead) throw new ArgumentException("Video stream must be readable.", nameof(source));
        return new(player => player.Open(source, leaveOpen));
    }

    /// <summary>Creates a reusable GAF source. The archive must remain open until Open is called.
    /// Each Open obtains a fresh asset stream owned by the player; no temporary file is created.</summary>
    public static VideoSource FromAsset(AssetsFile assets, string name)
    {
        ArgumentNullException.ThrowIfNull(assets);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new(player =>
        {
            var stream = assets.Get(AssetTypes.Video, name)
                ?? throw new FileNotFoundException($"Video asset '{name}' was not found in '{assets.FilePath}'.", name);
            try { player.Open(stream, leaveOpen: false); }
            catch { stream.Dispose(); throw; }
        });
    }
}
