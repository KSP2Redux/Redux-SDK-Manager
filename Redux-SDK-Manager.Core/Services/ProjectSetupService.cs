using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Redux_SDK_Manager.Wrappers;

namespace Redux_SDK_Manager.Services;

/// <summary>Outcome of <see cref="IProjectSetupService.RunSetupAsync"/>.</summary>
public enum ProjectSetupResult
{
    /// <summary>The import and the pipeline both finished.</summary>
    Completed,

    /// <summary>The project already carries an imported game, so nothing was run.</summary>
    AlreadyDone,

    /// <summary>The project's Unity editor isn't installed, so setup can't run.</summary>
    EditorMissing,

    /// <summary>No usable KSP2 executable was supplied.</summary>
    NoGamePath,

    /// <summary>Setup was cancelled before it finished.</summary>
    Cancelled,

    /// <summary>Setup ran but did not reach completion.</summary>
    Failed
}

/// <summary>A step report from a running setup: the phase and the human-facing step within it.</summary>
public record ProjectSetupProgress(string Phase, string Step);

/// <summary>
/// Drives a project's one-click setup by launching the project's Unity editor headlessly against the
/// template's automated-setup script (<c>ReduxAutomatedSetup</c>). The script runs in two phases, each
/// its own editor launch: phase A drives the ThunderKit game import, phase B runs the "Import KSP2 to
/// Editor" pipeline. Progress is read from a status file the script writes.
/// </summary>
public interface IProjectSetupService
{
    /// <summary>True if the project already looks imported (its game-assemblies package is present).</summary>
    bool IsAlreadySetUp(string projectPath);

    /// <summary>The path of the headless Unity log a setup run writes, for pointing the user at on failure.</summary>
    string SetupLogPath(string projectPath);

    /// <summary>
    /// Runs the two-phase setup, reporting each step through <paramref name="progress"/>. Returns without
    /// running when the project is already set up, the editor is missing, or no game path is given.
    /// </summary>
    Task<ProjectSetupResult> RunSetupAsync(
        string projectPath, string ksp2ExePath, IProgress<ProjectSetupProgress>? progress, CancellationToken cancellationToken);
}

