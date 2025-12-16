using System.Diagnostics;
using System.Reflection;

namespace Gondwana;

public static class EngineInfo
{
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
