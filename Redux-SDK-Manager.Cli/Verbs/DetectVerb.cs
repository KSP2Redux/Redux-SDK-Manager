using Redux_SDK_Manager.Services;

namespace Redux_SDK_Manager.Cli.Verbs;

/// <summary>Reports the template version a project is stamped with.</summary>
public static class DetectVerb
{
    public static int Run(CliContext context, DetectOptions options)
    {
        var version = context.Get<ITemplateVersionService>().DetectProjectVersion(options.Path!);

        if (version is null)
        {
            context.Output.Payload(
                new { managed = false },
                () => context.Output.Result("not a managed project (no template.version)"));
            return ExitCode.SUCCESS;
        }

        context.Output.Payload(
            new { managed = true, version = version.Raw, channel = version.Channel.ToString() },
            () => context.Output.Result($"{version.Raw} ({version.Channel})"));

        return ExitCode.SUCCESS;
    }
}
