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
    private readonly IKsp2DetectorService _ksp2Detector;
    private readonly IFilePickerService _picker;
    private readonly ILogService _log;

    private readonly bool _suppressSave;

    public SettingsViewModel(
        IConfigService config, IProcessRunner processRunner, IDialogService dialog,
        IUpdateCoordinator updateCoordinator, IAppVersion appVersion, IKsp2DetectorService ksp2Detector,
        IFilePickerService picker, ILogService log)
    {
        _config = config;
        _processRunner = processRunner;
        _dialog = dialog;
        _updateCoordinator = updateCoordinator;
        _appVersion = appVersion;
        _ksp2Detector = ksp2Detector;
        _picker = picker;
        _log = log;

        _suppressSave = true;
        try
        {
            ShowSnapshotVersions = _config.Config.ShowSnapshotVersions;
            EnableSdkEmbedding = _config.Config.EnableSdkEmbedding;
            AutoRunProjectSetup = _config.Config.AutoRunProjectSetup;
            Ksp2ExePath = _config.Config.Ksp2ExePath;
        }
        finally { _suppressSave = false; }
    }

    [ObservableProperty]
    private bool _showSnapshotVersions;

    [ObservableProperty]
    private bool _enableSdkEmbedding;

    [ObservableProperty]
    private bool _autoRunProjectSetup;

    [ObservableProperty]
    private string _ksp2ExePath = "";

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

    partial void OnAutoRunProjectSetupChanged(bool value)
    {
        if (_suppressSave) return;
        _config.Config.AutoRunProjectSetup = value;
        _config.Save();
        _log.Info($"Auto-run project setup set to {value}.");
    }

    partial void OnKsp2ExePathChanged(string value)
    {
        if (_suppressSave) return;
        _config.Config.Ksp2ExePath = value;
        _config.Save();
        _log.Info($"KSP2 path set to '{value}'.");
    }

    [RelayCommand]
    private async Task DetectKsp2()
    {
        var found = _ksp2Detector.DetectKsp2InstallLocation();
        if (found is null)
        {
            await _dialog.AlertAsync("KSP2 not found",
                "Could not find KSP2 automatically. Use Browse to point at KSP2_x64.exe.");
            return;
        }

        Ksp2ExePath = found;
    }

    [RelayCommand]
    private async Task BrowseKsp2()
    {
        var picked = await _picker.PickFileAsync("Locate KSP2_x64.exe", "KSP2 executable", "exe");
        if (!string.IsNullOrEmpty(picked)) Ksp2ExePath = picked;
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
