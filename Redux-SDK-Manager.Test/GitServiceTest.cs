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
}
