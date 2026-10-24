using System.Diagnostics;
using Gondwana.Tooling.Studio.WinForms.Panels;
using Gondwana.Tooling.WinForms;

namespace Gondwana.Tooling.Studio.WinForms.Services;

/// <summary>Persists the Studio working-directory icon size as a per-user preference.</summary>
internal sealed class DirectoryPreferenceStore
{
    internal string FilePath { get; }

    internal DirectoryPreferenceStore(string? applicationId = null, string? settingsRoot = null)
    {
        FilePath = Path.Combine(DockLayoutStore.ApplicationDirectory(applicationId, settingsRoot),
            "Studio", "directory-icon-size.txt");
    }

    internal DirectoryIconSize Read()
    {
        try
        {
            if (!File.Exists(FilePath))
                return DirectoryIconSize.Small;

            string value = File.ReadAllText(FilePath).Trim();
            return value switch
            {
                nameof(DirectoryIconSize.Medium) => DirectoryIconSize.Medium,
                nameof(DirectoryIconSize.Large) => DirectoryIconSize.Large,
                _ => DirectoryIconSize.Small
            };
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("Studio directory icon-size preference ignored: {0}", ex.Message);
            return DirectoryIconSize.Small;
        }
    }

    internal void Write(DirectoryIconSize size)
    {
        string temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(temporary, size.ToString());
            File.Move(temporary, FilePath, overwrite: true);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("Studio directory icon-size preference ignored: {0}", ex.Message);
        }
        finally
        {
            try
            {
                if (File.Exists(temporary))
                    File.Delete(temporary);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("Studio directory icon-size preference ignored: {0}", ex.Message);
            }
        }
    }
}
