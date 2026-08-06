using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Text.RegularExpressions;
using Redux_SDK_Manager.Wrappers;

namespace Redux_SDK_Manager.Services;

/// <summary>Locates an installed KSP2 executable so its assemblies can be imported into a project.</summary>
public interface IKsp2DetectorService
{
    /// <summary>The full path to KSP2_x64.exe if it can be found, otherwise null.</summary>
    string? DetectKsp2InstallLocation();
}

/// <summary>
/// Windows-only KSP2 locator, mirrored from the Launcher's detector. It reads Steam's library folders
/// and app manifest, then falls back to the known Epic and Private Division install paths.
/// </summary>
public partial class Ksp2DetectorService(
    IFileSystem fileSystem, IEnvironmentProvider environmentProvider, ILogService log) : IKsp2DetectorService
{
    private const string Ksp2SteamAppId = "954850";
    private const string Ksp2ExeName = "KSP2_x64.exe";

    private static readonly string[] EpicLocations =
    [
        @"C:\Program Files\Epic Games\KerbalSpaceProgram2\KSP2_x64.exe",
        @"D:\Program Files\Epic Games\KerbalSpaceProgram2\KSP2_x64.exe",
        @"E:\Program Files\Epic Games\KerbalSpaceProgram2\KSP2_x64.exe",
        @"F:\Program Files\Epic Games\KerbalSpaceProgram2\KSP2_x64.exe",
        @"G:\Program Files\Epic Games\KerbalSpaceProgram2\KSP2_x64.exe"
    ];

    private static readonly string[] SteamRoots =
    [
        @"C:\Program Files (x86)\Steam",
        @"C:\Program Files\Steam",
        @"D:\Steam",
        @"E:\Steam",
        @"F:\Steam",
        @"G:\Steam"
    ];

    public string? DetectKsp2InstallLocation() => DetectSteamInstall() ?? DetectNonSteam();

    private string? DetectSteamInstall()
    {
        foreach (var steamRoot in SteamRoots)
        {
            if (!fileSystem.Directory.Exists(steamRoot)) continue;

            foreach (var libraryPath in ReadLibraryFolders(steamRoot))
            {
                var steamapps = fileSystem.Path.Combine(libraryPath, "steamapps");
                var manifest = fileSystem.Path.Combine(steamapps, $"appmanifest_{Ksp2SteamAppId}.acf");
                if (!fileSystem.File.Exists(manifest)) continue;

                var installDir = ReadInstallDirFromManifest(manifest);
                if (string.IsNullOrWhiteSpace(installDir)) continue;

                var candidate = fileSystem.Path.Combine(steamapps, "common", installDir, Ksp2ExeName);
                if (fileSystem.File.Exists(candidate)) return candidate;
            }
        }

        return null;
    }

    private string? DetectNonSteam()
    {
        foreach (var file in EpicLocations)
        {
            if (fileSystem.File.Exists(file)) return file;
        }

        var privateDivisionPath = fileSystem.Path.Combine(
            environmentProvider.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs",
            "Kerbal Space Program 2", Ksp2ExeName);

        return fileSystem.File.Exists(privateDivisionPath) ? privateDivisionPath : null;
    }

    private IEnumerable<string> ReadLibraryFolders(string steamRoot)
    {
        yield return steamRoot;

        var libraryFoldersVdf = fileSystem.Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        if (!fileSystem.File.Exists(libraryFoldersVdf)) yield break;

        string content;
        try
        {
            content = fileSystem.File.ReadAllText(libraryFoldersVdf);
        }
        catch (Exception ex)
        {
            log.Warn($"Found {libraryFoldersVdf} but couldn't read it: {ex.Message}. Additional Steam libraries won't be checked.");
            yield break;
        }

        foreach (Match match in LibraryPathRegex().Matches(content))
        {
            var path = match.Groups["path"].Value.Replace(@"\\", @"\");
            if (!string.IsNullOrWhiteSpace(path)) yield return path;
        }
    }

    private string? ReadInstallDirFromManifest(string manifestPath)
    {
        string content;
        try
        {
            content = fileSystem.File.ReadAllText(manifestPath);
        }
        catch (Exception ex)
        {
            log.Warn($"Found {manifestPath} but couldn't read it: {ex.Message}.");
            return null;
        }

        var match = InstallDirRegex().Match(content);
        if (!match.Success)
        {
            log.Warn($"{manifestPath} exists but its installdir couldn't be parsed. If KSP2 isn't detected, it may have been read mid-write.");
            return null;
        }

        return match.Groups["installdir"].Value;
    }

    [GeneratedRegex("\"path\"\\s*\"(?<path>[^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex LibraryPathRegex();

    [GeneratedRegex("\"installdir\"\\s*\"(?<installdir>[^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex InstallDirRegex();
}
