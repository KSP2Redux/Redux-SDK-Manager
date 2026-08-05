using System;
using System.IO.Abstractions;
using Redux_SDK_Manager.Models;

namespace Redux_SDK_Manager.Services;

public interface ITemplateVersionService
{
    /// <summary>
    /// Reads the <c>template.version</c> stamp from the root of a project directory.
    /// Returns <c>null</c> if the file is absent, empty, or unreadable.
    /// </summary>
    TemplateVersion? DetectProjectVersion(string projectPath);
}

public class TemplateVersionService(IFileSystem fileSystem) : ITemplateVersionService
{
    public const string VersionFileName = "template.version";

    public TemplateVersion? DetectProjectVersion(string projectPath)
    {
        var versionFile = fileSystem.Path.Combine(projectPath, VersionFileName);
        if (!fileSystem.File.Exists(versionFile)) return null;

        string raw;
        try
        {
            raw = fileSystem.File.ReadAllText(versionFile);
        }
        catch (Exception)
        {
            return null;
        }

        raw = raw.Trim();
        return string.IsNullOrEmpty(raw) ? null : TemplateVersion.Parse(raw);
    }
}
