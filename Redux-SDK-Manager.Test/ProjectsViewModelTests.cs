using System.Threading.Tasks;
using Moq;
using Redux_SDK_Manager.Models;
using Redux_SDK_Manager.Services;
using Redux_SDK_Manager.ViewModels;

namespace Redux_SDK_Manager.Test;

public class ProjectsViewModelTests
{
    private const string PathA = @"C:\projects\CoolMod";

    private static (SdkManagerConfig config, Mock<IConfigService> configMock) Config(params string[] paths)
    {
        var config = new SdkManagerConfig();
        config.ProjectPaths.AddRange(paths);
        var mock = new Mock<IConfigService>();
        mock.Setup(c => c.Config).Returns(config);
        return (config, mock);
    }

    [Test]
    public void Load_PopulatesFromConfig_WithNameAndVersion()
    {
        var (_, configMock) = Config(PathA);
        var info = new Mock<IProjectInfoService>();
        info.Setup(i => i.Read(PathA)).Returns(new ProjectInfo { Name = "Cool Mod", Version = "26w32b" });
        var version = new Mock<ITemplateVersionService>();
        version.Setup(v => v.DetectProjectVersion(PathA)).Returns(TemplateVersion.Parse("26w32b"));

        var vm = new ProjectsViewModel(configMock.Object, info.Object, version.Object,
            Mock.Of<IUnityService>(), Mock.Of<IDialogService>(), Mock.Of<ILogService>());

        Assert.That(vm.HasProjects, Is.True);
        Assert.That(vm.Projects, Has.Count.EqualTo(1));
        Assert.That(vm.Projects[0].Name, Is.EqualTo("Cool Mod"));
        Assert.That(vm.Projects[0].VersionLabel, Does.Contain("26w32b"));
    }

    [Test]
    public void Load_FallsBackToFolderName_WhenNoProjectInfoName()
    {
        var (_, configMock) = Config(PathA);

        var vm = new ProjectsViewModel(configMock.Object, Mock.Of<IProjectInfoService>(),
            Mock.Of<ITemplateVersionService>(), Mock.Of<IUnityService>(),
            Mock.Of<IDialogService>(), Mock.Of<ILogService>());

        Assert.That(vm.Projects[0].Name, Is.EqualTo("CoolMod"));
        Assert.That(vm.Projects[0].VersionLabel, Is.EqualTo("unknown version"));
    }

    [Test]
    public async Task Remove_Confirmed_UntracksAndSaves()
    {
        var (config, configMock) = Config(PathA);
        var dialog = new Mock<IDialogService>();
        dialog.Setup(d => d.ConfirmAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);

        var vm = new ProjectsViewModel(configMock.Object, Mock.Of<IProjectInfoService>(),
            Mock.Of<ITemplateVersionService>(), Mock.Of<IUnityService>(), dialog.Object, Mock.Of<ILogService>());

        await vm.RemoveCommand.ExecuteAsync(vm.Projects[0]);

        Assert.That(config.ProjectPaths, Is.Empty);
        Assert.That(vm.Projects, Is.Empty);
        Assert.That(vm.HasProjects, Is.False);
        configMock.Verify(c => c.Save(), Times.Once);
    }

    [Test]
    public async Task Remove_Declined_KeepsProject()
    {
        var (config, configMock) = Config(PathA);
        var dialog = new Mock<IDialogService>();
        dialog.Setup(d => d.ConfirmAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);

        var vm = new ProjectsViewModel(configMock.Object, Mock.Of<IProjectInfoService>(),
            Mock.Of<ITemplateVersionService>(), Mock.Of<IUnityService>(), dialog.Object, Mock.Of<ILogService>());

        await vm.RemoveCommand.ExecuteAsync(vm.Projects[0]);

        Assert.That(config.ProjectPaths, Has.Count.EqualTo(1));
        Assert.That(vm.Projects, Has.Count.EqualTo(1));
        configMock.Verify(c => c.Save(), Times.Never);
    }

    [Test]
    public async Task Open_InvokesUnityService_AndReportsResult()
    {
        var (_, configMock) = Config(PathA);
        var unity = new Mock<IUnityService>();
        unity.Setup(u => u.OpenProject(PathA)).Returns(OpenProjectResult.Opened);
        var dialog = new Mock<IDialogService>();
        dialog.Setup(d => d.AlertAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

        var vm = new ProjectsViewModel(configMock.Object, Mock.Of<IProjectInfoService>(),
            Mock.Of<ITemplateVersionService>(), unity.Object, dialog.Object, Mock.Of<ILogService>());

        await vm.OpenCommand.ExecuteAsync(vm.Projects[0]);

        unity.Verify(u => u.OpenProject(PathA), Times.Once);
        dialog.Verify(d => d.AlertAsync("Open project", It.IsAny<string>()), Times.Once);
        Assert.That(vm.IsBusy, Is.False);
    }
}
