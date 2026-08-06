using System;
using Redux_SDK_Manager.Services;

namespace Redux_SDK_Manager.Cli.Verbs;

/// <summary>Opens a project in Unity via Unity Hub.</summary>
public static class OpenVerb
{
    public static int Run(CliContext context, OpenOptions options)
    {
        var unity = context.Get<IUnityService>();
        if (!unity.IsHubInstalled())
        {
            return context.Output.Fail(ExitCode.HUB_UNAVAILABLE, "Unity Hub is not installed.");
        }

        try
        {
            unity.OpenProject(options.Path!);
        }
        catch (Exception e)
        {
            return context.Output.Fail(ExitCode.FAILED, e.Message);
        }

        context.Output.Payload(
            new { ok = true, path = options.Path },
            () => context.Output.Result($"Opening {options.Path} via Unity Hub."));

        return ExitCode.SUCCESS;
    }
}
