namespace Redux_SDK_Manager.Cli;

/// <summary>
/// Process exit codes returned by the CLI verbs. Scripts branch on these, so append new members
/// rather than renumbering existing ones.
/// </summary>
public static class ExitCode
{
    /// <summary>The command completed.</summary>
    public const int SUCCESS = 0;

    /// <summary>Argument parsing failed, or a verb was given arguments it cannot satisfy.</summary>
    public const int USAGE_ERROR = 1;

    /// <summary>A referenced project or version could not be found.</summary>
    public const int NOT_FOUND = 2;

    /// <summary>The operation ran but failed.</summary>
    public const int FAILED = 3;

    /// <summary>git is required for this command but isn't installed / on PATH.</summary>
    public const int GIT_UNAVAILABLE = 4;

    /// <summary>Unity Hub is required for this command but isn't installed.</summary>
    public const int HUB_UNAVAILABLE = 5;
}
