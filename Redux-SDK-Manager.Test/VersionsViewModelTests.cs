using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using Redux_SDK_Manager.Models;
using Redux_SDK_Manager.Services;
using Redux_SDK_Manager.ViewModels;

namespace Redux_SDK_Manager.Test;

public class VersionsViewModelTests
{
    private static Mock<IConfigService> Config(bool showSnapshots = false)
    {
        var mock = new Mock<IConfigService>();
        mock.Setup(c => c.Config).Returns(new SdkManagerConfig { ShowSnapshotVersions = showSnapshots });
        return mock;
    }

    private static TemplateVersionInfo Info(string raw, string? unity, string? changeset = null)
        => new() { Version = TemplateVersion.Parse(raw), UnityVersion = unity, Changeset = changeset };

    private static Mock<ITemplateCatalogService> Catalog(params TemplateVersionInfo[] infos)
    {
        var mock = new Mock<ITemplateCatalogService>();
        mock.Setup(c => c.DescribeVersions()).Returns(infos.ToList());
        return mock;
    }

    private static Mock<IUnityService> UnityWithInstalled(params string[] versions)
    {
        var mock = new Mock<IUnityService>();
        IReadOnlyList<UnityInstall> installs = versions.Select(v => new UnityInstall(v, $@"C:\Unity\{v}\Editor\Unity.exe")).ToList();
        mock.Setup(u => u.DetectInstalls()).Returns(installs);
        return mock;
    }

    private static VersionsViewModel NewVm(
        IConfigService config, ITemplateCatalogService catalog, IUnityService unity,
        IDialogService? dialog = null, ILogService? log = null)
        => new(config, catalog, unity, dialog ?? Mock.Of<IDialogService>(), log ?? Mock.Of<ILogService>());

    private static IEnumerable<VersionCatalogItemViewModel> AllItems(VersionsViewModel vm)
        => vm.Groups.SelectMany(g => g.Items);

    [Test]
    public async Task Refresh_BuildsRows_WithInstalledStatus()
    {
        var catalog = Catalog(Info("0.2.8.5", "6000.4.1f1"), Info("0.2.10.0", "6000.5.0f1"));
        var unity = UnityWithInstalled("6000.4.1f1");

        var vm = NewVm(Config().Object, catalog.Object, unity.Object);
        await vm.RefreshCommand.ExecuteAsync(null);

        var installed = AllItems(vm).Single(v => v.Raw == "0.2.8.5");
        var missing = AllItems(vm).Single(v => v.Raw == "0.2.10.0");
        Assert.That(installed.IsInstalled, Is.True);
        Assert.That(installed.CanInstall, Is.False);
        Assert.That(missing.IsInstalled, Is.False);
        Assert.That(missing.CanInstall, Is.True);
        Assert.That(vm.HasVersions, Is.True);
    }

    [Test]
    public async Task Refresh_HidesSnapshots_WhenSettingOff()
    {
        var catalog = Catalog(Info("0.2.10.0", "6000.5.0f1"), Info("26w32a", "6000.4.1f1"));
        var vm = NewVm(Config(showSnapshots: false).Object, catalog.Object, UnityWithInstalled().Object);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.That(vm.Groups.Select(g => g.Channel), Is.EqualTo(new[] { "Release" }));
        Assert.That(AllItems(vm).Select(v => v.Raw), Is.EqualTo(new[] { "0.2.10.0" }));
    }

    [Test]
    public async Task Refresh_GroupsByChannel_ReleasesFirstNewestFirst()
    {
        var catalog = Catalog(
            Info("0.2.8.5", "6000.4.1f1"), Info("26w32a", "6000.4.1f1"),
            Info("0.2.10.0", "6000.5.0f1"), Info("26w32b", "6000.5.0f1"));
        var vm = NewVm(Config(showSnapshots: true).Object, catalog.Object, UnityWithInstalled().Object);

        await vm.RefreshCommand.ExecuteAsync(null);

        Assert.That(vm.Groups.Select(g => g.Channel), Is.EqualTo(new[] { "Release", "Snapshot" }));
        // Newest first within each channel section.
        Assert.That(vm.Groups[0].Items.Select(i => i.Raw), Is.EqualTo(new[] { "0.2.10.0", "0.2.8.5" }));
        Assert.That(vm.Groups[1].Items.Select(i => i.Raw), Is.EqualTo(new[] { "26w32b", "26w32a" }));
    }

    [Test]
    public async Task Refresh_UnknownUnityVersion_CannotInstall()
    {
        var catalog = Catalog(Info("0.2.8.5", null));
        var vm = NewVm(Config().Object, catalog.Object, UnityWithInstalled().Object);

        await vm.RefreshCommand.ExecuteAsync(null);

        var row = AllItems(vm).Single();
        Assert.That(row.IsInstalled, Is.False);
        Assert.That(row.CanInstall, Is.False);
        Assert.That(row.UnityVersionLabel, Is.EqualTo("Unity version unknown"));
    }

    [Test]
    public async Task InstallUnity_HubMissing_OffersUnityHubInstall()
    {
        var catalog = Catalog(Info("0.2.10.0", "6000.5.0f1", "88b47c5e7076"));
        var unity = UnityWithInstalled();
        unity.Setup(u => u.InstallUnityVersion("6000.5.0f1", "88b47c5e7076")).Returns(InstallUnityResult.HubUnavailable);
        var dialog = new Mock<IDialogService>();
        dialog.Setup(d => d.OfferLinkAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var vm = NewVm(Config().Object, catalog.Object, unity.Object, dialog.Object);
        await vm.RefreshCommand.ExecuteAsync(null);
        await vm.InstallUnityCommand.ExecuteAsync(AllItems(vm).Single());

        dialog.Verify(d => d.OfferLinkAsync("Unity Hub required", It.IsAny<string>(), "Install Unity Hub", It.IsAny<string>()), Times.Once);
        dialog.Verify(d => d.AlertAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Test]
    public async Task InstallUnity_InvokesServiceAndAlerts()
    {
        var catalog = Catalog(Info("0.2.10.0", "6000.5.0f1", "88b47c5e7076"));
        var unity = UnityWithInstalled();
        unity.Setup(u => u.InstallUnityVersion("6000.5.0f1", "88b47c5e7076")).Returns(InstallUnityResult.Started);
        var dialog = new Mock<IDialogService>();
        dialog.Setup(d => d.AlertAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

        var vm = NewVm(Config().Object, catalog.Object, unity.Object, dialog.Object);
        await vm.RefreshCommand.ExecuteAsync(null);
        await vm.InstallUnityCommand.ExecuteAsync(AllItems(vm).Single());

        unity.Verify(u => u.InstallUnityVersion("6000.5.0f1", "88b47c5e7076"), Times.Once);
        dialog.Verify(d => d.AlertAsync("Install Unity", It.IsAny<string>()), Times.Once);
        Assert.That(vm.IsBusy, Is.False);
    }
}
