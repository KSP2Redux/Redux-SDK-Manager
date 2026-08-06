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

[Verb("versions", HelpText = "List the template versions available in the distribution repo.")]
public sealed class VersionsOptions : BaseOptions;

[Verb("create", HelpText = "Create a new project from a template version into an empty directory.")]
public sealed class CreateOptions : ProjectVersionOptions
{
    [Option("name", Required = false, HelpText = "Project name to record in project.info (defaults to the directory name).")]
    public string? Name { get; set; }

    [Option("embed-sdk", Required = false, HelpText = "Embed the SDK package into Packages as a git checkout for SDK development.")]
    public bool EmbedSdk { get; set; }
}

[Verb("ingest", HelpText = "Adopt an existing pre-manager project and bring it to a template version.")]
public sealed class IngestOptions : ProjectVersionOptions
{
    [Option("name", Required = false, HelpText = "Project name to record in project.info (defaults to the directory name).")]
    public string? Name { get; set; }

    [Option("embed-sdk", Required = false, HelpText = "Embed the SDK package into Packages as a git checkout for SDK development.")]
    public bool EmbedSdk { get; set; }
}

[Verb("upgrade", HelpText = "Upgrade a managed project to a template version.")]
public sealed class UpgradeOptions : ProjectVersionOptions
{
    [Option("embed-sdk", Required = false, HelpText = "Embed the SDK package into Packages as a git checkout for SDK development.")]
    public bool EmbedSdk { get; set; }
}

[Verb("import", HelpText = "Register an already-managed project (has template.version) with the manager, unchanged.")]
public sealed class ImportOptions : ProjectPathOptions
{
    [Option("embed-sdk", Required = false, HelpText = "Embed the SDK package into Packages as a git checkout for SDK development.")]
    public bool EmbedSdk { get; set; }
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
