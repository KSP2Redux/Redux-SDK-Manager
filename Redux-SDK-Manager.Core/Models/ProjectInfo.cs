namespace Redux_SDK_Manager.Models;

/// <summary>
/// Manager-owned project metadata, persisted as TOML in <c>project.info</c> at the project root.
/// It sits outside the template tree, so a template overlay never touches it, which makes it the
/// local source of truth for the project's name and version. Unity's own <c>productName</c> is left
/// as the stock value the editor/game needs.
/// </summary>
public sealed class ProjectInfo
{
    /// <summary>The user-facing project name shown by the manager.</summary>
    public string Name { get; set; } = "";

    /// <summary>The template version the project is on (raw form, e.g. <c>26w32b</c>), or null.</summary>
    public string? Version { get; set; }
}
