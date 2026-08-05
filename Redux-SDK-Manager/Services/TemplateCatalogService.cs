using System.Collections.Generic;
using Redux_SDK_Manager.Models;

namespace Redux_SDK_Manager.Services;

public interface ITemplateCatalogService
{
    /// <summary>
    /// Template versions available in the configured distribution repo, derived from its git tags.
    /// Tags that don't classify into a known channel are still returned (as <c>Unknown</c>).
    /// </summary>
    IReadOnlyList<TemplateVersion> ListAvailableVersions();

    /// <summary>Fetches a version's template tree from the distribution repo into a directory.</summary>
    void FetchVersion(TemplateVersion version, string destinationPath);
}

public class TemplateCatalogService(IGitService gitService, IConfigService configService)
    : ITemplateCatalogService
{
    public IReadOnlyList<TemplateVersion> ListAvailableVersions()
    {
        var versions = new List<TemplateVersion>();
        foreach (var tag in gitService.ListRemoteTags(configService.Config.TemplatesRepositoryUrl))
        {
            versions.Add(TemplateVersion.Parse(tag));
        }

        return versions;
    }

    public void FetchVersion(TemplateVersion version, string destinationPath)
    {
        gitService.Clone(configService.Config.TemplatesRepositoryUrl, version.Raw, destinationPath);
    }
}
