using Gondwana.Hosting;
using Gondwana.Rendering;
using Gondwana.Scenes;
using Gondwana.WinForms;
using System.Windows.Forms;

namespace Gondwana.WinForms.Hosting;

/// <summary>
/// Provides shared Windows Forms host behavior for Gondwana game hosts.
/// </summary>
public abstract class WinFormsGameHostBase : GameHostBase
{
    private readonly Control _renderSurface;
    private readonly Action<Scene, bool> _bindScene;

    /// <summary>
    /// Initializes a new instance of the <see cref="WinFormsGameHostBase"/> class.
    /// </summary>
    /// <param name="renderSurface">The control used for input handling.</param>
    /// <param name="bindScene">The scene-binding callback for the render surface host.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="renderSurface"/> or <paramref name="bindScene"/> is null.</exception>
    protected WinFormsGameHostBase(Control renderSurface, Action<Scene, bool> bindScene)
    {
        _renderSurface = renderSurface ?? throw new ArgumentNullException(nameof(renderSurface));
        _bindScene = bindScene ?? throw new ArgumentNullException(nameof(bindScene));
    }

    /// <summary>
    /// Configures Windows Forms-specific platform features, including audio format support.
    /// </summary>
    protected sealed override void ConfigurePlatform()
    {
        Engine.Instance.InitializeWinFormsAudioFormats();
        OnConfigurePlatform();
    }

    /// <summary>
    /// Configures the keyboard adapter for the Windows Forms render surface.
    /// </summary>
    protected sealed override void ConfigureKeyboard()
    {
        Engine.Instance.InitializeWinFormsKeyboardAdapter(_renderSurface);
        OnKeyboardAdapterInitialized();
    }

    /// <summary>
    /// Configures the mouse adapter for the Windows Forms render surface.
    /// </summary>
    protected sealed override void ConfigureMouse()
    {
        Engine.Instance.InitializeWinFormsMouseAdapter(_renderSurface);
        OnMouseAdapterInitialized();
    }

    /// <summary>
    /// Configures gamepad support.
    /// </summary>
    protected sealed override void ConfigureGamepads()
    {
        //Engine.Instance.InitializeXInputGamepadManager();
        OnGamepadManagerInitialized();
    }

    /// <summary>
    /// Configures touch input.
    /// </summary>
    protected sealed override void ConfigureTouch()
    {
        OnTouchAdapterInitialized();
    }

    /// <summary>
    /// Binds the current scene to the render surface host.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when no scene has been created.</exception>
    protected sealed override void BindScene()
    {
        var scene = Scene
            ?? throw new InvalidOperationException(
                $"{nameof(BindScene)} cannot be called before {nameof(Scene)} has been created.");

        _bindScene(scene, false);
        OnSceneBound();
    }

    /// <summary>
    /// Called after the current scene has been bound to the Windows Forms render surface host.
    /// </summary>
    protected virtual void OnSceneBound()
    {
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

    /// <summary>
    /// Called after the touch adapter has been initialized. Override to perform additional touch setup,
    /// such as attaching gesture recognizers.
    /// </summary>
    protected virtual void OnTouchAdapterInitialized() { }
}
