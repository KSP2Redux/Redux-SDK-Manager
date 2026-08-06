using System;
using Redux_SDK_Manager.Models;
using Redux_SDK_Manager.Services;

namespace Redux_SDK_Manager.Cli.Verbs;

/// <summary>Creates a new project from a template version.</summary>
public static class CreateVerb
{
    public static int Run(CliContext context, CreateOptions options)
    {
        if (!context.GitAvailable)
        {
            return context.Output.Fail(ExitCode.GIT_UNAVAILABLE, "git is not installed or not on PATH.");
        }

        // --name feeds the project-name prompt without asking (like --yes/--no for confirmations).
        context.Get<CliPromptService>().ForcedText = options.Name;

        context.Output.Progress($"Creating project at {options.Path} from {options.Version}...");
        try
        {
            context.Get<IProjectService>().CreateProject(TemplateVersion.Parse(options.Version!), options.Path!);
        }
        catch (Exception e)
        {
            return context.Output.Fail(ExitCode.FAILED, e.Message);
        }

        context.Output.Payload(
            new { ok = true, path = options.Path, version = options.Version },
            () => context.Output.Result($"Created project at {options.Path} (version {options.Version})."));

        return ExitCode.SUCCESS;
    }
}
