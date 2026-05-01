using Gondwana.Avalonia.Rendering;
using Gondwana.Hosting;

namespace Gondwana.Avalonia.Hosting;

/// <summary>
/// Provides a base class for hosting Gondwana games in Avalonia applications using
/// GPU-accelerated (OpenGL) rendering via <see cref="AvaloniaGpuRenderSurfaceControl"/>.
/// </summary>
/// <remarks>
/// GPU rendering requires a desktop Avalonia target that supports OpenGL
/// (Windows, macOS, or Linux).  It is not suitable for WebAssembly (WASM) targets;
/// use <see cref="AvaloniaGameHost"/> with
/// <see cref="AvaloniaBitmapRenderSurfaceControl"/> for cross-platform / WASM scenarios.
/// </remarks>
public abstract class AvaloniaGpuGameHost : GameHostBase
{
    /// <summary>
    /// Gets the GPU render surface control used for displaying game content.
    /// </summary>
    public AvaloniaGpuRenderSurfaceControl RenderSurface { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AvaloniaGpuGameHost"/> class.
    /// </summary>
    /// <param name="renderSurface">The GPU render surface control to use for rendering.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="renderSurface"/> is null.</exception>
    protected AvaloniaGpuGameHost(AvaloniaGpuRenderSurfaceControl renderSurface)
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
