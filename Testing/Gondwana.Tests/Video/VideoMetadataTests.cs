using Gondwana.Video;

namespace Gondwana.Tests.Video;

public sealed class VideoMetadataTests
{
    [Fact]
    public void ReopenInvalidatesPriorCompletionAndResetsAuthoritativeValues()
    {
        var state = new VideoMetadataState();
        Assert.Equal(VideoMetadataStatus.Unavailable, state.Current.Status);
        long first = state.Begin();
        Assert.Equal(VideoMetadataStatus.Pending, state.Current.Status);
        var ready = new VideoMetadata(VideoMetadataStatus.Ready, true, TimeSpan.FromSeconds(42));
        Assert.True(state.Complete(first, ready));
        Assert.Equal(ready, state.Current);
        long second = state.Begin();
        Assert.False(state.Current.HasAudio);
        Assert.Equal(TimeSpan.Zero, state.Current.Duration);
        Assert.False(state.Complete(first, ready));
        Assert.Equal(VideoMetadataStatus.Pending, state.Current.Status);
        Assert.True(state.Complete(second, new(VideoMetadataStatus.Failed, false, TimeSpan.Zero)));
        state.Reset();
        Assert.False(state.Complete(second, ready));
        Assert.Equal(VideoMetadataStatus.Unavailable, state.Current.Status);
    }
}
