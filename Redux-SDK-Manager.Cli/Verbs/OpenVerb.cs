using System;
using Redux_SDK_Manager.Services;

namespace Redux_SDK_Manager.Cli.Verbs;

/// <summary>
/// Opens a project by launching its matching installed editor directly, offering to install the
/// editor via Unity Hub when it's missing.
/// </summary>
public static class OpenVerb
{
    public static int Run(CliContext context, OpenOptions options)
    {
        if (options is { Yes: true, No: true })
        {
            return context.Output.Fail(ExitCode.USAGE_ERROR, "--yes and --no cannot be combined.");
        }

        context.Get<CliPromptService>().ForcedAnswer =
            options.Yes ? true
            : options.No ? false
            : null;

        OpenProjectResult result;
        try
        {
            result = context.Get<IUnityService>().OpenProject(options.Path!);
        }
        catch (Exception e)
        {
            return context.Output.Fail(ExitCode.FAILED, e.Message);
        }

        switch (result)
        {
            case OpenProjectResult.Opened:
                context.Output.Payload(
                    new { ok = true, path = options.Path, action = "opened" },
                    () => context.Output.Result($"Opening {options.Path} in Unity."));
                return ExitCode.SUCCESS;

            case OpenProjectResult.InstallStarted:
                context.Output.Payload(
                    new { ok = true, path = options.Path, action = "installing" },
                    () => context.Output.Result(
                        "Required editor isn't installed - opened Unity Hub to install it. Re-run open once it finishes."));
                return ExitCode.SUCCESS;

            case OpenProjectResult.InstallDeclined:
                context.Output.Payload(
                    new { ok = true, path = options.Path, action = "declined" },
                    () => context.Output.Result("Required editor isn't installed - skipped the install."));
                return ExitCode.SUCCESS;

            case OpenProjectResult.VersionUnknown:
                return context.Output.Fail(ExitCode.FAILED,
                    $"Could not determine the Unity version for '{options.Path}'.");

            case OpenProjectResult.HubUnavailable:
                return context.Output.Fail(ExitCode.HUB_UNAVAILABLE,
                    "The required editor isn't installed and Unity Hub is missing - install it manually.");

            default:
                return ExitCode.FAILED;
        }
    }
}
