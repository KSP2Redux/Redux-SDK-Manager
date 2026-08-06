using System;
using System.Collections.Generic;
using System.Linq;
using Redux_SDK_Manager.Models;
using Redux_SDK_Manager.Services;

namespace Redux_SDK_Manager.Cli.Verbs;

/// <summary>Lists the template versions available in the distribution repo.</summary>
public static class VersionsVerb
{
    public static int Run(CliContext context)
    {
        if (!context.GitAvailable)
        {
            return context.Output.Fail(ExitCode.GIT_UNAVAILABLE,
                $"git is not installed or not on PATH. Install it from {DownloadLinks.Git}");
        }

        IReadOnlyList<TemplateVersion> versions;
        try
        {
            versions = context.Get<ITemplateCatalogService>().ListAvailableVersions();
        }
        catch (Exception e)
        {
            return context.Output.Fail(ExitCode.FAILED, e.Message);
        }

        context.Output.Payload(
            versions.Select(v => new { version = v.Raw, channel = v.Channel.ToString() }),
            () => context.Output.Table(
                ["VERSION", "CHANNEL"],
                versions.Select(v => (IReadOnlyList<string>)[v.Raw, v.Channel.ToString()]).ToList()));

        return ExitCode.SUCCESS;
    }
}
