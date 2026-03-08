namespace Gondwana;

/// <summary>
/// Represents different parts of the engine state that can be managed, saved, or loaded independently.
/// This is a flags enumeration, allowing multiple state parts to be combined using bitwise operations
/// to represent complex state management scenarios. For example, use <c>AssetsFiles | Tilesheets</c>
/// to represent both asset files and tilesheet state, or use <see cref="All"/> to represent all
/// engine state components.
/// </summary>
[Flags]
public enum EngineStateParts
{
    /// <summary>
    /// No engine state parts are selected. This represents an empty or null state selection,
    /// useful as a default or reset value when no specific state parts need to be managed.
    /// </summary>
    None = 0,

    /// <summary>
    /// Represents the asset files state, including loaded file references, asset metadata, and
    /// file system resources used by the engine. This encompasses all externally loaded resources
    /// such as images, data files, and other assets that the engine loads from disk.
    /// </summary>
    AssetsFiles = 1 << 0,

    /// <summary>
    /// Represents the tilesheets state, including loaded tilesheet definitions, tile graphics,
    /// tile properties, and tilesheet configurations. This encompasses all tile-based graphics
    /// resources used for rendering tile-based game worlds and scenes.
    /// </summary>
    Tilesheets = 1 << 1,

    /// <summary>
    /// Represents the animation cycles state, including sprite animations, frame sequences,
    /// timing information, and animation configurations. This encompasses all animation data
    /// used to create dynamic sprite behaviors and visual effects.
    /// </summary>
    Cycles = 1 << 2,

    /// <summary>
    /// Represents the scenes state, including scene definitions, layer configurations, entity
    /// placements, and scene hierarchies. This encompasses all data related to game scenes,
    /// including their structure, contents, and properties.
    /// </summary>
    Scenes = 1 << 3,

    /// <summary>
    /// Represents the sprites state, including sprite instances, positions, properties, and
    /// sprite-related game objects. This encompasses all active sprite entities and their
    /// runtime state within the game world.
    /// </summary>
    Sprites = 1 << 4,

    /// <summary>
    /// Represents the audio state, including loaded sound effects, music tracks, audio settings,
    /// playback states, and audio configurations. This encompasses all audio resources and their
    /// current playback and configuration state.
    /// </summary>
    Audio = 1 << 5,

    /// <summary>
    /// Represents the value bag state, which typically includes custom key-value pairs, game variables,
    /// flags, and other dynamic data storage used by the application. This provides a flexible
    /// storage mechanism for arbitrary game state data that doesn't fit into other specific categories.
    /// </summary>
    ValueBag = 1 << 6,

    /// <summary>
    /// Represents all engine state parts combined. This is a convenience value that includes
    /// <see cref="AssetsFiles"/>, <see cref="Tilesheets"/>, <see cref="Cycles"/>, <see cref="Scenes"/>,
    /// <see cref="Sprites"/>, <see cref="Audio"/>, and <see cref="ValueBag"/>. Use this when you need
    /// to manage, save, or load the complete engine state without selectively choosing individual parts.
    /// </summary>
    All = AssetsFiles | Tilesheets | Cycles | Scenes | Sprites | Audio | ValueBag
}