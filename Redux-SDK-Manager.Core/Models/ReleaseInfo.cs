using System.Collections.Generic;

namespace Redux_SDK_Manager.Models;

/// <summary>A published release of the manager, as returned by the GitHub releases API.</summary>
public sealed class ReleaseInfo
{
    public required string TagName { get; init; }
    public bool Prerelease { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<ReleaseAsset> Assets { get; init; } = [];
}

/// <summary>A downloadable file attached to a release.</summary>
public sealed class ReleaseAsset
{
    public required string Name { get; init; }
    public required string DownloadUrl { get; init; }

    /// <summary>The asset's SHA-256 (parsed from the API's <c>sha256:</c> digest), or null if absent.</summary>
    public string? Sha256 { get; init; }
}
