using Gondwana.Rendering;
using SkiaSharp;

namespace Gondwana.Tests;

/// <summary>
/// Contains regression tests for render frame mailbox.
/// </summary>
public sealed class RenderFrameMailboxTests
{
    private static RenderFrameSnapshot Frame(long sequence)
    {
        using var recorder = new SKPictureRecorder();
        recorder.BeginRecording(new SKRect(0, 0, 8, 8)).Clear(new SKColor((byte)(sequence % 255), 0, 0));
        return new(recorder.EndRecording(), sequence, sequence, 8, 8);
    }

    private static RenderFrameMailbox.Slot Publish(RenderFrameMailbox mailbox, long sequence)
    {
        var slot = Assert.IsType<RenderFrameMailbox.Slot>(mailbox.TryBeginBuild());
        mailbox.Publish(slot, Frame(sequence));
        return slot;
    }

    /// <summary>
    /// Verifies latest wins and recycles replaced frames.
    /// </summary>
    [Fact]
    public void LatestWins_AndRecyclesReplacedFrames()
    {
        using var mailbox = new RenderFrameMailbox();
        Publish(mailbox, 100);
        Publish(mailbox, 101);
        Publish(mailbox, 102);
        var slot = mailbox.TryAcquire()!;
        Assert.Equal(102, slot.Frame!.Sequence);
        Assert.Equal((3L, 2L, 1), mailbox.Counters);
        mailbox.Release(slot);
        Assert.Equal(0, mailbox.Counters.InUse);
    }

    /// <summary>
    /// Verifies stalled consumer cannot be recycled storage remains bounded.
    /// </summary>
    [Fact]
    public void StalledConsumerCannotBeRecycled_StorageRemainsBounded()
    {
        using var mailbox = new RenderFrameMailbox();
        Publish(mailbox, 100);
        var rendering = mailbox.TryAcquire()!;
        var frame = rendering.Frame;
        var slots = new HashSet<RenderFrameMailbox.Slot> { rendering };
        for (int i = 101; i < 1101; i++)
        {
            slots.Add(Publish(mailbox, i));
            Assert.Same(frame, rendering.Frame);
            Assert.Equal(100, rendering.Frame!.Sequence);
        }
        Assert.Equal(3, slots.Count);
        Assert.Equal(2, mailbox.Counters.InUse);
        mailbox.Release(rendering);
        var latest = mailbox.TryAcquire()!;
        Assert.Equal(1100, latest.Frame!.Sequence);
        mailbox.Release(latest);
        Assert.Equal(0, mailbox.Counters.InUse);
    }

    /// <summary>
    /// Verifies closing retains active consumer and rejects further production.
    /// </summary>
    [Fact]
    public void ClosingRetainsActiveConsumer_AndRejectsFurtherProduction()
    {
        var mailbox = new RenderFrameMailbox();
        Publish(mailbox, 7);
        var slot = mailbox.TryAcquire()!;
        Publish(mailbox, 8);
        var building = mailbox.TryBeginBuild()!;
        mailbox.Dispose();
        mailbox.Publish(building, Frame(9));
        Assert.Null(mailbox.TryBeginBuild());
        Assert.Null(mailbox.TryAcquire());
        using var surface = SKSurface.Create(new SKImageInfo(8, 8));
        slot.Frame!.Replay(surface.Canvas);
        using var image = surface.Snapshot();
        using var pixels = SKBitmap.FromImage(image);
        Assert.Equal((byte)7, pixels.GetPixel(0, 0).Red);
        mailbox.Release(slot);
        Assert.Equal(0, mailbox.Counters.InUse);
    }

    /// <summary>
    /// Verifies concurrent producer consumer observe complete monotonic frames.
    /// </summary>
    /// <returns>A task that represents completion of the operation.</returns>
    [Fact]
    public async Task ConcurrentProducerConsumer_ObserveCompleteMonotonicFrames()
    {
        using var mailbox = new RenderFrameMailbox();
        using var start = new Barrier(2);
        int done = 0;
        const int count = 5000;
        var producer = Task.Run(() =>
        {
            start.SignalAndWait();
            for (int i = 1; i <= count; i++) Publish(mailbox, i);
            Volatile.Write(ref done, 1);
        });
        var consumer = Task.Run(() =>
        {
            using var surface = SKSurface.Create(new SKImageInfo(8, 8));
            long previous = 0;
            start.SignalAndWait();
            while (true)
            {
                var slot = mailbox.TryAcquire();
                if (slot is null)
                {
                    if (Volatile.Read(ref done) == 1)
                    {
                        slot = mailbox.TryAcquire();
                        if (slot is null) break;
                    }
                    else { Thread.Yield(); continue; }
                }
                var frame = slot.Frame!;
                Assert.True(frame.Sequence > previous);
                frame.Replay(surface.Canvas);
                using var image = surface.Snapshot();
                using var pixels = SKBitmap.FromImage(image);
                Assert.Equal((byte)(frame.Sequence % 255), pixels.GetPixel(0, 0).Red);
                Assert.Same(frame, slot.Frame);
                previous = frame.Sequence;
                mailbox.Release(slot);
            }
            Assert.Equal(count, previous);
        });
        await Task.WhenAll(producer, consumer).WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(0, mailbox.Counters.InUse);
    }

    /// <summary>
    /// Verifies recording retains image and copies mutable paint and bitmap.
    /// </summary>
    [Fact]
    public void RecordingRetainsImageAndCopiesMutablePaintAndBitmap()
    {
        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(new SKRect(0, 0, 8, 8));
        using (var bitmap = new SKBitmap(8, 8))
        using (var paint = new SKPaint())
        {
            bitmap.Erase(SKColors.Red);
            canvas.DrawBitmap(bitmap, 0, 0, paint);
            bitmap.Erase(SKColors.Blue);
            paint.Color = SKColors.Green;
        }
        using var frame = new RenderFrameSnapshot(recorder.EndRecording(), 1, 0, 8, 8);
        using var surface = SKSurface.Create(new SKImageInfo(8, 8));
        frame.Replay(surface.Canvas);
        using var image = surface.Snapshot();
        using var pixels = SKBitmap.FromImage(image);
        Assert.Equal(SKColors.Red, pixels.GetPixel(4, 4));
    }
}
