using System;
using System.IO.Abstractions;
using Redux_SDK_Manager.Models;
using Tomlyn;
using Tomlyn.Model;

namespace Redux_SDK_Manager.Services;

public interface IProjectInfoService
{
    /// <summary>Reads <c>project.info</c> from a project directory, or null when absent or unreadable.</summary>
    ProjectInfo? Read(string projectPath);

    /// <summary>Writes <c>project.info</c> to a project directory, creating it if needed.</summary>
    void Write(string projectPath, ProjectInfo info);
}

public class ProjectInfoService(IFileSystem fileSystem, ILogService logService) : IProjectInfoService
{
    public const string FileName = "project.info";

    public ProjectInfo? Read(string projectPath)
    {
        var path = fileSystem.Path.Combine(projectPath, FileName);
        if (!fileSystem.File.Exists(path)) return null;

        try
        {
            var table = Toml.ToModel(fileSystem.File.ReadAllText(path));
            return new ProjectInfo
            {
                Name = table.TryGetValue("name", out var name) ? name?.ToString() ?? "" : "",
                Version = table.TryGetValue("version", out var version) ? version?.ToString() : null,
                EmbedSdk = table.TryGetValue("embed_sdk", out var embed) && embed is true,
            };
        }
        catch (Exception e)
        {
            logService.Warn($"Could not read {FileName} at '{path}': {e.Message}");
            return null;
        }
    }

    public void Write(string projectPath, ProjectInfo info)
    {
        fileSystem.Directory.CreateDirectory(projectPath);

        // A TomlTable serializes with proper TOML escaping, so a name with quotes or other special
        // characters round-trips cleanly.
        var table = new TomlTable { ["name"] = info.Name };
        if (!string.IsNullOrEmpty(info.Version)) table["version"] = info.Version;
        if (info.EmbedSdk) table["embed_sdk"] = true;

        var path = fileSystem.Path.Combine(projectPath, FileName);
        fileSystem.File.WriteAllText(path, Toml.FromModel(table));
    }
}
