using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Redux_SDK_Manager.Cli.Verbs;
using Redux_SDK_Manager.Models;
using Redux_SDK_Manager.Services;

namespace Redux_SDK_Manager.Cli;

public static class Program
{
    public static int Main(string[] args)
    {
        // Help and parse errors go to stderr so stdout stays the data channel in every invocation.
        var parser = new Parser(settings =>
        {
            settings.HelpWriter = Console.Error;
            settings.CaseInsensitiveEnumValues = true;
        });

        return parser.ParseArguments<
                VersionsOptions, CreateOptions, IngestOptions, UpgradeOptions, ImportOptions,
                CloneOptions, SetupOptions, DetectOptions, OpenOptions, UnityOptions, ProjectsOptions, DoctorOptions>(args)
            .MapResult(
                (VersionsOptions o) => Run(o, VersionsVerb.Run),
                (CreateOptions o) => Run(o, ctx => CreateVerb.Run(ctx, o)),
                (IngestOptions o) => Run(o, ctx => IngestVerb.Run(ctx, o)),
                (UpgradeOptions o) => Run(o, ctx => UpgradeVerb.Run(ctx, o)),
                (ImportOptions o) => Run(o, ctx => ImportVerb.Run(ctx, o)),
                (CloneOptions o) => Run(o, ctx => CloneVerb.Run(ctx, o)),
                (SetupOptions o) => Run(o, ctx => SetupVerb.Run(ctx, o)),
                (DetectOptions o) => Run(o, ctx => DetectVerb.Run(ctx, o)),
                (OpenOptions o) => Run(o, ctx => OpenVerb.Run(ctx, o)),
                (UnityOptions o) => Run(o, UnityVerb.Run),
                (ProjectsOptions o) => Run(o, ProjectsVerb.Run),
                (DoctorOptions o) => Run(o, DoctorVerb.Run),
                _ => ExitCode.USAGE_ERROR);
    }

    // Capture the real stdout for results, then point Console.Out at stderr so the Core LogService's
    // console writes can't corrupt stdout or interleave with a JSON document.
    private static int Run(BaseOptions options, Func<CliContext, int> verb)
    {
        var results = Console.Out;
        Console.SetOut(Console.Error);
        var output = new CliOutput(results, options.IsJson);

        try
        {
            var services = CliServiceProvider.Build();

            // LogService writes a session header to the console from its constructor, so keep it out
            // of the CLI streams by muting the console across the one resolve that constructs it.
            if (!options.IsVerbose)
            {
                Console.SetOut(TextWriter.Null);
            }

            var log = services.GetRequiredService<ILogService>();
            Console.SetOut(Console.Error);

            // Info is right for an hours-long GUI session and far too chatty for a command that runs
            // for seconds. Warn and above still reaches stderr, and the full log still hits the file.
            log.MinimumLevel = options.IsVerbose ? LogLevel.Debug : LogLevel.Warn;

            // Kick off a best-effort update check alongside the verb; notify (never apply) afterwards.
            var updateCheck = StartUpdateCheck(services);
            var exit = verb(new CliContext(services, output));
            NotifyIfUpdateAvailable(updateCheck);
            return exit;
        }
        catch (Exception e)
        {
            return output.Fail(ExitCode.FAILED, e.Message);
        }
    }

    // Runs the update check off-thread with a tight timeout so it overlaps the verb. Opt out with
    // REDUX_NO_UPDATE_CHECK. Never throws - a failed check just yields no notice.
    private static Task<UpdateCheckResult?> StartUpdateCheck(IServiceProvider services)
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("REDUX_NO_UPDATE_CHECK")))
            return Task.FromResult<UpdateCheckResult?>(null);

        var updateService = services.GetService<IUpdateService>();
        if (updateService is null) return Task.FromResult<UpdateCheckResult?>(null);

        return Task.Run(async () =>
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
                return await updateService.CheckForUpdateAsync(cts.Token);
            }
            catch
            {
                return null;
            }
        });
    }

    // The CLI notifies only - it never downloads or replaces itself. Waits briefly for the overlapping
    // check so a fast command isn't held up if the network is slow.
    private static void NotifyIfUpdateAvailable(Task<UpdateCheckResult?> updateCheck)
    {
        try
        {
            if (!updateCheck.Wait(TimeSpan.FromSeconds(2))) return;
            if (updateCheck.Result is { IsUpdateAvailable: true } result)
            {
                Console.Error.WriteLine(
                    $"Update available: v{result.LatestVersion} (current v{result.CurrentVersion}). Download: {result.ReleasesPageUrl}");
            }
        }
        catch
        {
            // Best-effort notice only.
        }
    }
}
