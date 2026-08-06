using Avalonia;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace Redux_SDK_Manager;

class Program
{
    private const int StageSettleDelayMs = 1000;

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // A downloaded update re-runs the exe with swap arguments before the app really starts.
        if (TryRunUpdateSwap(args)) return;

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Three-step self-replace, matching the restart the update service triggers:
    //   1. (normal launch) - no swap args, fall through to start the app.
    //   2. --pid P --exe E : this is the freshly downloaded build. Kill the old process, copy self
    //      over E, relaunch E with --pid/--prev, and exit.
    //   3. --pid P --prev V : running from the real location again. Kill stage 2, delete the temp
    //      download V, and fall through to start the app.
    // Returns true when it handled a swap step and the caller should exit without starting the app.
    private static bool TryRunUpdateSwap(string[] args)
    {
        var pid = GetArg(args, "--pid");
        var exe = GetArg(args, "--exe");
        var prev = GetArg(args, "--prev");

        if (pid is null) return false;

        if (exe is not null)
        {
            TryKillProcess(pid);
            Thread.Sleep(StageSettleDelayMs);
            try
            {
                var whereAmI = Path.GetFullPath(Environment.ProcessPath!);
                File.Copy(whereAmI, exe, true);
                Thread.Sleep(StageSettleDelayMs);

                var startInfo = new ProcessStartInfo
                {
                    FileName = exe,
                    UseShellExecute = false,
                    WorkingDirectory = Path.GetDirectoryName(exe),
                };
                startInfo.ArgumentList.Add("--pid");
                startInfo.ArgumentList.Add(Environment.ProcessId.ToString());
                startInfo.ArgumentList.Add("--prev");
                startInfo.ArgumentList.Add(whereAmI);
                Process.Start(startInfo);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Update step 2 failed: {e}");
            }
            return true;
        }

        if (prev is not null)
        {
            TryKillProcess(pid);
            Thread.Sleep(StageSettleDelayMs);
            try { File.Delete(prev); }
            catch (Exception e) { Console.Error.WriteLine($"Update step 3 cleanup failed: {e}"); }
            // Fall through: this process is the updated app at its real location.
            return false;
        }

        return false;
    }

    private static string? GetArg(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name) return args[i + 1];
        }
        return null;
    }

    private static void TryKillProcess(string pid)
    {
        if (!int.TryParse(pid, out var id)) return;
        try
        {
            var process = Process.GetProcessById(id);
            process.Kill(false);
            process.WaitForExit();
        }
        catch
        {
            // The parent may already have exited or be inaccessible - expected, not fatal.
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
