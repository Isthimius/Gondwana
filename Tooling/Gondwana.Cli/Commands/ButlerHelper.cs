using System.Runtime.InteropServices;

namespace Gondwana.Cli.Commands;

internal static class ButlerHelper
{
    internal static bool TryResolve(out string butlerPath, out string? version)
    {
        var output = ProcessHelper.Run("butler", "--version", out int exitCode);
        if (exitCode == 0 && !string.IsNullOrWhiteSpace(output))
        {
            butlerPath = "butler";
            version = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
            return true;
        }

        if (TryGetFromKnownLocations(out butlerPath))
        {
            output = ProcessHelper.Run(butlerPath, "--version", out exitCode);
            if (exitCode == 0)
            {
                version = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();
                return true;
            }
        }

        butlerPath = string.Empty;
        version = null;
        return false;
    }

    internal static bool TryGetFromKnownLocations(out string butlerPath)
    {
        foreach (var candidate in EnumerateKnownPaths())
        {
            if (File.Exists(candidate))
            {
                butlerPath = candidate;
                return true;
            }
        }

        butlerPath = string.Empty;
        return false;
    }

    private static IEnumerable<string> EnumerateKnownPaths()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrWhiteSpace(localAppData))
                yield return Path.Combine(localAppData, "itch", "butler", "butler.exe");

            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(userProfile))
                yield return Path.Combine(userProfile, ".itch", "butler", "butler.exe");

            yield break;
        }

        var userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userHome))
            yield return Path.Combine(userHome, ".itch", "butler", "butler");
    }
}
