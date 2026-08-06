using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Redux_SDK_Manager.Models;

namespace Redux_SDK_Manager.Services;

/// <summary>Fetches the manager's published releases from its GitHub repository.</summary>
public interface IReleaseClient
{
    Task<IReadOnlyList<ReleaseInfo>> GetReleasesAsync(string owner, string repo, CancellationToken cancellationToken = default);
}

public sealed class GitHubReleaseClient : IReleaseClient
{
    private const string Sha256Prefix = "sha256:";

    private readonly HttpClient _http;

    public GitHubReleaseClient()
    {
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        // GitHub rejects API requests without a User-Agent.
        _http.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("Redux-SDK-Manager", typeof(GitHubReleaseClient).Assembly.GetName().Version?.ToString() ?? "0.0.0"));
    }

    public async Task<IReadOnlyList<ReleaseInfo>> GetReleasesAsync(string owner, string repo, CancellationToken cancellationToken = default)
    {
        var url = $"https://api.github.com/repos/{owner}/{repo}/releases";
        var releases = await _http.GetFromJsonAsync<List<GitHubRelease>>(url, cancellationToken) ?? [];
        return releases.Select(Map).ToList();
    }

    private static ReleaseInfo Map(GitHubRelease r) => new()
    {
        TagName = r.TagName,
        Prerelease = r.Prerelease,
        Notes = r.Body,
        Assets = r.Assets.Select(a => new ReleaseAsset
        {
            Name = a.Name,
            DownloadUrl = a.BrowserDownloadUrl,
            Sha256 = a.Digest is not null && a.Digest.StartsWith(Sha256Prefix, StringComparison.OrdinalIgnoreCase)
                ? a.Digest[Sha256Prefix.Length..].Trim().ToLowerInvariant()
                : null,
        }).ToList(),
    };

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")] public string TagName { get; set; } = "";
        [JsonPropertyName("prerelease")] public bool Prerelease { get; set; }
        [JsonPropertyName("body")] public string? Body { get; set; }
        [JsonPropertyName("assets")] public GitHubAsset[] Assets { get; set; } = [];
    }

    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("browser_download_url")] public string BrowserDownloadUrl { get; set; } = "";
        [JsonPropertyName("digest")] public string? Digest { get; set; }
    }
}
