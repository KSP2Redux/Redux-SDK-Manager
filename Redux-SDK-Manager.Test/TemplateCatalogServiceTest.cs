using System.Collections.Generic;
using System.Linq;
using Moq;
using Redux_SDK_Manager.Models;
using Redux_SDK_Manager.Services;

namespace Redux_SDK_Manager.Test;

public class TemplateCatalogServiceTest
{
    private const string RepoPath = @"C:\storage\templates-repo";

    private static Mock<ITemplateRepositoryCache> CacheAt(string path)
    {
        var cache = new Mock<ITemplateRepositoryCache>();
        cache.Setup(c => c.RepositoryPath).Returns(path);
        return cache;
    }

    [Test]
    public void ListAvailableVersions_SyncsThenParsesLocalTags()
    {
        var cache = CacheAt(RepoPath);
        var git = new Mock<IGitService>();
        git.Setup(g => g.ListTags(RepoPath)).Returns(new List<string> { "0.2.8.5", "26w32a" });

        var catalog = new TemplateCatalogService(git.Object, cache.Object);
        var versions = catalog.ListAvailableVersions();

        cache.Verify(c => c.Sync(), Times.Once);
        Assert.That(versions.Select(v => v.Raw), Is.EqualTo(new[] { "0.2.8.5", "26w32a" }));
        Assert.That(versions.Select(v => v.Channel),
            Is.EqualTo(new[] { TemplateChannel.Release, TemplateChannel.Snapshot }));
    }

    [Test]
    public void DescribeVersions_ReadsUnityVersionAndChangesetPerTag()
    {
        var cache = CacheAt(RepoPath);
        var git = new Mock<IGitService>();
        git.Setup(g => g.ListTags(RepoPath)).Returns(new List<string> { "0.2.8.5", "26w32a" });
        git.Setup(g => g.ShowFile(RepoPath, "0.2.8.5", "ProjectSettings/ProjectVersion.txt"))
            .Returns("m_EditorVersion: 6000.4.1f1\nm_EditorVersionWithRevision: 6000.4.1f1 (336a400b9ea2)\n");
        // A version with no readable ProjectVersion.txt reports null Unity info rather than failing.
        git.Setup(g => g.ShowFile(RepoPath, "26w32a", "ProjectSettings/ProjectVersion.txt"))
            .Returns((string?)null);

        var catalog = new TemplateCatalogService(git.Object, cache.Object);
        var described = catalog.DescribeVersions();

        cache.Verify(c => c.Sync(), Times.Once);
        var release = described.Single(d => d.Version.Raw == "0.2.8.5");
        Assert.That(release.UnityVersion, Is.EqualTo("6000.4.1f1"));
        Assert.That(release.Changeset, Is.EqualTo("336a400b9ea2"));
        var snapshot = described.Single(d => d.Version.Raw == "26w32a");
        Assert.That(snapshot.UnityVersion, Is.Null);
        Assert.That(snapshot.Changeset, Is.Null);
    }

    [Test]
    public void FetchVersion_EnsuresMirror_ThenClonesTagFromIt()
    {
        var cache = CacheAt(RepoPath);
        var git = new Mock<IGitService>();
        var catalog = new TemplateCatalogService(git.Object, cache.Object);

        catalog.FetchVersion(TemplateVersion.Parse("26w32a"), @"C:\dest");

        cache.Verify(c => c.EnsureCloned(), Times.Once);
        git.Verify(g => g.Clone(RepoPath, "26w32a", @"C:\dest"), Times.Once);
    }
}
