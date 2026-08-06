using System;
using Redux_SDK_Manager.Models;
using Redux_SDK_Manager.Services;

namespace Redux_SDK_Manager.Cli.Verbs;

/// <summary>Upgrades a managed project to a template version.</summary>
public static class UpgradeVerb
{
    public static int Run(CliContext context, UpgradeOptions options)
    {
        if (!context.GitAvailable)
        {
            return context.Output.Fail(ExitCode.GIT_UNAVAILABLE, "git is not installed or not on PATH.");
        }

        context.Output.Progress($"Upgrading {options.Path} to {options.Version}...");
        try
        {
            context.Get<IProjectService>().UpgradeProject(options.Path!, TemplateVersion.Parse(options.Version!));
        }
        catch (Exception e)
        {
            return context.Output.Fail(ExitCode.FAILED, e.Message);
        }

        context.Output.Payload(
            new { ok = true, path = options.Path, version = options.Version },
            () => context.Output.Result($"Upgraded {options.Path} to version {options.Version}."));

        return ExitCode.SUCCESS;
    }
}
