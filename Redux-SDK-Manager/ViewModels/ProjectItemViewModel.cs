namespace Redux_SDK_Manager.ViewModels;

/// <summary>One row in the projects list: a tracked project with its resolved name and version.</summary>
public sealed class ProjectItemViewModel(string path, string name, string version, string channel) : ViewModelBase
{
    public string Path { get; } = path;
    public string Name { get; } = name;
    public string Version { get; } = version;
    public string Channel { get; } = channel;

    public string VersionLabel => string.IsNullOrEmpty(Version) ? "unknown version" : $"{Version} ({Channel})";
}
