using System.Diagnostics;
using Gondwana;
using Gondwana.Rendering;
using SkiaSharp;

// CPU final-presentation microbenchmark. No scene rendering, upload, window compositor,
// screenshots, or GPU measurements are included. No monitor at these sizes is required.
const int frames = 200;
using var source = new SKBitmap(1920, 1080);
using (var canvas = new SKCanvas(source))
using (var paint = new SKPaint())
{
    using var shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(1920, 1080),
        [SKColors.Red, SKColors.Blue], SKShaderTileMode.Clamp);
    paint.Shader = shader;
    canvas.DrawPaint(paint);
}
using var image = SKImage.FromBitmap(source);
foreach (int width in new[] { 1920, 3840 })
foreach (var filter in new[] { RenderScalingFilter.Linear, RenderScalingFilter.NearestNeighbor })
{
    Engine.Instance.Configuration.RenderScalingFilter = filter;
    int height = width * 9 / 16;
    var adapter = new Adapter(width, height);
    using var destination = new SKBitmap(width, height);
    using var canvas = new SKCanvas(destination);
    for (int i = 0; i < 20; i++) adapter.DrawImage(canvas, image, SKColors.Black);
    long bytesBefore = GC.GetAllocatedBytesForCurrentThread();
    var watch = Stopwatch.StartNew();
    for (int i = 0; i < frames; i++) adapter.DrawImage(canvas, image, SKColors.Black);
    watch.Stop();
    long bytes = GC.GetAllocatedBytesForCurrentThread() - bytesBefore;
    Console.WriteLine($"1920x1080 -> {width}x{height}, {filter}: {watch.Elapsed.TotalMilliseconds / frames:F3} ms/frame, {bytes / frames} managed bytes/frame");
}

sealed class Adapter(int width, int height) : RenderSurfaceAdapterBase(width, height)
{
    public override void Present(SKImage image, SKRectI source, SKRect destination) => image.Dispose();
}
