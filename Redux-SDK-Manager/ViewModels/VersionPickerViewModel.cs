using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Redux_SDK_Manager.Models;

namespace Redux_SDK_Manager.ViewModels;

/// <summary>One selectable version row. <see cref="IsSelected"/> drives the row highlight.</summary>
public sealed partial class VersionItemViewModel(TemplateVersion version) : ViewModelBase
{
    public TemplateVersion Version { get; } = version;
    public string Raw => Version.Raw;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

/// <summary>A collapsible channel section plus its version rows, newest first.</summary>
public sealed partial class VersionGroupViewModel(string channel, IReadOnlyList<VersionItemViewModel> versions) : ViewModelBase
{
    public string Channel { get; } = channel;
    public IReadOnlyList<VersionItemViewModel> Versions { get; } = versions;

    [ObservableProperty]
    private bool _isExpanded = true;
}

/// <summary>
/// The version picker: a filter box over a list grouped by channel (newest first), so it stays usable
/// with many versions. Rows are selected directly (single click across sections). <see cref="Completion"/>
/// yields the chosen version's raw string, or null on cancel.
/// </summary>
public partial class VersionPickerViewModel : ViewModelBase
{
    // Releases first (the stable line), then snapshots, then anything unclassified.
    private static readonly TemplateChannel[] ChannelOrder =
        [TemplateChannel.Release, TemplateChannel.Snapshot, TemplateChannel.Unknown];

    private readonly TaskCompletionSource<string?> _completion = new();
    private readonly IReadOnlyList<TemplateVersion> _all;
    private readonly List<VersionItemViewModel> _shownItems = [];

    public VersionPickerViewModel(string title, string message, IReadOnlyList<TemplateVersion> versions)
    {
        Title = title;
        Message = message;
        _all = versions;

        Rebuild();

        // Default to the latest stable release, falling back to whatever is newest.
        Select(_shownItems.FirstOrDefault(i => i.Version.Channel == TemplateChannel.Release) ?? _shownItems.FirstOrDefault());
    }

    public string Title { get; }
    public string Message { get; }

    public ObservableCollection<VersionGroupViewModel> Groups { get; } = [];

    [ObservableProperty]
    private string _filter = "";

    [ObservableProperty]
    private TemplateVersion? _selectedVersion;

    public Task<string?> Completion => _completion.Task;

    partial void OnFilterChanged(string value) => Rebuild();

    private void Rebuild()
    {
        var query = string.IsNullOrWhiteSpace(Filter)
            ? _all
            : _all.Where(v => v.Raw.Contains(Filter.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();

        Groups.Clear();
        _shownItems.Clear();
        foreach (var channel in ChannelOrder)
        {
            var items = query.Where(v => v.Channel == channel)
                .OrderBy(v => v, TemplateVersion.NewestFirst)
                .Select(v => new VersionItemViewModel(v))
                .ToList();
            if (items.Count == 0) continue;

            _shownItems.AddRange(items);
            Groups.Add(new VersionGroupViewModel(channel.ToString(), items));
        }

        // Re-apply the highlight to the still-shown item. The chosen version is kept even when a
        // filter hides it, so "Use version" stays enabled.
        foreach (var item in _shownItems)
        {
            item.IsSelected = item.Version.Raw == SelectedVersion?.Raw;
        }
    }

    [RelayCommand]
    private void Select(VersionItemViewModel? item)
    {
        if (item is null) return;

        foreach (var shown in _shownItems)
        {
            shown.IsSelected = false;
        }

        item.IsSelected = true;
        SelectedVersion = item.Version;
    }

    [RelayCommand]
    private void Confirm() => _completion.TrySetResult(SelectedVersion?.Raw);

    [RelayCommand]
    private void Cancel() => _completion.TrySetResult(null);
}
