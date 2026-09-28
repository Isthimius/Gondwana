using System.Diagnostics;
using Gondwana.Tooling.WinForms;

namespace Gondwana.Tooling.Studio.WinForms.Services;

/// <summary>Persists the last selected Studio project folder as a per-user preference.</summary>
internal sealed class ProjectFolderStore
{
    internal string FilePath { get; }

    internal ProjectFolderStore(string? applicationId = null, string? settingsRoot = null)
    {
        FilePath = Path.Combine(DockLayoutStore.ApplicationDirectory(applicationId, settingsRoot),
            "Studio", "project-folder.txt");
    }

    internal string? Read()
    {
        try
        {
            if (!File.Exists(FilePath)) return null;
            string path = File.ReadAllText(FilePath);
            return path.Length > 0 && Directory.Exists(path) ? Path.GetFullPath(path) : null;
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("Studio project-folder preference ignored: {0}", ex.Message);
            return null;
        }
    }

    internal void Write(string path)
    {
        string temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            path = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(temporary, path);
            File.Move(temporary, FilePath, overwrite: true);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("Studio project-folder preference ignored: {0}", ex.Message);
        }
        finally
        {
            try
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("Studio project-folder preference ignored: {0}", ex.Message);
            }
        }
    }
}
