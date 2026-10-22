using System;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Redux_SDK_Manager.Services;

/// <summary>Resolves a published SDK version to its recorded source commit for embedding.</summary>
public interface IUpmPackageSourceService
{
    string ResolveCommit(string packageName, string version);
}

/// <summary>Reads public registry provenance without translating versions into guessed Git refs.</summary>
public sealed partial class UpmPackageSourceService : IUpmPackageSourceService
{
    private readonly HttpClient _http;

    public UpmPackageSourceService() : this(new HttpClient { Timeout = TimeSpan.FromSeconds(30) }) { }

    public UpmPackageSourceService(HttpClient http) => _http = http;

    public string ResolveCommit(string packageName, string version)
    {
        if (packageName is not (SdkEmbedService.CurrentSdkPackageName or SdkEmbedService.SdkPackageName))
            throw new ArgumentException("Not a Redux SDK package.", nameof(packageName));

        var url = $"https://upm.ksp2redux.org/{Uri.EscapeDataString(packageName)}/{Uri.EscapeDataString(version)}";
        string json;
        try
        {
            json = _http.GetStringAsync(url).GetAwaiter().GetResult();
        }
        catch (Exception e) when (e is HttpRequestException or System.Threading.Tasks.TaskCanceledException)
        {
            throw new InvalidOperationException(
                $"Could not read the source of SDK {packageName}@{version} from the package registry. Check the connection and try again.", e);
        }

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (ReadString(root, "name") != packageName || ReadString(root, "version") != version
            || !root.TryGetProperty("x-ksp2redux-source", out var source)
            || ReadString(source, "repository") != "KSP2Redux/SDK"
            || ReadString(source, "commit") is not { } commit || !CommitRegex().IsMatch(commit))
        {
            throw new InvalidOperationException(
                $"SDK {packageName}@{version} has no valid SDK source commit recorded. Embed it from a Git dependency instead.");
        }

        return commit;
    }

    private static string? ReadString(JsonElement value, string name)
        => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(name, out var property)
            && property.ValueKind == JsonValueKind.String ? property.GetString() : null;

    [GeneratedRegex("^[a-fA-F0-9]{40}$")]
    private static partial Regex CommitRegex();
}
