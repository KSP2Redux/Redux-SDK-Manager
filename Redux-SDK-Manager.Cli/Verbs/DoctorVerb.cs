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
                context.Output.Result($"git:        {Status(gitInstalled, DownloadLinks.Git)}");
                context.Output.Result($"Unity Hub:  {Status(hubInstalled, DownloadLinks.UnityHub)}");
            });

        return gitInstalled && hubInstalled ? ExitCode.SUCCESS : ExitCode.FAILED;
    }

    private static string Status(bool installed, string downloadUrl)
        => installed ? "installed" : $"MISSING (get it at {downloadUrl})";
}
