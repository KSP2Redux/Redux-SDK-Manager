using System;
using Moq;
using Redux_SDK_Manager.Models;
using Redux_SDK_Manager.Services;
using Testably.Abstractions.Testing;

namespace Redux_SDK_Manager.Test;

public class TemplateRepositoryCacheTest
{
    private const string Storage = @"C:\storage";
    private const string RepoUrl = "https://example/templates.git";
    private static readonly string RepoPath = @"C:\storage\templates-repo";
    private static readonly string GitDir = @"C:\storage\templates-repo\.git";

    private static (MockFileSystem fs, Mock<IGitService> git, Mock<IConfigService> config) Build()
    {
        var fs = new MockFileSystem(o => o.SimulatingOperatingSystem(SimulationMode.Windows));
        var git = new Mock<IGitService>();
        var config = new Mock<IConfigService>();
        config.Setup(c => c.GetLocalStorageDirectory()).Returns(Storage);
        config.Setup(c => c.Config).Returns(new SdkManagerConfig { TemplatesRepositoryUrl = RepoUrl });
        return (fs, git, config);
    }

    private static TemplateRepositoryCache NewCache(MockFileSystem fs, Mock<IGitService> git, Mock<IConfigService> config, ILogService? log = null)
        => new(git.Object, config.Object, fs, log ?? Mock.Of<ILogService>());

    [Test]
    public void RepositoryPath_IsRepoFolderUnderStorage()
    {
        var (fs, git, config) = Build();
        Assert.That(NewCache(fs, git, config).RepositoryPath, Is.EqualTo(RepoPath));
    }

    [Test]
    public void Sync_Clones_WhenMirrorAbsent()
    {
        var (fs, git, config) = Build();

        NewCache(fs, git, config).Sync();

        git.Verify(g => g.CloneMirror(RepoUrl, RepoPath), Times.Once);
        git.Verify(g => g.Fetch(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public void Sync_Fetches_WhenMirrorPresent()
    {
        var (fs, git, config) = Build();
        fs.Directory.CreateDirectory(GitDir);

        NewCache(fs, git, config).Sync();

        git.Verify(g => g.Fetch(RepoPath), Times.Once);
        git.Verify(g => g.CloneMirror(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public void Sync_ToleratesFetchFailure_AndWarns()
    {
        var (fs, git, config) = Build();
        fs.Directory.CreateDirectory(GitDir);
        git.Setup(g => g.Fetch(RepoPath)).Throws(new InvalidOperationException("offline"));
        var log = new Mock<ILogService>();

        Assert.That(() => NewCache(fs, git, config, log.Object).Sync(), Throws.Nothing);
        log.Verify(l => l.Warn(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Test]
    public void EnsureCloned_Clones_WhenAbsent_NoOp_WhenPresent()
    {
        var (fs, git, config) = Build();

        NewCache(fs, git, config).EnsureCloned();
        git.Verify(g => g.CloneMirror(RepoUrl, RepoPath), Times.Once);

        fs.Directory.CreateDirectory(GitDir);
        NewCache(fs, git, config).EnsureCloned();
        // Still only the first clone; the present mirror is left alone.
        git.Verify(g => g.CloneMirror(RepoUrl, RepoPath), Times.Once);
    }
}
