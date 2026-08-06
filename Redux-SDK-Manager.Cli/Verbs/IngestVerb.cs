using System;
using Redux_SDK_Manager.Models;
using Redux_SDK_Manager.Services;

namespace Redux_SDK_Manager.Cli.Verbs;

/// <summary>Adopts an existing pre-manager project and brings it to a template version.</summary>
public static class IngestVerb
{
    public static int Run(CliContext context, IngestOptions options)
    {
        if (!context.GitAvailable)
        {
            return context.Output.Fail(ExitCode.GIT_UNAVAILABLE,
                $"git is not installed or not on PATH. Install it from {DownloadLinks.Git}");
        }

        // --name feeds the project-name prompt without asking (like --yes/--no for confirmations).
        context.Get<CliPromptService>().ForcedText = options.Name;

        context.Output.Progress($"Ingesting {options.Path} at {options.Version}...");
        try
        {
            context.Get<IProjectService>().IngestProject(options.Path!, TemplateVersion.Parse(options.Version!), options.EmbedSdk);
        }
        catch (Exception e)
        {
            return context.Output.Fail(ExitCode.FAILED, e.Message);
        }

        context.Output.Payload(
            new { ok = true, path = options.Path, version = options.Version },
            () => context.Output.Result($"Ingested {options.Path} at version {options.Version}."));

        return ExitCode.SUCCESS;
    }
}
