using System.Collections.Generic;

namespace Redux_SDK_Manager.ViewModels;

/// <summary>A channel section in the versions catalog with its version rows, newest first.</summary>
public sealed class VersionCatalogGroupViewModel(string channel, IReadOnlyList<VersionCatalogItemViewModel> items)
    : ViewModelBase
{
    public string Channel { get; } = channel;
    public IReadOnlyList<VersionCatalogItemViewModel> Items { get; } = items;
}
