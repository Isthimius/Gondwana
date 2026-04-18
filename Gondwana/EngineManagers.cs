using Gondwana.Audio;
using Gondwana.Drawing.Direct;
using Gondwana.Drawing.Sprites;
using Gondwana.Drawing.Tilesheets;
using Gondwana.Rendering.Text;

namespace Gondwana;

/// <summary>
/// Provides centralized access to all engine resource managers.
/// </summary>
public sealed class EngineManagers
{
    internal EngineManagers() { }

    /// <summary>
    /// Gets the audio resource manager for loading and managing audio assets.
    /// </summary>
    public AudioResourceManager AudioResources { get; } = AudioResourceManager.Instance;

    /// <summary>
    /// Gets the direct drawing manager for immediate-mode rendering operations.
    /// </summary>
    public DirectDrawingManager DirectDrawings { get; } = DirectDrawingManager.Instance;

    /// <summary>
    /// Gets the font manager for loading and managing font resources.
    /// </summary>
    public FontManager Fonts { get; } = FontManager.Instance;

    /// <summary>
    /// Gets the sprite manager for managing sprite assets and rendering.
    /// </summary>
    public SpriteManager Sprites { get; } = SpriteManager.Instance;

    /// <summary>
    /// Gets the tilesheet registry for managing tilesheet resources.
    /// </summary>
    public TilesheetRegistry Tilesheets { get; } = TilesheetRegistry.Instance;
}
