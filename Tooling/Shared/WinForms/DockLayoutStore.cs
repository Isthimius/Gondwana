using System.Diagnostics;
using System.Reflection;

namespace Gondwana.Tooling.WinForms;

/// <summary>Per-user preferences, separate from authoring files and runtime state.</summary>
internal sealed class DockLayoutStore
{
    // AppContext overrides let embedding hosts and tests select their own preference scope.
    internal const string RootSetting = "Gondwana.Tooling.SettingsRoot";
    internal const string ApplicationSetting = "Gondwana.Tooling.ApplicationId";
    internal string FilePath { get; }

    internal DockLayoutStore(string profile, string? applicationId = null, string? settingsRoot = null)
    {
        applicationId ??= AppContext.GetData(ApplicationSetting) as string
            ?? Assembly.GetEntryAssembly()?.GetName().Name ?? "Gondwana.Tooling.Host";
        settingsRoot ??= AppContext.GetData(RootSetting) as string
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Hidden Worlds Games", "Gondwana", "Tooling");
        FilePath = Path.Combine(settingsRoot, SafeKey(applicationId), "Docking", SafeKey(profile) + ".xml");
    }

    private static string SafeKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key is "." or ".." ||
            key.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new ArgumentException("A layout key must be a single file name.", nameof(key));
        return key;
    }

    internal byte[]? Read()
    {
        try { return File.Exists(FilePath) ? File.ReadAllBytes(FilePath) : null; }
        catch (Exception ex) { Warn(ex); return null; }
    }

    internal bool Write(byte[] layout)
    {
        string temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllBytes(temporary, layout);
            File.Move(temporary, FilePath, overwrite: true);
            return true;
        }
        catch (Exception ex) { Warn(ex); return false; }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (Exception ex) { Warn(ex); }
        }
    }

    internal void Delete()
    {
        try { File.Delete(FilePath); }
        catch (Exception ex) { Warn(ex); }
    }

    internal static void Warn(Exception exception) =>
        Trace.TraceWarning("Dock layout preference ignored: {0}", exception.Message);
}
