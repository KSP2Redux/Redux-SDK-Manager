using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Redux_SDK_Manager.Models;
using Redux_SDK_Manager.Services;
using Redux_SDK_Manager.Wrappers;
using Testably.Abstractions.Testing;

namespace Redux_SDK_Manager.Test;

public class ProjectSetupServiceTest
{
    private const string ProjectPath = @"C:\proj";
    private const string Ksp2Exe = @"C:\ksp2\KSP2_x64.exe";
    private const string EditorExe = @"C:\unity\6000.5.0f1\Editor\Unity.exe";
    private const string EditorVersion = "6000.5.0f1";
    private static readonly string StatusPath = @"C:\proj\Library\redux-setup-status.txt";

    private static MockFileSystem WindowsFs()
    {
        var fs = new MockFileSystem(o => o.SimulatingOperatingSystem(SimulationMode.Windows));
        fs.Directory.CreateDirectory(@"C:\ksp2");
        fs.File.WriteAllText(Ksp2Exe, "");
        fs.Directory.CreateDirectory(ProjectPath);
        return fs;
    }

    private static Mock<IUnityService> EditorInstalled()
    {
        var unity = new Mock<IUnityService>();
        unity.Setup(u => u.GetProjectUnityVersion(ProjectPath)).Returns(EditorVersion);
        unity.Setup(u => u.DetectInstalls()).Returns(new List<UnityInstall> { new(EditorVersion, EditorExe) });
        return unity;
    }

    // A process runner that writes the given phase to the status file on each successive launch.
    private static Mock<IProcessRunner> RunnerWritingPhases(MockFileSystem fs, params string[] phasesPerLaunch)
    {
        var runner = new Mock<IProcessRunner>();
        var launch = 0;
        runner.Setup(r => r.RunToExitAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                var phase = phasesPerLaunch[launch < phasesPerLaunch.Length ? launch : phasesPerLaunch.Length - 1];
                launch++;
                fs.File.WriteAllText(StatusPath, phase);
                return Task.FromResult(0);
            });
        return runner;
    }

    private static ProjectSetupService NewService(MockFileSystem fs, IUnityService unity, IProcessRunner runner)
        => new(fs, unity, runner, Mock.Of<ILogService>());

    [Test]
    public async Task RunSetup_ImportThenPipeline_Completes()
    {
        var fs = WindowsFs();
        var runner = RunnerWritingPhases(fs, "import-done|", "done|");
        var service = NewService(fs, EditorInstalled().Object, runner.Object);

        var result = await service.RunSetupAsync(ProjectPath, Ksp2Exe, null, CancellationToken.None);

        Assert.That(result, Is.EqualTo(ProjectSetupResult.Completed));
        runner.Verify(r => r.RunToExitAsync(EditorExe, It.IsAny<IReadOnlyList<string>>(),
            It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Test]
    public async Task RunSetup_ImportError_Fails_WithoutPipeline()
    {
        var fs = WindowsFs();
        var runner = RunnerWritingPhases(fs, "error|boom");
        var service = NewService(fs, EditorInstalled().Object, runner.Object);

        var result = await service.RunSetupAsync(ProjectPath, Ksp2Exe, null, CancellationToken.None);

        Assert.That(result, Is.EqualTo(ProjectSetupResult.Failed));
        runner.Verify(r => r.RunToExitAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
            It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task RunSetup_AlreadyImported_SkipsWithoutLaunching()
    {
        var fs = WindowsFs();
        fs.Directory.CreateDirectory(fs.Path.Combine(ProjectPath, "Packages", "KSP2_x64"));
        var runner = RunnerWritingPhases(fs, "done|");
        var service = NewService(fs, EditorInstalled().Object, runner.Object);

        var result = await service.RunSetupAsync(ProjectPath, Ksp2Exe, null, CancellationToken.None);

        Assert.That(result, Is.EqualTo(ProjectSetupResult.AlreadyDone));
        runner.Verify(r => r.RunToExitAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
            It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task RunSetup_EditorNotInstalled_ReturnsEditorMissing()
    {
        var fs = WindowsFs();
        var unity = new Mock<IUnityService>();
        unity.Setup(u => u.GetProjectUnityVersion(ProjectPath)).Returns(EditorVersion);
        unity.Setup(u => u.DetectInstalls()).Returns(new List<UnityInstall>()); // none installed
        var runner = RunnerWritingPhases(fs, "done|");
        var service = NewService(fs, unity.Object, runner.Object);

        var result = await service.RunSetupAsync(ProjectPath, Ksp2Exe, null, CancellationToken.None);

        Assert.That(result, Is.EqualTo(ProjectSetupResult.EditorMissing));
        runner.Verify(r => r.RunToExitAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
            It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task RunSetup_MissingKsp2_ReturnsNoGamePath()
    {
        var fs = WindowsFs();
        var runner = RunnerWritingPhases(fs, "done|");
        var service = NewService(fs, EditorInstalled().Object, runner.Object);

        var result = await service.RunSetupAsync(ProjectPath, @"C:\nope\KSP2_x64.exe", null, CancellationToken.None);

        Assert.That(result, Is.EqualTo(ProjectSetupResult.NoGamePath));
    }

    [Test]
    public async Task RunSetup_PhaseKilled_ReturnsFailed()
    {
        // A timeout kills the editor: RunToExitAsync surfaces cancellation while the outer token is live.
        var fs = WindowsFs();
        var runner = new Mock<IProcessRunner>();
        runner.Setup(r => r.RunToExitAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new System.OperationCanceledException());
        var service = NewService(fs, EditorInstalled().Object, runner.Object);

        var result = await service.RunSetupAsync(ProjectPath, Ksp2Exe, null, CancellationToken.None);

        Assert.That(result, Is.EqualTo(ProjectSetupResult.Failed));
    }

    [Test]
    public void SetupLogPath_PointsAtProjectLibraryLog()
    {
        var fs = WindowsFs();
        var service = NewService(fs, EditorInstalled().Object, Mock.Of<IProcessRunner>());

        Assert.That(service.SetupLogPath(ProjectPath), Is.EqualTo(@"C:\proj\Library\redux-setup.log"));
    }

    [Test]
    public void IsAlreadySetUp_ReflectsGamePackagePresence()
    {
        var fs = WindowsFs();
        var service = NewService(fs, EditorInstalled().Object, Mock.Of<IProcessRunner>());

        Assert.That(service.IsAlreadySetUp(ProjectPath), Is.False);
        fs.Directory.CreateDirectory(fs.Path.Combine(ProjectPath, "Packages", "KSP2_x64"));
        Assert.That(service.IsAlreadySetUp(ProjectPath), Is.True);
    }

    [Test]
    public void DescribeProgress_MapsPhasesToText()
    {
        Assert.That(ProjectSetupService.DescribeProgress(new ProjectSetupProgress("import", "Import Assemblies")),
            Is.EqualTo("Importing game: Import Assemblies"));
        Assert.That(ProjectSetupService.DescribeProgress(new ProjectSetupProgress("pipeline", "Import KSP2 to Editor")),
            Is.EqualTo("Copying game data..."));
        Assert.That(ProjectSetupService.DescribeProgress(new ProjectSetupProgress("done", "")),
            Is.EqualTo("Setup complete."));
        Assert.That(ProjectSetupService.DescribeProgress(new ProjectSetupProgress("error", "boom")),
            Is.EqualTo("Setup failed: boom"));
    }
}
