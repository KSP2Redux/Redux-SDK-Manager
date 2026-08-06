using Redux_SDK_Manager.Models;

namespace Redux_SDK_Manager.ViewModels;

/// <summary>
/// One row in the versions catalog: a template version, the Unity editor it targets, and whether
/// that editor is installed locally.
/// </summary>
public sealed class VersionCatalogItemViewModel : ViewModelBase
{
    public VersionCatalogItemViewModel(TemplateVersionInfo info, bool isInstalled)
    {
        Raw = info.Version.Raw;
        Channel = info.Version.Channel.ToString();
        UnityVersion = info.UnityVersion;
        Changeset = info.Changeset;
        IsInstalled = isInstalled;
    }

    public string Raw { get; }
    public string Channel { get; }
    public string? UnityVersion { get; }
    public string? Changeset { get; }
    public bool IsInstalled { get; }

    public string UnityVersionLabel =>
        string.IsNullOrEmpty(UnityVersion) ? "Unity version unknown" : $"Unity {UnityVersion}";

    public string InstalledLabel => IsInstalled ? "Installed" : "Not installed";

    // Offer install only when we know which editor is needed and it isn't already present.
    public bool CanInstall => !IsInstalled && !string.IsNullOrEmpty(UnityVersion);
}
