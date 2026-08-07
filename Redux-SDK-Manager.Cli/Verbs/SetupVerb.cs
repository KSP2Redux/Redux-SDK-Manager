using System.Threading;
using Redux_SDK_Manager.Services;

namespace Redux_SDK_Manager.Cli.Verbs;

/// <summary>
/// Runs the automated ThunderKit import + "Import KSP2 to Editor" pipeline on an existing project after
/// the fact. Unlike the auto-run that follows create/ingest, this always runs when asked.
/// </summary>
public static class SetupVerb
{
    public static int Run(CliContext context, SetupOptions options)
    {
        var setup = context.Get<IProjectSetupService>();
        if (setup.IsAlreadySetUp(options.Path!))
        {
            context.Output.Payload(
                new { ok = true, path = options.Path, alreadySetUp = true },
                () => context.Output.Result("Project already set up, nothing to do."));
            return ExitCode.SUCCESS;
        }

        var ksp2 = !string.IsNullOrWhiteSpace(options.Ksp2)
            ? options.Ksp2!
            : context.Get<IConfigService>().Config.Ksp2ExePath;
        if (string.IsNullOrWhiteSpace(ksp2))
        {
            return context.Output.Fail(ExitCode.USAGE_ERROR,
                "No KSP2 path. Pass --ksp2 or set it in the GUI first.");
        }

        context.Output.Progress("Setting up project (this launches Unity and can take a few minutes)...");
        var progress = new System.Progress<ProjectSetupProgress>(p => context.Output.Progress(ProjectSetupService.DescribeProgress(p)));

        var result = setup.RunSetupAsync(options.Path!, ksp2, progress, CancellationToken.None).GetAwaiter().GetResult();
        switch (result)
        {
            case ProjectSetupResult.Completed:
                context.Output.Payload(
                    new { ok = true, path = options.Path },
                    () => context.Output.Result("Project setup complete."));
                return ExitCode.SUCCESS;

            case ProjectSetupResult.EditorMissing:
                return context.Output.Fail(ExitCode.FAILED, ProjectSetupService.EditorMissingMessage);

            case ProjectSetupResult.UnityVersionMismatch:
                var unity = context.Get<IUnityService>();
                return context.Output.Fail(ExitCode.FAILED,
                    ProjectSetupService.UnityMismatchMessage(unity.GetGameUnityVersion(ksp2), unity.GetProjectUnityVersion(options.Path!)));

            case ProjectSetupResult.NoGamePath:
                return context.Output.Fail(ExitCode.FAILED, "The KSP2 path is not valid.");

            default:
                return context.Output.Fail(ExitCode.FAILED,
                    $"Setup did not finish. See {setup.SetupLogPath(options.Path!)}, or open the project in Unity to finish by hand.");
        }
    }
}
