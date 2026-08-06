using System;
using System.IO.Abstractions;
using Redux_SDK_Manager.Models;

namespace Redux_SDK_Manager.Services;

public interface ITemplateVersionService
{
    /// <summary>
    /// Detects the template version a project is on. Prefers the version recorded in
    /// <c>project.info</c> (the local source of truth), falling back to the <c>template.version</c>
    /// stamp for a project the manager has not written <c>project.info</c> into yet (e.g. a fresh
    /// clone). Returns null when neither yields a valid version.
    /// </summary>
    TemplateVersion? DetectProjectVersion(string projectPath);
}

public class TemplateVersionService(IFileSystem fileSystem, IProjectInfoService projectInfoService) : ITemplateVersionService
{
    public const string VersionFileName = "template.version";

    public TemplateVersion? DetectProjectVersion(string projectPath)
    {
        var recorded = projectInfoService.Read(projectPath)?.Version;
        if (TryParse(recorded, out var fromInfo)) return fromInfo;

        return TryParse(ReadTemplateStamp(projectPath), out var fromStamp) ? fromStamp : null;
    }

    private string? ReadTemplateStamp(string projectPath)
    {
        var versionFile = fileSystem.Path.Combine(projectPath, VersionFileName);
        if (!fileSystem.File.Exists(versionFile)) return null;

        try
        {
            return fileSystem.File.ReadAllText(versionFile);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool TryParse(string? raw, out TemplateVersion version)
    {
        version = null!;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        try
        {
            version = TemplateVersion.Parse(raw.Trim());
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
