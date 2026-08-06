using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Redux_SDK_Manager.Services;

namespace Redux_SDK_Manager.ViewModels;

/// <summary>
/// Root view model for the shell. Holds the three top-bar sections and the current selection,
/// exposing the selected section's view model as <see cref="CurrentPage"/> for the content host.
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    public const int ProjectsTabId = 0;
    public const int VersionsTabId = 1;
    public const int SettingsTabId = 2;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentPage))]
    private int _currentTab = ProjectsTabId;

    // Refresh the versions catalog each time its tab is opened, so installed-editor status and any
    // newly released template versions are current. Fire-and-forget: the command runs off the UI
    // thread behind its own busy scrim.
    partial void OnCurrentTabChanged(int value)
    {
        if (value == VersionsTabId) Versions.RefreshCommand.Execute(null);
    }

    public ProjectsViewModel Projects { get; }
    public VersionsViewModel Versions { get; }
    public SettingsViewModel Settings { get; }

    /// <summary>The modal dialog host the window overlay binds to.</summary>
    public IDialogService Dialog { get; }

    public MainWindowViewModel(
        ProjectsViewModel projects, VersionsViewModel versions, SettingsViewModel settings, IDialogService dialog)
    {
        Projects = projects;
        Versions = versions;
        Settings = settings;
        Dialog = dialog;
    }

    /// <summary>The view model the content host shows for the current tab.</summary>
    public ViewModelBase CurrentPage => CurrentTab switch
    {
        VersionsTabId => Versions,
        SettingsTabId => Settings,
        _ => Projects,
    };

    [RelayCommand]
    private void GoToTab(string index)
    {
        if (int.TryParse(index, out var i)) CurrentTab = i;
    }
}
