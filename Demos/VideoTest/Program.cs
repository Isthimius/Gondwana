using Gondwana.Assets;
using Gondwana.Drawing.Direct;
using Gondwana.Scenes;
using Gondwana.Video;
using Gondwana.WinForms.Hosting;
using Gondwana.WinForms.Rendering;

namespace VideoTest;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("VideoTest <media path> [--headless | --stream | --gaf <asset name>]");
            return 1;
        }
        try
        {
            if (args.Contains("--headless")) return NativeSmoke.Run(args[0]);
            ApplicationConfiguration.Initialize();
            using var assets = args.Contains("--gaf")
                ? AssetsFile.LoadOrCreate(Path.GetFullPath(args[0]), null, false, register: false) : null;
            VideoSource source = assets is not null
                ? VideoSource.FromAsset(assets, args[Array.IndexOf(args, "--gaf") + 1])
                : args.Contains("--stream")
                    ? VideoSource.FromStream(File.OpenRead(args[0]))
                    : VideoSource.FromUri(new Uri(Path.GetFullPath(args[0])));
            Application.Run(new VideoWindow(source));
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}

// Same GPU hosting and post-Shown initialization pattern as the New Project template.
internal sealed class VideoWindow : Form
{
    private readonly WinFormGpuRenderSurfaceControl _surface = new() { Dock = DockStyle.Fill };
    private readonly System.Windows.Forms.Timer _statusTimer = new() { Interval = 250 };
    private readonly VideoSource _source;
    private VideoHost? _host;
    internal VideoWindow(VideoSource source)
    {
        _source = source;
        ClientSize = new Size(1000, 650);
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 70, AutoSize = false };
        void Add(string title, Action action)
        {
            var button = new Button { Text = title, AutoSize = true };
            button.Click += (_, _) => action();
            bar.Controls.Add(button);
        }
        Add("Play", () => _host?.Video?.Play());
        Add("Pause", () => _host?.Video?.Pause());
        Add("Seek +5s", () => _host?.Video?.Seek(_host.Player!.Position + TimeSpan.FromSeconds(5)));
        Add("Loop on/off", () => { if (_host?.Video is { } video) video.Loop = !video.Loop; });
        Add("0.5x", () => { if (_host?.Video is { } video) video.PlaybackRate = 0.5; });
        Add("1x", () => { if (_host?.Video is { } video) video.PlaybackRate = 1; });
        Add("2x", () => { if (_host?.Video is { } video) video.PlaybackRate = 2; });
        Add("Stop", () => _host?.Video?.Stop());
        Add("Fade out", () => _host?.Video?.FadeOut(1));
        Add("Fade in", () => _host?.Video?.FadeIn(1));
        Add("Dispose", () => _host?.DisposeVideo());
        Controls.Add(_surface);
        Controls.Add(bar);
        _statusTimer.Tick += (_, _) =>
        {
            var player = _host?.Player;
            Text = player is null ? "VideoTest — disposed" :
                $"VideoTest — {player.NaturalSize} {player.Metadata.Status} audio={player.HasAudio} " +
                $"{player.Position:mm\\:ss}/{player.Duration:mm\\:ss} loop={player.Loop} error={player.LastError?.Message}";
        };
    }
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _host = new VideoHost(_surface, _source);
        _host.Initialize();
        _statusTimer.Start();
    }
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _statusTimer.Dispose();
        _host?.Dispose();
        base.OnFormClosed(e);
    }
}

internal sealed class VideoHost(WinFormGpuRenderSurfaceControl surface, VideoSource source) : WinFormsGpuGameHost(surface)
{
    internal VlcVideoPlayer? Player { get; private set; }
    internal DirectVideo? Video { get; private set; }
    protected override Scene CreateInitialScene() => Scene.Empty;
    protected override void CreateInitialViews() => RenderSurface.Host.ViewManager.ConfigureSingleFullView();
    protected override void CreateDirectDrawings()
    {
        var host = RenderSurface.Host;
        Player = new VlcVideoPlayer();
        Video = new DirectVideo(Player, source, host, host.ViewManager.Views[0], new Rectangle(20, 20, 940, 500))
        { Stretch = StretchMode.Uniform };
    }
    internal void DisposeVideo() { Video?.Dispose(); Video = null; Player = null; }
    protected override void OnDisposing() => DisposeVideo();
}
