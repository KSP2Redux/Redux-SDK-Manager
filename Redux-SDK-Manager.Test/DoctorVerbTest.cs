using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Redux_SDK_Manager.Cli;
using Redux_SDK_Manager.Cli.Verbs;
using Redux_SDK_Manager.Services;

namespace Redux_SDK_Manager.Test;

public class DoctorVerbTest
{
    private static (int code, string output) RunDoctor(bool git, bool hub)
    {
        var gitMock = new Mock<IGitService>();
        gitMock.Setup(g => g.IsInstalled()).Returns(git);
        var unityMock = new Mock<IUnityService>();
        unityMock.Setup(u => u.IsHubInstalled()).Returns(hub);

        var services = new ServiceCollection()
            .AddSingleton(gitMock.Object)
            .AddSingleton(unityMock.Object)
            .BuildServiceProvider();

        var writer = new StringWriter();
        var context = new CliContext(services, new CliOutput(writer, isJson: false));
        var code = DoctorVerb.Run(context);
        return (code, writer.ToString());
    }

    [Test]
    public void AllInstalled_ReportsSuccess_WithoutLinks()
    {
        var (code, output) = RunDoctor(git: true, hub: true);

        Assert.That(code, Is.EqualTo(ExitCode.SUCCESS));
        Assert.That(output, Does.Contain("installed"));
        Assert.That(output, Does.Not.Contain("MISSING"));
        Assert.That(output, Does.Not.Contain("git-scm.com"));
    }

    [Test]
    public void GitMissing_ReportsFailure_WithGitInstallLink()
    {
        var (code, output) = RunDoctor(git: false, hub: true);

        Assert.That(code, Is.EqualTo(ExitCode.FAILED));
        Assert.That(output, Does.Contain("git:        MISSING"));
        Assert.That(output, Does.Contain(DownloadLinks.Git));
    }

    [Test]
    public void HubMissing_ReportsFailure_WithUnityHubInstallLink()
    {
        var (code, output) = RunDoctor(git: true, hub: false);

        Assert.That(code, Is.EqualTo(ExitCode.FAILED));
        Assert.That(output, Does.Contain("Unity Hub:  MISSING"));
        Assert.That(output, Does.Contain(DownloadLinks.UnityHub));
    }
}
