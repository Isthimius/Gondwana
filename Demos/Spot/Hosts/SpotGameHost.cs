using Gondwana.Hosting;
using Gondwana.Rendering;
using Gondwana.Rendering.Backbuffers;
using Gondwana.Scenes;
using Gondwana.WinForms.Hosting;
using Gondwana.WinForms.Rendering;
using SkiaSharp;

namespace Gondwana.Demos.Spot;

internal sealed class SpotGameHost : WinFormsGameHost, ISpotGameHost, ISpotHostContext
{
    private readonly SpotHostCore _spot;

    internal SpotGameHost(WinFormBitmapRenderSurfaceControl renderSurface)
        : base(renderSurface)
    {
        ((BitmapBackbuffer)renderSurface.Host.Backbuffer).FilterQuality = SKFilterQuality.High;
        _spot = new SpotHostCore(this);
    }

    Scene ISpotHostContext.Scene => Scene!;
    RenderSurfaceHostBase ISpotHostContext.SurfaceHost => RenderSurface.Host;
    int ISpotHostContext.SurfaceWidth => RenderSurface.Width;
    int ISpotHostContext.SurfaceHeight => RenderSurface.Height;

    public NewGameOptions? LastNewGameOptions => _spot.LastNewGameOptions;

    protected override SplashScreen? CreateSplash(RenderSurfaceHostBase host)
        => _spot.CreateSplash(host);

    protected override void LoadAssets()
        => _spot.LoadAssets();

    protected override void LoadTilesheets()
        => _spot.LoadTilesheets();

    protected override Scene CreateInitialScene()
        => _spot.CreateInitialScene();

    protected override void CreateSceneGraph()
    {
        base.CreateSceneGraph();
        _spot.CreateSceneGraph();
    }

    protected override void CreateDirectDrawings()
    {
        // Deliberately empty: startup presentation is created in BeginPostSplashStartup()
        // so it does not appear beneath the Gondwana splash.
    }

    protected override void OnStartEngine()
    {
        // Deliberately empty: startup music begins in BeginPostSplashStartup()
        // after the Gondwana splash has fully faded out.
    }

    protected override void OnMouseAdapterInitialized()
        => _spot.OnMouseAdapterInitialized();

    protected override void OnKeyboardAdapterInitialized()
        => _spot.OnKeyboardAdapterInitialized();

    protected override void UnhookEvents()
        => _spot.UnhookEvents();

    public void BeginPostSplashStartup()
        => _spot.BeginPostSplashStartup();

    public void OpenNewGameDialog(NewGameOptions? newGameOptions = null)
        => _spot.OpenNewGameDialog(newGameOptions);

    public void SetMusicEnabled(bool enabled)
        => _spot.SetMusicEnabled(enabled);

    public void SetSoundEffectsEnabled(bool enabled)
        => _spot.SetSoundEffectsEnabled(enabled);

    public void SetJiggleEnabled(bool enabled)
        => _spot.SetJiggleEnabled(enabled);

    public void SetCloudsEnabled(bool enabled)
        => _spot.SetCloudsEnabled(enabled);

    public void StartNewGame(NewGameOptions options)
        => _spot.StartNewGame(options);
}
