using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Redux_SDK_Manager.Models;
using Redux_SDK_Manager.Wrappers;

namespace Redux_SDK_Manager.Services;

/// <summary>Outcome of <see cref="IUnityService.OpenProject"/>.</summary>
public enum OpenProjectResult
{
    /// <summary>The matching editor was installed and launched on the project.</summary>
    Opened,

    /// <summary>The editor was missing, the user agreed, and a Unity Hub install was started.</summary>
    InstallStarted,

    /// <summary>The editor was missing and the user declined to install it.</summary>
    InstallDeclined,

    /// <summary>The project's required Unity version could not be determined.</summary>
    VersionUnknown,

    /// <summary>The editor was missing and Unity Hub isn't installed to install it.</summary>
    HubUnavailable
}

/// <summary>Outcome of <see cref="IUnityService.InstallUnityVersion"/>.</summary>
public enum InstallUnityResult
{
    /// <summary>Unity Hub was opened to install the version.</summary>
    Started,

    /// <summary>The version is already installed, so nothing was done.</summary>
    AlreadyInstalled,

    /// <summary>Unity Hub isn't installed, so the version cannot be installed.</summary>
    HubUnavailable
}

public interface IUnityService
{
    /// <summary>Unity editors found via Unity Hub's known install locations.</summary>
    IReadOnlyList<UnityInstall> DetectInstalls();

    /// <summary>The editor version a project requires (<c>m_EditorVersion</c>), or null.</summary>
    string? GetProjectUnityVersion(string projectPath);

    /// <summary>True if Unity Hub is installed (launch-time warning hook).</summary>
    bool IsHubInstalled();

    /// <summary>
    /// Opens a project by launching its matching installed editor directly
    /// (<c>Unity.exe -projectPath</c>). When that editor isn't installed, asks (via
    /// <see cref="IPromptService"/>) whether to install it and, if agreed, opens Unity Hub to
    /// install it. It does not then open the project. The editor appears once the install finishes,
    /// so re-run open. See <see cref="OpenProjectResult"/> for the outcomes.
    /// </summary>
    OpenProjectResult OpenProject(string projectPath);

    /// <summary>
    /// Installs a Unity editor version by opening Unity Hub at its <c>unityhub://</c> install link.
    /// The changeset (when known) pins the exact version even if it isn't in Hub's release list. Does
    /// nothing if the version is already installed. See <see cref="InstallUnityResult"/>.
    /// </summary>
    InstallUnityResult InstallUnityVersion(string version, string? changeset);
}

public partial class UnityService(
    IFileSystem fileSystem,
    IEnvironmentProvider environmentProvider,
    IProcessRunner processRunner,
    IPromptService promptService,
    ILogService logService) : IUnityService
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

    public string? GetProjectUnityVersion(string projectPath) => ReadEditorInfo(projectPath).version;

    // Reads the editor version and its changeset from ProjectSettings/ProjectVersion.txt. The
    // changeset (from m_EditorVersionWithRevision's "(...)") is what Hub needs to install a version
    // that isn't in its promoted releases list.
    private (string? version, string? changeset) ReadEditorInfo(string projectPath)
    {
        var versionFile = fileSystem.Path.Combine(projectPath, "ProjectSettings", "ProjectVersion.txt");
        if (!fileSystem.File.Exists(versionFile)) return (null, null);

        string content;
        try
        {
            content = fileSystem.File.ReadAllText(versionFile);
        }
        catch (Exception)
        {
            return (null, null);
        }

        var version = EditorVersionRegex().Match(content) is { Success: true } versionMatch
            ? versionMatch.Groups["version"].Value.Trim()
            : null;
        var changeset = EditorRevisionRegex().Match(content) is { Success: true } revisionMatch
            ? revisionMatch.Groups["changeset"].Value.Trim()
            : null;
        return (version, changeset);
    }

    public bool IsHubInstalled() => FindUnityHub() != null;

    public OpenProjectResult OpenProject(string projectPath)
    {
        logService.Info($"Opening project '{projectPath}'.");

        var (version, changeset) = ReadEditorInfo(projectPath);
        if (version is null)
        {
            logService.Warn($"Could not determine the Unity version for '{projectPath}' (no readable ProjectVersion.txt).");
            return OpenProjectResult.VersionUnknown;
        }

        // Opening a project is the editor's job, not Hub's (the Hub CLI has no open command).
        var install = DetectInstalls()
            .FirstOrDefault(i => string.Equals(i.Version, version, StringComparison.OrdinalIgnoreCase));
        if (install is not null)
        {
            logService.Info($"Launching Unity {version} on '{projectPath}'.");
            processRunner.Start(install.ExecutablePath, ["-projectPath", projectPath]);
            return OpenProjectResult.Opened;
        }

        var hub = FindUnityHub();
        if (hub is null)
        {
            logService.Warn($"Unity {version} is not installed and Unity Hub is missing - cannot install it.");
            return OpenProjectResult.HubUnavailable;
        }

        if (!promptService.Confirm($"Unity {version} is not installed. Install it via Unity Hub?", defaultAnswer: false))
        {
            logService.Info($"User declined installing Unity {version}.");
            return OpenProjectResult.InstallDeclined;
        }

        // Hand off to Hub through its unityhub:// protocol link (Hub has no reliable headless install
        // for a project's exact version+changeset) - the changeset pins a version not in Hub's release
        // list. Opened the Launcher's way - shell-execute so the OS resolves the protocol handler.
        logService.Info($"Unity {version} not installed - opening Unity Hub to install it.");
        OpenHubInstall(version, changeset);
        return OpenProjectResult.InstallStarted;
    }

    public InstallUnityResult InstallUnityVersion(string version, string? changeset)
    {
        var alreadyInstalled = DetectInstalls()
            .Any(i => string.Equals(i.Version, version, StringComparison.OrdinalIgnoreCase));
        if (alreadyInstalled)
        {
            logService.Info($"Unity {version} is already installed.");
            return InstallUnityResult.AlreadyInstalled;
        }

        if (FindUnityHub() is null)
        {
            logService.Warn($"Cannot install Unity {version} - Unity Hub is not installed.");
            return InstallUnityResult.HubUnavailable;
        }

        logService.Info($"Opening Unity Hub to install Unity {version}.");
        OpenHubInstall(version, changeset);
        return InstallUnityResult.Started;
    }

    // Opens Unity Hub's install link for a version. The changeset, when known, pins the exact build
    // even if it isn't in Hub's promoted release list.
    private void OpenHubInstall(string version, string? changeset)
    {
        var installUri = string.IsNullOrEmpty(changeset)
            ? $"unityhub://{version}"
            : $"unityhub://{version}/{changeset}";
        processRunner.OpenUrl(installUri);
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
            // The file holds a JSON string (a quoted path). Fall back to the raw text otherwise.
            return content.StartsWith('"') ? JsonSerializer.Deserialize<string>(content) : content;
        }
        catch (Exception)
        {
            logService.Warn("Could not read secondary install path...");
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
            logService.Warn("Malformed editors.json, list of manually added installs will be partial...");
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

    [GeneratedRegex(@"^m_EditorVersionWithRevision:\s*\S+\s*\((?<changeset>[^)]+)\)", RegexOptions.Multiline)]
    private static partial Regex EditorRevisionRegex();
}
