using System.Runtime.InteropServices;
using Gondwana.Video;
using SkiaSharp;

namespace Gondwana.Tests.Video;

public sealed class VideoFrameMailboxTests
{
    [Fact]
    public void BgrxPaddedRowsPreserveColorsAndIgnoreUnusedAlpha()
    {
        using var mailbox = new VideoFrameMailbox();
        byte[] pixels = [0, 0, 255, 0, 255, 0, 0, 17, 99, 99, 99, 99,
                         0, 255, 0, 0, 255, 255, 255, 0, 99, 99, 99, 99];
        Publish(mailbox, pixels, 2, 2, 12);
        Array.Clear(pixels); // The decoder is free to reuse its source now.
        SKBitmap? bitmap = null;
        try
        {
            Assert.True(mailbox.Consume(ref bitmap));
            using var target = new SKBitmap(2, 2);
            using var canvas = new SKCanvas(target);
            canvas.Clear(SKColors.Transparent);
            canvas.DrawBitmap(bitmap!, 0, 0);
            Assert.Equal(SKColors.Red, target.GetPixel(0, 0));
            Assert.Equal(SKColors.Blue, target.GetPixel(1, 0));
            Assert.Equal(SKColors.Lime, target.GetPixel(0, 1));
            Assert.Equal(SKColors.White, target.GetPixel(1, 1));
            Assert.False(mailbox.Consume(ref bitmap));
        }
        finally { bitmap?.Dispose(); }
    }

    [Fact]
    public void LatestFrameWinsAndDimensionChangesRecreateBitmap()
    {
        using var mailbox = new VideoFrameMailbox();
        SKBitmap? bitmap = null;
        try
        {
            Publish(mailbox, [0, 0, 255, 0], 1, 1, 4);
            Publish(mailbox, [255, 0, 0, 0, 255, 0, 0, 0], 2, 1, 8);
            mailbox.Consume(ref bitmap);
            Assert.Equal(2, bitmap!.Width);
            var old = bitmap;
            Publish(mailbox, [0, 255, 0, 0], 1, 1, 4);
            mailbox.Consume(ref bitmap);
            Assert.NotSame(old, bitmap);
            Assert.Equal(1, bitmap!.Width);
            var current = bitmap;
            Publish(mailbox, [255, 0, 0, 0], 1, 1, 4);
            mailbox.Consume(ref bitmap);
            Assert.Same(current, bitmap);
            mailbox.Reset();
            Assert.True(mailbox.Consume(ref bitmap));
            Assert.Null(bitmap);
        }
        finally { bitmap?.Dispose(); }
    }

    [Fact]
    public async Task ConcurrentProducerConsumerAndDisposeDoNotTearFrames()
    {
        using var mailbox = new VideoFrameMailbox();
        using var start = new ManualResetEventSlim();
        var producer = Task.Run(() =>
        {
            start.Wait();
            for (int i = 0; i < 2000; i++)
            {
                byte value = (byte)i;
                Publish(mailbox, Enumerable.Repeat(value, 64 * 4).ToArray(), 8, 8, 32);
            }
        });
        SKBitmap? bitmap = null;
        try
        {
            start.Set();
            for (int i = 0; i < 2000; i++)
            {
                if (!mailbox.Consume(ref bitmap)) continue;
                byte[] data = new byte[256];
                Marshal.Copy(bitmap!.GetPixels(), data, 0, data.Length);
                for (int j = 0; j < data.Length; j++)
                    Assert.Equal(j % 4 == 3 ? (byte)255 : data[0], data[j]);
            }
            mailbox.Dispose();
            await producer;
            Assert.False(mailbox.Consume(ref bitmap));
        }
        finally { bitmap?.Dispose(); }
    }

    [Theory]
    [InlineData(640, 480)]
    [InlineData(1920, 1080)]
    [InlineData(17, 3)]
    public void DecodeBufferUsesSourceDimensionsAlignedStorageAndIdempotentCleanup(int width, int height)
    {
        using var buffer = new VideoDecodeBuffer(width, height);
        Assert.Equal(width, buffer.Width);
        Assert.Equal(height, buffer.Height);
        Assert.Equal(0, buffer.Stride % 32);
        Assert.Equal(0, buffer.Lines % 32);
        Assert.Equal(0, buffer.Pixels.ToInt64() % 32);
        Assert.True(buffer.Stride >= width * 4);
        Assert.True(buffer.Lines >= height);
        buffer.Dispose();
        Assert.Equal(IntPtr.Zero, buffer.Pixels);

    }

    [Fact]
    public void ResetDropsPendingFrameAndInvalidStrideIsRejected()
    {
        using var mailbox = new VideoFrameMailbox();
        Publish(mailbox, [0, 0, 255, 0], 1, 1, 4);
        mailbox.Reset();
        SKBitmap? bitmap = null;
        mailbox.Consume(ref bitmap);
        Assert.Null(bitmap);
        Assert.Throws<ArgumentException>(() => Publish(mailbox, [0, 0, 0, 0], 1, 1, 3));
    }

    private static void Publish(VideoFrameMailbox mailbox, byte[] pixels, int width, int height, int stride)
    {
        var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try { mailbox.Publish(new(handle.AddrOfPinnedObject(), width, height, stride, 0)); }
        finally { handle.Free(); }
    }
}
