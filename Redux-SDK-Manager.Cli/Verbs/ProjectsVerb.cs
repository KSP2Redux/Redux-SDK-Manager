using System.Collections.Generic;
using System.Linq;
using Redux_SDK_Manager.Services;

namespace Redux_SDK_Manager.Cli.Verbs;

/// <summary>Lists the projects the manager is tracking, with each one's detected version.</summary>
public static class ProjectsVerb
{
    public static int Run(CliContext context)
    {
        var versionService = context.Get<ITemplateVersionService>();
        var rows = context.Get<IConfigService>().Config.ProjectPaths
            .Select(path =>
            {
                var version = versionService.DetectProjectVersion(path);
                return new { path, version = version?.Raw, channel = version?.Channel.ToString() };
            })
            .ToList();

        context.Output.Payload(
            rows,
            () => context.Output.Table(
                ["PATH", "VERSION", "CHANNEL"],
                rows.Select(r => (IReadOnlyList<string>)[r.path, r.version ?? "?", r.channel ?? "?"]).ToList()));

        return ExitCode.SUCCESS;
    }
}
