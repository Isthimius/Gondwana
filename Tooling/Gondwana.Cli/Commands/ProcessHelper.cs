using System.Diagnostics;

namespace Gondwana.Cli.Commands;

/// <summary>
/// Runs external processes and captures their output.
/// </summary>
internal static class ProcessHelper
{
    /// <summary>
    /// Runs a command, streaming output live to the console.
    /// Watches for an ASP.NET Core "Now listening on: &lt;url&gt;" line and opens
    /// the URL in the default browser as soon as it appears.
    /// Returns the exit code.
    /// </summary>
    public static int RunLiveAndOpenUrl(string fileName, IEnumerable<string> arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                CreateNoWindow = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            foreach (var arg in arguments)
                psi.ArgumentList.Add(arg);

            using var process = Process.Start(psi);
            if (process is null)
                return -1;

            var urlOpened = false;
            var syncLock = new object();

            void HandleLine(string? line)
            {
                if (line is null) return;
                Console.WriteLine(line);

                if (urlOpened) return;
                var url = ExtractListeningUrl(line);
                if (url is null) return;

                lock (syncLock)
                {
                    if (urlOpened) return;
                    urlOpened = true;
                }

                OpenBrowser(url);
            }

            process.OutputDataReceived += (_, e) => HandleLine(e.Data);
            process.ErrorDataReceived += (_, e) => HandleLine(e.Data);
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            process.WaitForExit();
            return process.ExitCode;
        }
        catch
        {
            return -1;
        }
    }

    private static string? ExtractListeningUrl(string line)
    {
        const string marker = "Now listening on: ";
        var idx = line.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        var url = line[(idx + marker.Length)..].Trim();
        return url.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? url : null;
    }

    private static void OpenBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            Console.WriteLine($"Could not open browser automatically. Navigate to: {url}");
        }
    }

    /// <summary>
    /// Runs a command and returns its combined stdout+stderr output.
    /// </summary>
    public static string Run(string fileName, string arguments, out int exitCode)
    {
        try
        {
            var resolved = ResolveWindowsExecutable(fileName);
            ProcessStartInfo psi;
            if (IsWindowsScript(resolved))
            {
                psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"{resolved}\" {arguments}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
            }
            else
            {
                psi = new ProcessStartInfo
                {
                    FileName = resolved,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
            }

            using var process = Process.Start(psi);
            if (process is null)
            {
                exitCode = -1;
                return string.Empty;
            }

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            exitCode = process.ExitCode;

            return stdout + stderr;
        }
        catch
        {
            exitCode = -1;
            return string.Empty;
        }
    }

    /// <summary>
    /// Runs a command, streaming output live to the console.
    /// Returns the exit code.
    /// </summary>
    public static int RunLive(string fileName, string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = false,
            };

            using var process = Process.Start(psi);
            if (process is null)
                return -1;

            process.WaitForExit();
            return process.ExitCode;
        }
        catch
        {
            return -1;
        }
    }

    /// <summary>
    /// Runs a command with individual argument tokens (no shell quoting needed).
    /// Streams output live to the console. Returns the exit code.
    /// </summary>
    public static int RunLive(string fileName, IEnumerable<string> arguments)
    {
        try
        {
            var resolved = ResolveWindowsExecutable(fileName);
            var psi = new ProcessStartInfo
            {
                FileName = IsWindowsScript(resolved) ? "cmd.exe" : resolved,
                UseShellExecute = false,
                CreateNoWindow = false,
            };

            if (IsWindowsScript(resolved))
            {
                psi.ArgumentList.Add("/c");
                psi.ArgumentList.Add(resolved);
            }

            foreach (var arg in arguments)
                psi.ArgumentList.Add(arg);

            using var process = Process.Start(psi);
            if (process is null)
                return -1;

            process.WaitForExit();
            return process.ExitCode;
        }
        catch
        {
            return -1;
        }
    }

    private static string ResolveWindowsExecutable(string fileName)
    {
        if (!OperatingSystem.IsWindows() || Path.IsPathRooted(fileName) || Path.HasExtension(fileName))
            return fileName;

        var pathExt = Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD";
        var searchPaths = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        foreach (var ext in pathExt.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var dir in searchPaths)
            {
                var candidate = Path.Combine(dir.Trim(), fileName + ext);
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        return fileName;
    }

    private static bool IsWindowsScript(string filePath) =>
        filePath.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) ||
        filePath.EndsWith(".bat", StringComparison.OrdinalIgnoreCase);
}
