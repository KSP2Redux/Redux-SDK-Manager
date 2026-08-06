using Redux_SDK_Manager.Services;

namespace Redux_SDK_Manager.Cli.Verbs;

/// <summary>Checks that git and Unity Hub are available.</summary>
public static class DoctorVerb
{
    public static int Run(CliContext context)
    {
        var gitInstalled = context.Get<IGitService>().IsInstalled();
        var hubInstalled = context.Get<IUnityService>().IsHubInstalled();

        context.Output.Payload(
            new { git = gitInstalled, unityHub = hubInstalled },
            () =>
            {
                context.Output.Result($"git:        {(gitInstalled ? "installed" : "MISSING")}");
                context.Output.Result($"Unity Hub:  {(hubInstalled ? "installed" : "MISSING")}");
            });

        return gitInstalled && hubInstalled ? ExitCode.SUCCESS : ExitCode.FAILED;
    }
}
