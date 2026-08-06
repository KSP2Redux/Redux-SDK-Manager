using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

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

    /// <summary>
    /// Starts an executable and returns immediately without waiting for it to exit, for
    /// launching long-running GUI apps (e.g. Unity Hub). Throws if it can't be started.
    /// </summary>
    void Start(string fileName, IReadOnlyList<string> arguments, string? workingDirectory = null);

    /// <summary>
    /// Opens a URL or custom-scheme link (e.g. <c>unityhub://</c>) through the OS shell so the
    /// registered protocol handler takes it. This is the same shell-execute approach the Launcher
    /// uses for external links. Throws if it can't be started.
    /// </summary>
    void OpenUrl(string url);

    /// <summary>
    /// Starts an executable and asynchronously waits for it to exit, returning its exit code. Output is
    /// left to the process (e.g. a headless Unity run writing its own log file). Cancelling kills the
    /// process. Throws if it can't be started.
    /// </summary>
    Task<int> RunToExitAsync(
        string fileName, IReadOnlyList<string> arguments, string? workingDirectory, CancellationToken cancellationToken);
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

        using var process = new Process();
        process.StartInfo = startInfo;
        process.Start();

        // Drain both streams concurrently before waiting, so a full stderr buffer can't
        // deadlock against a parent blocked on stdout.
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        process.WaitForExit();

        return new ProcessResult(process.ExitCode, stdout.GetAwaiter().GetResult(), stderr.GetAwaiter().GetResult());
    }

    public void Start(string fileName, IReadOnlyList<string> arguments, string? workingDirectory = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        if (!string.IsNullOrEmpty(workingDirectory))
        {
            startInfo.WorkingDirectory = workingDirectory;
        }

        // Fire-and-forget: disposing the handle doesn't terminate the launched process.
        Process.Start(startInfo)?.Dispose();
    }

    public void OpenUrl(string url)
    {
        // UseShellExecute lets the OS resolve the protocol handler (http, unityhub, ...). The handler
        // owns whatever it launches, so this is fire-and-forget.
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })?.Dispose();
    }

    public async Task<int> RunToExitAsync(
        string fileName, IReadOnlyList<string> arguments, string? workingDirectory, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
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

        using var process = new Process();
        process.StartInfo = startInfo;
        process.Start();

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Cancellation means "stop the setup" - take the launched process down with it.
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch { /* already gone */ }
            throw;
        }

        return process.ExitCode;
    }
}
