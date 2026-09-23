using System.Diagnostics;
using System.Runtime.InteropServices;
using Gondwana.Assets;
using Gondwana.Video;

namespace VideoTest;

// Opt-in native integration check; never part of the normal unit-test suite.
internal static class NativeSmoke
{
    internal static int Run(string path)
    {
        using var player = new VlcVideoPlayer(["--no-audio", "--no-video-title-show"]);
        int frames = 0;
        int ends = 0;
        player.Ended += (_, _) => Interlocked.Increment(ref ends);
        player.FrameReady += (_, frame) =>
        {
            // Read while the callback's pointer is valid. No engine or player calls here.
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
