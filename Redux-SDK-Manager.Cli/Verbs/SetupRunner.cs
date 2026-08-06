using System;
using System.Threading;
using Redux_SDK_Manager.Services;

namespace Redux_SDK_Manager.Cli.Verbs;

/// <summary>
/// Runs the automated ThunderKit import + "Import KSP2 to Editor" pipeline after a project operation,
/// streaming each step to stderr. Honors the config toggle and --no-setup, skips already-imported
/// projects, and resolves the KSP2 path from --ksp2 or the config.
/// </summary>
public static class SetupRunner
{
    public static void RunAfter(CliContext context, string projectPath, ISetupCapableOptions options)
    {
        if (options.NoSetup) return;

        var config = context.Get<IConfigService>().Config;
        var explicitKsp2 = !string.IsNullOrWhiteSpace(options.Ksp2);
        // Passing --ksp2 is itself a request to run setup even if the config toggle is off.
        if (!config.AutoRunProjectSetup && !explicitKsp2) return;

        var setup = context.Get<IProjectSetupService>();
        if (setup.IsAlreadySetUp(projectPath))
        {
            context.Output.Progress("Project already set up, skipping automated setup.");
            return;
        }

        var ksp2 = explicitKsp2 ? options.Ksp2! : config.Ksp2ExePath;
        if (string.IsNullOrWhiteSpace(ksp2))
        {
            context.Output.Progress("No KSP2 path set, skipping automated setup (set it in the GUI or pass --ksp2).");
            return;
        }

        context.Output.Progress("Setting up project (this launches Unity and can take a few minutes)...");
        var progress = new Progress<ProjectSetupProgress>(p => context.Output.Progress(ProjectSetupService.DescribeProgress(p)));

        var result = setup.RunSetupAsync(projectPath, ksp2, progress, CancellationToken.None).GetAwaiter().GetResult();
        switch (result)
        {
            case ProjectSetupResult.Completed:
                context.Output.Progress("Project setup complete.");
                break;
            case ProjectSetupResult.EditorMissing:
                context.Output.Warn(ProjectSetupService.EditorMissingMessage);
                break;
            case ProjectSetupResult.UnityVersionMismatch:
                var unity = context.Get<IUnityService>();
                context.Output.Warn(ProjectSetupService.UnityMismatchMessage(
                    unity.GetGameUnityVersion(ksp2), unity.GetProjectUnityVersion(projectPath)));
                break;
            case ProjectSetupResult.NoGamePath:
                context.Output.Warn("The KSP2 path is not valid; automated setup was skipped.");
                break;
            case ProjectSetupResult.Failed:
                context.Output.Warn($"Automated setup did not finish. See {setup.SetupLogPath(projectPath)}, "
                    + "or open the project in Unity to finish setup by hand.");
                break;
        }
    }
}
