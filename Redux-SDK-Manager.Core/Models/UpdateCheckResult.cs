using System;
using System.Collections.Generic;

namespace Redux_SDK_Manager.Models;

/// <summary>
/// Outcome of an update check. When <see cref="IsUpdateAvailable"/> is false the manager is current
/// (or the check could not be completed); the app carries on normally either way.
/// </summary>
public sealed class UpdateCheckResult
{
    public bool IsUpdateAvailable { get; init; }
    public Version? CurrentVersion { get; init; }
    public Version? LatestVersion { get; init; }
    public string? ReleaseNotes { get; init; }
    public string ReleasesPageUrl { get; init; } = "";

    /// <summary>Assets attached to the latest release, for a frontend to download.</summary>
    public IReadOnlyList<ReleaseAsset> Assets { get; init; } = [];

    public static UpdateCheckResult NotAvailable(Version? current, string releasesPageUrl) =>
        new() { CurrentVersion = current, ReleasesPageUrl = releasesPageUrl };
}
