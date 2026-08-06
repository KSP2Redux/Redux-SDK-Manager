using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Redux_SDK_Manager.Services;

namespace Redux_SDK_Manager.ViewModels;

/// <summary>
/// The projects tab: the list of tracked projects with their names and versions, plus open and
/// remove actions. Long-running operations run off the UI thread and surface results as dialogs.
/// </summary>
public partial class ProjectsViewModel : ViewModelBase
{
    private readonly IConfigService _config;
    private readonly IProjectInfoService _projectInfo;
    private readonly ITemplateVersionService _versionService;
    private readonly IUnityService _unityService;
    private readonly IDialogService _dialog;
    private readonly ILogService _log;

    public ProjectsViewModel(
        IConfigService config,
        IProjectInfoService projectInfo,
        ITemplateVersionService versionService,
        IUnityService unityService,
        IDialogService dialog,
        ILogService log)
    {
        _config = config;
        _projectInfo = projectInfo;
        _versionService = versionService;
        _unityService = unityService;
        _dialog = dialog;
        _log = log;

        Load();
    }

    public ObservableCollection<ProjectItemViewModel> Projects { get; } = [];

    [ObservableProperty]
    private bool _isBusy;

    public bool HasProjects => Projects.Count > 0;

    [RelayCommand]
    private void Load()
    {
        Projects.Clear();
        foreach (var path in _config.Config.ProjectPaths.ToList())
        {
            var name = NonEmpty(_projectInfo.Read(path)?.Name) ?? Path.GetFileName(path.TrimEnd('/', '\\'));
            var version = _versionService.DetectProjectVersion(path);
            Projects.Add(new ProjectItemViewModel(path, name, version?.Raw ?? "", version?.Channel.ToString() ?? ""));
        }

        OnPropertyChanged(nameof(HasProjects));
    }

    [RelayCommand]
    private async Task Open(ProjectItemViewModel? item)
    {
        if (item is null || IsBusy) return;

        IsBusy = true;
        try
        {
            // OpenProject shells out and may prompt (install a missing editor), so it runs off the UI
            // thread. Its IPromptService calls marshal back to the UI thread as dialogs.
            var result = await Task.Run(() => _unityService.OpenProject(item.Path));
            var message = result switch
            {
                OpenProjectResult.Opened => $"Opening {item.Name} in Unity.",
                OpenProjectResult.InstallStarted =>
                    "The required editor is not installed. Opened Unity Hub to install it. Re-run open once it finishes.",
                OpenProjectResult.InstallDeclined => "The required editor is not installed, so nothing was opened.",
                OpenProjectResult.VersionUnknown => "Could not determine the project's Unity version.",
                OpenProjectResult.HubUnavailable =>
                    "The required editor is not installed and Unity Hub is missing, so it cannot be installed.",
                _ => "",
            };

            if (!string.IsNullOrEmpty(message)) await _dialog.AlertAsync("Open project", message);
        }
        catch (Exception e)
        {
            _log.Error($"Failed to open '{item.Path}'.", e);
            await _dialog.AlertAsync("Open failed", e.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task Remove(ProjectItemViewModel? item)
    {
        if (item is null) return;

        var confirmed = await _dialog.ConfirmAsync("Remove project",
            $"Remove '{item.Name}' from the manager? The project files are kept on disk.");
        if (!confirmed) return;

        _config.Config.ProjectPaths.RemoveAll(p => string.Equals(p, item.Path, StringComparison.OrdinalIgnoreCase));
        _config.Save();
        Projects.Remove(item);
        OnPropertyChanged(nameof(HasProjects));
    }

    private static string? NonEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
