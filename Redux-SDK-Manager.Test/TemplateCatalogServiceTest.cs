using System.Collections.Generic;
using System.Linq;
using Moq;
using Redux_SDK_Manager.Models;
using Redux_SDK_Manager.Services;

namespace Redux_SDK_Manager.Test;

public class TemplateCatalogServiceTest
{
    private const string RepoUrl = "https://github.com/KSP2Redux/Redux.Templates.git";

    private static Mock<IConfigService> ConfigWithUrl(string url)
    {
        var config = new Mock<IConfigService>();
        config.Setup(c => c.Config).Returns(new SdkManagerConfig { TemplatesRepositoryUrl = url });
        return config;
    }

    [Test]
    public void ListAvailableVersions_ParsesTagsIntoChanneledVersions()
    {
        var git = new Mock<IGitService>();
        git.Setup(g => g.ListRemoteTags(RepoUrl))
            .Returns(new List<string> { "0.2.8.5", "26w32a" });

        var catalog = new TemplateCatalogService(git.Object, ConfigWithUrl(RepoUrl).Object);
        var versions = catalog.ListAvailableVersions();

        Assert.That(versions.Select(v => v.Raw), Is.EqualTo(new[] { "0.2.8.5", "26w32a" }));
        Assert.That(versions.Select(v => v.Channel),
            Is.EqualTo(new[] { TemplateChannel.Release, TemplateChannel.Snapshot }));
    }

    [Test]
    public void FetchVersion_ClonesConfiguredRepoAtVersionRef()
    {
        var git = new Mock<IGitService>();
        var catalog = new TemplateCatalogService(git.Object, ConfigWithUrl(RepoUrl).Object);

        catalog.FetchVersion(TemplateVersion.Parse("26w32a"), @"C:\dest");

        git.Verify(g => g.Clone(RepoUrl, "26w32a", @"C:\dest"), Times.Once);
    }
}
