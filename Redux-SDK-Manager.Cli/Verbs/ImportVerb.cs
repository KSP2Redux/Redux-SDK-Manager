using System;
using Redux_SDK_Manager.Services;

namespace Redux_SDK_Manager.Cli.Verbs;

/// <summary>Registers an already-managed project with the manager, without modifying it.</summary>
public static class ImportVerb
{
    public static int Run(CliContext context, ImportOptions options)
    {
        try
        {
            var version = context.Get<IProjectService>().ImportProject(options.Path!);

            context.Output.Payload(
                new { ok = true, path = options.Path, version = version.Raw, channel = version.Channel.ToString() },
                () => context.Output.Result($"Imported {options.Path} (version {version.Raw})."));

            return ExitCode.SUCCESS;
        }
        catch (Exception e)
        {
            return context.Output.Fail(ExitCode.FAILED, e.Message);
        }
    }
}
