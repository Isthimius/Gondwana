using System.Diagnostics;

namespace Gondwana.Cli.Commands;

/// <summary>
/// Runs external processes and captures their output.
/// </summary>
internal static class ProcessHelper
{
    /// <summary>
    /// Runs a command and returns its combined stdout+stderr output.
    /// </summary>
    public static string Run(string fileName, string arguments, out int exitCode)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

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
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                CreateNoWindow = false,
            };

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
}
