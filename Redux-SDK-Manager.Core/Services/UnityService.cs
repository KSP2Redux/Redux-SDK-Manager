using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Redux_SDK_Manager.Models;
using Redux_SDK_Manager.Wrappers;

namespace Redux_SDK_Manager.Services;

public interface IUnityService
{
    /// <summary>Unity editors found via Unity Hub's known install locations.</summary>
    IReadOnlyList<UnityInstall> DetectInstalls();

    /// <summary>The editor version a project requires (<c>m_EditorVersion</c>), or null.</summary>
    string? GetProjectUnityVersion(string projectPath);

    /// <summary>True if Unity Hub is installed (launch-time warning hook).</summary>
    bool IsHubInstalled();

    /// <summary>
    /// Opens a project via Unity Hub, which selects (or offers to install) the matching editor.
    /// Throws if Unity Hub isn't installed.
    /// </summary>
    void OpenProject(string projectPath);
}

public partial class UnityService(
    IFileSystem fileSystem,
    IEnvironmentProvider environmentProvider,
    IProcessRunner processRunner) : IUnityService
{
    private const string UnityExeName = "Unity.exe";
    private const string HubExeName = "Unity Hub.exe";

    public IReadOnlyList<UnityInstall> DetectInstalls()
    {
        var byExe = new Dictionary<string, UnityInstall>(StringComparer.OrdinalIgnoreCase);

        foreach (var install in EnumerateHubFolderInstalls().Concat(EnumerateManuallyAddedInstalls()))
        {
            byExe.TryAdd(install.ExecutablePath, install);
        }

        return byExe.Values.ToList();
    }

    public string? GetProjectUnityVersion(string projectPath)
    {
        var versionFile = fileSystem.Path.Combine(projectPath, "ProjectSettings", "ProjectVersion.txt");
        if (!fileSystem.File.Exists(versionFile)) return null;

        string content;
        try
        {
            content = fileSystem.File.ReadAllText(versionFile);
        }
        catch (Exception)
        {
            return null;
        }

        var match = EditorVersionRegex().Match(content);
        return match.Success ? match.Groups["version"].Value.Trim() : null;
    }

    public bool IsHubInstalled() => FindUnityHub() != null;

    public void OpenProject(string projectPath)
    {
        var hub = FindUnityHub()
            ?? throw new InvalidOperationException("Unity Hub is not installed.");

        // Hub CLI: everything after "--" is passed through; it opens the project in the
        // matching editor, offering to install it if absent.
        processRunner.Start(hub, ["--", "--projectPath", projectPath]);
    }

    private IEnumerable<UnityInstall> EnumerateHubFolderInstalls()
    {
        foreach (var root in EditorRoots())
        {
            if (!fileSystem.Directory.Exists(root)) continue;

            foreach (var versionDir in fileSystem.Directory.EnumerateDirectories(root))
            {
                var exe = fileSystem.Path.Combine(versionDir, "Editor", UnityExeName);
                if (fileSystem.File.Exists(exe))
                {
                    yield return new UnityInstall(fileSystem.Path.GetFileName(versionDir), exe);
                }
            }
        }
    }

    private IEnumerable<string> EditorRoots()
    {
        var programFiles = environmentProvider.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrEmpty(programFiles))
        {
            yield return fileSystem.Path.Combine(programFiles, "Unity", "Hub", "Editor");
        }

        var secondary = ReadSecondaryInstallPath();
        if (!string.IsNullOrEmpty(secondary))
        {
            yield return secondary;
        }
    }

    private string? ReadSecondaryInstallPath()
    {
        var path = HubConfigFile("secondaryInstallPath.json");
        if (path is null || !fileSystem.File.Exists(path)) return null;

        try
        {
            var content = fileSystem.File.ReadAllText(path).Trim();
            if (content.Length == 0) return null;
            // The file holds a JSON string (a quoted path); fall back to the raw text otherwise.
            return content.StartsWith('"') ? JsonSerializer.Deserialize<string>(content) : content;
        }
        catch (Exception)
        {
            return null;
        }
    }

    // Manually-added editors live in editors.json, each entry carrying its own version + location.
    private IEnumerable<UnityInstall> EnumerateManuallyAddedInstalls()
    {
        var results = new List<UnityInstall>();

        var path = HubConfigFile("editors.json");
        if (path is null || !fileSystem.File.Exists(path)) return results;

        string content;
        try
        {
            content = fileSystem.File.ReadAllText(path);
        }
        catch (Exception)
        {
            return results;
        }

        try
        {
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return results;

            foreach (var entry in doc.RootElement.EnumerateObject())
            {
                if (entry.Value.ValueKind != JsonValueKind.Object) continue;

                var exe = ExtractLocation(entry.Value);
                if (string.IsNullOrEmpty(exe)) continue;

                var version = entry.Value.TryGetProperty("version", out var v) && v.ValueKind == JsonValueKind.String
                    ? v.GetString()!
                    : entry.Name;

                results.Add(new UnityInstall(version, exe));
            }
        }
        catch (JsonException)
        {
            // Malformed editors.json - return whatever parsed before the failure.
        }

        return results;
    }

    private static string? ExtractLocation(JsonElement entry)
    {
        if (!entry.TryGetProperty("location", out var location)) return null;

        return location.ValueKind switch
        {
            JsonValueKind.String => location.GetString(),
            JsonValueKind.Array => location.EnumerateArray()
                .Where(e => e.ValueKind == JsonValueKind.String)
                .Select(e => e.GetString())
                .FirstOrDefault(),
            _ => null
        };
    }

    private string? HubConfigFile(string fileName)
    {
        var appData = environmentProvider.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return string.IsNullOrEmpty(appData) ? null : fileSystem.Path.Combine(appData, "UnityHub", fileName);
    }

    private string? FindUnityHub()
    {
        var programFiles = environmentProvider.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (string.IsNullOrEmpty(programFiles)) return null;

        var hub = fileSystem.Path.Combine(programFiles, "Unity Hub", HubExeName);
        return fileSystem.File.Exists(hub) ? hub : null;
    }

    [GeneratedRegex(@"^m_EditorVersion:\s*(?<version>\S+)", RegexOptions.Multiline)]
    private static partial Regex EditorVersionRegex();
}
