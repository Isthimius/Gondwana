namespace Gondwana.Tests;

/// <summary>Runs link security tests unless Windows denies the symbolic-link privilege.</summary>
public sealed class SymbolicLinkTheoryAttribute : TheoryAttribute
{
    /// <summary>Probes link creation; unrelated filesystem errors remain test discovery failures.</summary>
    public SymbolicLinkTheoryAttribute()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = Path.Combine(Path.GetTempPath(), "Gondwana-LinkCapability-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        var target = Path.Combine(root, "target");
        var link = Path.Combine(root, "link");
        try
        {
            File.WriteAllText(target, "probe");
            try { File.CreateSymbolicLink(link, target); }
            catch (IOException ex) when ((ex.HResult & 0xffff) == 1314)
            {
                Skip = "Windows does not grant this test process the symbolic-link privilege (ERROR_PRIVILEGE_NOT_HELD). Junction security tests still run.";
            }
        }
        finally
        {
            File.Delete(link);
            File.Delete(target);
            Directory.Delete(root);
        }
    }
}
