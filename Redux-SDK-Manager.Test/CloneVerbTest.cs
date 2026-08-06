using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Redux_SDK_Manager.Cli;
using Redux_SDK_Manager.Cli.Verbs;
using Redux_SDK_Manager.Models;
using Redux_SDK_Manager.Services;

namespace Redux_SDK_Manager.Test;

public class CloneVerbTest
{
    private const string Url = "https://github.com/Falki-git/SASExtended.git";
    private const string Dest = @"C:\clones\SASExtended";

    private static (int code, Mock<IProjectService> project, Mock<IGitService> git) RunClone(
        CloneOptions options, bool gitInstalled, TemplateVersion? detected, TemplateVersion? importResult = null)
    {
        var git = new Mock<IGitService>();
        git.Setup(g => g.IsInstalled()).Returns(gitInstalled);
        var version = new Mock<ITemplateVersionService>();
        version.Setup(v => v.DetectProjectVersion(It.IsAny<string>())).Returns(detected);
        var project = new Mock<IProjectService>();
        if (importResult is not null)
            project.Setup(p => p.ImportProject(It.IsAny<string>(), It.IsAny<bool>())).Returns(importResult);

        var services = new ServiceCollection()
            .AddSingleton(git.Object)
            .AddSingleton(version.Object)
            .AddSingleton(project.Object)
            .BuildServiceProvider();

        var context = new CliContext(services, new CliOutput(new StringWriter(), isJson: false));
        var code = CloneVerb.Run(context, options);
        return (code, project, git);
    }

    // no-setup keeps SetupRunner from needing the config/setup services in these focused tests.
    private static CloneOptions Options(string? version = null) =>
        new() { Url = Url, Path = Dest, Version = version, NoSetup = true };

    [Test]
    public void GitUnavailable_Fails_WithoutCloning()
    {
        var (code, project, git) = RunClone(Options(), gitInstalled: false, detected: null);

        Assert.That(code, Is.EqualTo(ExitCode.GIT_UNAVAILABLE));
        git.Verify(g => g.CloneRepository(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        project.Verify(p => p.ImportProject(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Test]
    public void UnmanagedRepo_WithoutVersion_IsUsageError()
    {
        var (code, project, git) = RunClone(Options(version: null), gitInstalled: true, detected: null);

        Assert.That(code, Is.EqualTo(ExitCode.USAGE_ERROR));
        git.Verify(g => g.CloneRepository(Url, Dest), Times.Once);
        project.Verify(p => p.IngestProject(It.IsAny<string>(), It.IsAny<TemplateVersion>(), It.IsAny<bool>()), Times.Never);
    }

    [Test]
    public void ManagedRepo_ClonesThenImports()
    {
        var (code, project, git) = RunClone(Options(), gitInstalled: true,
            detected: TemplateVersion.Parse("26w32b"), importResult: TemplateVersion.Parse("26w32b"));

        Assert.That(code, Is.EqualTo(ExitCode.SUCCESS));
        git.Verify(g => g.CloneRepository(Url, Dest), Times.Once);
        project.Verify(p => p.ImportProject(Dest, It.IsAny<bool>()), Times.Once);
    }
}
