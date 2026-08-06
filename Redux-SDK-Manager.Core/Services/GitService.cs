using System;
using System.Collections.Generic;
using System.Linq;
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

    /// <summary>
    /// Full-clones a repository (all history and tags) into <paramref name="destinationPath"/>, for
    /// keeping a local mirror the manager reads tags and files from.
    /// </summary>
    void CloneMirror(string repositoryUrl, string destinationPath);

    /// <summary>Fetches all tags into an existing local clone, pruning deleted ones.</summary>
    void Fetch(string repositoryPath);

    /// <summary>Tag names in a local clone.</summary>
    IReadOnlyList<string> ListTags(string repositoryPath);

    /// <summary>
    /// Reads a single file's contents at a reference (tag/branch/commit) from a local clone, without
    /// checking it out. Returns null when the file or reference does not exist.
    /// </summary>
    string? ShowFile(string repositoryPath, string reference, string filePath);
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
            // Run throws when git isn't on PATH, so treat any start failure as "not installed".
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

        return (from line in result.StandardOutput.Split('\n')
            let marker = line.IndexOf(TagRefPrefix, StringComparison.Ordinal)
            where marker >= 0
            select line[(marker + TagRefPrefix.Length)..].Trim()
            into name
            where name.Length != 0 && !name.EndsWith("^{}", StringComparison.Ordinal)
            select name).ToList();
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

    public void CloneMirror(string repositoryUrl, string destinationPath)
    {
        var result = processRunner.Run(GitExecutable, ["clone", repositoryUrl, destinationPath]);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"git clone of '{repositoryUrl}' failed (exit {result.ExitCode}): {result.StandardError.Trim()}");
        }
    }

    public void Fetch(string repositoryPath)
    {
        var result = processRunner.Run(GitExecutable, ["fetch", "--tags", "--prune", "--force"], repositoryPath);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"git fetch in '{repositoryPath}' failed (exit {result.ExitCode}): {result.StandardError.Trim()}");
        }
    }

    public IReadOnlyList<string> ListTags(string repositoryPath)
    {
        var result = processRunner.Run(GitExecutable, ["tag"], repositoryPath);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"git tag in '{repositoryPath}' failed (exit {result.ExitCode}): {result.StandardError.Trim()}");
        }

        return result.StandardOutput.Split('\n')
            .Select(line => line.Trim())
            .Where(name => name.Length != 0)
            .ToList();
    }

    public string? ShowFile(string repositoryPath, string reference, string filePath)
    {
        // git addresses tree paths with forward slashes regardless of platform.
        var spec = $"{reference}:{filePath.Replace('\\', '/')}";
        var result = processRunner.Run(GitExecutable, ["show", spec], repositoryPath);
        // A missing file or ref is an expected "not there", not an error - return null so callers can
        // decide (e.g. show "unknown" for a template version without a readable ProjectVersion.txt).
        return result.ExitCode == 0 ? result.StandardOutput : null;
    }
}