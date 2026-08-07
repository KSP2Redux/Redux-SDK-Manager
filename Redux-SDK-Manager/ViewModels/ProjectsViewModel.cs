using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Redux_SDK_Manager.Models;
using Redux_SDK_Manager.Services;
using Redux_SDK_Manager.Wrappers;

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
    private readonly IProcessRunner _processRunner;
    private readonly IProjectSetupService _setup;
    private readonly IKsp2DetectorService _ksp2Detector;
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
        IProcessRunner processRunner,
        IProjectSetupService setup,
        IKsp2DetectorService ksp2Detector,
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
        _processRunner = processRunner;
        _setup = setup;
        _ksp2Detector = ksp2Detector;
        _fileSystem = fileSystem;
        _log = log;

        Load();
    }

    public ObservableCollection<ProjectItemViewModel> Projects { get; } = [];

    /// <summary>True while any tracked project is mid setup, used by the window's close guard.</summary>
    public bool AnySettingUp => Projects.Any(p => p.IsSettingUp);

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
            Projects.Add(new ProjectItemViewModel(path, name, version?.Raw ?? "", version?.Channel.ToString() ?? "")
            {
                NeedsSetup = !_setup.IsAlreadySetUp(path)
            });
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

        var choice = await PickVersionAsync();
        if (choice is null) return;

        var target = await _picker.PickFolderAsync("Choose an empty folder for the new project",
            _config.Config.LastProjectDirectory);
        if (string.IsNullOrEmpty(target)) return;

        RememberProjectParent(target);

        await RunProjectOperationAsync("Create failed", target,
            () => _projectService.CreateProject(TemplateVersion.Parse(choice.Version), target, choice.EmbedSdk));
    }

    // Remembers where the user put a project so the next new-project picker opens in the same place.
    private void RememberProjectParent(string projectPath)
    {
        var parent = _fileSystem.Path.GetDirectoryName(projectPath.TrimEnd('/', '\\'));
        if (string.IsNullOrEmpty(parent)) return;
        _config.Config.LastProjectDirectory = parent;
        _config.Save();
    }

    [RelayCommand]
    private void OpenFolder(ProjectItemViewModel? item)
    {
        if (item is null) return;
        try
        {
            _processRunner.OpenUrl(item.Path);
        }
        catch (Exception e)
        {
            _log.Error($"Failed to open folder '{item.Path}'.", e);
        }
    }

    [RelayCommand]
    private async Task AddProject()
    {
        if (IsBusy) return;

        var target = await _picker.PickFolderAsync("Choose an existing project to add");
        if (string.IsNullOrEmpty(target)) return;

        await AdoptFolderAsync(target);
    }

    [RelayCommand]
    private async Task AddFromGit()
    {
        if (IsBusy || !await RequireGitAsync()) return;

        var url = await _dialog.AskAsync("Add from Git", "Repository URL to clone:", "");
        if (string.IsNullOrWhiteSpace(url)) return;

        var name = GitUrl.RepoName(url);
        if (string.IsNullOrEmpty(name))
        {
            await _dialog.AlertAsync("Add from Git", "Could not work out a folder name from that URL.");
            return;
        }

        var parent = await _picker.PickFolderAsync("Choose where to clone the project", _config.Config.LastProjectDirectory);
        if (string.IsNullOrEmpty(parent)) return;

        var dest = _fileSystem.Path.Combine(parent, name);
        if (_fileSystem.Directory.Exists(dest) && _fileSystem.Directory.EnumerateFileSystemEntries(dest).Any())
        {
            await _dialog.AlertAsync("Add from Git", $"'{dest}' already exists and is not empty.");
            return;
        }

        RememberProjectParent(dest);

        var cloned = false;
        IsBusy = true;
        try
        {
            await Task.Run(() => _gitService.CloneRepository(url, dest));
            cloned = true;
        }
        catch (Exception e)
        {
            _log.Error($"Clone of '{url}' failed.", e);
            await _dialog.AlertAsync("Clone failed", e.Message);
        }
        finally
        {
            IsBusy = false;
        }

        if (cloned) await AdoptFolderAsync(dest);
    }

    // Adopts an existing project folder: an already-managed project (has project.info or a
    // template.version stamp) is imported as-is; an unmanaged one is ingested at a chosen version.
    private async Task AdoptFolderAsync(string target)
    {
        if (_versionService.DetectProjectVersion(target) is not null)
        {
            // Import has no version picker, so ask about embedding separately (only in SDK dev mode).
            var embed = await ConfirmEmbedSdkAsync();
            await RunProjectOperationAsync("Import failed", target, () => _projectService.ImportProject(target, embed));
            return;
        }

        if (!await RequireGitAsync()) return;

        var choice = await PickVersionAsync();
        if (choice is null) return;

        await RunProjectOperationAsync("Add failed", target,
            () => _projectService.IngestProject(target, TemplateVersion.Parse(choice.Version), choice.EmbedSdk));
    }

    [RelayCommand]
    private async Task Upgrade(ProjectItemViewModel? item)
    {
        if (item is null || IsBusy || !await RequireGitAsync()) return;

        var choice = await PickVersionAsync();
        if (choice is null) return;

        await RunProjectOperationAsync("Upgrade failed", item.Path,
            () => _projectService.UpgradeProject(item.Path, TemplateVersion.Parse(choice.Version), choice.EmbedSdk));
    }

    // Asks whether to embed the SDK for the import path (which has no version picker). Only prompts
    // in SDK development mode; otherwise never embeds.
    private async Task<bool> ConfirmEmbedSdkAsync()
        => _config.Config.EnableSdkEmbedding
           && await _dialog.ConfirmAsync("Embed SDK",
               "Embed the SDK package into Packages for local development?", "Embed", "Skip");

    // Fetches the available versions off the UI thread, then shows the picker. Null when cancelled
    // or unavailable.
    private async Task<VersionChoice?> PickVersionAsync()
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

        return await _dialog.SelectVersionAsync("Choose a version", "Template version to use:", versions,
            _config.Config.EnableSdkEmbedding);
    }

    // Runs a project operation off the UI thread, refreshes the list on success, and reports failures.
    // On success, kicks off automated setup for the resulting project (after the busy scrim clears, so
    // the per-row status is what the user watches).
    private async Task RunProjectOperationAsync(string failTitle, string projectPath, Action operation)
    {
        IsBusy = true;
        var succeeded = false;
        try
        {
            await Task.Run(operation);
            Load();
            succeeded = true;
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

        if (succeeded) await MaybeRunSetupAsync(projectPath);
    }

    // Runs setup automatically after a create/add/upgrade, unless disabled or already imported.
    private async Task MaybeRunSetupAsync(string projectPath)
    {
        if (!_config.Config.AutoRunProjectSetup) return;
        if (_setup.IsAlreadySetUp(projectPath)) return;

        var item = Projects.FirstOrDefault(p => string.Equals(p.Path, projectPath, StringComparison.OrdinalIgnoreCase));
        if (item is not null) await ExecuteSetupAsync(item);
    }

    // The manual "Setup" action: runs the import for a project that hasn't had the game imported yet.
    [RelayCommand]
    private async Task Setup(ProjectItemViewModel? item)
    {
        if (item is null || item.IsSettingUp) return;
        await ExecuteSetupAsync(item);
    }

    // Runs the ThunderKit import + pipeline for a project, greying its row and streaming the current step
    // to it. Prompts for the KSP2 path if it isn't set. Shared by the auto-run and the manual Setup action.
    private async Task ExecuteSetupAsync(ProjectItemViewModel item)
    {
        var ksp2 = await EnsureKsp2PathAsync();
        if (ksp2 is null) return;

        item.IsSettingUp = true;
        item.SetupStatus = "Starting setup...";
        var progress = new Progress<ProjectSetupProgress>(p => item.SetupStatus = ProjectSetupService.DescribeProgress(p));

        ProjectSetupResult result;
        try
        {
            result = await _setup.RunSetupAsync(item.Path, ksp2, progress, CancellationToken.None);
        }
        catch (Exception e)
        {
            _log.Error("Automated setup failed.", e);
            result = ProjectSetupResult.Failed;
        }
        finally
        {
            item.IsSettingUp = false;
            item.SetupStatus = "";
            item.NeedsSetup = !_setup.IsAlreadySetUp(item.Path);
        }

        switch (result)
        {
            case ProjectSetupResult.EditorMissing:
                await _dialog.AlertAsync("Automated setup", ProjectSetupService.EditorMissingMessage);
                break;
            case ProjectSetupResult.UnityVersionMismatch:
                await _dialog.AlertAsync("Automated setup skipped",
                    ProjectSetupService.UnityMismatchMessage(
                        _unityService.GetGameUnityVersion(ksp2), _unityService.GetProjectUnityVersion(item.Path)));
                break;
            case ProjectSetupResult.Failed:
                await _dialog.AlertAsync("Automated setup failed",
                    "The automated project setup did not finish. See the log at:\n"
                    + $"{_setup.SetupLogPath(item.Path)}\n\n"
                    + "Or open the project in Unity to finish setup by hand.");
                break;
        }
    }

    // Resolves the KSP2 executable, prompting (detect then browse) when the config path is unset or gone.
    // Returns null when the user declines to provide one, which skips setup.
    private async Task<string?> EnsureKsp2PathAsync()
    {
        var path = _config.Config.Ksp2ExePath;
        if (!string.IsNullOrEmpty(path) && _fileSystem.File.Exists(path)) return path;

        var detected = _ksp2Detector.DetectKsp2InstallLocation();
        if (detected is not null
            && await _dialog.ConfirmAsync("KSP2 found",
                $"Use this KSP2 install to set up the project?\n{detected}", "Use", "Choose another"))
        {
            SaveKsp2Path(detected);
            return detected;
        }

        var picked = await _picker.PickFileAsync("Locate KSP2_x64.exe", "KSP2 executable", "exe");
        if (string.IsNullOrEmpty(picked)) return null;

        SaveKsp2Path(picked);
        return picked;
    }

    private void SaveKsp2Path(string path)
    {
        _config.Config.Ksp2ExePath = path;
        _config.Save();
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
