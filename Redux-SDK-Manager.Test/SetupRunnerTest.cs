using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Redux_SDK_Manager.Cli;
using Redux_SDK_Manager.Cli.Verbs;
using Redux_SDK_Manager.Models;
using Redux_SDK_Manager.Services;

namespace Redux_SDK_Manager.Test;

public class SetupRunnerTest
{
    private const string ProjectPath = @"C:\proj";
    private const string ConfiguredKsp2 = @"C:\ksp2\KSP2_x64.exe";

    private static (Mock<IProjectSetupService> setup, CliContext context) Build(SdkManagerConfig config)
    {
        var configMock = new Mock<IConfigService>();
        configMock.Setup(c => c.Config).Returns(config);

        var setup = new Mock<IProjectSetupService>();
        setup.Setup(s => s.IsAlreadySetUp(It.IsAny<string>())).Returns(false);
        setup.Setup(s => s.RunSetupAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<System.IProgress<ProjectSetupProgress>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProjectSetupResult.Completed);

        var services = new ServiceCollection()
            .AddSingleton(configMock.Object)
            .AddSingleton(setup.Object)
            .BuildServiceProvider();

        var context = new CliContext(services, new CliOutput(new StringWriter(), isJson: false));
        return (setup, context);
    }

    [Test]
    public void RunsSetup_WhenEnabledWithConfiguredKsp2()
    {
        var config = new SdkManagerConfig { AutoRunProjectSetup = true, Ksp2ExePath = ConfiguredKsp2 };
        var (setup, context) = Build(config);

        SetupRunner.RunAfter(context, ProjectPath, new CreateOptions());

        setup.Verify(s => s.RunSetupAsync(ProjectPath, ConfiguredKsp2,
            It.IsAny<System.IProgress<ProjectSetupProgress>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public void SkipsSetup_WhenNoSetupFlag()
    {
        var config = new SdkManagerConfig { AutoRunProjectSetup = true, Ksp2ExePath = ConfiguredKsp2 };
        var (setup, context) = Build(config);

        SetupRunner.RunAfter(context, ProjectPath, new CreateOptions { NoSetup = true });

        setup.Verify(s => s.RunSetupAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<System.IProgress<ProjectSetupProgress>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public void SkipsSetup_WhenAutoRunOffAndNoOverride()
    {
        var config = new SdkManagerConfig { AutoRunProjectSetup = false, Ksp2ExePath = ConfiguredKsp2 };
        var (setup, context) = Build(config);

        SetupRunner.RunAfter(context, ProjectPath, new CreateOptions());

        setup.Verify(s => s.RunSetupAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<System.IProgress<ProjectSetupProgress>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public void RunsSetup_WhenKsp2OverridePassed_EvenWithAutoRunOff()
    {
        var config = new SdkManagerConfig { AutoRunProjectSetup = false, Ksp2ExePath = "" };
        var (setup, context) = Build(config);

        SetupRunner.RunAfter(context, ProjectPath, new CreateOptions { Ksp2 = @"D:\games\KSP2_x64.exe" });

        setup.Verify(s => s.RunSetupAsync(ProjectPath, @"D:\games\KSP2_x64.exe",
            It.IsAny<System.IProgress<ProjectSetupProgress>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public void SkipsSetup_WhenAlreadyImported()
    {
        var config = new SdkManagerConfig { AutoRunProjectSetup = true, Ksp2ExePath = ConfiguredKsp2 };
        var (setup, context) = Build(config);
        setup.Setup(s => s.IsAlreadySetUp(ProjectPath)).Returns(true);

        SetupRunner.RunAfter(context, ProjectPath, new CreateOptions());

        setup.Verify(s => s.RunSetupAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<System.IProgress<ProjectSetupProgress>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
