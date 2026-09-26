using System.Drawing;
using Gondwana.Drawing.Direct;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Rendering.Views;
using SkiaSharp;

namespace Gondwana.Tests.Drawing.Direct;

public sealed class TextBlockScrollingTests
{
    private const string OverflowText = """
        First line of scrollable text.
        Second line of scrollable text.
        Third line of scrollable text.
        Fourth line of scrollable text.
        Fifth line of scrollable text.
        """;

    [Theory]
    [InlineData(TextBlock.VerticalAlign.Center)]
    [InlineData(TextBlock.VerticalAlign.Bottom)]
    public void OverflowingContent_UsesTopOriginAtZeroScroll(TextBlock.VerticalAlign verticalAlign)
    {
        var (expectedPixels, expectedMaximumScrollOffset) = RenderPixels(
            TextBlock.VerticalAlign.Top,
            scrollOffsetPx: 0f);
        var (actualPixels, actualMaximumScrollOffset) = RenderPixels(
            verticalAlign,
            scrollOffsetPx: 0f);

        Assert.True(expectedMaximumScrollOffset > 0f);
        Assert.Equal(expectedMaximumScrollOffset, actualMaximumScrollOffset);
        Assert.Equal(expectedPixels, actualPixels);
    }

    [Theory]
    [InlineData(TextBlock.VerticalAlign.Center)]
    [InlineData(TextBlock.VerticalAlign.Bottom)]
    public void OverflowingContent_UsesTopOriginAtMaximumScroll(TextBlock.VerticalAlign verticalAlign)
    {
        var (_, maximumScrollOffset) = RenderPixels(
            TextBlock.VerticalAlign.Top,
            scrollOffsetPx: 0f);
        var (expectedPixels, expectedMaximumScrollOffset) = RenderPixels(
            TextBlock.VerticalAlign.Top,
            scrollOffsetPx: maximumScrollOffset);
        var (actualPixels, actualMaximumScrollOffset) = RenderPixels(
            verticalAlign,
            scrollOffsetPx: maximumScrollOffset);

        Assert.True(expectedMaximumScrollOffset > 0f);
        Assert.Equal(expectedMaximumScrollOffset, actualMaximumScrollOffset);
        Assert.Equal(expectedPixels, actualPixels);
    }

    private static (SKColor[] Pixels, float MaximumScrollOffsetPx) RenderPixels(
        TextBlock.VerticalAlign verticalAlign,
        float scrollOffsetPx)
    {
        using var host = new TestRenderSurfaceHost();
        View view = AddView(host, new Rectangle(0, 0, 140, 52));
        using var backbuffer = new BitmapBackbuffer(140, 52);
        using var textBlock = new TextBlock(host, view, new Rectangle(0, 0, 140, 52))
            .SetText(OverflowText)
            .SetFont(SKTypeface.Default, 16f)
            .SetColors(SKColors.White, SKColors.Transparent)
            .SetPadding(4f, 4f)
            .SetAlignment(SKTextAlign.Left, verticalAlign);

        float maximumScrollOffsetPx = textBlock.MeasureMaximumVerticalScrollOffsetPx();
        textBlock.VerticalScrollOffsetPx = Math.Min(scrollOffsetPx, maximumScrollOffsetPx);
        textBlock.Draw(backbuffer, new RectangleF(0, 0, 140, 52));

        using SKImage snapshot = backbuffer.Snapshot();
        using SKBitmap bitmap = SKBitmap.FromImage(snapshot);
        return (bitmap.Pixels.ToArray(), maximumScrollOffsetPx);
    }

    private static View AddView(TestRenderSurfaceHost host, Rectangle bounds)
    {
        host.ViewManager.AddView(bounds, zOrder: 10);
        return host.ViewManager.Views.Single(view => view.ZOrder == 10);
    }
}
