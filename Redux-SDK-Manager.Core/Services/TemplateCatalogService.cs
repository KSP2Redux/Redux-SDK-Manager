using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Redux_SDK_Manager.Models;

namespace Redux_SDK_Manager.Services;

public interface ITemplateCatalogService
{
    /// <summary>
    /// Template versions available in the configured distribution repo, derived from its git tags.
    /// Tags that don't classify into a known channel are still returned (as <c>Unknown</c>).
    /// </summary>
    IReadOnlyList<TemplateVersion> ListAvailableVersions();

    /// <summary>
    /// Every available version paired with the Unity editor it targets (read from that version's
    /// ProjectSettings/ProjectVersion.txt). Used by the versions catalog view.
    /// </summary>
    IReadOnlyList<TemplateVersionInfo> DescribeVersions();

    /// <summary>Fetches a version's template tree from the distribution repo into a directory.</summary>
    void FetchVersion(TemplateVersion version, string destinationPath);
}

public partial class TemplateCatalogService(IGitService gitService, ITemplateRepositoryCache cache)
    : ITemplateCatalogService
{
    private const string ProjectVersionFile = "ProjectSettings/ProjectVersion.txt";

    public IReadOnlyList<TemplateVersion> ListAvailableVersions()
    {
        cache.Sync();
        return gitService.ListTags(cache.RepositoryPath).Select(TemplateVersion.Parse).ToList();
    }

    public IReadOnlyList<TemplateVersionInfo> DescribeVersions()
    {
        cache.Sync();
        return gitService.ListTags(cache.RepositoryPath)
            .Select(TemplateVersion.Parse)
            .Select(version =>
            {
                var content = gitService.ShowFile(cache.RepositoryPath, version.Raw, ProjectVersionFile);
                var (unityVersion, changeset) = ParseEditorInfo(content);
                return new TemplateVersionInfo
                {
                    Version = version,
                    UnityVersion = unityVersion,
                    Changeset = changeset,
                };
            })
            .ToList();
    }

    public void FetchVersion(TemplateVersion version, string destinationPath)
    {
        // Source the version from the local mirror rather than the remote: it is faster, works
        // offline, and the clone's .git never lands in the project (it is stripped on copy).
        cache.EnsureCloned();
        gitService.Clone(cache.RepositoryPath, version.Raw, destinationPath);
    }

    private static (string? version, string? changeset) ParseEditorInfo(string? content)
    {
        if (string.IsNullOrEmpty(content)) return (null, null);

        var version = EditorVersionRegex().Match(content) is { Success: true } m
            ? m.Groups["version"].Value.Trim()
            : null;
        var changeset = EditorRevisionRegex().Match(content) is { Success: true } r
            ? r.Groups["changeset"].Value.Trim()
            : null;
        return (version, changeset);
    }

    [GeneratedRegex(@"^m_EditorVersion:\s*(?<version>\S+)", RegexOptions.Multiline)]
    private static partial Regex EditorVersionRegex();

    [GeneratedRegex(@"^m_EditorVersionWithRevision:\s*\S+\s*\((?<changeset>[^)]+)\)", RegexOptions.Multiline)]
    private static partial Regex EditorRevisionRegex();
}
