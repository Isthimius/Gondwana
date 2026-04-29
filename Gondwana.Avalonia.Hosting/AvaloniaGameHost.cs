using Gondwana.Avalonia.Rendering;
using Gondwana.Hosting;

namespace Gondwana.Avalonia.Hosting;

/// <summary>
/// Provides a base class for hosting Gondwana games in Avalonia applications.
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
    protected override void ConfigurePlatform()
    {
        OnConfigurePlatform();
    }

    /// <summary>
    /// Configures the keyboard adapter for the Avalonia render surface.
    /// </summary>
    protected override void ConfigureKeyboard()
    {
        Engine.Instance.InitializeAvaloniaKeyboardAdapter(RenderSurface);
        OnKeyboardAdapterInitialized();
    }

    /// <summary>
    /// Configures the mouse adapter for the Avalonia render surface.
    /// </summary>
    protected override void ConfigureMouse()
    {
        Engine.Instance.InitializeAvaloniaMouseAdapter(RenderSurface);
        OnMouseAdapterInitialized();
    }

    /// <summary>
    /// Configures gamepad support. Override to provide platform-specific gamepad integration.
    /// </summary>
    protected override void ConfigureGamepads()
    {
        OnGamepadManagerInitialized();
    }

    /// <summary>
    /// Binds the current scene to the render surface host.
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
