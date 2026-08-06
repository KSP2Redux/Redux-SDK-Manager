using Redux_SDK_Manager.ViewModels;

namespace Redux_SDK_Manager.Test;

public class MainWindowViewModelTests
{
    private static MainWindowViewModel NewViewModel()
        => new(new ProjectsViewModel(), new VersionsViewModel(), new SettingsViewModel());

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
    public void GoToTab_IgnoresNonNumericArgument()
    {
        var vm = NewViewModel();

        vm.GoToTabCommand.Execute("nope");

        Assert.That(vm.CurrentTab, Is.EqualTo(MainWindowViewModel.ProjectsTabId));
    }
}
