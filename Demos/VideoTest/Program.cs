using Gondwana.Assets;
using Gondwana.Drawing.Direct;
using Gondwana.Scenes;
using Gondwana.Video;
using Gondwana.Video.Widgets;
using Gondwana.Widgets;
using Gondwana.Widgets.Controls;
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
        Controls.Add(_surface);
        _statusTimer.Tick += (_, _) =>
        {
            var player = _host?.Player;
            Text = player is null ? "VideoTest — disposed" :
                $"VideoTest — {player.NaturalSize} {player.Metadata.Status} audio={player.HasAudio} " +
                $"{player.Position:mm\\:ss}/{player.Duration:mm\\:ss} loop={player.Loop} " +
                $"stretch={_host?.Video?.Stretch} drag={_host?.Video?.IsDragEnabled} error={player.LastError?.Message}";
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
    internal VideoWidget? Video { get; private set; }
    private readonly List<WidgetBase> _controls = [];
    protected override Scene CreateInitialScene() => Scene.Empty;
    protected override void CreateInitialViews() => RenderSurface.Host.ViewManager.ConfigureSingleFullView();
    protected override void OnKeyboardAdapterInitialized()
    {
        // Monitoring is a host policy; VideoWidget assigns no keys or commands.
        var keyboard = Engine.Input.KeyboardEventPoller!;
        for (int key = (int)Keys.A; key <= (int)Keys.Z; key++)
            keyboard.StartMonitoringKey(key);
        foreach (var key in new[] { Keys.Space, Keys.Enter, Keys.Escape })
            keyboard.StartMonitoringKey((int)key);
    }
    protected override void CreateDirectDrawings()
    {
        var host = RenderSurface.Host;
        var view = host.ViewManager.Views[0];
        Player = new VlcVideoPlayer();
        Video = new VideoWidget(host, view, new Rectangle(20, 20, 640, 360), source, Player)
        { Stretch = StretchMode.Uniform };
        Video.IsDragEnabled = true; // Explicit demo choice; the package default is false.
        var status = new LabelWidget(host, view, new Rectangle(20, 405, 950, 45),
            "Drag the video. Click logs an event; playback does not toggle. Controls below are separate Widgets.");
        status.SetZOrder(100);
        status.Show();
        _controls.Add(status);
        int clicks = 0;
        Video.PointerClick += _ => status.SetText($"Consumer PointerClick #{++clicks}; playback unchanged.");
        Video.PointerEnter += _ => status.SetText("Pointer entered video. Drag anywhere inside its bounds.");
        Video.PointerLeave += _ => status.SetText("Pointer left video.");
        Video.KeyboardInput += e => status.SetText($"Consumer KeyboardInput: {e.Key}; no playback command assigned.");
        Video.DragEnded += _ => status.SetText($"Drag ended at {Video.Bounds.Location}; no click emitted.");
        Video.Show();

        // Ordinary consumer-owned Widgets; VideoWidget itself contains no playback chrome.
        void Add(string text, Action<VideoWidget> action)
        {
            int index = _controls.Count - 1;
            var button = new ButtonWidget(host, view,
                new Rectangle(20 + index % 7 * 138, 465 + index / 7 * 48, 130, 40), text);
            button.Clicked += () => { if (Video is { } video) action(video); };
            button.SetZOrder(100);
            button.Show();
            _controls.Add(button);
        }
        Add("Play", v => v.Play());
        Add("Pause", v => v.Pause());
        Add("Stop", v => v.Stop());
        Add("Seek +5s", v => v.Seek(v.Position + TimeSpan.FromSeconds(5)));
        Add("Loop toggle", v => v.Loop = !v.Loop);
        Add("0.5x", v => v.PlaybackRate = 0.5);
        Add("1x", v => v.PlaybackRate = 1);
        Add("Show", v => v.Show());
        Add("Hide", v => v.Hide());
        Add("Drag toggle", v => v.IsDragEnabled = !v.IsDragEnabled);
        Add("Fade out", v => v.FadeOut(1));
        Add("Fade in", v => v.FadeIn(1));
        Add("Opacity 50%", v => v.SetOpacity(0.5f));
        Add("Stretch next", v => v.Stretch = (StretchMode)(((int)v.Stretch + 1) % 4));
        Add("Reset bounds", v => v.SetBounds(new Rectangle(20, 20, 640, 360)));
        Add("Resize", v => v.SetBounds(new Rectangle(v.Bounds.Location,
            v.Bounds.Width == 640 ? new Size(400, 300) : new Size(640, 360))));
        Add("Dispose", _ => DisposeVideo());
    }
    internal void DisposeVideo() { Video?.Dispose(); Video = null; Player = null; }
    protected override void OnDisposing()
    {
        foreach (var control in _controls) control.Dispose();
        _controls.Clear();
        DisposeVideo();
    }
}
