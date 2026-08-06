using System;
using Redux_SDK_Manager.Models;
using Redux_SDK_Manager.Services;

namespace Redux_SDK_Manager.Cli.Verbs;

/// <summary>
/// Clones a repository URL and adds the result as a project: imports it as-is if it's already a
/// managed project, otherwise ingests it at the given template version. Automated setup follows.
/// </summary>
public static class CloneVerb
{
    public static int Run(CliContext context, CloneOptions options)
    {
        if (!context.GitAvailable)
        {
            return context.Output.Fail(ExitCode.GIT_UNAVAILABLE,
                $"git is not installed or not on PATH. Install it from {DownloadLinks.Git}");
        }

        context.Output.Progress($"Cloning {options.Url} into {options.Path}...");
        try
        {
            context.Get<IGitService>().CloneRepository(options.Url!, options.Path!);
        }
        catch (Exception e)
        {
            return context.Output.Fail(ExitCode.FAILED, e.Message);
        }

        var projectService = context.Get<IProjectService>();
        try
        {
            // An already-managed clone is imported unchanged; anything else is ingested at --version.
            if (context.Get<ITemplateVersionService>().DetectProjectVersion(options.Path!) is not null)
            {
                var version = projectService.ImportProject(options.Path!, options.EmbedSdk);
                context.Output.Payload(
                    new { ok = true, path = options.Path, version = version.Raw, cloned = true },
                    () => context.Output.Result($"Cloned and imported {options.Path} (version {version.Raw})."));
            }
            else
            {
                if (string.IsNullOrWhiteSpace(options.Version))
                {
                    return context.Output.Fail(ExitCode.USAGE_ERROR,
                        "The cloned repository isn't a managed project. Pass --version to ingest it at a template version.");
                }

                // --name feeds the project-name prompt without asking (like --yes/--no for confirmations).
                context.Get<CliPromptService>().ForcedText = options.Name;
                projectService.IngestProject(options.Path!, TemplateVersion.Parse(options.Version), options.EmbedSdk);
                context.Output.Payload(
                    new { ok = true, path = options.Path, version = options.Version, cloned = true },
                    () => context.Output.Result($"Cloned and ingested {options.Path} at version {options.Version}."));
            }
        }
        catch (Exception e)
        {
            return context.Output.Fail(ExitCode.FAILED, e.Message);
        }

        SetupRunner.RunAfter(context, options.Path!, options);
        return ExitCode.SUCCESS;
    }
}
