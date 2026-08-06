using System.Collections.Generic;
using System.Linq;
using Redux_SDK_Manager.Services;

namespace Redux_SDK_Manager.Cli.Verbs;

/// <summary>Lists the Unity editors installed via Unity Hub.</summary>
public static class UnityVerb
{
    public static int Run(CliContext context)
    {
        var installs = context.Get<IUnityService>().DetectInstalls();

        context.Output.Payload(
            installs.Select(i => new { version = i.Version, path = i.ExecutablePath }),
            () => context.Output.Table(
                ["VERSION", "PATH"],
                installs.Select(i => (IReadOnlyList<string>)[i.Version, i.ExecutablePath]).ToList()));

        return ExitCode.SUCCESS;
    }
}
