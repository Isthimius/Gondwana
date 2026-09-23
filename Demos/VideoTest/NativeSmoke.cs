using System.Diagnostics;
using System.Runtime.InteropServices;
using Gondwana.Assets;
using Gondwana.Video;
using SkiaSharp;

namespace VideoTest;

// Opt-in native integration check; never part of the normal unit-test suite.
internal static class NativeSmoke
{
    internal static int Run(string path)
    {
        using var player = new VlcVideoPlayer(["--no-audio", "--no-video-title-show"]);
        int frames = 0;
        int ends = 0;
        int stopAtEnd = 0;
        int checkColors = 0;
        string? colorError = null;
        player.Ended += (_, _) =>
        {
            if (Interlocked.Exchange(ref stopAtEnd, 0) != 0) player.Stop();
            Interlocked.Increment(ref ends);
        };
        player.FrameReady += (_, frame) =>
        {
            // Read while the callback's pointer is valid. No engine or player calls here.
            if (Volatile.Read(ref checkColors) != 0)
            {
                int red = Marshal.ReadByte(frame.Pixels, 2);
                int blue = Marshal.ReadByte(frame.Pixels, (frame.Width - 1) * 4);
                if (red != 255 || blue != 255)
                    Volatile.Write(ref colorError, $"Native BGRX mismatch: red={red:X6}, blue={blue:X6}");
            }
            if (Interlocked.Increment(ref frames) == 1)
                Console.WriteLine($"First frame: {frame.Width}x{frame.Height}, BGRX={Marshal.ReadInt32(frame.Pixels):X8}");
        };
        var uri = new Uri(Path.GetFullPath(path));
        player.Open(uri);
        player.Play();
        Wait(() => Volatile.Read(ref frames) > 2 && player.IsMetadataReady, "URI decode and metadata");
        Console.WriteLine($"NaturalSize={player.NaturalSize}, audio={player.HasAudio}, duration={player.Duration}");
        player.Pause();
        player.Seek(TimeSpan.FromMilliseconds(200));
        player.SetRate(0.5);
        int before = Volatile.Read(ref frames);
        player.Play();
        Wait(() => Volatile.Read(ref frames) > before + 2, "resume/seek/rate");
        player.Stop();
        using var stream = File.OpenRead(path);
        player.Open(stream, leaveOpen: true);
        player.Loop = true;
        player.SetRate(2);
        before = Volatile.Read(ref frames);
        player.Play();
        Wait(() => Volatile.Read(ref frames) > before + 2 && player.IsMetadataReady, "stream decode and metadata");
        Console.WriteLine($"Stream NaturalSize={player.NaturalSize}, audio={player.HasAudio}");
        if (player.Duration > TimeSpan.FromSeconds(1))
        {
            int priorEnds = Volatile.Read(ref ends);
            player.Seek(player.Duration - TimeSpan.FromMilliseconds(400));
            Wait(() => Volatile.Read(ref ends) > priorEnds, "loop end");
            before = Volatile.Read(ref frames);
            Wait(() => Volatile.Read(ref frames) > before + 2 && player.Position < TimeSpan.FromSeconds(2), "loop restart frames");
            priorEnds = Volatile.Read(ref ends);
            Volatile.Write(ref stopAtEnd, 1);
            player.Seek(player.Duration - TimeSpan.FromMilliseconds(400));
            Wait(() => Volatile.Read(ref ends) > priorEnds, "stop from Ended handler");
            if (player.IsPlaying) throw new Exception("Loop restarted despite an explicit Stop in Ended.");
        }
        player.Stop();
        string gafPath = Path.Combine(Path.GetTempPath(), $"gondwana-video-smoke-{Guid.NewGuid():N}.gaf");
        try
        {
            using (var assets = AssetsFile.LoadOrCreate(gafPath, null, false, register: false))
            {
                assets.Add(AssetTypes.Video, path, "clip" + Path.GetExtension(path));
                assets.Save();
            }
            using var archive = AssetsFile.LoadOrCreate(gafPath, null, false, register: false);
            using var assetStream = archive.Get(AssetTypes.Video, "clip")!;
            player.Open(assetStream, leaveOpen: false);
            before = Volatile.Read(ref frames);
            player.Play();
            Wait(() => Volatile.Read(ref frames) > before + 2 && player.IsMetadataReady, "GAF decode");
            player.Open(uri);
            if (assetStream.CanRead) throw new Exception("Owned GAF stream was not closed on replacement.");
        }
        finally { File.Delete(gafPath); }
        for (int i = 0; i < 10; i++) player.Open(uri); // Cancel/replace pending parse repeatedly.
        before = Volatile.Read(ref frames);
        player.Play();
        Wait(() => Volatile.Read(ref frames) > before + 2 && player.IsMetadataReady, "repeated open");
        player.Stop();
        player.Loop = false;
        // Generate lossless red/blue fixtures locally: exercise real native channel order
        // and two source sizes without committing any media or needing FFmpeg.
        foreach (var size in new[] { (640, 480), (1920, 1080) })
        {
            string imagePath = Path.Combine(Path.GetTempPath(), $"video-colors-{Guid.NewGuid():N}.png");
            try
            {
                using (var bitmap = new SKBitmap(size.Item1, size.Item2))
                using (var canvas = new SKCanvas(bitmap))
                using (var paint = new SKPaint { Color = SKColors.Blue })
                {
                    canvas.Clear(SKColors.Red);
                    canvas.DrawRect(size.Item1 / 2, 0, size.Item1 / 2, size.Item2, paint);
                    using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
                    using var output = File.Create(imagePath);
                    data.SaveTo(output);
                }
                player.Open(new Uri(imagePath));
                Volatile.Write(ref checkColors, 1);
                before = Volatile.Read(ref frames);
                player.Play();
                Wait(() => Volatile.Read(ref frames) > before, "native color fixture");
                player.Stop();
                if (player.NaturalSize != size) throw new Exception($"Wrong source dimensions: {player.NaturalSize}, expected {size}");
                if (colorError is not null) throw new Exception(colorError);
                Volatile.Write(ref checkColors, 0);
                Console.WriteLine($"Verified native red/blue pixels and {size.Item1}x{size.Item2} dimensions.");
            }
            finally { File.Delete(imagePath); }
        }
        player.Dispose();
        player.Dispose();
        if (!stream.CanRead) throw new Exception("Borrowed stream was closed.");
        if (player.LastError is { } error) throw error;
        Console.WriteLine($"PASS: {frames} frames, URI/stream/GAF, metadata, controls, cleanup. Use the GPU window to check presentation and audio.");
        return 0;
    }
    private static void Wait(Func<bool> condition, string operation)
    {
        var timer = Stopwatch.StartNew();
        while (!condition())
        {
            if (timer.Elapsed > TimeSpan.FromSeconds(15)) throw new TimeoutException(operation);
            Thread.Sleep(20);
        }
    }
}
