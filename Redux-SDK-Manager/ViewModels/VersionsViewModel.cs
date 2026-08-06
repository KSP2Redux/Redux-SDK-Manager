using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Redux_SDK_Manager.Models;
using Redux_SDK_Manager.Services;

namespace Redux_SDK_Manager.ViewModels;

/// <summary>
/// The versions tab (template catalog): every template version, the Unity editor it targets, and
/// whether that editor is installed. Refreshes off the UI thread when the tab is opened, and can
/// install a missing editor via Unity Hub.
/// </summary>
public partial class VersionsViewModel : ViewModelBase
{
    // Releases first (the stable line), then snapshots, then anything unclassified.
    private static readonly TemplateChannel[] ChannelOrder =
        [TemplateChannel.Release, TemplateChannel.Snapshot, TemplateChannel.Unknown];

    private readonly IConfigService _config;
    private readonly ITemplateCatalogService _catalog;
    private readonly IUnityService _unityService;
    private readonly IDialogService _dialog;
    private readonly ILogService _log;

    public VersionsViewModel(
        IConfigService config,
        ITemplateCatalogService catalog,
        IUnityService unityService,
        IDialogService dialog,
        ILogService log)
    {
        _config = config;
        _catalog = catalog;
        _unityService = unityService;
        _dialog = dialog;
        _log = log;
    }

    public ObservableCollection<VersionCatalogGroupViewModel> Groups { get; } = [];

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _hasLoaded;

    public bool HasVersions => Groups.Count > 0;

    [RelayCommand]
    private async Task Refresh()
    {
        if (IsBusy) return;

        IsBusy = true;
        try
        {
            // Syncing the mirror, reading each version's Unity target, and scanning installed editors
            // all touch git / the filesystem, so run them off the UI thread.
            var (infos, installed) = await Task.Run(() =>
            {
                var described = _catalog.DescribeVersions();
                var installedVersions = _unityService.DetectInstalls()
                    .Select(i => i.Version)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                return (described, installedVersions);
            });

            var showSnapshots = _config.Config.ShowSnapshotVersions;
            var visible = infos.Where(i => showSnapshots || i.Version.Channel != TemplateChannel.Snapshot);

            Groups.Clear();
            foreach (var channel in ChannelOrder)
            {
                var items = visible
                    .Where(i => i.Version.Channel == channel)
                    .OrderBy(i => i.Version, TemplateVersion.NewestFirst)
                    .Select(i => new VersionCatalogItemViewModel(
                        i, i.UnityVersion is not null && installed.Contains(i.UnityVersion)))
                    .ToList();
                if (items.Count == 0) continue;

                Groups.Add(new VersionCatalogGroupViewModel(channel.ToString(), items));
            }

            HasLoaded = true;
            OnPropertyChanged(nameof(HasVersions));
        }
        catch (Exception e)
        {
            _log.Error("Could not load the versions catalog.", e);
            await _dialog.AlertAsync("Versions unavailable", e.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task InstallUnity(VersionCatalogItemViewModel? item)
    {
        if (item is null || IsBusy || string.IsNullOrEmpty(item.UnityVersion)) return;

        IsBusy = true;
        try
        {
            var result = await Task.Run(() => _unityService.InstallUnityVersion(item.UnityVersion!, item.Changeset));

            // Without Unity Hub there is nothing to install into, so offer the Hub download instead.
            if (result == InstallUnityResult.HubUnavailable)
            {
                await _dialog.OfferLinkAsync("Unity Hub required",
                    "Unity Hub is not installed, so the editor cannot be installed. Install Unity Hub, then try again.",
                    "Install Unity Hub", DownloadLinks.UnityHub);
                return;
            }

            var message = result switch
            {
                InstallUnityResult.Started =>
                    $"Opened Unity Hub to install Unity {item.UnityVersion}. Re-open this tab once it finishes to refresh the status.",
                InstallUnityResult.AlreadyInstalled => $"Unity {item.UnityVersion} is already installed.",
                _ => "",
            };

            if (!string.IsNullOrEmpty(message)) await _dialog.AlertAsync("Install Unity", message);
        }
        catch (Exception e)
        {
            _log.Error($"Failed to install Unity {item.UnityVersion}.", e);
            await _dialog.AlertAsync("Install failed", e.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