public class ProjectSetupService(
    IFileSystem fileSystem, IUnityService unityService, IProcessRunner processRunner, ILogService logService)
    : IProjectSetupService
{
    /// <summary>Shown when the project's editor isn't installed. Shared verbatim by both frontends.</summary>
    public const string EditorMissingMessage = "Project unity version not detected, automated setup will not happen.";

    private const string ExecuteMethod = "Redux.Template.Editor.ReduxAutomatedSetup.RunSetup";
    private const string ImportedGamePackage = "KSP2_x64"; // Packages/KSP2_x64 - ThunderKit's imported game package
    private const string StatusFileName = "redux-setup-status.txt";
    private const string LogFileName = "redux-setup.log";
    private const int PollIntervalMs = 400;

    // A generous per-phase ceiling: real imports take minutes, but a hung editor shouldn't leave a
    // project "setting up" forever. On expiry the editor is killed and the phase is treated as failed.
    private static readonly TimeSpan PhaseTimeout = TimeSpan.FromMinutes(30);

    private const string PhaseImportDone = "import-done";
    private const string PhaseDone = "done";
    private const string PhaseError = "error";

    public bool IsAlreadySetUp(string projectPath)
        => fileSystem.Directory.Exists(fileSystem.Path.Combine(projectPath, "Packages", ImportedGamePackage));

    public string SetupLogPath(string projectPath)
        => fileSystem.Path.Combine(projectPath, "Library", LogFileName);

    public async Task<ProjectSetupResult> RunSetupAsync(
        string projectPath, string ksp2ExePath, IProgress<ProjectSetupProgress>? progress, CancellationToken cancellationToken)
    {
        if (IsAlreadySetUp(projectPath))
        {
            logService.Info($"Project '{projectPath}' already imported; skipping automated setup.");
            return ProjectSetupResult.AlreadyDone;
        }

        if (string.IsNullOrWhiteSpace(ksp2ExePath) || !fileSystem.File.Exists(ksp2ExePath))
        {
            logService.Warn($"No usable KSP2 executable ('{ksp2ExePath}'); skipping automated setup.");
            return ProjectSetupResult.NoGamePath;
        }

        var editorExe = ResolveEditor(projectPath);
        if (editorExe is null)
        {
            logService.Warn($"Editor for '{projectPath}' not installed; {EditorMissingMessage}");
            return ProjectSetupResult.EditorMissing;
        }

        var libraryDir = fileSystem.Path.Combine(projectPath, "Library");
        var statusPath = fileSystem.Path.Combine(libraryDir, StatusFileName);
        var logPath = fileSystem.Path.Combine(libraryDir, LogFileName);
        fileSystem.Directory.CreateDirectory(libraryDir);
        TryDelete(statusPath); // a stale "done" from a prior run must not short-circuit the script

        var args = BuildArgs(projectPath, ksp2ExePath, statusPath, logPath);

        using var pollCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var pollTask = Task.Run(() => PollStatusAsync(statusPath, progress, pollCts.Token), CancellationToken.None);
        try
        {
            logService.Info($"Automated setup phase A (import) for '{projectPath}'.");
            var phase = await RunEditorAsync(editorExe, projectPath, args, statusPath, cancellationToken);

            if (phase == PhaseImportDone)
            {
                logService.Info($"Automated setup phase B (pipeline) for '{projectPath}'.");
                phase = await RunEditorAsync(editorExe, projectPath, args, statusPath, cancellationToken);
            }

            return phase == PhaseDone ? ProjectSetupResult.Completed : ProjectSetupResult.Failed;
        }
        catch (OperationCanceledException)
        {
            logService.Warn($"Automated setup for '{projectPath}' was cancelled.");
            return ProjectSetupResult.Cancelled;
        }
        catch (Exception e)
        {
            logService.Error($"Automated setup for '{projectPath}' failed.", e);
            return ProjectSetupResult.Failed;
        }
        finally
        {
            pollCts.Cancel();
            try { await pollTask; } catch { /* poll loop only ever ends by cancellation */ }
        }
    }

    private async Task<string> RunEditorAsync(
        string editorExe, string projectPath, IReadOnlyList<string> args, string statusPath, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(PhaseTimeout);
        try
        {
            await processRunner.RunToExitAsync(editorExe, args, projectPath, timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The linked token fired on the timeout, not an external cancel: the editor was killed.
            logService.Warn($"Automated setup phase for '{projectPath}' timed out after {PhaseTimeout.TotalMinutes:0} minutes.");
            return PhaseError;
        }

        return ReadPhase(statusPath);
    }

    private string? ResolveEditor(string projectPath)
    {
        var version = unityService.GetProjectUnityVersion(projectPath);
        if (string.IsNullOrEmpty(version)) return null;

        return unityService.DetectInstalls()
            .FirstOrDefault(i => string.Equals(i.Version, version, StringComparison.OrdinalIgnoreCase))
            ?.ExecutablePath;
    }

    private IReadOnlyList<string> BuildArgs(string projectPath, string ksp2ExePath, string statusPath, string logPath) =>
    [
        "-batchmode",
        "-ignoreCompilerErrors",   // load past the phase-A compile errors instead of dropping into Safe Mode
        "-disable-assembly-updater", // suppress ThunderKit's mid-import editor restart
        "-projectPath", projectPath,
        "-executeMethod", ExecuteMethod,
        "-redux-run-setup",
        $"-redux-ksp2={ksp2ExePath}",
        $"-redux-status={statusPath}",
        "-logFile", logPath
    ];

    private async Task PollStatusAsync(string statusPath, IProgress<ProjectSetupProgress>? progress, CancellationToken token)
    {
        if (progress is null) return;

        string? last = null;
        while (!token.IsCancellationRequested)
        {
            var line = ReadStatusLine(statusPath);
            if (line is not null && line != last)
            {
                last = line;
                progress.Report(ParseProgress(line));
            }

            try { await Task.Delay(PollIntervalMs, token); }
            catch (OperationCanceledException) { break; }
        }
    }

    private string ReadPhase(string statusPath)
    {
        var line = ReadStatusLine(statusPath);
        if (line is null) return "";
        var bar = line.IndexOf('|');
        return (bar >= 0 ? line[..bar] : line).Trim();
    }

    private string? ReadStatusLine(string statusPath)
    {
        try
        {
            return fileSystem.File.Exists(statusPath) ? fileSystem.File.ReadAllText(statusPath).Trim() : null;
        }
        catch
        {
            // The script may be mid-write; try again on the next poll.
            return null;
        }
    }

    private static ProjectSetupProgress ParseProgress(string line)
    {
        var bar = line.IndexOf('|');
        return bar >= 0
            ? new ProjectSetupProgress(line[..bar].Trim(), line[(bar + 1)..].Trim())
            : new ProjectSetupProgress(line.Trim(), "");
    }

    /// <summary>Turns a raw phase/step report into a short line for a status row or console.</summary>
    public static string DescribeProgress(ProjectSetupProgress p) => p.Phase switch
    {
        "import" => string.IsNullOrEmpty(p.Step) ? "Importing game..." : $"Importing game: {p.Step}",
        PhaseImportDone => "Game imported, preparing...",
        "pipeline" => "Copying game data...",
        PhaseDone => "Setup complete.",
        PhaseError => string.IsNullOrEmpty(p.Step) ? "Setup failed." : $"Setup failed: {p.Step}",
        _ => "Setting up..."
    };

    private void TryDelete(string path)
    {
        try { if (fileSystem.File.Exists(path)) fileSystem.File.Delete(path); }
        catch { /* best effort */ }
    }
}
