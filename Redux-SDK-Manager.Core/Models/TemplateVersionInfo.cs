namespace Redux_SDK_Manager.Models;

/// <summary>
/// A template version together with the Unity editor it targets, read from that version's
/// ProjectSettings/ProjectVersion.txt. <see cref="UnityVersion"/> and <see cref="Changeset"/> are
/// null when the version has no readable ProjectVersion.txt.
/// </summary>
public sealed class TemplateVersionInfo
{
    public required TemplateVersion Version { get; init; }

    /// <summary>The Unity editor version (<c>m_EditorVersion</c>), e.g. <c>6000.4.1f1</c>.</summary>
    public string? UnityVersion { get; init; }

    /// <summary>The Unity editor changeset, used to install the exact version via Unity Hub.</summary>
    public string? Changeset { get; init; }
}
