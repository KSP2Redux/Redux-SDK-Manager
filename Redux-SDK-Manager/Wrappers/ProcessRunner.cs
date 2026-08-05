using System.Collections.Generic;
using System.Diagnostics;

namespace Redux_SDK_Manager.Wrappers;

/// <summary>Result of running an external process to completion.</summary>
public record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

public interface IProcessRunner
{
    /// <summary>
    /// Runs an executable to completion, capturing its stdout/stderr. Throws if the
    /// executable cannot be started (e.g. not found on PATH).
    /// </summary>
    ProcessResult Run(string fileName, IReadOnlyList<string> arguments, string? workingDirectory = null);
}

public class ProcessRunner : IProcessRunner
{
    public ProcessResult Run(string fileName, IReadOnlyList<string> arguments, string? workingDirectory = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (!string.IsNullOrEmpty(workingDirectory))
        {
            startInfo.WorkingDirectory = workingDirectory;
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        // Drain both streams concurrently before waiting, so a full stderr buffer can't
        // deadlock against a parent blocked on stdout.
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();

        return new ProcessResult(process.ExitCode, stdout.GetAwaiter().GetResult(), stderr.GetAwaiter().GetResult());
    }
}
