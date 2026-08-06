using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Redux_SDK_Manager.Models;
using Redux_SDK_Manager.Services;

namespace Redux_SDK_Manager.ViewModels;

/// <summary>
/// The projects tab: the list of tracked projects plus the project operations (open, remove, create,
/// ingest, import, upgrade). Long-running operations run off the UI thread with a busy scrim, prompt
/// through the dialog service, and surface results or errors as dialogs.
/// </summary>
public partial class ProjectsViewModel : ViewModelBase
{
    private readonly IConfigService _config;
    private readonly IProjectInfoService _projectInfo;
    private readonly ITemplateVersionService _versionService;
    private readonly IUnityService _unityService;
    private readonly IProjectService _projectService;
    private readonly ITemplateCatalogService _catalog;
    private readonly IGitService _gitService;
    private readonly IFilePickerService _picker;
    private readonly IDialogService _dialog;
    private readonly IFileSystem _fileSystem;
    private readonly ILogService _log;

    public ProjectsViewModel(
        IConfigService config,
        IProjectInfoService projectInfo,
        ITemplateVersionService versionService,
        IUnityService unityService,
        IProjectService projectService,
        ITemplateCatalogService catalog,
        IGitService gitService,
        IFilePickerService picker,
        IDialogService dialog,
        IFileSystem fileSystem,
        ILogService log)
    {
        _config = config;
        _projectInfo = projectInfo;
        _versionService = versionService;
        _unityService = unityService;
        _projectService = projectService;
        _catalog = catalog;
        _gitService = gitService;
        _picker = picker;
        _dialog = dialog;
        _fileSystem = fileSystem;
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
            var name = NonEmpty(_projectInfo.Read(path)?.Name)
                ?? _fileSystem.Path.GetFileName(path.TrimEnd('/', '\\'));
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

            // A missing Unity Hub is offered as an install link rather than a dead-end message.
            if (result == OpenProjectResult.HubUnavailable)
            {
                await _dialog.OfferLinkAsync("Unity Hub required",
                    "The required editor is not installed and Unity Hub is missing. Install Unity Hub, then try again.",
                    "Install Unity Hub", DownloadLinks.UnityHub);
                return;
            }

            var message = result switch
            {
                OpenProjectResult.Opened => $"Opening {item.Name} in Unity.",
                OpenProjectResult.InstallStarted =>
                    "The required editor is not installed. Opened Unity Hub to install it. Re-run open once it finishes.",
                OpenProjectResult.InstallDeclined => "The required editor is not installed, so nothing was opened.",
                OpenProjectResult.VersionUnknown => "Could not determine the project's Unity version.",
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

    [RelayCommand]
    private async Task CreateProject()
    {
        if (IsBusy || !await RequireGitAsync()) return;

        var version = await PickVersionAsync();
        if (version is null) return;

        var target = await _picker.PickFolderAsync("Choose an empty folder for the new project");
        if (string.IsNullOrEmpty(target)) return;

        await RunProjectOperationAsync("Create failed",
            () => _projectService.CreateProject(TemplateVersion.Parse(version), target));
    }

    [RelayCommand]
    private async Task AddProject()
    {
        if (IsBusy) return;

        var target = await _picker.PickFolderAsync("Choose an existing project to add");
        if (string.IsNullOrEmpty(target)) return;

        // An already-managed project (has project.info or a template.version stamp) is imported as-is.
        // An unmanaged one is adopted (ingested) at a chosen version, which needs git.
        if (_versionService.DetectProjectVersion(target) is not null)
        {
            await RunProjectOperationAsync("Import failed", () => _projectService.ImportProject(target));
            return;
        }

        if (!await RequireGitAsync()) return;

        var version = await PickVersionAsync();
        if (version is null) return;

        await RunProjectOperationAsync("Add failed",
            () => _projectService.IngestProject(target, TemplateVersion.Parse(version)));
    }

    [RelayCommand]
    private async Task Upgrade(ProjectItemViewModel? item)
    {
        if (item is null || IsBusy || !await RequireGitAsync()) return;

        var version = await PickVersionAsync();
        if (version is null) return;

        await RunProjectOperationAsync("Upgrade failed",
            () => _projectService.UpgradeProject(item.Path, TemplateVersion.Parse(version)));
    }

    // Fetches the available versions off the UI thread, then shows the picker. Null when cancelled
    // or unavailable.
    private async Task<string?> PickVersionAsync()
    {
        IsBusy = true;
        List<TemplateVersion> versions;
        try
        {
            versions = await Task.Run(() => _catalog.ListAvailableVersions().ToList());
        }
        catch (Exception e)
        {
            _log.Error("Could not list template versions.", e);
            await _dialog.AlertAsync("Versions unavailable", e.Message);
            return null;
        }
        finally
        {
            IsBusy = false;
        }

        // Snapshots are hidden unless the user opts into them in settings. Releases (and anything
        // unclassified) are always offered. Already-tracked snapshot projects are unaffected.
        if (!_config.Config.ShowSnapshotVersions)
        {
            versions = versions.Where(v => v.Channel != TemplateChannel.Snapshot).ToList();
        }

        if (versions.Count == 0)
        {
            await _dialog.AlertAsync("No versions", "The template repository has no versions.");
            return null;
        }

        return await _dialog.SelectVersionAsync("Choose a version", "Template version to use:", versions);
    }

    // Runs a project operation off the UI thread, refreshes the list on success, and reports failures.
    private async Task RunProjectOperationAsync(string failTitle, Action operation)
    {
        IsBusy = true;
        try
        {
            await Task.Run(operation);
            Load();
        }
        catch (Exception e)
        {
            _log.Error(failTitle, e);
            await _dialog.AlertAsync(failTitle, e.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<bool> RequireGitAsync()
    {
        if (_gitService.IsInstalled()) return true;
        await _dialog.OfferLinkAsync("Git required",
            "This action needs git installed and on your PATH. Install it, then try again.",
            "Install Git", DownloadLinks.Git);
        return false;
    }

    private static string? NonEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
