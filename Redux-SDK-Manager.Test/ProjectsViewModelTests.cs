using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Redux_SDK_Manager.Models;
using Redux_SDK_Manager.Services;
using Redux_SDK_Manager.ViewModels;
using Redux_SDK_Manager.Wrappers;
using Testably.Abstractions.Testing;

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

    private static MockFileSystem WindowsFileSystem() =>
        new(o => o.SimulatingOperatingSystem(SimulationMode.Windows));

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
        IProcessRunner? processRunner = null,
        IProjectSetupService? setup = null,
        IKsp2DetectorService? ksp2Detector = null,
        IFileSystem? fileSystem = null,
        ILogService? log = null)
        => new(config, info ?? Mock.Of<IProjectInfoService>(), version ?? Mock.Of<ITemplateVersionService>(),
            unity ?? Mock.Of<IUnityService>(), project ?? Mock.Of<IProjectService>(),
            catalog ?? Mock.Of<ITemplateCatalogService>(), git ?? Mock.Of<IGitService>(),
            picker ?? Mock.Of<IFilePickerService>(), dialog ?? Mock.Of<IDialogService>(),
            processRunner ?? Mock.Of<IProcessRunner>(), setup ?? Mock.Of<IProjectSetupService>(),
            ksp2Detector ?? Mock.Of<IKsp2DetectorService>(),
            fileSystem ?? WindowsFileSystem(), log ?? Mock.Of<ILogService>());

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
        dialog.Setup(d => d.SelectVersionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<TemplateVersion>>(), It.IsAny<bool>()))
            .ReturnsAsync(new VersionChoice(version, false));
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
        picker.Setup(p => p.PickFolderAsync(It.IsAny<string>(), It.IsAny<string?>())).ReturnsAsync(@"C:\new\Mod");
        var project = new Mock<IProjectService>();

        var vm = NewVm(configMock.Object, git: GitAvailable().Object, catalog: CatalogWith("26w32b").Object,
            picker: picker.Object, dialog: DialogSelecting("26w32b").Object, project: project.Object);
        await vm.CreateProjectCommand.ExecuteAsync(null);

        project.Verify(p => p.CreateProject(It.Is<TemplateVersion>(v => v.Raw == "26w32b"), @"C:\new\Mod", It.IsAny<bool>()), Times.Once);
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
        project.Verify(p => p.CreateProject(It.IsAny<TemplateVersion>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
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
        picker.Setup(p => p.PickFolderAsync(It.IsAny<string>(), It.IsAny<string?>())).ReturnsAsync(managed);
        var version = new Mock<ITemplateVersionService>();
        version.Setup(v => v.DetectProjectVersion(managed)).Returns(TemplateVersion.Parse("26w32b"));
        var project = new Mock<IProjectService>();
        var dialog = new Mock<IDialogService>();

        var vm = NewVm(configMock.Object, version: version.Object, picker: picker.Object,
            dialog: dialog.Object, project: project.Object);
        await vm.AddProjectCommand.ExecuteAsync(null);

        project.Verify(p => p.ImportProject(managed, It.IsAny<bool>()), Times.Once);
        project.Verify(p => p.IngestProject(It.IsAny<string>(), It.IsAny<TemplateVersion>(), It.IsAny<bool>()), Times.Never);
        dialog.Verify(d => d.SelectVersionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<TemplateVersion>>(), It.IsAny<bool>()), Times.Never);
    }

    [Test]
    public async Task AddProject_UnmanagedFolder_IngestsAtPickedVersion()
    {
        var (config, configMock) = Config();
        config.ShowSnapshotVersions = true;
        const string unmanaged = @"C:\existing\Raw";
        var picker = new Mock<IFilePickerService>();
        picker.Setup(p => p.PickFolderAsync(It.IsAny<string>(), It.IsAny<string?>())).ReturnsAsync(unmanaged);
        var version = new Mock<ITemplateVersionService>();
        version.Setup(v => v.DetectProjectVersion(unmanaged)).Returns((TemplateVersion?)null);
        var project = new Mock<IProjectService>();

        var vm = NewVm(configMock.Object, version: version.Object, git: GitAvailable().Object,
            catalog: CatalogWith("26w32b").Object, picker: picker.Object,
            dialog: DialogSelecting("26w32b").Object, project: project.Object);
        await vm.AddProjectCommand.ExecuteAsync(null);

        project.Verify(p => p.IngestProject(unmanaged, It.Is<TemplateVersion>(v => v.Raw == "26w32b"), It.IsAny<bool>()), Times.Once);
        project.Verify(p => p.ImportProject(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
    }

    [Test]
    public async Task PickVersion_HidesSnapshots_WhenSettingOff()
    {
        var (_, configMock) = Config(); // ShowSnapshotVersions defaults to false
        var picker = new Mock<IFilePickerService>();
        picker.Setup(p => p.PickFolderAsync(It.IsAny<string>(), It.IsAny<string?>())).ReturnsAsync(@"C:\new\Mod");

        IReadOnlyList<TemplateVersion>? offered = null;
        var dialog = new Mock<IDialogService>();
        dialog.Setup(d => d.SelectVersionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<TemplateVersion>>(), It.IsAny<bool>()))
            .Callback<string, string, IReadOnlyList<TemplateVersion>, bool>((_, _, v, _) => offered = v)
            .ReturnsAsync(new VersionChoice("0.2.10.0", false));

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
        picker.Setup(p => p.PickFolderAsync(It.IsAny<string>(), It.IsAny<string?>())).ReturnsAsync(@"C:\new\Mod");

        IReadOnlyList<TemplateVersion>? offered = null;
        var dialog = new Mock<IDialogService>();
        dialog.Setup(d => d.SelectVersionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<TemplateVersion>>(), It.IsAny<bool>()))
            .Callback<string, string, IReadOnlyList<TemplateVersion>, bool>((_, _, v, _) => offered = v)
            .ReturnsAsync(new VersionChoice("26w32b", false));

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
        picker.Setup(p => p.PickFolderAsync(It.IsAny<string>(), It.IsAny<string?>())).ReturnsAsync(@"C:\new\Mod");
        var dialog = new Mock<IDialogService>();
        dialog.Setup(d => d.AlertAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
        var project = new Mock<IProjectService>();

        var vm = NewVm(configMock.Object, git: GitAvailable().Object,
            catalog: CatalogWith("26w32a", "26w32b").Object, picker: picker.Object,
            dialog: dialog.Object, project: project.Object);
        await vm.CreateProjectCommand.ExecuteAsync(null);

        dialog.Verify(d => d.AlertAsync("No versions", It.IsAny<string>()), Times.Once);
        dialog.Verify(d => d.SelectVersionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<TemplateVersion>>(), It.IsAny<bool>()), Times.Never);
        project.Verify(p => p.CreateProject(It.IsAny<TemplateVersion>(), It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
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

        project.Verify(p => p.UpgradeProject(PathA, It.Is<TemplateVersion>(v => v.Raw == "26w32b"), It.IsAny<bool>()), Times.Once);
    }

    private const string Ksp2Exe = @"C:\ksp2\KSP2_x64.exe";

    // A filesystem holding the KSP2 exe, so EnsureKsp2PathAsync accepts a configured path unchanged.
    private static MockFileSystem FileSystemWithKsp2()
    {
        var fs = WindowsFileSystem();
        fs.Directory.CreateDirectory(@"C:\ksp2");
        fs.File.WriteAllText(Ksp2Exe, "");
        return fs;
    }

    private static Mock<IProjectSetupService> SetupReturning(ProjectSetupResult result)
    {
        var setup = new Mock<IProjectSetupService>();
        setup.Setup(s => s.IsAlreadySetUp(It.IsAny<string>())).Returns(false);
        setup.Setup(s => s.RunSetupAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IProgress<ProjectSetupProgress>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        return setup;
    }

    // A project service whose create registers the path in config, so a row exists for setup to attach to
    // (mirrors the real service adding the project and the view model reloading the list).
    private static Mock<IProjectService> ProjectRegistering(SdkManagerConfig config, string path)
    {
        var project = new Mock<IProjectService>();
        project.Setup(p => p.CreateProject(It.IsAny<TemplateVersion>(), path, It.IsAny<bool>()))
            .Callback(() => config.ProjectPaths.Add(path));
        return project;
    }

    [Test]
    public async Task CreateProject_RunsSetup_WithProjectPathAndConfiguredKsp2()
    {
        var (config, configMock) = Config();
        config.ShowSnapshotVersions = true;
        config.Ksp2ExePath = Ksp2Exe; // AutoRunProjectSetup defaults on
        var picker = new Mock<IFilePickerService>();
        picker.Setup(p => p.PickFolderAsync(It.IsAny<string>(), It.IsAny<string?>())).ReturnsAsync(@"C:\new\Mod");
        var setup = SetupReturning(ProjectSetupResult.Completed);

        var vm = NewVm(configMock.Object, git: GitAvailable().Object, catalog: CatalogWith("26w32b").Object,
            picker: picker.Object, dialog: DialogSelecting("26w32b").Object,
            project: ProjectRegistering(config, @"C:\new\Mod").Object,
            setup: setup.Object, fileSystem: FileSystemWithKsp2());
        await vm.CreateProjectCommand.ExecuteAsync(null);

        setup.Verify(s => s.RunSetupAsync(@"C:\new\Mod", Ksp2Exe,
            It.IsAny<IProgress<ProjectSetupProgress>?>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.That(vm.AnySettingUp, Is.False);
    }

    [Test]
    public async Task CreateProject_SkipsSetup_WhenAutoRunOff()
    {
        var (config, configMock) = Config();
        config.ShowSnapshotVersions = true;
        config.AutoRunProjectSetup = false;
        config.Ksp2ExePath = Ksp2Exe;
        var picker = new Mock<IFilePickerService>();
        picker.Setup(p => p.PickFolderAsync(It.IsAny<string>(), It.IsAny<string?>())).ReturnsAsync(@"C:\new\Mod");
        var setup = SetupReturning(ProjectSetupResult.Completed);

        var vm = NewVm(configMock.Object, git: GitAvailable().Object, catalog: CatalogWith("26w32b").Object,
            picker: picker.Object, dialog: DialogSelecting("26w32b").Object, project: new Mock<IProjectService>().Object,
            setup: setup.Object, fileSystem: FileSystemWithKsp2());
        await vm.CreateProjectCommand.ExecuteAsync(null);

        setup.Verify(s => s.RunSetupAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<IProgress<ProjectSetupProgress>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task CreateProject_SkipsSetup_WhenAlreadyImported()
    {
        var (config, configMock) = Config();
        config.ShowSnapshotVersions = true;
        config.Ksp2ExePath = Ksp2Exe;
        var picker = new Mock<IFilePickerService>();
        picker.Setup(p => p.PickFolderAsync(It.IsAny<string>(), It.IsAny<string?>())).ReturnsAsync(@"C:\new\Mod");
        var setup = new Mock<IProjectSetupService>();
        setup.Setup(s => s.IsAlreadySetUp(@"C:\new\Mod")).Returns(true);

        var vm = NewVm(configMock.Object, git: GitAvailable().Object, catalog: CatalogWith("26w32b").Object,
            picker: picker.Object, dialog: DialogSelecting("26w32b").Object, project: new Mock<IProjectService>().Object,
            setup: setup.Object, fileSystem: FileSystemWithKsp2());
        await vm.CreateProjectCommand.ExecuteAsync(null);

        setup.Verify(s => s.RunSetupAsync(It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<IProgress<ProjectSetupProgress>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task CreateProject_SetupEditorMissing_AlertsWithExactMessage()
    {
        var (config, configMock) = Config();
        config.ShowSnapshotVersions = true;
        config.Ksp2ExePath = Ksp2Exe;
        var picker = new Mock<IFilePickerService>();
        picker.Setup(p => p.PickFolderAsync(It.IsAny<string>(), It.IsAny<string?>())).ReturnsAsync(@"C:\new\Mod");
        var dialog = DialogSelecting("26w32b");
        dialog.Setup(d => d.AlertAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
        var setup = SetupReturning(ProjectSetupResult.EditorMissing);

        var vm = NewVm(configMock.Object, git: GitAvailable().Object, catalog: CatalogWith("26w32b").Object,
            picker: picker.Object, dialog: dialog.Object, project: ProjectRegistering(config, @"C:\new\Mod").Object,
            setup: setup.Object, fileSystem: FileSystemWithKsp2());
        await vm.CreateProjectCommand.ExecuteAsync(null);

        dialog.Verify(d => d.AlertAsync("Automated setup", ProjectSetupService.EditorMissingMessage), Times.Once);
    }

    [Test]
    public async Task CreateProject_NoKsp2Path_Detects_ThenRunsSetup_AndSaves()
    {
        var (config, configMock) = Config(); // Ksp2ExePath empty
        config.ShowSnapshotVersions = true;
        var picker = new Mock<IFilePickerService>();
        picker.Setup(p => p.PickFolderAsync(It.IsAny<string>(), It.IsAny<string?>())).ReturnsAsync(@"C:\new\Mod");
        var detector = new Mock<IKsp2DetectorService>();
        detector.Setup(d => d.DetectKsp2InstallLocation()).Returns(Ksp2Exe);
        var dialog = DialogSelecting("26w32b");
        // "Use" the detected install.
        dialog.Setup(d => d.ConfirmAsync("KSP2 found", It.IsAny<string>(), "Use", "Choose another")).ReturnsAsync(true);
        var setup = SetupReturning(ProjectSetupResult.Completed);

        var vm = NewVm(configMock.Object, git: GitAvailable().Object, catalog: CatalogWith("26w32b").Object,
            picker: picker.Object, dialog: dialog.Object, project: ProjectRegistering(config, @"C:\new\Mod").Object,
            setup: setup.Object, ksp2Detector: detector.Object, fileSystem: FileSystemWithKsp2());
        await vm.CreateProjectCommand.ExecuteAsync(null);

        setup.Verify(s => s.RunSetupAsync(@"C:\new\Mod", Ksp2Exe,
            It.IsAny<IProgress<ProjectSetupProgress>?>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.That(config.Ksp2ExePath, Is.EqualTo(Ksp2Exe));
    }

    [Test]
    public void OpenFolder_OpensProjectPath()
    {
        var (_, configMock) = Config(PathA);
        var runner = new Mock<IProcessRunner>();

        var vm = NewVm(configMock.Object, processRunner: runner.Object);
        vm.OpenFolderCommand.Execute(vm.Projects[0]);

        runner.Verify(r => r.OpenUrl(PathA), Times.Once);
    }

    [Test]
    public async Task CreateProject_SeedsPickerFromLastDir_AndRemembersParent()
    {
        var (config, configMock) = Config();
        config.ShowSnapshotVersions = true;
        config.LastProjectDirectory = @"C:\old\parent";
        string? seededStart = null;
        var picker = new Mock<IFilePickerService>();
        picker.Setup(p => p.PickFolderAsync(It.IsAny<string>(), It.IsAny<string?>()))
            .Callback<string, string?>((_, start) => seededStart = start)
            .ReturnsAsync(@"C:\new\Mod");

        var vm = NewVm(configMock.Object, git: GitAvailable().Object, catalog: CatalogWith("26w32b").Object,
            picker: picker.Object, dialog: DialogSelecting("26w32b").Object,
            project: ProjectRegistering(config, @"C:\new\Mod").Object);
        await vm.CreateProjectCommand.ExecuteAsync(null);

        Assert.That(seededStart, Is.EqualTo(@"C:\old\parent"));
        Assert.That(config.LastProjectDirectory, Is.EqualTo(@"C:\new"));
    }

    [Test]
    public async Task CreateProject_SetupFailed_AlertNamesLogPath()
    {
        var (config, configMock) = Config();
        config.ShowSnapshotVersions = true;
        config.Ksp2ExePath = Ksp2Exe;
        var picker = new Mock<IFilePickerService>();
        picker.Setup(p => p.PickFolderAsync(It.IsAny<string>(), It.IsAny<string?>())).ReturnsAsync(@"C:\new\Mod");
        var dialog = DialogSelecting("26w32b");
        dialog.Setup(d => d.AlertAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
        var setup = SetupReturning(ProjectSetupResult.Failed);
        setup.Setup(s => s.SetupLogPath(@"C:\new\Mod")).Returns(@"C:\new\Mod\Library\redux-setup.log");

        var vm = NewVm(configMock.Object, git: GitAvailable().Object, catalog: CatalogWith("26w32b").Object,
            picker: picker.Object, dialog: dialog.Object, project: ProjectRegistering(config, @"C:\new\Mod").Object,
            setup: setup.Object, fileSystem: FileSystemWithKsp2());
        await vm.CreateProjectCommand.ExecuteAsync(null);

        dialog.Verify(d => d.AlertAsync("Automated setup failed",
            It.Is<string>(m => m.Contains(@"C:\new\Mod\Library\redux-setup.log"))), Times.Once);
    }

    [Test]
    public async Task CreateProject_SetupUnityMismatch_AlertsSkippedWithVersions()
    {
        var (config, configMock) = Config();
        config.ShowSnapshotVersions = true;
        config.Ksp2ExePath = Ksp2Exe;
        var picker = new Mock<IFilePickerService>();
        picker.Setup(p => p.PickFolderAsync(It.IsAny<string>(), It.IsAny<string?>())).ReturnsAsync(@"C:\new\Mod");
        var dialog = DialogSelecting("26w32b");
        dialog.Setup(d => d.AlertAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);
        var setup = SetupReturning(ProjectSetupResult.UnityVersionMismatch);
        var unity = new Mock<IUnityService>();
        unity.Setup(u => u.GetGameUnityVersion(It.IsAny<string>())).Returns("6000.5.0f1");
        unity.Setup(u => u.GetProjectUnityVersion(@"C:\new\Mod")).Returns("6000.4.1f1");

        var vm = NewVm(configMock.Object, unity: unity.Object, git: GitAvailable().Object,
            catalog: CatalogWith("26w32b").Object, picker: picker.Object, dialog: dialog.Object,
            project: ProjectRegistering(config, @"C:\new\Mod").Object, setup: setup.Object, fileSystem: FileSystemWithKsp2());
        await vm.CreateProjectCommand.ExecuteAsync(null);

        dialog.Verify(d => d.AlertAsync("Automated setup skipped",
            It.Is<string>(m => m.Contains("6000.5.0f1") && m.Contains("6000.4.1f1"))), Times.Once);
    }

    private const string RepoUrl = "https://github.com/Falki-git/SASExtended.git";
    private const string CloneParent = @"C:\clones";
    private const string CloneDest = @"C:\clones\SASExtended";

    private static Mock<IDialogService> DialogForGit(string version)
    {
        var dialog = DialogSelecting(version);
        dialog.Setup(d => d.AskAsync("Add from Git", It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(RepoUrl);
        return dialog;
    }

    private static Mock<IFilePickerService> PickerReturning(string parent)
    {
        var picker = new Mock<IFilePickerService>();
        picker.Setup(p => p.PickFolderAsync(It.IsAny<string>(), It.IsAny<string?>())).ReturnsAsync(parent);
        return picker;
    }

    [Test]
    public async Task AddFromGit_UnmanagedRepo_ClonesThenIngestsAtPickedVersion()
    {
        var (config, configMock) = Config();
        config.ShowSnapshotVersions = true;
        var git = GitAvailable();
        var version = new Mock<ITemplateVersionService>();
        version.Setup(v => v.DetectProjectVersion(CloneDest)).Returns((TemplateVersion?)null);
        var project = new Mock<IProjectService>();

        var vm = NewVm(configMock.Object, version: version.Object, git: git.Object,
            catalog: CatalogWith("26w32b").Object, picker: PickerReturning(CloneParent).Object,
            dialog: DialogForGit("26w32b").Object, project: project.Object);
        await vm.AddFromGitCommand.ExecuteAsync(null);

        git.Verify(g => g.CloneRepository(RepoUrl, CloneDest), Times.Once);
        project.Verify(p => p.IngestProject(CloneDest, It.Is<TemplateVersion>(v => v.Raw == "26w32b"), It.IsAny<bool>()), Times.Once);
        Assert.That(config.LastProjectDirectory, Is.EqualTo(CloneParent));
    }

    [Test]
    public async Task AddFromGit_ManagedRepo_ClonesThenImports_WithoutPickingVersion()
    {
        var (_, configMock) = Config();
        var git = GitAvailable();
        var version = new Mock<ITemplateVersionService>();
        version.Setup(v => v.DetectProjectVersion(CloneDest)).Returns(TemplateVersion.Parse("26w32b"));
        var project = new Mock<IProjectService>();
        var dialog = DialogForGit("26w32b");

        var vm = NewVm(configMock.Object, version: version.Object, git: git.Object,
            picker: PickerReturning(CloneParent).Object, dialog: dialog.Object, project: project.Object);
        await vm.AddFromGitCommand.ExecuteAsync(null);

        git.Verify(g => g.CloneRepository(RepoUrl, CloneDest), Times.Once);
        project.Verify(p => p.ImportProject(CloneDest, It.IsAny<bool>()), Times.Once);
        project.Verify(p => p.IngestProject(It.IsAny<string>(), It.IsAny<TemplateVersion>(), It.IsAny<bool>()), Times.Never);
        dialog.Verify(d => d.SelectVersionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<TemplateVersion>>(), It.IsAny<bool>()), Times.Never);
    }

    [Test]
    public async Task AddFromGit_CloneFails_Alerts_AndDoesNotAdopt()
    {
        var (_, configMock) = Config();
        var git = GitAvailable();
        git.Setup(g => g.CloneRepository(It.IsAny<string>(), It.IsAny<string>()))
            .Throws(new System.InvalidOperationException("network down"));
        var project = new Mock<IProjectService>();
        var dialog = DialogForGit("26w32b");
        dialog.Setup(d => d.AlertAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

        var vm = NewVm(configMock.Object, git: git.Object, picker: PickerReturning(CloneParent).Object,
            dialog: dialog.Object, project: project.Object);
        await vm.AddFromGitCommand.ExecuteAsync(null);

        dialog.Verify(d => d.AlertAsync("Clone failed", It.IsAny<string>()), Times.Once);
        project.Verify(p => p.ImportProject(It.IsAny<string>(), It.IsAny<bool>()), Times.Never);
        project.Verify(p => p.IngestProject(It.IsAny<string>(), It.IsAny<TemplateVersion>(), It.IsAny<bool>()), Times.Never);
    }
}
