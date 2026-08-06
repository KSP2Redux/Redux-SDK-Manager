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
    /// for and download self-updates. Placeholder until the real repo is decided.
    /// </summary>
    public string ManagerRepositoryUrl { get; set; } = "https://github.com/KSP2Redux/Redux-SDK-Manager";

    /// <summary>Filesystem paths of KSP2 mod projects the manager is tracking.</summary>
    public List<string> ProjectPaths { get; set; } = [];

    /// <summary>
    /// Whether snapshot (in-development) template versions are offered in the version picker.
    /// Off by default so most users only see stable releases.
    /// </summary>
    public bool ShowSnapshotVersions { get; set; } = false;
}
