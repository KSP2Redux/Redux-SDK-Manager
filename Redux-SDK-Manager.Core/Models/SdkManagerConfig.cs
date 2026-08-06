using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Redux_SDK_Manager.Models;

/// <summary>Persisted application configuration, stored as JSON under %LocalAppData%.</summary>
public class SdkManagerConfig
{
    /// <summary>Absolute path this config was loaded from / will be saved to. Not serialized.</summary>
    [JsonIgnore]
    public string StoragePath { get; set; } = "";

    /// <summary>Git URL of the template distribution repo the manager pulls versions from.</summary>
    public string TemplatesRepositoryUrl { get; set; } = "https://github.com/KSP2Redux/Redux.Templates.git";

    /// <summary>
    /// GitHub repo (owner/name form or full URL) hosting the manager's own releases, used to check
    /// for and download self-updates.
    /// </summary>
    public string ManagerRepositoryUrl { get; set; } = "https://github.com/KSP2Redux/Redux-SDK-Manager";

    /// <summary>Filesystem paths of KSP2 mod projects the manager is tracking.</summary>
    public List<string> ProjectPaths { get; set; } = [];

    /// <summary>
    /// Whether snapshot (in-development) template versions are offered in the version picker.
    /// Off by default so most users only see stable releases.
    /// </summary>
    public bool ShowSnapshotVersions { get; set; } = false;

    /// <summary>
    /// Whether the "embed the SDK package for development" option is offered when creating, adding, or
    /// upgrading a project. Off by default; only SDK developers need it.
    /// </summary>
    public bool EnableSdkEmbedding { get; set; } = false;

    /// <summary>
    /// Full path to KSP2_x64.exe, used to import the game into a project during automated setup. Empty
    /// until detected or set. Both frontends share it.
    /// </summary>
    public string Ksp2ExePath { get; set; } = "";

    /// <summary>
    /// Whether the manager runs the ThunderKit import + "Import KSP2 to Editor" pipeline automatically
    /// after creating, adding, importing, or upgrading a project. On by default; prompts for the KSP2
    /// path if it isn't set yet.
    /// </summary>
    public bool AutoRunProjectSetup { get; set; } = true;

    /// <summary>
    /// The folder that held the most recently created project, used to open the new-project picker in
    /// the same place next time. Empty until the first project is created.
    /// </summary>
    public string LastProjectDirectory { get; set; } = "";
}
