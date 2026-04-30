using System.Runtime.InteropServices;

namespace Gondwana.Cli.Commands;

/// <summary>
/// Probes for native library availability without crashing the process.
/// </summary>
internal static class NativeLibraryProbe
{
    /// <summary>
    /// Returns true if the native library with the given name can be loaded.
    /// </summary>
    public static bool CanLoad(string libraryName)
    {
        try
        {
            if (NativeLibrary.TryLoad(libraryName, out var handle))
            {
                NativeLibrary.Free(handle);
                return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }
}
