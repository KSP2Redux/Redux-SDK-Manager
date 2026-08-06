using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Redux_SDK_Manager.Models;

namespace Redux_SDK_Manager.Services;

/// <summary>
/// Checks whether a newer manager release is available. This is UI-agnostic and never throws to the
/// caller: a failed check (offline, rate-limited, misconfigured repo) reports "no update" so the app
/// keeps working. Updates are always optional; the check itself never applies anything.
/// </summary>
public interface IUpdateService
{
    Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default);
}

public class UpdateService(
    IReleaseClient releaseClient, IAppVersion appVersion, IConfigService config, ILogService log)
    : IUpdateService
{
    public async Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        var current = appVersion.Current;
        var repo = ParseRepo(config.Config.ManagerRepositoryUrl);
        if (repo is null)
        {
            log.Warn($"Manager repository URL '{config.Config.ManagerRepositoryUrl}' is not a valid owner/repo, skipping update check.");
            return UpdateCheckResult.NotAvailable(current, "");
        }

        var (owner, name, pageUrl) = repo.Value;
        var releasesPage = $"{pageUrl}/releases";

        IReadOnlyList<ReleaseInfo> releases;
        try
        {
            releases = await releaseClient.GetReleasesAsync(owner, name, cancellationToken);
        }
        catch (Exception e)
        {
            log.Warn($"Update check against {owner}/{name} failed, assuming up to date. {e.Message}");
            return UpdateCheckResult.NotAvailable(current, releasesPage);
        }

        var latest = releases
            .Where(r => !r.Prerelease)
            .Select(r => (release: r, version: ParseTag(r.TagName)))
            .Where(x => x.version is not null)
            .OrderByDescending(x => x.version)
            .FirstOrDefault();

        if (latest.release is null || latest.version is null)
        {
            log.Info($"No published release tags found for {owner}/{name}.");
            return UpdateCheckResult.NotAvailable(current, releasesPage);
        }

        if (current is not null && latest.version <= current)
        {
            log.Info($"Manager is up to date (current {current}, latest {latest.version}).");
            return UpdateCheckResult.NotAvailable(current, releasesPage);
        }

        // A release whose assets are still uploading is treated as not-yet-published so we never
        // offer an update that can't be downloaded. Assets carry a sha256 digest once fully uploaded.
        if (!latest.release.Assets.Any(a => a.Sha256 is not null))
        {
            log.Warn($"Release {latest.release.TagName} has no uploaded assets yet, skipping until a later check.");
            return UpdateCheckResult.NotAvailable(current, releasesPage);
        }

        log.Info($"Update available: {latest.version} (current {current}).");
        return new UpdateCheckResult
        {
            IsUpdateAvailable = true,
            CurrentVersion = current,
            LatestVersion = latest.version,
            ReleaseNotes = latest.release.Notes,
            ReleasesPageUrl = releasesPage,
            Assets = latest.release.Assets,
        };
    }

    // Accepts "owner/name", "https://github.com/owner/name", or a trailing ".git"/slash. Returns the
    // owner, repo name, and a normalized https://github.com/owner/name page URL.
    private static (string owner, string name, string pageUrl)? ParseRepo(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        var trimmed = value.Trim().TrimEnd('/');
        if (trimmed.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^4];
        }

        var segments = trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2) return null;

        var name = segments[^1];
        var owner = segments[^2];
        return (owner, name, $"https://github.com/{owner}/{name}");
    }

    // Release tags are v{semver}, e.g. v0.1.0. Anything else is ignored.
    private static Version? ParseTag(string tag)
    {
        var trimmed = tag.Trim();
        if (trimmed.StartsWith('v') || trimmed.StartsWith('V')) trimmed = trimmed[1..];
        return Version.TryParse(trimmed, out var version) ? version : null;
    }
}
