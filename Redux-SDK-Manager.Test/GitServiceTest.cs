using System;
using System.Collections.Generic;
using Moq;
using Redux_SDK_Manager.Services;
using Redux_SDK_Manager.Wrappers;

namespace Redux_SDK_Manager.Test;

public class GitServiceTest
{
    private static Mock<IProcessRunner> Runner() => new(MockBehavior.Strict);

    [Test]
    public void IsInstalled_True_WhenGitVersionSucceeds()
    {
        var runner = Runner();
        runner.Setup(r => r.Run("git", It.IsAny<IReadOnlyList<string>>(), It.IsAny<string?>()))
            .Returns(new ProcessResult(0, "git version 2.43.0.windows.1", ""));

        Assert.That(new GitService(runner.Object).IsInstalled(), Is.True);
    }

    [Test]
    public void IsInstalled_False_WhenExitNonZero()
    {
        var runner = Runner();
        runner.Setup(r => r.Run("git", It.IsAny<IReadOnlyList<string>>(), It.IsAny<string?>()))
            .Returns(new ProcessResult(1, "", "not found"));

        Assert.That(new GitService(runner.Object).IsInstalled(), Is.False);
    }

    [Test]
    public void IsInstalled_False_WhenRunnerThrows()
    {
        var runner = Runner();
        runner.Setup(r => r.Run("git", It.IsAny<IReadOnlyList<string>>(), It.IsAny<string?>()))
            .Throws(new InvalidOperationException("git not on PATH"));

        Assert.That(new GitService(runner.Object).IsInstalled(), Is.False);
    }

    [Test]
    public void ListRemoteTags_ParsesTags_AndDropsPeeledEntries()
    {
        var stdout =
            "aaaa\trefs/tags/0.2.8.5\n" +
            "bbbb\trefs/tags/26w32a\n" +
            "bbbb\trefs/tags/26w32a^{}\n";
        var runner = Runner();
        runner.Setup(r => r.Run("git",
                It.Is<IReadOnlyList<string>>(a => a.Contains("ls-remote") && a.Contains("--tags")),
                It.IsAny<string?>()))
            .Returns(new ProcessResult(0, stdout, ""));

        var tags = new GitService(runner.Object).ListRemoteTags("https://example/repo.git");

        Assert.That(tags, Is.EqualTo(new[] { "0.2.8.5", "26w32a" }));
    }

    [Test]
    public void ListRemoteTags_Throws_WhenExitNonZero()
    {
        var runner = Runner();
        runner.Setup(r => r.Run("git", It.IsAny<IReadOnlyList<string>>(), It.IsAny<string?>()))
            .Returns(new ProcessResult(128, "", "repository not found"));

        var git = new GitService(runner.Object);

        Assert.That(() => git.ListRemoteTags("https://example/repo.git"),
            Throws.TypeOf<InvalidOperationException>());
    }

    [Test]
    public void Clone_InvokesGitCloneWithTagAndDestination()
    {
        var runner = Runner();
        runner.Setup(r => r.Run("git", It.IsAny<IReadOnlyList<string>>(), It.IsAny<string?>()))
            .Returns(new ProcessResult(0, "", ""));

        new GitService(runner.Object).Clone("https://example/repo.git", "26w32a", @"C:\dest");

        runner.Verify(r => r.Run("git",
            It.Is<IReadOnlyList<string>>(a =>
                a.Contains("clone") && a.Contains("26w32a") &&
                a.Contains("https://example/repo.git") && a.Contains(@"C:\dest")),
            It.IsAny<string?>()), Times.Once);
    }

    [Test]
    public void Clone_Throws_WhenExitNonZero()
    {
        var runner = Runner();
        runner.Setup(r => r.Run("git", It.IsAny<IReadOnlyList<string>>(), It.IsAny<string?>()))
            .Returns(new ProcessResult(1, "", "fatal"));

        var git = new GitService(runner.Object);

        Assert.That(() => git.Clone("https://example/repo.git", "26w32a", @"C:\dest"),
            Throws.TypeOf<InvalidOperationException>());
    }

    [Test]
    public void CloneMirror_InvokesFullCloneWithoutBranchOrDepth()
    {
        var runner = Runner();
        runner.Setup(r => r.Run("git", It.IsAny<IReadOnlyList<string>>(), It.IsAny<string?>()))
            .Returns(new ProcessResult(0, "", ""));

        new GitService(runner.Object).CloneMirror("https://example/repo.git", @"C:\mirror");

        runner.Verify(r => r.Run("git",
            It.Is<IReadOnlyList<string>>(a =>
                a.Contains("clone") && !a.Contains("--depth") && !a.Contains("--branch") &&
                a.Contains("https://example/repo.git") && a.Contains(@"C:\mirror")),
            It.IsAny<string?>()), Times.Once);
    }

    [Test]
    public void Fetch_RunsFetchInRepoWorkingDirectory()
    {
        var runner = Runner();
        runner.Setup(r => r.Run("git", It.IsAny<IReadOnlyList<string>>(), @"C:\mirror"))
            .Returns(new ProcessResult(0, "", ""));

        new GitService(runner.Object).Fetch(@"C:\mirror");

        runner.Verify(r => r.Run("git",
            It.Is<IReadOnlyList<string>>(a => a.Contains("fetch") && a.Contains("--tags") && a.Contains("--prune")),
            @"C:\mirror"), Times.Once);
    }

    [Test]
    public void Fetch_Throws_WhenExitNonZero()
    {
        var runner = Runner();
        runner.Setup(r => r.Run("git", It.IsAny<IReadOnlyList<string>>(), It.IsAny<string?>()))
            .Returns(new ProcessResult(1, "", "network down"));

        Assert.That(() => new GitService(runner.Object).Fetch(@"C:\mirror"),
            Throws.TypeOf<InvalidOperationException>());
    }

    [Test]
    public void ListTags_SplitsLocalTagOutput()
    {
        var runner = Runner();
        runner.Setup(r => r.Run("git", It.Is<IReadOnlyList<string>>(a => a.Contains("tag")), @"C:\mirror"))
            .Returns(new ProcessResult(0, "0.2.8.5\n26w32a\n26w32b\n", ""));

        var tags = new GitService(runner.Object).ListTags(@"C:\mirror");

        Assert.That(tags, Is.EqualTo(new[] { "0.2.8.5", "26w32a", "26w32b" }));
    }

    [Test]
    public void ShowFile_ReturnsContent_OnSuccess()
    {
        var runner = Runner();
        runner.Setup(r => r.Run("git",
                It.Is<IReadOnlyList<string>>(a => a.Contains("show") && a.Contains("26w32a:ProjectSettings/ProjectVersion.txt")),
                @"C:\mirror"))
            .Returns(new ProcessResult(0, "m_EditorVersion: 6000.4.1f1\n", ""));

        var content = new GitService(runner.Object).ShowFile(@"C:\mirror", "26w32a", "ProjectSettings/ProjectVersion.txt");

        Assert.That(content, Does.Contain("6000.4.1f1"));
    }

    [Test]
    public void ShowFile_ReturnsNull_WhenFileOrRefMissing()
    {
        var runner = Runner();
        runner.Setup(r => r.Run("git", It.IsAny<IReadOnlyList<string>>(), It.IsAny<string?>()))
            .Returns(new ProcessResult(128, "", "path does not exist"));

        var content = new GitService(runner.Object).ShowFile(@"C:\mirror", "26w32a", "ProjectSettings/ProjectVersion.txt");

        Assert.That(content, Is.Null);
    }
}
