using System.Diagnostics;
using System.Reflection;

namespace Gondwana;

/// <summary>
/// Provides static information about the Gondwana engine, including version details.
/// This class offers convenient access to engine metadata derived from assembly attributes
/// and file version information.
/// </summary>
public static class EngineInfo
{
    /// <summary>
    /// Gets the version string of the Gondwana engine.
    /// </summary>
    /// <value>
    /// A string representing the engine version, obtained from assembly metadata in the following priority order:
    /// <list type="number">
    /// <item><description>
    /// <see cref="AssemblyInformationalVersionAttribute"/> (typically contains semantic versioning
    /// information including git commit hashes from tools like NerdBank.GitVersioning)
    /// </description></item>
    /// <item><description>
    /// File version information from <see cref="FileVersionInfo"/> (cross-platform file version)
    /// </description></item>
    /// <item><description>
    /// <see cref="AssemblyName.Version"/> as a fallback (standard assembly version)
    /// </description></item>
    /// <item><description>
    /// The string "unknown" if no version information is available
    /// </description></item>
    /// </list>
    /// </value>
    /// <remarks>
    /// This property queries the version information from the assembly containing the <see cref="Engine"/> type.
    /// The version string format depends on the build configuration and versioning strategy used during compilation.
    /// </remarks>
    public static string Version => GetVersion(typeof(Engine).Assembly);

    private static string GetVersion(Assembly asm)
    {
        // 1) Best: NerdBank / SemVer / git hash lives here
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(info))
            return info;

        // 2) Next: Windows file version (works cross-platform too; returns empty sometimes)
        var fvi = FileVersionInfo.GetVersionInfo(asm.Location);
        if (!string.IsNullOrWhiteSpace(fvi.FileVersion))
            return fvi.FileVersion;

        // 3) Last resort: AssemblyName.Version (often stable/boring)
        return asm.GetName().Version?.ToString() ?? "unknown";
    }
}
