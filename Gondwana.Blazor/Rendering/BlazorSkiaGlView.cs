using System.ComponentModel;
using System.Runtime.Versioning;

namespace Gondwana.Blazor.Rendering;

/// <summary>
/// Gives Razor a local component name for SkiaSharp's WebGL view. This avoids Razor resolving
/// <c>SkiaSharp</c> relative to Gondwana's own <c>Gondwana.SkiaSharp</c> namespace.
/// </summary>
[SupportedOSPlatform("browser")]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class BlazorSkiaGlView : global::SkiaSharp.Views.Blazor.SKGLView
{
}
