using System.IO;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Redux_SDK_Manager.Cli;
using Redux_SDK_Manager.Cli.Verbs;
using Redux_SDK_Manager.Models;
using Redux_SDK_Manager.Services;

namespace Redux_SDK_Manager.Test;

public class SetupVerbTest
{
    private const string ProjectPath = @"C:\proj";
    private const string ConfiguredKsp2 = @"C:\ksp2\KSP2_x64.exe";

    private static (int code, Mock<IProjectSetupService> setup) RunSetup(
        SetupOptions options, bool alreadySetUp, string configuredKsp2, ProjectSetupResult result)
    {
        var setup = new Mock<IProjectSetupService>();
        setup.Setup(s => s.IsAlreadySetUp(It.IsAny<string>())).Returns(alreadySetUp);
        setup.Setup(s => s.RunSetupAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<System.IProgress<ProjectSetupProgress>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        var config = new SdkManagerConfig { Ksp2ExePath = configuredKsp2 };
        var configMock = new Mock<IConfigService>();
        configMock.Setup(c => c.Config).Returns(config);

        var services = new ServiceCollection()
            .AddSingleton(setup.Object)
            .AddSingleton(configMock.Object)
            .AddSingleton(Mock.Of<IUnityService>())
            .BuildServiceProvider();

        var context = new CliContext(services, new CliOutput(new StringWriter(), isJson: false));
        var code = SetupVerb.Run(context, options);
        return (code, setup);
    }

    private static SetupOptions Options(string? ksp2 = null) => new() { Path = ProjectPath, Ksp2 = ksp2 };

    [Test]
    public void AlreadySetUp_Succeeds_WithoutRunning()
    {
        var (code, setup) = RunSetup(Options(), alreadySetUp: true, ConfiguredKsp2, ProjectSetupResult.Completed);

        Assert.That(code, Is.EqualTo(ExitCode.SUCCESS));
        setup.Verify(s => s.RunSetupAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<System.IProgress<ProjectSetupProgress>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public void NoKsp2Path_IsUsageError()
    {
        var (code, setup) = RunSetup(Options(ksp2: null), alreadySetUp: false, configuredKsp2: "", ProjectSetupResult.Completed);

        Assert.That(code, Is.EqualTo(ExitCode.USAGE_ERROR));
        setup.Verify(s => s.RunSetupAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<System.IProgress<ProjectSetupProgress>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public void RunsSetup_WithConfiguredKsp2_OnSuccess()
    {
        var (code, setup) = RunSetup(Options(), alreadySetUp: false, ConfiguredKsp2, ProjectSetupResult.Completed);

        Assert.That(code, Is.EqualTo(ExitCode.SUCCESS));
        setup.Verify(s => s.RunSetupAsync(ProjectPath, ConfiguredKsp2,
            It.IsAny<System.IProgress<ProjectSetupProgress>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public void OverrideKsp2_TakesPrecedence()
    {
        var (code, setup) = RunSetup(Options(ksp2: @"D:\alt\KSP2_x64.exe"), alreadySetUp: false, ConfiguredKsp2, ProjectSetupResult.Completed);

        Assert.That(code, Is.EqualTo(ExitCode.SUCCESS));
        setup.Verify(s => s.RunSetupAsync(ProjectPath, @"D:\alt\KSP2_x64.exe",
            It.IsAny<System.IProgress<ProjectSetupProgress>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public void Failed_ReturnsFailure()
    {
        var (code, _) = RunSetup(Options(), alreadySetUp: false, ConfiguredKsp2, ProjectSetupResult.Failed);

        Assert.That(code, Is.EqualTo(ExitCode.FAILED));
    }
}
