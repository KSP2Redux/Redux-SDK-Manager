using Redux_SDK_Manager.Models;

namespace Redux_SDK_Manager.ViewModels;

/// <summary>
/// One row in the versions catalog: a template version, the Unity editor it targets, and whether
/// that editor is installed locally.
/// </summary>
public sealed class VersionCatalogItemViewModel(TemplateVersionInfo info, bool isInstalled) : ViewModelBase
{
    public string Raw { get; } = info.Version.Raw;
    public string Channel { get; } = info.Version.Channel.ToString();
    public string? UnityVersion { get; } = info.UnityVersion;
    public string? Changeset { get; } = info.Changeset;
    public bool IsInstalled { get; } = isInstalled;

    public string UnityVersionLabel =>
        string.IsNullOrEmpty(UnityVersion) ? "Unity version unknown" : $"Unity {UnityVersion}";

    public string InstalledLabel => IsInstalled ? "Installed" : "Not installed";

    // Offer install only when we know which editor is needed and it isn't already present.
    public bool CanInstall => !IsInstalled && !string.IsNullOrEmpty(UnityVersion);
}
