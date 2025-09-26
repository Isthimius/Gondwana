namespace Gondwana.Rendering.Direct;

public enum StretchMode
{
    None,           // draw at native size from Bounds.TopLeft
    Fill,           // stretch to Bounds (ignore aspect)
    Uniform,        // fit inside Bounds (preserve aspect)
    UniformToFill   // cover Bounds (preserve aspect; crop overflow)
}
