using Gondwana.Hosting;
using Gondwana.WinForms.Rendering;

namespace Gondwana.WinForms.Hosting;

/// <summary>
/// Provides a base class for hosting Gondwana games in Windows Forms applications using
/// GPU-accelerated (OpenGL) rendering via <see cref="WinFormGpuRenderSurfaceControl"/>.
/// </summary>
public abstract class WinFormsGpuGameHost : GameHostBase
{
    /// <summary>
    /// Gets the GPU render surface control used for displaying game content.
    /// </summary>
    public WinFormGpuRenderSurfaceControl RenderSurface { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="WinFormsGpuGameHost"/> class.
    /// </summary>
    /// <param name="renderSurface">The GPU render surface control to use for rendering.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="renderSurface"/> is null.</exception>
    protected WinFormsGpuGameHost(WinFormGpuRenderSurfaceControl renderSurface)
    {
        RenderSurface = renderSurface ?? throw new ArgumentNullException(nameof(renderSurface));
    }

    /// <summary>
    /// Configures Windows Forms-specific platform features, including audio format support.
    /// </summary>
    protected override void ConfigurePlatform()
    {
        Engine.Instance.InitializeWinFormsAudioFormats();
        OnConfigurePlatform();
    }

    /// <summary>
    /// Configures the keyboard adapter for the Windows Forms render surface.
    /// </summary>
    protected override void ConfigureKeyboard()
    {
        Engine.Instance.InitializeWinFormsKeyboardAdapter(RenderSurface);
        OnKeyboardAdapterInitialized();
    }

    /// <summary>
    /// Configures the mouse adapter for the Windows Forms render surface.
    /// </summary>
    protected override void ConfigureMouse()
    {
        Engine.Instance.InitializeWinFormsMouseAdapter(RenderSurface);
        OnMouseAdapterInitialized();
    }

    /// <summary>
    /// Configures the XInput gamepad manager for Xbox controller support.
    /// </summary>
    protected override void ConfigureGamepads()
    {
        //Engine.Instance.InitializeXInputGamepadManager();
        OnGamepadManagerInitialized();
    }

    /// <summary>
    /// Binds the current scene to the GPU render surface host.
    /// </summary>
    protected override void BindScene()
    {
        RenderSurface.Host.Bind(Scene!, false);
    }

    /// <summary>
    /// Provides a hook for configuring platform-specific settings during initialization.
    /// </summary>
    protected virtual void OnConfigurePlatform() { }

    /// <summary>
    /// Called after the keyboard adapter has been initialized. Override to perform additional keyboard setup.
    /// </summary>
    protected virtual void OnKeyboardAdapterInitialized() { }

    /// <summary>
    /// Called after the mouse adapter has been initialized. Override to perform additional mouse setup.
    /// </summary>
    protected virtual void OnMouseAdapterInitialized() { }

    /// <summary>
    /// Called after the gamepad manager has been initialized. Override to perform additional gamepad setup.
    /// </summary>
    protected virtual void OnGamepadManagerInitialized() { }
}
