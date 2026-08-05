using System;
using System.Collections.Generic;
using Redux_SDK_Manager.Wrappers;

namespace Redux_SDK_Manager.Services;

public interface IGitService
{
    /// <summary>True if a usable <c>git</c> executable is available on PATH.</summary>
    bool IsInstalled();

    /// <summary>Tag names on a remote repository (peeled <c>^{}</c> entries removed).</summary>
    IReadOnlyList<string> ListRemoteTags(string repositoryUrl);

    /// <summary>Shallow-clones a single tag or branch into <paramref name="destinationPath"/>.</summary>
    void Clone(string repositoryUrl, string reference, string destinationPath);
}

public class GitService(IProcessRunner processRunner) : IGitService
{
    private const string GitExecutable = "git";
    private const string TagRefPrefix = "refs/tags/";

    public bool IsInstalled()
    {
        try
        {
            return processRunner.Run(GitExecutable, ["--version"]).ExitCode == 0;
        }
        catch (Exception)
        {
            // Run throws when git isn't on PATH; treat any start failure as "not installed".
            return false;
        }
    }

    public IReadOnlyList<string> ListRemoteTags(string repositoryUrl)
    {
        var result = processRunner.Run(GitExecutable, ["ls-remote", "--tags", repositoryUrl]);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"git ls-remote failed for '{repositoryUrl}' (exit {result.ExitCode}): {result.StandardError.Trim()}");
        }

        var tags = new List<string>();
        foreach (var line in result.StandardOutput.Split('\n'))
        {
            var marker = line.IndexOf(TagRefPrefix, StringComparison.Ordinal);
            if (marker < 0) continue;

            var name = line[(marker + TagRefPrefix.Length)..].Trim();
            // A peeled entry ("<tag>^{}") duplicates the annotated tag it dereferences - skip it.
            if (name.Length == 0 || name.EndsWith("^{}", StringComparison.Ordinal)) continue;

            tags.Add(name);
        }

        return tags;
    }

    public void Clone(string repositoryUrl, string reference, string destinationPath)
    {
        var result = processRunner.Run(GitExecutable,
            ["clone", "--depth", "1", "--branch", reference, repositoryUrl, destinationPath]);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"git clone of '{reference}' from '{repositoryUrl}' failed (exit {result.ExitCode}): {result.StandardError.Trim()}");
        }
    }
}
