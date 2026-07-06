using Gondwana.Avalonia.Rendering;
using Gondwana.Hosting;

namespace Gondwana.Avalonia.Hosting;

/// <summary>
/// Provides a base class for hosting Gondwana games in Avalonia desktop applications.
/// </summary>
public abstract class AvaloniaGameHost : GameHostBase
{
    /// <summary>
    /// Gets the render surface control used for displaying game content.
    /// </summary>
    public AvaloniaBitmapRenderSurfaceControl RenderSurface { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AvaloniaGameHost"/> class.
    /// </summary>
    /// <param name="renderSurface">The render surface control to use for rendering.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="renderSurface"/> is null.</exception>
    protected AvaloniaGameHost(AvaloniaBitmapRenderSurfaceControl renderSurface)
    {
        RenderSurface = renderSurface ?? throw new ArgumentNullException(nameof(renderSurface));
    }

    /// <summary>
    /// Configures Avalonia-specific platform features.
    /// </summary>
    protected sealed override void ConfigurePlatform()
    {
        OnConfigurePlatform();
    }

    /// <summary>
    /// Configures the keyboard adapter for the Avalonia render surface.
    /// </summary>
    protected sealed override void ConfigureKeyboard()
    {
        Engine.InitializeAvaloniaKeyboardAdapter(RenderSurface);
        OnKeyboardAdapterInitialized();
    }

    /// <summary>
    /// Configures the mouse adapter for the Avalonia render surface.
    /// </summary>
    protected sealed override void ConfigureMouse()
    {
        Engine.InitializeAvaloniaMouseAdapter(RenderSurface);
        OnMouseAdapterInitialized();
    }

    /// <summary>
    /// Configures gamepad support.
    /// </summary>
    protected sealed override void ConfigureGamepads()
    {
        OnConfigureGamepads();
    }

    /// <summary>
    /// Configures the touch adapter for the Avalonia render surface.
    /// </summary>
    protected sealed override void ConfigureTouch()
    {
        Engine.InitializeAvaloniaTouchAdapter(RenderSurface);
        OnTouchAdapterInitialized();
    }

    /// <summary>
    /// Binds the current scene to the Avalonia render surface host.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when no scene has been created.</exception>
    protected sealed override void BindScene()
    {
        var scene = Scene
            ?? throw new InvalidOperationException(
                $"{nameof(BindScene)} cannot be called before {nameof(Scene)} has been created.");

        RenderSurface.Host.Bind(scene, false);
        OnSceneBound();
    }

    /// <summary>
    /// Provides a hook for configuring additional Avalonia-specific platform settings during initialization.
    /// </summary>
    protected virtual void OnConfigurePlatform()
    {
    }

    /// <summary>
    /// Called after the keyboard adapter has been initialized.
    /// </summary>
    protected virtual void OnKeyboardAdapterInitialized()
    {
    }

    /// <summary>
    /// Called after the mouse adapter has been initialized.
    /// </summary>
    protected virtual void OnMouseAdapterInitialized()
    {
    }

    /// <summary>
    /// Provides a hook for configuring gamepad support.
    /// </summary>
    protected virtual void OnConfigureGamepads()
    {
    }

    /// <summary>
    /// Called after the touch adapter has been initialized.
    /// </summary>
    protected virtual void OnTouchAdapterInitialized()
    {
    }
}
