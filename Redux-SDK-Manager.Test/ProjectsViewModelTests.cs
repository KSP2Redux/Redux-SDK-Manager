using System.Collections.Generic;
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

    private static ProjectsViewModel NewVm(
        IConfigService config,
        IProjectInfoService? info = null,
        ITemplateVersionService? version = null,
        IUnityService? unity = null,
        IProjectService? project = null,
        ITemplateCatalogService? catalog = null,
        IGitService? git = null,
        IFilePickerService? picker = null,
        IDialogService? dialog = null,
        ILogService? log = null)
        => new(config, info ?? Mock.Of<IProjectInfoService>(), version ?? Mock.Of<ITemplateVersionService>(),
            unity ?? Mock.Of<IUnityService>(), project ?? Mock.Of<IProjectService>(),
            catalog ?? Mock.Of<ITemplateCatalogService>(), git ?? Mock.Of<IGitService>(),
            picker ?? Mock.Of<IFilePickerService>(), dialog ?? Mock.Of<IDialogService>(), log ?? Mock.Of<ILogService>());

    private static Mock<IGitService> GitAvailable()
    {
        var git = new Mock<IGitService>();
        git.Setup(g => g.IsInstalled()).Returns(true);
        return git;
    }

    private static Mock<ITemplateCatalogService> CatalogWith(params string[] versions)
    {
        var catalog = new Mock<ITemplateCatalogService>();
        IReadOnlyList<TemplateVersion> parsed = versions.Select(TemplateVersion.Parse).ToList();
        catalog.Setup(c => c.ListAvailableVersions()).Returns(parsed);
        return catalog;
    }

    private static Mock<IDialogService> DialogSelecting(string version)
    {
        var dialog = new Mock<IDialogService>();
        dialog.Setup(d => d.SelectVersionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<TemplateVersion>>()))
            .ReturnsAsync(version);
        return dialog;
    }

    [Test]
    public void Load_PopulatesFromConfig_WithNameAndVersion()
    {
        var (_, configMock) = Config(PathA);
        var info = new Mock<IProjectInfoService>();
        info.Setup(i => i.Read(PathA)).Returns(new ProjectInfo { Name = "Cool Mod", Version = "26w32b" });
        var version = new Mock<ITemplateVersionService>();
        version.Setup(v => v.DetectProjectVersion(PathA)).Returns(TemplateVersion.Parse("26w32b"));

        var vm = NewVm(configMock.Object, info.Object, version.Object);

        Assert.That(vm.HasProjects, Is.True);
        Assert.That(vm.Projects, Has.Count.EqualTo(1));
        Assert.That(vm.Projects[0].Name, Is.EqualTo("Cool Mod"));
        Assert.That(vm.Projects[0].VersionLabel, Does.Contain("26w32b"));
    }

    [Test]
    public void Load_FallsBackToFolderName_WhenNoProjectInfoName()
    {
        var (_, configMock) = Config(PathA);

        var vm = NewVm(configMock.Object);

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

        var vm = NewVm(configMock.Object, dialog: dialog.Object);
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

        var vm = NewVm(configMock.Object, dialog: dialog.Object);
        await vm.RemoveCommand.ExecuteAsync(vm.Projects[0]);

        Assert.That(config.ProjectPaths, Has.Count.EqualTo(1));
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

        var vm = NewVm(configMock.Object, unity: unity.Object, dialog: dialog.Object);
        await vm.OpenCommand.ExecuteAsync(vm.Projects[0]);

        unity.Verify(u => u.OpenProject(PathA), Times.Once);
        dialog.Verify(d => d.AlertAsync("Open project", It.IsAny<string>()), Times.Once);
        Assert.That(vm.IsBusy, Is.False);
    }

    [Test]
    public async Task CreateProject_PicksVersionAndFolder_ThenCreates()
    {
        var (config, configMock) = Config();
        config.ShowSnapshotVersions = true;
        var picker = new Mock<IFilePickerService>();
        picker.Setup(p => p.PickFolderAsync(It.IsAny<string>())).ReturnsAsync(@"C:\new\Mod");
        var project = new Mock<IProjectService>();

        var vm = NewVm(configMock.Object, git: GitAvailable().Object, catalog: CatalogWith("26w32b").Object,
            picker: picker.Object, dialog: DialogSelecting("26w32b").Object, project: project.Object);
        await vm.CreateProjectCommand.ExecuteAsync(null);

        project.Verify(p => p.CreateProject(It.Is<TemplateVersion>(v => v.Raw == "26w32b"), @"C:\new\Mod"), Times.Once);
    }

    [Test]
    public async Task CreateProject_AbortsAndOffersInstall_WhenGitMissing()
    {
        var (_, configMock) = Config();
        var git = new Mock<IGitService>();
        git.Setup(g => g.IsInstalled()).Returns(false);
        var dialog = new Mock<IDialogService>();
        dialog.Setup(d => d.OfferLinkAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        var project = new Mock<IProjectService>();

        var vm = NewVm(configMock.Object, git: git.Object, dialog: dialog.Object, project: project.Object);
        await vm.CreateProjectCommand.ExecuteAsync(null);

        dialog.Verify(d => d.OfferLinkAsync("Git required", It.IsAny<string>(), "Install Git", It.IsAny<string>()), Times.Once);
        project.Verify(p => p.CreateProject(It.IsAny<TemplateVersion>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task Open_HubMissing_OffersUnityHubInstall()
    {
        var (_, configMock) = Config(PathA);
        var unity = new Mock<IUnityService>();
        unity.Setup(u => u.OpenProject(PathA)).Returns(OpenProjectResult.HubUnavailable);
        var dialog = new Mock<IDialogService>();
        dialog.Setup(d => d.OfferLinkAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var vm = NewVm(configMock.Object, unity: unity.Object, dialog: dialog.Object);
        await vm.OpenCommand.ExecuteAsync(vm.Projects[0]);

        dialog.Verify(d => d.OfferLinkAsync("Unity Hub required", It.IsAny<string>(), "Install Unity Hub", It.IsAny<string>()), Times.Once);
        dialog.Verify(d => d.AlertAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task AddProject_ManagedFolder_Imports_WithoutPickingVersion()
    {
        var (_, configMock) = Config();
        const string managed = @"C:\existing\Managed";
        var picker = new Mock<IFilePickerService>();
        picker.Setup(p => p.PickFolderAsync(It.IsAny<string>())).ReturnsAsync(managed);
        var version = new Mock<ITemplateVersionService>();
        version.Setup(v => v.DetectProjectVersion(managed)).Returns(TemplateVersion.Parse("26w32b"));
        var project = new Mock<IProjectService>();
        var dialog = new Mock<IDialogService>();

        var vm = NewVm(configMock.Object, version: version.Object, picker: picker.Object,
            dialog: dialog.Object, project: project.Object);
        await vm.AddProjectCommand.ExecuteAsync(null);

        project.Verify(p => p.ImportProject(managed), Times.Once);
        project.Verify(p => p.IngestProject(It.IsAny<string>(), It.IsAny<TemplateVersion>()), Times.Never);
        dialog.Verify(d => d.SelectVersionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<TemplateVersion>>()), Times.Never);
    }

    [Test]
    public async Task AddProject_UnmanagedFolder_IngestsAtPickedVersion()
    {
        var (config, configMock) = Config();
        config.ShowSnapshotVersions = true;
        const string unmanaged = @"C:\existing\Raw";
        var picker = new Mock<IFilePickerService>();
        picker.Setup(p => p.PickFolderAsync(It.IsAny<string>())).ReturnsAsync(unmanaged);
        var version = new Mock<ITemplateVersionService>();
        version.Setup(v => v.DetectProjectVersion(unmanaged)).Returns((TemplateVersion?)null);
        var project = new Mock<IProjectService>();

        var vm = NewVm(configMock.Object, version: version.Object, git: GitAvailable().Object,
            catalog: CatalogWith("26w32b").Object, picker: picker.Object,
            dialog: DialogSelecting("26w32b").Object, project: project.Object);
        await vm.AddProjectCommand.ExecuteAsync(null);

        project.Verify(p => p.IngestProject(unmanaged, It.Is<TemplateVersion>(v => v.Raw == "26w32b")), Times.Once);
        project.Verify(p => p.ImportProject(It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task PickVersion_HidesSnapshots_WhenSettingOff()
    {
        var (_, configMock) = Config(); // ShowSnapshotVersions defaults to false
        var picker = new Mock<IFilePickerService>();
        picker.Setup(p => p.PickFolderAsync(It.IsAny<string>())).ReturnsAsync(@"C:\new\Mod");

        IReadOnlyList<TemplateVersion>? offered = null;
        var dialog = new Mock<IDialogService>();
        dialog.Setup(d => d.SelectVersionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<TemplateVersion>>()))
            .Callback<string, string, IReadOnlyList<TemplateVersion>>((_, _, v) => offered = v)
            .ReturnsAsync("0.2.10.0");

        var vm = NewVm(configMock.Object, git: GitAvailable().Object,
            catalog: CatalogWith("0.2.10.0", "26w32a", "26w32b").Object, picker: picker.Object, dialog: dialog.Object);
        await vm.CreateProjectCommand.ExecuteAsync(null);

        Assert.That(offered, Is.Not.Null);
        Assert.That(offered!.Select(v => v.Raw), Is.EqualTo(new[] { "0.2.10.0" }));
    }

    [Test]
    public async Task PickVersion_ShowsSnapshots_WhenSettingOn()
    {
        var (config, configMock) = Config();
        config.ShowSnapshotVersions = true;
        var picker = new Mock<IFilePickerService>();
        picker.Setup(p => p.PickFolderAsync(It.IsAny<string>())).ReturnsAsync(@"C:\new\Mod");

        IReadOnlyList<TemplateVersion>? offered = null;
        var dialog = new Mock<IDialogService>();
        dialog.Setup(d => d.SelectVersionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<TemplateVersion>>()))
            .Callback<string, string, IReadOnlyList<TemplateVersion>>((_, _, v) => offered = v)
            .ReturnsAsync("26w32b");

        var vm = NewVm(configMock.Object, git: GitAvailable().Object,
            catalog: CatalogWith("0.2.10.0", "26w32a", "26w32b").Object, picker: picker.Object, dialog: dialog.Object);
        await vm.CreateProjectCommand.ExecuteAsync(null);

        Assert.That(offered, Is.Not.Null);
        Assert.That(offered!.Select(v => v.Raw), Is.EquivalentTo(new[] { "0.2.10.0", "26w32a", "26w32b" }));
    }

    [Test]
    public async Task PickVersion_AlertsNoVersions_WhenOnlySnapshotsAndSettingOff()
    {
        var (_, configMock) = Config();
        var picker = new Mock<IFilePickerService>();
        picker.Setup(p => p.PickFolderAsync(It.IsAny<string>())).ReturnsAsync(@"C:\new\Mod");
        var dialog = new Mock<IDialogService>();
        dialog.Setup(d => d.AlertAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
        var project = new Mock<IProjectService>();

        var vm = NewVm(configMock.Object, git: GitAvailable().Object,
            catalog: CatalogWith("26w32a", "26w32b").Object, picker: picker.Object,
            dialog: dialog.Object, project: project.Object);
        await vm.CreateProjectCommand.ExecuteAsync(null);

        dialog.Verify(d => d.AlertAsync("No versions", It.IsAny<string>()), Times.Once);
        dialog.Verify(d => d.SelectVersionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<TemplateVersion>>()), Times.Never);
        project.Verify(p => p.CreateProject(It.IsAny<TemplateVersion>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task Upgrade_PicksVersion_ThenUpgrades()
    {
        var (config, configMock) = Config(PathA);
        config.ShowSnapshotVersions = true;
        var project = new Mock<IProjectService>();

        var vm = NewVm(configMock.Object, git: GitAvailable().Object, catalog: CatalogWith("26w32b").Object,
            dialog: DialogSelecting("26w32b").Object, project: project.Object);
        await vm.UpgradeCommand.ExecuteAsync(vm.Projects[0]);

        project.Verify(p => p.UpgradeProject(PathA, It.Is<TemplateVersion>(v => v.Raw == "26w32b")), Times.Once);
    }
}
