namespace Gondwana.Tooling.Tilesheets.Editing;

internal enum OverlayKind
{
    /// <summary>Outlines of all tilesheet regions.</summary>
    Regions,

    /// <summary>Outline of the selected tilesheet region.</summary>
    SelectedRegion,

    /// <summary>Frame grid outlines.</summary>
    Frames,

    /// <summary>Outline of the selected frame.</summary>
    SelectedFrame,

    /// <summary>Region margins around the frame grid.</summary>
    Margin,

    /// <summary>Padding between frame cells.</summary>
    Padding,

    /// <summary>Visual overhang beyond tile bounds.</summary>
    Overhang,

    /// <summary>Effective collision-area outlines.</summary>
    Collision
}
