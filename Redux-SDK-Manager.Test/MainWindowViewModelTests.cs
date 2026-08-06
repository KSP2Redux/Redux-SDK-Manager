using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using Redux_SDK_Manager.Models;
using Redux_SDK_Manager.Services;
using Redux_SDK_Manager.ViewModels;
using Redux_SDK_Manager.Wrappers;
using Testably.Abstractions.Testing;

namespace Redux_SDK_Manager.Test;

public class MainWindowViewModelTests
{
    private static ProjectsViewModel NewProjects()
    {
        var config = new Mock<IConfigService>();
        config.Setup(c => c.Config).Returns(new SdkManagerConfig());
        return new ProjectsViewModel(config.Object, Mock.Of<IProjectInfoService>(),
            Mock.Of<ITemplateVersionService>(), Mock.Of<IUnityService>(), Mock.Of<IProjectService>(),
            Mock.Of<ITemplateCatalogService>(), Mock.Of<IGitService>(), Mock.Of<IFilePickerService>(),
            Mock.Of<IDialogService>(), new MockFileSystem(o => o.SimulatingOperatingSystem(SimulationMode.Windows)),
            Mock.Of<ILogService>());
    }

    private static SettingsViewModel NewSettings()
    {
        var config = new Mock<IConfigService>();
        config.Setup(c => c.Config).Returns(new SdkManagerConfig());
        return new SettingsViewModel(config.Object, Mock.Of<IProcessRunner>(),
            Mock.Of<IDialogService>(), Mock.Of<IUpdateCoordinator>(), Mock.Of<IAppVersion>(), Mock.Of<ILogService>());
    }

    private static VersionsViewModel NewVersions(Mock<ITemplateCatalogService>? catalog = null)
    {
        var config = new Mock<IConfigService>();
        config.Setup(c => c.Config).Returns(new SdkManagerConfig());
        catalog ??= new Mock<ITemplateCatalogService>();
        catalog.Setup(c => c.DescribeVersions()).Returns(new List<TemplateVersionInfo>());
        var unity = new Mock<IUnityService>();
        unity.Setup(u => u.DetectInstalls()).Returns(new List<UnityInstall>());
        return new VersionsViewModel(config.Object, catalog.Object,
            unity.Object, Mock.Of<IDialogService>(), Mock.Of<ILogService>());
    }

    private static MainWindowViewModel NewViewModel(VersionsViewModel? versions = null)
        => new(NewProjects(), versions ?? NewVersions(), NewSettings(), Mock.Of<IDialogService>());

    [Test]
    public void CurrentTab_DefaultsToProjects()
    {
        var vm = NewViewModel();

        Assert.Multiple(() =>
        {
            Assert.That(vm.CurrentTab, Is.EqualTo(MainWindowViewModel.ProjectsTabId));
            Assert.That(vm.CurrentPage, Is.SameAs(vm.Projects));
        });
    }

    [Test]
    public void GoToTab_SwitchesCurrentTabAndPage()
    {
        var vm = NewViewModel();

        vm.GoToTabCommand.Execute("1");
        Assert.Multiple(() =>
        {
            Assert.That(vm.CurrentTab, Is.EqualTo(MainWindowViewModel.VersionsTabId));
            Assert.That(vm.CurrentPage, Is.SameAs(vm.Versions));
        });

        vm.GoToTabCommand.Execute("2");
        Assert.Multiple(() =>
        {
            Assert.That(vm.CurrentTab, Is.EqualTo(MainWindowViewModel.SettingsTabId));
            Assert.That(vm.CurrentPage, Is.SameAs(vm.Settings));
        });
    }

    [Test]
    public async Task SwitchingToVersionsTab_RefreshesTheCatalog()
    {
        var catalog = new Mock<ITemplateCatalogService>();
        var versions = NewVersions(catalog);
        var vm = NewViewModel(versions);

        vm.GoToTabCommand.Execute("1"); // Versions
        await versions.RefreshCommand.ExecutionTask!;

        catalog.Verify(c => c.DescribeVersions(), Times.Once);
    }

    [Test]
    public void GoToTab_IgnoresNonNumericArgument()
    {
        var vm = NewViewModel();

        vm.GoToTabCommand.Execute("nope");

        Assert.That(vm.CurrentTab, Is.EqualTo(MainWindowViewModel.ProjectsTabId));
    }
}
