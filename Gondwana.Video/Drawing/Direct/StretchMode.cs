namespace Gondwana.Drawing.Direct;

/// <summary>
/// Defines how video content should be stretched or scaled to fit within its display bounds.
/// </summary>
public enum StretchMode
{
    /// <summary>
    /// Draws the video at its native size from the top-left corner of the bounds, without any scaling.
    /// </summary>
    None,

    /// <summary>
    /// Stretches the video to fill the entire bounds, ignoring the aspect ratio.
    /// </summary>
    Fill,

    /// <summary>
    /// Scales the video uniformly to fit inside the bounds while preserving the aspect ratio.
    /// The video will be letterboxed or pillarboxed if necessary.
    /// </summary>
    Uniform,

    /// <summary>
    /// Scales the video uniformly to cover the entire bounds while preserving the aspect ratio.
    /// Content that overflows the bounds will be cropped.
    /// </summary>
    UniformToFill
}