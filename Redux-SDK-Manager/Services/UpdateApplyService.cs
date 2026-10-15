using System;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Redux_SDK_Manager.Models;

namespace Redux_SDK_Manager.Services;

/// <summary>Outcome of attempting to download and apply an update.</summary>
public enum UpdateApplyResult
{
    /// <summary>The new build was downloaded and verified; a restart into the swap has been triggered.</summary>
    RestartTriggered,

    /// <summary>The release has no GUI asset for this OS to download.</summary>
    NoAsset,

    /// <summary>This is not a single-file build, so it cannot replace itself.</summary>
    NotSingleFile,

    /// <summary>The download did not match the expected checksum.</summary>
    ChecksumMismatch,

    /// <summary>The download or write failed.</summary>
    Failed
}

/// <summary>Downloads the GUI build for an available update, verifies it, and triggers the swap.</summary>
public interface IUpdateApplyService
{
    Task<UpdateApplyResult> DownloadAndApplyAsync(UpdateCheckResult update);
}

public sealed class UpdateApplyService(
    IFileDownloader downloader,
    IApplicationRestarter restarter,
    IConfigService config,
    IFileSystem fileSystem,
    IHostPlatform hostPlatform,
    ILogService log) : IUpdateApplyService
{
    private const string GuiAssetPrefix = "redux-sdk-gui";
    private const string UpdateFolderName = "update";

    // win-x64 keeps the name it was released under before there were other platforms, so builds
    // already installed keep finding their update.
    private const string LegacyWindowsRuntime = "win-x64";
    private const string LegacyWindowsAssetName = GuiAssetPrefix + ".exe";

    /// <summary>
    /// The release asset holding the GUI build for a runtime, e.g. redux-sdk-gui-osx-arm64. Each
    /// platform swaps in only its own build, so a release without one reports NoAsset rather than
    /// replacing the running binary with another platform's.
    /// </summary>
    public static string GuiAssetName(HostOs os, string runtimeIdentifier)
        => runtimeIdentifier == LegacyWindowsRuntime
            ? LegacyWindowsAssetName
            : $"{GuiAssetPrefix}-{runtimeIdentifier}{(os == HostOs.Windows ? ".exe" : "")}";

    public async Task<UpdateApplyResult> DownloadAndApplyAsync(UpdateCheckResult update)
    {
        var assetName = GuiAssetName(hostPlatform.Os, hostPlatform.RuntimeIdentifier);
        var asset = update.Assets.FirstOrDefault(a =>
            string.Equals(a.Name, assetName, StringComparison.OrdinalIgnoreCase));
        if (asset is null)
        {
            log.Warn($"Update {update.LatestVersion} has no '{assetName}' asset to download.");
            return UpdateApplyResult.NoAsset;
        }

        if (!restarter.IsSingleFileDeployment)
        {
            log.Warn("Not a single-file build, refusing to self-update.");
            return UpdateApplyResult.NotSingleFile;
        }

        byte[] bytes;
        try
        {
            log.Info($"Downloading update {update.LatestVersion} from {asset.DownloadUrl}.");
            bytes = await downloader.DownloadAsync(asset.DownloadUrl);
        }
        catch (Exception e)
        {
            log.Error($"Failed to download update {update.LatestVersion}.", e);
            return UpdateApplyResult.Failed;
        }

        if (asset.Sha256 is not null && !ChecksumMatches(bytes, asset.Sha256))
        {
            log.Error($"Checksum mismatch for {asset.Name}; discarding the download.");
            return UpdateApplyResult.ChecksumMismatch;
        }

        try
        {
            var updateDir = fileSystem.Path.Combine(config.GetLocalStorageDirectory(), UpdateFolderName);
            fileSystem.Directory.CreateDirectory(updateDir);
            foreach (var stale in fileSystem.Directory.EnumerateFiles(updateDir))
            {
                try { fileSystem.File.Delete(stale); }
                catch { /* best-effort cleanup of a previous download */ }
            }

            var updatePath = fileSystem.Path.Combine(updateDir, assetName);
            await fileSystem.File.WriteAllBytesAsync(updatePath, bytes);
            if (hostPlatform.Os != HostOs.Windows)
            {
                // A download has no execute bit, and the restarter launches it directly.
#pragma warning disable CA1416
                fileSystem.File.SetUnixFileMode(updatePath, fileSystem.File.GetUnixFileMode(updatePath)
                    | UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
#pragma warning restore CA1416
            }
            log.Info($"Wrote update to {updatePath}, triggering restart.");
            restarter.LaunchUpdaterAndExit(updatePath);
            return UpdateApplyResult.RestartTriggered;
        }
        catch (Exception e)
        {
            log.Error($"Failed to stage update {update.LatestVersion}.", e);
            return UpdateApplyResult.Failed;
        }
    }

    public static bool ChecksumMatches(byte[] bytes, string expectedSha256)
    {
        var actual = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return string.Equals(actual, expectedSha256.Trim().ToLowerInvariant(), StringComparison.Ordinal);
    }
}
