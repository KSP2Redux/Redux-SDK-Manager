using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Redux_SDK_Manager.Services;
using Redux_SDK_Manager.Wrappers;

namespace Redux_SDK_Manager.ViewModels;

/// <summary>
/// The settings tab (cog): app-wide preferences and utility actions. Toggles persist to the config
/// as they change; utility actions (open logs folder) run through the process runner.
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
    private readonly IConfigService _config;
    private readonly IProcessRunner _processRunner;
    private readonly IDialogService _dialog;
    private readonly ILogService _log;

    // Seeding the checkbox from config raises OnChanged, which would immediately write the config
    // back. Guard the first assignment so loading is not treated as a user edit.
    private bool _suppressSave;

    public SettingsViewModel(
        IConfigService config, IProcessRunner processRunner, IDialogService dialog, ILogService log)
    {
        _config = config;
        _processRunner = processRunner;
        _dialog = dialog;
        _log = log;

        _suppressSave = true;
        try { ShowSnapshotVersions = _config.Config.ShowSnapshotVersions; }
        finally { _suppressSave = false; }
    }

    [ObservableProperty]
    private bool _showSnapshotVersions;

    public string AppVersion => GetType().Assembly.GetName().Version?.ToString() ?? "?";

    partial void OnShowSnapshotVersionsChanged(bool value)
    {
        if (_suppressSave) return;
        _config.Config.ShowSnapshotVersions = value;
        _config.Save();
        _log.Info($"Show snapshot versions set to {value}.");
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
}
