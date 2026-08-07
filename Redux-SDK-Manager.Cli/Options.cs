using CommandLine;

namespace Redux_SDK_Manager.Cli;

/// <summary>Options shared by every verb.</summary>
public abstract class BaseOptions
{
    [Option("json", Required = false, HelpText = "Emit a JSON document on stdout instead of text.")]
    public bool IsJson { get; set; }

    [Option("verbose", Required = false, HelpText = "Print the manager's info/debug log lines to stderr.")]
    public bool IsVerbose { get; set; }
}

/// <summary>Options for a verb that acts on a single project directory.</summary>
public abstract class ProjectPathOptions : BaseOptions
{
    [Value(0, MetaName = "path", Required = true, HelpText = "Path to the project directory.")]
    public string? Path { get; set; }
}

/// <summary>Options for a verb that applies a version to a project directory.</summary>
public abstract class ProjectVersionOptions : ProjectPathOptions
{
    [Value(1, MetaName = "version", Required = true, HelpText = "Template version, e.g. 0.2.8.5 or 26w32a.")]
    public string? Version { get; set; }
}

/// <summary>Options a verb carries when it can run automated project setup afterwards.</summary>
public interface ISetupCapableOptions
{
    /// <summary>Skip the automated ThunderKit import + pipeline that would otherwise run after the verb.</summary>
    bool NoSetup { get; }

    /// <summary>Path to KSP2_x64.exe to import from, overriding the configured one for this run.</summary>
    string? Ksp2 { get; }
}

[Verb("versions", HelpText = "List the template versions available in the distribution repo.")]
public sealed class VersionsOptions : BaseOptions;

[Verb("create", HelpText = "Create a new project from a template version into an empty directory.")]
public sealed class CreateOptions : ProjectVersionOptions, ISetupCapableOptions
{
    [Option("name", Required = false, HelpText = "Project name to record in project.info (defaults to the directory name).")]
    public string? Name { get; set; }

    [Option("embed-sdk", Required = false, HelpText = "Embed the SDK package into Packages as a git checkout for SDK development.")]
    public bool EmbedSdk { get; set; }

    [Option("no-setup", Required = false, HelpText = "Skip the automated ThunderKit import + pipeline after creating.")]
    public bool NoSetup { get; set; }

    [Option("ksp2", Required = false, HelpText = "Path to KSP2_x64.exe for automated setup, overriding the configured one.")]
    public string? Ksp2 { get; set; }
}

[Verb("ingest", HelpText = "Adopt an existing pre-manager project and bring it to a template version.")]
public sealed class IngestOptions : ProjectVersionOptions, ISetupCapableOptions
{
    [Option("name", Required = false, HelpText = "Project name to record in project.info (defaults to the directory name).")]
    public string? Name { get; set; }

    [Option("embed-sdk", Required = false, HelpText = "Embed the SDK package into Packages as a git checkout for SDK development.")]
    public bool EmbedSdk { get; set; }

    [Option("no-setup", Required = false, HelpText = "Skip the automated ThunderKit import + pipeline after adding.")]
    public bool NoSetup { get; set; }

    [Option("ksp2", Required = false, HelpText = "Path to KSP2_x64.exe for automated setup, overriding the configured one.")]
    public string? Ksp2 { get; set; }
}

[Verb("upgrade", HelpText = "Upgrade a managed project to a template version.")]
public sealed class UpgradeOptions : ProjectVersionOptions, ISetupCapableOptions
{
    [Option("embed-sdk", Required = false, HelpText = "Embed the SDK package into Packages as a git checkout for SDK development.")]
    public bool EmbedSdk { get; set; }

    [Option("no-setup", Required = false, HelpText = "Skip the automated ThunderKit import + pipeline after upgrading.")]
    public bool NoSetup { get; set; }

    [Option("ksp2", Required = false, HelpText = "Path to KSP2_x64.exe for automated setup, overriding the configured one.")]
    public string? Ksp2 { get; set; }
}

[Verb("import", HelpText = "Register an already-managed project (has template.version) with the manager, unchanged.")]
public sealed class ImportOptions : ProjectPathOptions, ISetupCapableOptions
{
    [Option("embed-sdk", Required = false, HelpText = "Embed the SDK package into Packages as a git checkout for SDK development.")]
    public bool EmbedSdk { get; set; }

    [Option("no-setup", Required = false, HelpText = "Skip the automated ThunderKit import + pipeline after registering.")]
    public bool NoSetup { get; set; }

    [Option("ksp2", Required = false, HelpText = "Path to KSP2_x64.exe for automated setup, overriding the configured one.")]
    public string? Ksp2 { get; set; }
}

[Verb("clone", HelpText = "Clone a repo URL and add it as a project (import if managed, else ingest at --version).")]
public sealed class CloneOptions : BaseOptions, ISetupCapableOptions
{
    [Value(0, MetaName = "url", Required = true, HelpText = "Repository URL to clone.")]
    public string? Url { get; set; }

    [Value(1, MetaName = "path", Required = true, HelpText = "Destination directory to clone into.")]
    public string? Path { get; set; }

    [Option("version", Required = false, HelpText = "Template version to ingest at when the repo isn't already a managed project.")]
    public string? Version { get; set; }

    [Option("name", Required = false, HelpText = "Project name to record in project.info (defaults to the directory name).")]
    public string? Name { get; set; }

    [Option("embed-sdk", Required = false, HelpText = "Embed the SDK package into Packages as a git checkout for SDK development.")]
    public bool EmbedSdk { get; set; }

    [Option("no-setup", Required = false, HelpText = "Skip the automated ThunderKit import + pipeline after adding.")]
    public bool NoSetup { get; set; }

    [Option("ksp2", Required = false, HelpText = "Path to KSP2_x64.exe for automated setup, overriding the configured one.")]
    public string? Ksp2 { get; set; }
}

[Verb("setup", HelpText = "Run the ThunderKit import + Import KSP2 to Editor pipeline on a project after the fact.")]
public sealed class SetupOptions : ProjectPathOptions
{
    [Option("ksp2", Required = false, HelpText = "Path to KSP2_x64.exe to import from, overriding the configured one.")]
    public string? Ksp2 { get; set; }
}

[Verb("detect", HelpText = "Report the template version a project is stamped with.")]
public sealed class DetectOptions : ProjectPathOptions;

[Verb("open", HelpText = "Open a project in its Unity editor, offering to install it via Unity Hub if missing.")]
public sealed class OpenOptions : ProjectPathOptions
{
    [Option('y', "yes", Required = false, HelpText = "Answer yes to prompts (e.g. install a missing editor) without asking.")]
    public bool Yes { get; set; }

    [Option('n', "no", Required = false, HelpText = "Answer no to prompts (e.g. skip installing a missing editor) without asking.")]
    public bool No { get; set; }
}

[Verb("unity", HelpText = "List the Unity editors installed via Unity Hub.")]
public sealed class UnityOptions : BaseOptions;

[Verb("projects", HelpText = "List the projects the manager is tracking.")]
public sealed class ProjectsOptions : BaseOptions;

[Verb("doctor", HelpText = "Check that git and Unity Hub are available.")]
public sealed class DoctorOptions : BaseOptions;
