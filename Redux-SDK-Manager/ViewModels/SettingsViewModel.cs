using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Redux_SDK_Manager.Services;
using Redux_SDK_Manager.Wrappers;

namespace Redux_SDK_Manager.ViewModels;

/// <summary>
/// The settings tab (cog): app-wide preferences and utility actions. Toggles persist to the config
/// as they change; utility actions (open logs folder, check for updates) run their services.
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
    private readonly IConfigService _config;
    private readonly IProcessRunner _processRunner;
    private readonly IDialogService _dialog;
    private readonly IUpdateCoordinator _updateCoordinator;
    private readonly IAppVersion _appVersion;
    private readonly ILogService _log;
    
    private readonly bool _suppressSave;

    public SettingsViewModel(
        IConfigService config, IProcessRunner processRunner, IDialogService dialog,
        IUpdateCoordinator updateCoordinator, IAppVersion appVersion, ILogService log)
    {
        _config = config;
        _processRunner = processRunner;
        _dialog = dialog;
        _updateCoordinator = updateCoordinator;
        _appVersion = appVersion;
        _log = log;

        _suppressSave = true;
        try
        {
            ShowSnapshotVersions = _config.Config.ShowSnapshotVersions;
            EnableSdkEmbedding = _config.Config.EnableSdkEmbedding;
        }
        finally { _suppressSave = false; }
    }

    [ObservableProperty]
    private bool _showSnapshotVersions;

    [ObservableProperty]
    private bool _enableSdkEmbedding;

    [ObservableProperty]
    private bool _isCheckingForUpdates;

    // The canonical version is the Core assembly's, shared by the GUI and CLI.
    public string AppVersion => _appVersion.Current?.ToString() ?? "?";

    partial void OnShowSnapshotVersionsChanged(bool value)
    {
        if (_suppressSave) return;
        _config.Config.ShowSnapshotVersions = value;
        _config.Save();
        _log.Info($"Show snapshot versions set to {value}.");
    }

    partial void OnEnableSdkEmbeddingChanged(bool value)
    {
        if (_suppressSave) return;
        _config.Config.EnableSdkEmbedding = value;
        _config.Save();
        _log.Info($"SDK embedding option set to {value}.");
    }

    [RelayCommand]
    private async Task OpenLogsFolder()
    {
        try
        {
            var logsDir = _config.GetLogsDirectory();
            _processRunner.OpenUrl(logsDir);
        }
        catch (Exception e)
        {
            _log.Error("Failed to open the logs folder.", e);
            await _dialog.AlertAsync("Open logs folder", $"Could not open the logs folder. {e.Message}");
        }
    }

    [RelayCommand]
    private async Task CheckForUpdates()
    {
        if (IsCheckingForUpdates) return;

        IsCheckingForUpdates = true;
        try
        {
            await _updateCoordinator.CheckAsync(notifyWhenCurrent: true);
        }
        finally
        {
            IsCheckingForUpdates = false;
        }
    }
}
