using System.Linq;
using System.Threading.Tasks;
using Redux_SDK_Manager.Models;
using Redux_SDK_Manager.ViewModels;

namespace Redux_SDK_Manager.Test;

public class VersionPickerViewModelTests
{
    private static VersionPickerViewModel Picker(params string[] versions)
        => new("Choose", "Pick one", versions.Select(TemplateVersion.Parse).ToList());

    [Test]
    public void Groups_ReleasesFirst_NewestFirst_AndPreselectsLatestRelease()
    {
        var vm = Picker("0.2.8.5", "26w32a", "26w32b", "0.2.10.0");

        Assert.That(vm.Groups[0].Channel, Is.EqualTo("Release"));
        // numeric sort: 0.2.10.0 is newer than 0.2.8.5
        Assert.That(vm.Groups[0].Versions.Select(v => v.Raw), Is.EqualTo(new[] { "0.2.10.0", "0.2.8.5" }));
        Assert.That(vm.Groups[1].Channel, Is.EqualTo("Snapshot"));
        Assert.That(vm.Groups[1].Versions.Select(v => v.Raw), Is.EqualTo(new[] { "26w32b", "26w32a" }));
        Assert.That(vm.SelectedVersion!.Raw, Is.EqualTo("0.2.10.0"));
    }

    [Test]
    public void Filter_NarrowsToMatchingVersions()
    {
        var vm = Picker("26w32a", "0.2.8.5", "26w31a");

        vm.Filter = "26w";

        Assert.That(vm.Groups.SelectMany(g => g.Versions).Select(v => v.Raw),
            Is.EquivalentTo(new[] { "26w32a", "26w31a" }));
    }

    [Test]
    public void Select_MovesSelectionAcrossChannels_AndUpdatesIsSelected()
    {
        var vm = Picker("0.2.8.5", "26w32a");
        var release = vm.Groups.First(g => g.Channel == "Release").Versions[0];   // 0.2.8.5
        var snapshot = vm.Groups.First(g => g.Channel == "Snapshot").Versions[0]; // 26w32a

        // latest stable release is pre-selected
        Assert.That(vm.SelectedVersion!.Raw, Is.EqualTo("0.2.8.5"));
        Assert.That(release.IsSelected, Is.True);

        vm.SelectCommand.Execute(snapshot);

        Assert.That(vm.SelectedVersion!.Raw, Is.EqualTo("26w32a"));
        Assert.That(snapshot.IsSelected, Is.True);
        Assert.That(release.IsSelected, Is.False);
    }

    [Test]
    public async Task Confirm_ReturnsSelectedRawVersion()
    {
        var vm = Picker("26w32a", "26w32b");
        vm.SelectCommand.Execute(vm.Groups[0].Versions.Last()); // 26w32a

        vm.ConfirmCommand.Execute(null);

        Assert.That(await vm.Completion, Is.EqualTo("26w32a"));
    }

    [Test]
    public async Task Cancel_ReturnsNull()
    {
        var vm = Picker("26w32a");

        vm.CancelCommand.Execute(null);

        Assert.That(await vm.Completion, Is.Null);
    }
}
