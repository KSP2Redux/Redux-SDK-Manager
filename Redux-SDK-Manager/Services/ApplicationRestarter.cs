using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Redux_SDK_Manager.Services;

/// <summary>
/// Launches a freshly downloaded build so it can replace the running executable, then exits. The
/// downloaded exe re-runs with the swap arguments handled in <c>Program</c> (kill parent, copy over,
/// relaunch, clean up).
/// </summary>
public interface IApplicationRestarter
{
    /// <summary>
    /// True only for a single-file published build. Self-update copies one exe over another, so a
    /// multi-file (development) build must not attempt it.
    /// </summary>
    bool IsSingleFileDeployment { get; }

    /// <summary>Launches the downloaded exe with the swap arguments and exits this process.</summary>
    void LaunchUpdaterAndExit(string downloadedExePath);
}

public sealed class ApplicationRestarter : IApplicationRestarter
{
    // A single-file deployment reports an empty assembly Location.
    public bool IsSingleFileDeployment
    {
#pragma warning disable IL3000
        get => string.IsNullOrEmpty(Assembly.GetEntryAssembly()?.Location);
#pragma warning restore IL3000
    }

    public void LaunchUpdaterAndExit(string downloadedExePath)
    {
        var self = Path.GetFullPath(Environment.ProcessPath!);
        var startInfo = new ProcessStartInfo
        {
            FileName = downloadedExePath,
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(downloadedExePath),
        };
        startInfo.ArgumentList.Add("--pid");
        startInfo.ArgumentList.Add(Environment.ProcessId.ToString());
        startInfo.ArgumentList.Add("--exe");
        startInfo.ArgumentList.Add(self);

        Process.Start(startInfo);
        Environment.Exit(0);
    }
}
