using System.Drawing;
using Gondwana.Assets;
using Gondwana.Drawing.Direct;
using Gondwana.Tests.Drawing.Direct;
using Gondwana.Video;

namespace Gondwana.Tests.Video;

public sealed class VideoSourceTests
{
    [Fact]
    public void GafSourceOpensFreshOwnedStreamsThroughDirectVideo()
    {
        string path = Path.Combine(Path.GetTempPath(), $"video-{Guid.NewGuid():N}.gaf");
        try
        {
            using (var assets = AssetsFile.LoadOrCreate(path, null, false, register: false))
            {
                assets.Add(AssetTypes.Video, "intro.mp4", new MemoryStream([1, 2, 3, 4]));
                assets.Save();
            }
            using var loaded = AssetsFile.LoadOrCreate(path, null, false, register: false);
            using var host = new TestRenderSurfaceHost();
            host.ViewManager.AddView(new Rectangle(0, 0, 8, 8), zOrder: 0);
            var player = new FakeVideoPlayer();
            var source = VideoSource.FromAsset(loaded, "intro");
            using var video = new DirectVideo(player, source, host, host.ViewManager.Views.Single(), new Rectangle(0, 0, 8, 8));
            var first = player.Stream!;
            Assert.Equal(1, first.ReadByte());
            video.Open(source);
            Assert.False(first.CanRead);
            Assert.NotSame(first, player.Stream);
            Assert.Equal(1, player.Stream!.ReadByte());
            video.Dispose();
            Assert.False(player.Stream.CanRead);
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void StreamOwnershipIsExplicit(bool leaveOpen)
    {
        using var stream = new MemoryStream([1, 2, 3]);
        using var player = new FakeVideoPlayer();
        VideoSource.FromStream(stream, leaveOpen).Open(player);
        player.Open(new Uri("file:///next.mp4"));
        Assert.Equal(leaveOpen, stream.CanRead);
    }

    [Fact]
    public void MissingAssetFailsClearlyAndFailedConstructionUnsubscribesPlayer()
    {
        string path = Path.Combine(Path.GetTempPath(), $"video-{Guid.NewGuid():N}.gaf");
        try
        {
            using var assets = AssetsFile.LoadOrCreate(path, null, false, register: false);
            using var host = new TestRenderSurfaceHost();
            host.ViewManager.AddView(new Rectangle(0, 0, 8, 8), zOrder: 0);
            var player = new FakeVideoPlayer();
            Assert.Throws<FileNotFoundException>(() => new DirectVideo(player, VideoSource.FromAsset(assets, "missing"),
                host, host.ViewManager.Views.Single(), new Rectangle(0, 0, 8, 8)));
            Assert.Equal(0, player.FrameSubscribers);
            Assert.Equal(1, player.DisposeCount);
        }
        finally { File.Delete(path); }
    }
}
