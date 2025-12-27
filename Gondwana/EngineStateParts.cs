namespace Gondwana;

[Flags]
public enum EngineStateParts
{
    None = 0,

    AssetsFiles = 1 << 0,
    Tilesheets = 1 << 1,
    Cycles = 1 << 2,
    Scenes = 1 << 3,
    Sprites = 1 << 4,
    Audio = 1 << 5,
    ValueBag = 1 << 6,

    All = AssetsFiles | Tilesheets | Cycles | Scenes | Sprites | Audio | ValueBag
}