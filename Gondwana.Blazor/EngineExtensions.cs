using Gondwana.Blazor.Input.Keyboard;
using Gondwana.Blazor.Input.Mouse;
using Gondwana.Blazor.Input.Touch;
using Gondwana.Blazor.Rendering;
using Gondwana.Input.Keyboard;
using Gondwana.Input.Mouse;
using Microsoft.Extensions.Logging;

namespace Gondwana.Blazor;

/// <summary>
/// Provides extension methods for configuring Blazor-specific features on the Gondwana engine.
/// </summary>
public static class EngineExtensions
{
    /// <summary>
    /// Initializes the Blazor keyboard adapter for the specified render surface component.
    /// Key codes correspond to <see cref="BlazorKey"/> values cast to <see cref="int"/>.
    /// </summary>
    /// <param name="engine">The engine instance to configure.</param>
    /// <param name="component">The render surface component to capture keyboard input from.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="component"/> is null.</exception>
    public static void InitializeBlazorKeyboardAdapter(this Engine engine, BlazorBitmapRenderSurfaceComponent component)
    {
        Engine.Logger.LogInformation("Initializing BlazorKeyboardAdapter...");

        if (component == null)
        {
            Engine.Logger.LogError("BlazorKeyboardAdapter initialization failed: Component cannot be null.");
            throw new ArgumentNullException(nameof(component));
        }

        KeyboardEventPoller.Initialize(new BlazorKeyboardAdapter(component));
    }

    /// <summary>
    /// Initializes the Blazor mouse adapter for the specified render surface component.
    /// </summary>
    /// <param name="engine">The engine instance to configure.</param>
    /// <param name="component">The render surface component to capture mouse input from.</param>
    /// <param name="mouseEventConfiguration">Optional configuration for mouse event handling.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="component"/> is null.</exception>
    public static void InitializeBlazorMouseAdapter(this Engine engine, BlazorBitmapRenderSurfaceComponent component, MouseEventConfiguration? mouseEventConfiguration = null)
    {
        Engine.Logger.LogInformation("Initializing BlazorMouseAdapter...");

        if (component == null)
        {
            Engine.Logger.LogError("BlazorMouseAdapter initialization failed: Component cannot be null.");
            throw new ArgumentNullException(nameof(component));
        }

        MouseEventPoller.Initialize(new BlazorMouseAdapter(component), mouseEventConfiguration);
    }

    /// <summary>
    /// Initializes the Blazor touch adapter for the specified render surface component and
    /// registers it with <see cref="TouchEventPoller"/>, enabling touch input on Blazor WASM.
    /// </summary>
    /// <param name="engine">The engine instance to configure.</param>
    /// <param name="component">The render surface component to capture touch input from.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="component"/> is null.</exception>
    public static void InitializeBlazorTouchAdapter(this Engine engine, BlazorBitmapRenderSurfaceComponent component)
    {
        Engine.Logger.LogInformation("Initializing BlazorTouchAdapter...");

        if (component == null)
        {
            Engine.Logger.LogError("BlazorTouchAdapter initialization failed: Component cannot be null.");
            throw new ArgumentNullException(nameof(component));
        }

        engine.Input.TouchAdapter = new BlazorTouchAdapter(component);
    }
}
