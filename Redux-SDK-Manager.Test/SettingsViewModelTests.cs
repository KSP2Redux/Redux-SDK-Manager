using System;
using System.Threading.Tasks;
using Moq;
using Redux_SDK_Manager.Models;
using Redux_SDK_Manager.Services;
using Redux_SDK_Manager.ViewModels;
using Redux_SDK_Manager.Wrappers;

namespace Redux_SDK_Manager.Test;

public class SettingsViewModelTests
{
    private static (SdkManagerConfig config, Mock<IConfigService> mock) Config(bool showSnapshots = false)
    {
        var config = new SdkManagerConfig { ShowSnapshotVersions = showSnapshots };
        var mock = new Mock<IConfigService>();
        mock.Setup(c => c.Config).Returns(config);
        return (config, mock);
    }

    private static SettingsViewModel NewVm(
        IConfigService config,
        IProcessRunner? runner = null,
        IDialogService? dialog = null,
        ILogService? log = null)
        => new(config, runner ?? Mock.Of<IProcessRunner>(), dialog ?? Mock.Of<IDialogService>(), log ?? Mock.Of<ILogService>());

    [Test]
    public void Ctor_SeedsFromConfig_WithoutSaving()
    {
        var (_, configMock) = Config(showSnapshots: true);

        var vm = NewVm(configMock.Object);

        Assert.That(vm.ShowSnapshotVersions, Is.True);
        configMock.Verify(c => c.Save(), Times.Never);
    }

    [Test]
    public void ToggleSnapshots_PersistsAndSaves()
    {
        var (config, configMock) = Config(showSnapshots: false);
        var vm = NewVm(configMock.Object);

        vm.ShowSnapshotVersions = true;

        Assert.That(config.ShowSnapshotVersions, Is.True);
        configMock.Verify(c => c.Save(), Times.Once);
    }

    [Test]
    public async Task OpenLogsFolder_OpensLogsDirectory()
    {
        var (_, configMock) = Config();
        configMock.Setup(c => c.GetLogsDirectory()).Returns(@"C:\logs");
        var runner = new Mock<IProcessRunner>();

        var vm = NewVm(configMock.Object, runner: runner.Object);
        await vm.OpenLogsFolderCommand.ExecuteAsync(null);

        runner.Verify(r => r.OpenUrl(@"C:\logs"), Times.Once);
    }

    [Test]
    public async Task OpenLogsFolder_Fails_Alerts()
    {
        var (_, configMock) = Config();
        configMock.Setup(c => c.GetLogsDirectory()).Returns(@"C:\logs");
        var runner = new Mock<IProcessRunner>();
        runner.Setup(r => r.OpenUrl(It.IsAny<string>())).Throws(new InvalidOperationException("nope"));
        var dialog = new Mock<IDialogService>();
        dialog.Setup(d => d.AlertAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

        var vm = NewVm(configMock.Object, runner: runner.Object, dialog: dialog.Object);
        await vm.OpenLogsFolderCommand.ExecuteAsync(null);

        dialog.Verify(d => d.AlertAsync("Open logs folder", It.IsAny<string>()), Times.Once);
    }
}
