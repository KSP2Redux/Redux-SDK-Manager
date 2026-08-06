using System;

namespace Redux_SDK_Manager.Services;

/// <summary>Helpers for working with git repository URLs.</summary>
public static class GitUrl
{
    /// <summary>
    /// The repository (and thus default folder) name from a clone URL: the last path segment with any
    /// trailing <c>.git</c> and slashes removed. Handles https and scp-style (<c>git@host:owner/repo.git</c>)
    /// URLs. Returns an empty string when nothing usable can be parsed.
    /// </summary>
    public static string RepoName(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return "";

        var trimmed = url.Trim().TrimEnd('/', '\\');
        if (trimmed.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^4];
        }

        // The last segment sits after the final '/' (https) or ':' (scp-style git@host:owner/repo).
        var separator = trimmed.LastIndexOfAny(['/', '\\', ':']);
        var name = separator >= 0 ? trimmed[(separator + 1)..] : trimmed;
        return name.Trim();
    }
}
